using System.Reflection;
using System.Runtime.CompilerServices;
using Blastzone.Simulation;

namespace Blastzone.Tests.Determinism;

/// <summary>
/// The state hash and snapshots work on raw bytes (ADR 0011), so state structs must be blittable
/// and free of padding: padding bytes are not guaranteed to be copied consistently and would make
/// the hash nondeterministic. Reflection is fine here; this is the Tests assembly.
/// </summary>
public class StateLayoutTests
{
    public static TheoryData<Type> StateStructs => new() { typeof(MatchHeader), typeof(CharacterState) };

    [Theory]
    [MemberData(nameof(StateStructs))]
    public void State_structs_are_unmanaged_and_have_no_padding(Type type)
    {
        var size = SizeOf(type);
        var fieldBytes = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Sum(f => FieldSize(f.FieldType));

        Assert.Equal(fieldBytes, size);
        Assert.Equal(0, size % 8);
    }

    static int FieldSize(Type type)
    {
        var containsReferences = (bool)typeof(RuntimeHelpers)
            .GetMethod(nameof(RuntimeHelpers.IsReferenceOrContainsReferences))!
            .MakeGenericMethod(type).Invoke(null, null)!;
        Assert.False(containsReferences, $"{type} must be unmanaged (no managed references, ADR 0011)");
        return SizeOf(type);
    }

    static int SizeOf(Type type) =>
        (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!.MakeGenericMethod(type).Invoke(null, null)!;
}
