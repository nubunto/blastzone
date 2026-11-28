using Blastzone.Simulation.Numerics;

namespace Blastzone.Simulation;

/// <summary>
/// Match-wide values a Game supplies to the Simulation (ADR 0005). Nothing here has a default in code:
/// the Game's settings data provides every value.
/// </summary>
public sealed record MatchSettings
{
    /// <summary>Fixed capacity of the Character array (ADR 0011).</summary>
    public required int MaxCharacters { get; init; }
    public required StickSettings Stick { get; init; }
    public required BufferWindows Buffer { get; init; }
    public required AirDodgeSettings AirDodge { get; init; }
    public required LedgeSettings Ledge { get; init; }

    /// <summary>A grounded Character at or below this horizontal speed stops at an edge in Teeter instead of leaving it.</summary>
    public required Fixed TeeterMaxSpeed { get; init; }
    /// <summary>How close the Collision Box must be to a wall for a Wall Jump.</summary>
    public required Fixed WallJumpReach { get; init; }
}

/// <summary>Stick thresholds, in stick units (0 to 127).</summary>
public sealed record StickSettings
{
    /// <summary>Below this deflection the stick counts as neutral horizontally.</summary>
    public required int WalkThreshold { get; init; }
    /// <summary>A horizontal tap must reach this deflection (dash, Wall Jump).</summary>
    public required int TapThreshold { get; init; }
    /// <summary>A tap only counts if the previous Tick's deflection in that direction was below this.</summary>
    public required int TapOriginMax { get; init; }
    /// <summary>A down tap must reach this deflection (fast-fall, drop-through).</summary>
    public required int DownTapThreshold { get; init; }
    /// <summary>Deflection that picks a ledge option (getup, drop) while hanging.</summary>
    public required int LedgeThreshold { get; init; }
    /// <summary>Deflection below which a directional Air Dodge is neutral.</summary>
    public required int AirDodgeThreshold { get; init; }
}

/// <summary>Buffer Window per action type, in Ticks (glossary: Buffer Window).</summary>
public sealed record BufferWindows
{
    public required int Jump { get; init; }
    /// <summary>Air Dodge and ledge roll.</summary>
    public required int Shield { get; init; }
    /// <summary>Horizontal taps: dash, Dash Dance, Wall Jump.</summary>
    public required int Dash { get; init; }
    /// <summary>Down taps: fast-fall, drop-through.</summary>
    public required int Down { get; init; }
}

/// <summary>The knobs Air Dodge presets (Melee, Ultimate, Rivals 2 style) are built from, as data.</summary>
public sealed record AirDodgeSettings
{
    /// <summary>True: the stick picks one of 8 directions. False: always neutral (no movement).</summary>
    public required bool Directional { get; init; }
    /// <summary>True: velocity is set to zero when the Air Dodge ends.</summary>
    public required bool ZeroVelocityAtEnd { get; init; }
    /// <summary>True: the Character is Helpless after the Air Dodge until it lands or grabs a ledge.</summary>
    public required bool HelplessAfter { get; init; }
}

public sealed record LedgeSettings
{
    /// <summary>Ledge Trump: a newcomer takes an occupied ledge. Off: Ledge Hog blocks the newcomer.</summary>
    public required bool Trump { get; init; }
    /// <summary>Intangibility on the first ledge grab since landing.</summary>
    public required int IntangibilityTicks { get; init; }
    /// <summary>Each regrab since landing gives this many fewer Intangibility Ticks.</summary>
    public required int RegrabIntangibilityShrinkTicks { get; init; }
    /// <summary>Intangibility left over from the ledge is kept for at most this many Ticks after dropping or jumping off.</summary>
    public required int ExitIntangibilityTicks { get; init; }
    /// <summary>Ticks after dropping or jumping off before the Character can grab a ledge again.</summary>
    public required int RegrabCooldownTicks { get; init; }
    /// <summary>Velocity given to a trumped holder, pointing away from the Stage.</summary>
    public required Fixed TrumpVelocityX { get; init; }
    public required Fixed TrumpVelocityY { get; init; }
    /// <summary>Ticks a trumped holder can't act.</summary>
    public required int TrumpStunTicks { get; init; }
    /// <summary>Ticks after being trumped before the Character can grab a ledge again.</summary>
    public required int TrumpRegrabCooldownTicks { get; init; }
}
