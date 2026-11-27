using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Blastzone.Simulation.Analyzers;

/// <summary>
/// Fails the build when Blastzone.Simulation uses anything that would break determinism (ADR 0001):
/// floating-point types (ADR 0004), System.Math, wall clocks, System.Random and reflection (ADR 0002).
/// Assembly isolation alone can't ban language types or BCL APIs, so this closes the gap (ADR 0013).
/// It is referenced only by Blastzone.Simulation, so it never constrains the Engine or the Game.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SimulationDeterminismAnalyzer : DiagnosticAnalyzer
{
    public const string FloatingPointId = "BZS001";
    public const string NondeterministicApiId = "BZS002";
    public const string ReflectionId = "BZS003";
    const string Category = "Blastzone.Determinism";

    static readonly DiagnosticDescriptor FloatingPoint = new(
        FloatingPointId,
        "Floating point in the Simulation",
        "'{0}' is a floating-point type; the Simulation uses Q32.32 fixed point (ADR 0004)",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    static readonly DiagnosticDescriptor NondeterministicApi = new(
        NondeterministicApiId,
        "Non-deterministic API in the Simulation",
        "'{0}' is not deterministic across machines or runs; use FixedMath, Ticks or seeded Simulation state instead (ADR 0001)",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    static readonly DiagnosticDescriptor Reflection = new(
        ReflectionId,
        "Reflection in the Simulation",
        "'{0}' uses runtime reflection, which the Engine avoids (ADR 0002)",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    static readonly ImmutableHashSet<string> NondeterministicTypes = ImmutableHashSet.Create(
        "System.Math",
        "System.MathF",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.TimeProvider",
        "System.Diagnostics.Stopwatch",
        "System.Environment",
        "System.Random");

    static readonly ImmutableHashSet<string> ReflectionTypes = ImmutableHashSet.Create(
        "System.Activator");

    static readonly ImmutableHashSet<string> FloatingPointTypes = ImmutableHashSet.Create(
        "System.Half");

    static readonly OperationKind[] AllOperationKinds =
        Enum.GetValues(typeof(OperationKind)).Cast<OperationKind>().Where(k => k != OperationKind.None).ToArray();

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(FloatingPoint, NondeterministicApi, Reflection);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // One report per rule and location, even when several actions see the same node.
            var reported = new ConcurrentDictionary<(string, Location), byte>();

            void Report(Action<Diagnostic> report, DiagnosticDescriptor rule, Location location, ISymbol symbol)
            {
                if (reported.TryAdd((rule.Id, location), 0))
                    report(Diagnostic.Create(rule, location, symbol.ToDisplayString()));
            }

            // Every spelled-out type or member name: float, System.Single, var, Math, Abs (via using static) ...
            start.RegisterSyntaxNodeAction(node =>
            {
                var symbol = node.SemanticModel.GetSymbolInfo(node.Node, node.CancellationToken).Symbol;
                if (symbol is null)
                    return;
                var type = symbol as ITypeSymbol ?? (symbol.Kind is SymbolKind.Method or SymbolKind.Property or SymbolKind.Field or SymbolKind.Event
                    ? symbol.ContainingType
                    : null);
                if (type is not null && Classify(type) is { } rule)
                    Report(node.ReportDiagnostic, rule.Rule, node.Node.GetLocation(), rule.Type);
            }, SyntaxKind.PredefinedType, SyntaxKind.IdentifierName, SyntaxKind.GenericName);

            // Every expression whose value is banned, even if its type is never written: 1.5, t.TotalSeconds ...
            start.RegisterOperationAction(operation =>
            {
                if (operation.Operation.Type is { } type && Classify(type) is { } rule)
                    Report(operation.ReportDiagnostic, rule.Rule, operation.Operation.Syntax.GetLocation(), rule.Type);
            }, AllOperationKinds);
        });
    }

    /// <summary>The rule a type breaks, looking through arrays, pointers and generic arguments.</summary>
    static (DiagnosticDescriptor Rule, ITypeSymbol Type)? Classify(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return Classify(array.ElementType);
            case IPointerTypeSymbol pointer:
                return Classify(pointer.PointedAtType);
            case INamedTypeSymbol named:
                if (named.SpecialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal)
                    return (FloatingPoint, named);
                var name = FullName(named.OriginalDefinition);
                if (FloatingPointTypes.Contains(name))
                    return (FloatingPoint, named);
                if (NondeterministicTypes.Contains(name))
                    return (NondeterministicApi, named);
                if (ReflectionTypes.Contains(name) || (IsInReflectionNamespace(named) && !IsAttribute(named)))
                    return (Reflection, named);
                foreach (var argument in named.TypeArguments)
                {
                    if (Classify(argument) is { } inner)
                        return inner;
                }
                return named.ContainingType is { } outer ? Classify(outer) : null;
            default:
                return null;
        }
    }

    static string FullName(INamedTypeSymbol type) =>
        type.ContainingType is { } outer
            ? FullName(outer) + "+" + type.MetadataName
            : type.ContainingNamespace is { IsGlobalNamespace: false } ns
                ? ns.ToDisplayString() + "." + type.MetadataName
                : type.MetadataName;

    /// <summary>Attributes such as AssemblyVersion live in System.Reflection but are compile-time metadata.</summary>
    static bool IsAttribute(INamedTypeSymbol type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
        {
            if (t.ContainingNamespace?.ToDisplayString() == "System" && t.MetadataName == "Attribute")
                return true;
        }
        return false;
    }

    static bool IsInReflectionNamespace(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        return ns == "System.Reflection" || ns.StartsWith("System.Reflection.", StringComparison.Ordinal);
    }
}
