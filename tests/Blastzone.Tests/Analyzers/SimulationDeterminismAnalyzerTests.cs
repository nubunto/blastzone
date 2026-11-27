using System.Collections.Immutable;
using Blastzone.Simulation.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Blastzone.Tests.Analyzers;

/// <summary>
/// Proves the analyzer that Blastzone.Simulation loads turns banned code into build errors (ADR 0013).
/// Each snippet is compiled as if it were part of Blastzone.Simulation.
/// </summary>
public class SimulationDeterminismAnalyzerTests
{
    const string Float = SimulationDeterminismAnalyzer.FloatingPointId;
    const string Api = SimulationDeterminismAnalyzer.NondeterministicApiId;
    const string Reflection = SimulationDeterminismAnalyzer.ReflectionId;

    [Theory]
    // Floating-point types, however they are spelled or reached.
    [InlineData(Float, "class C { float f; }")]
    [InlineData(Float, "class C { double M() => 0; }")]
    [InlineData(Float, "class C { decimal d; }")]
    [InlineData(Float, "class C { System.Single s; }")]
    [InlineData(Float, "class C { void M() { var x = 1.5; } }")]
    [InlineData(Float, "class C { long M() => (long)2.5f; }")]
    [InlineData(Float, "using System.Collections.Generic; class C { List<float> l = new(); }")]
    [InlineData(Float, "class C { long M(System.TimeSpan t) => (long)t.TotalSeconds; }")]
    [InlineData(Float, "class C { object M() => typeof(double); }")]
    // Math, wall clocks and unseeded randomness.
    [InlineData(Api, "class C { int M() => System.Math.Abs(-1); }")]
    [InlineData(Api, "using static System.Math; class C { int M() => Abs(-1); }")]
    [InlineData(Api, "class C { object M() => typeof(System.MathF); }")]
    [InlineData(Api, "class C { long M() => System.DateTime.Now.Ticks; }")]
    [InlineData(Api, "class C { long M() => System.DateTimeOffset.UtcNow.Ticks; }")]
    [InlineData(Api, "class C { long M() => System.Diagnostics.Stopwatch.GetTimestamp(); }")]
    [InlineData(Api, "class C { int M() => System.Environment.TickCount; }")]
    [InlineData(Api, "class C { long M() => System.Environment.TickCount64; }")]
    [InlineData(Api, "class C { int M() => new System.Random(1).Next(); }")]
    // Reflection (ADR 0002).
    [InlineData(Reflection, "class C { object M() => typeof(C).GetMethods(); }")]
    [InlineData(Reflection, "class C { object M() => System.Reflection.Assembly.GetExecutingAssembly(); }")]
    [InlineData(Reflection, "using System.Reflection; class C { MethodInfo? m; }")]
    [InlineData(Reflection, "class C { object? M() => System.Activator.CreateInstance(typeof(C)); }")]
    public void Banned_code_is_reported_as_an_error(string expectedId, string source)
    {
        var diagnostics = Analyze(source);

        Assert.Contains(diagnostics, d => d.Id == expectedId);
        Assert.All(diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
    }

    [Fact]
    public void Integer_and_fixed_point_style_code_is_allowed()
    {
        const string source = """
            using System;
            using System.Numerics;
            using System.Runtime.InteropServices;

            struct Body { public long X; public long Y; }

            static class Ok
            {
                public static long Mul(long a, long b) => (long)(((Int128)a * b) >> 32);
                public static int Bits(ulong v) => BitOperations.LeadingZeroCount(v);
                public static Span<byte> Bytes(Span<Body> bodies) => MemoryMarshal.AsBytes(bodies);
                public static string Name => nameof(Body);
                public static int Abs(int v) => v < 0 ? -v : v;
            }
            """;

        Assert.Empty(Analyze(source));
    }

    [Fact]
    public void Metadata_attributes_from_System_Reflection_are_allowed()
    {
        // The SDK generates these for every assembly. They are compile-time metadata, not runtime reflection.
        const string source = """
            [assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
            [assembly: System.Reflection.AssemblyTitle("Blastzone.Simulation")]
            [System.Reflection.Obfuscation]
            class C { }
            """;

        Assert.Empty(Analyze(source));
    }

    [Fact]
    public void Each_banned_use_is_reported_once_per_location()
    {
        var diagnostics = Analyze("class C { float f; }");

        Assert.Single(diagnostics);
        Assert.Equal(Float, diagnostics[0].Id);
    }

    static ImmutableArray<Diagnostic> Analyze(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Blastzone.Simulation",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(compileErrors.Count == 0, "Snippet does not compile: " + string.Join("; ", compileErrors));

        return compilation
            .WithAnalyzers([new SimulationDeterminismAnalyzer()])
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }
}
