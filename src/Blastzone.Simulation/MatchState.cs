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
    /// <summary>How many Characters are in the Match (the rest of the array is unused).</summary>
    public int CharacterCount;
    public int Reserved;
}

/// <summary>
/// One Character's Simulation state. Plain data only (ADR 0011): Entities refer to each other and to Stage
/// parts by index, -1 meaning none. Field order keeps the struct free of padding.
/// </summary>
public struct CharacterState
{
    /// <summary>The Ground Probe: bottom-center of the Collision Box (ADR 0009).</summary>
    public Fixed PositionX;
    public Fixed PositionY;
    public Fixed VelocityX;
    public Fixed VelocityY;
    /// <summary>Where a scripted ledge action (getup, roll) started, for interpolating its motion.</summary>
    public Fixed ActionStartX;
    public Fixed ActionStartY;

    /// <summary>A <see cref="CharacterAction"/>.</summary>
    public int Action;
    /// <summary>Ticks completed in the current action (0 on the Tick it began).</summary>
    public int ActionTick;
    /// <summary>Length of the current action in Ticks, when it has one (Landing Lag, ledge actions, stun).</summary>
    public int ActionDuration;
    /// <summary>-1 facing left, +1 facing right.</summary>
    public int Facing;
    public int JumpsUsed;
    /// <summary>1 when an Air Dodge was used this airtime.</summary>
    public int AirDodgeUsed;
    /// <summary>1 while fast-falling.</summary>
    public int FastFalling;
    /// <summary>Platform the Character stands on, or -1.</summary>
    public int Platform;
    /// <summary>Drop-through platform being dropped through, ignored for landing until the Ground Probe is below it, or -1.</summary>
    public int IgnoredPlatform;
    /// <summary>Ledge held, or -1.</summary>
    public int Ledge;
    /// <summary>Ledge grabs since last landing.</summary>
    public int Regrabs;
    /// <summary>Remaining Ticks of Intangibility.</summary>
    public int IntangibleTicks;
    /// <summary>Ticks before the Character may grab a ledge again.</summary>
    public int LedgeCooldown;
    /// <summary>The previous Tick's input, for detecting presses and taps.</summary>
    public int PreviousStickX;
    public int PreviousStickY;
    public int PreviousButtons;
    /// <summary>Remaining Buffer Window Ticks of each buffered press; 0 means not buffered.</summary>
    public int BufferedJump;
    public int BufferedShield;
    public int BufferedTapLeft;
    public int BufferedTapRight;
    public int BufferedTapDown;
    public int Reserved;

    public CharacterAction CurrentAction
    {
        readonly get => (CharacterAction)Action;
        set => Action = (int)value;
    }
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
