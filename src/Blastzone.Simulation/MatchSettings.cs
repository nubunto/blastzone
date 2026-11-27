using Blastzone.Simulation.Numerics;

namespace Blastzone.Simulation;

/// <summary>
/// Values a Game supplies to the Simulation (ADR 0005). Milestone 1 stub: just enough to move points.
/// </summary>
public readonly struct MatchSettings
{
    /// <summary>Fixed capacity of the Character array (ADR 0011).</summary>
    public int MaxCharacters { get; init; }
    /// <summary>Velocity added per Tick per unit of stick deflection.</summary>
    public Fixed Acceleration { get; init; }
    /// <summary>Velocity multiplier applied every Tick before acceleration.</summary>
    public Fixed Friction { get; init; }
    /// <summary>Velocity magnitude is clamped to this.</summary>
    public Fixed MaxSpeed { get; init; }
}
