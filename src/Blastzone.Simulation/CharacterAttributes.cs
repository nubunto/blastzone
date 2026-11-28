using System;
using Blastzone.Simulation.Numerics;

namespace Blastzone.Simulation;

/// <summary>Collision Box size for one group of actions (ADR 0009). The Ground Probe is its bottom-center.</summary>
public readonly record struct BoxSize(Fixed Width, Fixed Height);

/// <summary>A capsule relative to the Ground Probe, for a Character facing right. A circle has equal ends.</summary>
public readonly record struct Capsule(Fixed X1, Fixed Y1, Fixed X2, Fixed Y2, Fixed Radius);

/// <summary>Which Collision Box size an action uses.</summary>
public enum BoxKind { Standing, Crouching, Airborne, Ledge }

/// <summary>
/// A Character's movement attributes, loaded from data (ADR 0005). Distances are in Stage units,
/// speeds in units per Tick, accelerations in units per Tick per Tick.
/// </summary>
public sealed record CharacterAttributes
{
    public required string Name { get; init; }
    /// <summary>Unused until knockback (milestone 3).</summary>
    public required Fixed Weight { get; init; }

    // Ground
    public required Fixed WalkSpeed { get; init; }
    public required Fixed WalkAcceleration { get; init; }
    public required Fixed DashInitialSpeed { get; init; }
    public required int DashTicks { get; init; }
    public required Fixed RunSpeed { get; init; }
    public required Fixed RunAcceleration { get; init; }
    public required int RunTurnTicks { get; init; }
    /// <summary>Ground deceleration per Tick when not accelerating.</summary>
    public required Fixed Traction { get; init; }

    // Air
    public required Fixed AirSpeed { get; init; }
    public required Fixed AirAcceleration { get; init; }
    /// <summary>Air deceleration per Tick when the stick doesn't push further.</summary>
    public required Fixed AirFriction { get; init; }
    public required Fixed Gravity { get; init; }
    public required Fixed MaxFallSpeed { get; init; }
    public required Fixed FastFallSpeed { get; init; }

    // Jumps
    public required int JumpSquatTicks { get; init; }
    public required Fixed FullHopHeight { get; init; }
    public required Fixed ShortHopHeight { get; init; }
    public required Fixed DoubleJumpHeight { get; init; }
    /// <summary>Double jumps available per airtime.</summary>
    public required int AirJumps { get; init; }
    public required int LandingTicks { get; init; }
    public required int HelplessLandingTicks { get; init; }

    // Air Dodge
    public required Fixed AirDodgeSpeed { get; init; }
    /// <summary>Multiplier applied to Air Dodge velocity every Tick after the first.</summary>
    public required Fixed AirDodgeDecay { get; init; }
    public required int AirDodgeTicks { get; init; }
    public required int AirDodgeIntangibilityTicks { get; init; }
    /// <summary>Landing Lag when landing during an Air Dodge (waveland, wavedash).</summary>
    public required int AirDodgeLandingTicks { get; init; }

    // Wall Jump
    public required bool CanWallJump { get; init; }
    public required Fixed WallJumpSpeedX { get; init; }
    public required Fixed WallJumpSpeedY { get; init; }

    // Ledge
    /// <summary>Half width of the ledge grab area around the Ground Probe.</summary>
    public required Fixed LedgeGrabHalfWidth { get; init; }
    /// <summary>The ledge must be between these heights above the Ground Probe to be grabbed.</summary>
    public required Fixed LedgeGrabBottom { get; init; }
    public required Fixed LedgeGrabTop { get; init; }
    /// <summary>Hanging Ground Probe position: this far out from the ledge and this far below it.</summary>
    public required Fixed LedgeHangOffsetX { get; init; }
    public required Fixed LedgeHangOffsetY { get; init; }
    public required int LedgeActionableTicks { get; init; }
    public required int LedgeGetupTicks { get; init; }
    public required Fixed LedgeGetupDistance { get; init; }
    public required int LedgeGetupIntangibilityTicks { get; init; }
    public required int LedgeRollTicks { get; init; }
    public required Fixed LedgeRollDistance { get; init; }
    public required int LedgeRollIntangibilityTicks { get; init; }
    public required Fixed LedgeJumpHeight { get; init; }

    // Geometry (ADR 0008)
    public required BoxSize StandingBox { get; init; }
    public required BoxSize CrouchingBox { get; init; }
    public required BoxSize AirborneBox { get; init; }
    public required BoxSize LedgeBox { get; init; }
    public required Capsule[] Hurtboxes { get; init; }

    public BoxSize Box(BoxKind kind) => kind switch
    {
        BoxKind.Standing => StandingBox,
        BoxKind.Crouching => CrouchingBox,
        BoxKind.Airborne => AirborneBox,
        BoxKind.Ledge => LedgeBox,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// Initial upward velocity that peaks at <paramref name="height"/> under this Character's gravity, for the
    /// Simulation's integration (velocity applied, then gravity subtracted each later Tick). The discrete peak
    /// is about v²/2g + v/2, so v = (sqrt(g² + 8gh) - g) / 2.
    /// </summary>
    public Fixed JumpVelocity(Fixed height)
    {
        var g = Gravity;
        return (FixedMath.Sqrt(g * g + Fixed.FromInt(8) * g * height) - g) / Fixed.FromInt(2);
    }
}
