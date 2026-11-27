using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Blastzone.Simulation.Numerics;

namespace Blastzone.Simulation;

/// <summary>Match-wide state stored at the start of the state block.</summary>
public struct MatchHeader
{
    /// <summary>Number of Ticks simulated so far.</summary>
    public long Tick;
}

/// <summary>Milestone 1 stub of a Character's state: a point with a velocity.</summary>
public struct CharacterState
{
    public Fixed PositionX;
    public Fixed PositionY;
    public Fixed VelocityX;
    public Fixed VelocityY;
}

/// <summary>
/// The whole Simulation state as one contiguous block of memory (ADR 0011): a header followed by one
/// fixed-capacity array of plain structs per Entity kind. Typed views are carved out of the block,
/// so a Rollback snapshot is a single memory copy and the state hash is a pass over the bytes.
/// </summary>
/// <remarks>
/// State structs hold no managed references and no padding (enforced by StateLayoutTests), so equal
/// states are equal byte for byte.
/// </remarks>
public sealed class MatchState
{
    static readonly int HeaderBytes = Unsafe.SizeOf<MatchHeader>();
    static readonly int CharacterBytes = Unsafe.SizeOf<CharacterState>();

    readonly byte[] _memory;

    public MatchState(int maxCharacters)
    {
        if (maxCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));
        MaxCharacters = maxCharacters;
        _memory = new byte[HeaderBytes + maxCharacters * CharacterBytes];
    }

    public int MaxCharacters { get; }

    public int SizeInBytes => _memory.Length;

    public ref MatchHeader Header => ref MemoryMarshal.AsRef<MatchHeader>(_memory.AsSpan(0, HeaderBytes));

    public Span<CharacterState> Characters =>
        MemoryMarshal.Cast<byte, CharacterState>(_memory.AsSpan(HeaderBytes, MaxCharacters * CharacterBytes));

    /// <summary>Copies this whole state into <paramref name="destination"/>, which must have the same layout.</summary>
    public void CopyTo(MatchState destination)
    {
        if (destination._memory.Length != _memory.Length)
            throw new ArgumentException("States have different layouts.", nameof(destination));
        _memory.AsSpan().CopyTo(destination._memory);
    }

    /// <summary>64-bit FNV-1a over every byte of the state.</summary>
    public ulong ComputeHash()
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;
        foreach (var b in _memory)
            hash = unchecked((hash ^ b) * prime);
        return hash;
    }
}
