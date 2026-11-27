using Blastzone.Simulation;
using Blastzone.Simulation.Numerics;

namespace Blastzone.Tests;

/// <summary>
/// Code-built settings, Stage and Characters for Simulation tests. They mirror the Game's JSON data,
/// but are fixed here so tuning the Game doesn't break mechanic tests.
/// </summary>
static class TestData
{
    public static Fixed F(int value) => Fixed.FromInt(value);
    /// <summary>A decimal literal as text, e.g. F("1.25"): exact, and no floats in sight.</summary>
    public static Fixed F(string value) => FixedDecimal.Parse(System.Text.Encoding.UTF8.GetBytes(value));

    public static MatchSettings Settings => new()
    {
        MaxCharacters = 4,
        Stick = new StickSettings
        {
            WalkThreshold = 24,
            TapThreshold = 80,
            TapOriginMax = 40,
            DownTapThreshold = 64,
            LedgeThreshold = 64,
            AirDodgeThreshold = 40,
        },
        Buffer = new BufferWindows { Jump = 6, Shield = 6, Dash = 6, Down = 6 },
        AirDodge = Melee,
        Ledge = new LedgeSettings
        {
            Trump = false,
            IntangibilityTicks = 30,
            RegrabIntangibilityShrinkTicks = 8,
            ExitIntangibilityTicks = 0,
            RegrabCooldownTicks = 20,
            TrumpVelocityX = F("1.5"),
            TrumpVelocityY = F("1.5"),
            TrumpStunTicks = 20,
            TrumpRegrabCooldownTicks = 30,
        },
        TeeterMaxSpeed = F("1.6"),
        WallJumpReach = F(2),
    };

    public static AirDodgeSettings Melee => new() { Directional = true, ZeroVelocityAtEnd = false, HelplessAfter = true };
    public static AirDodgeSettings Ultimate => new() { Directional = false, ZeroVelocityAtEnd = true, HelplessAfter = false };
    public static AirDodgeSettings Rivals2 => new() { Directional = true, ZeroVelocityAtEnd = true, HelplessAfter = false };

    public const int MainPlatform = 0, LeftPlatform = 1, RightPlatform = 2, TopPlatform = 3;
    public const int LeftLedge = 0, RightLedge = 1;

    public static Stage Stage => new()
    {
        Name = "Test Stage",
        Platforms =
        [
            new Platform(F(-70), F(70), F(0), F(-20), Solid: true, GrabbableLedges: true),
            new Platform(F(-58), F(-22), F(30), F(30), Solid: false, GrabbableLedges: false),
            new Platform(F(22), F(58), F(30), F(30), Solid: false, GrabbableLedges: false),
            new Platform(F(-18), F(18), F(58), F(58), Solid: false, GrabbableLedges: false),
        ],
        Spawns =
        [
            new SpawnPoint(F(-35), F(0), 1),
            new SpawnPoint(F(35), F(0), -1),
            new SpawnPoint(F(-40), F(30), 1),
            new SpawnPoint(F(40), F(30), -1),
        ],
        BlastZone = new Rect(F(-220), F(220), F(-150), F(200)),
    };

    /// <summary>Fast and light, 2 double jumps (3 jumps in all), can Wall Jump.</summary>
    public static CharacterAttributes Fast => new()
    {
        Name = "Fast",
        Weight = F(75),
        WalkSpeed = F("1.5"), WalkAcceleration = F("0.2"),
        DashInitialSpeed = F("1.9"), DashTicks = 11,
        RunSpeed = F("2.2"), RunAcceleration = F("0.12"), RunTurnTicks = 6,
        Traction = F("0.08"),
        AirSpeed = F("0.9"), AirAcceleration = F("0.08"), AirFriction = F("0.02"),
        Gravity = F("0.23"), MaxFallSpeed = F("2.8"), FastFallSpeed = F("3.4"),
        JumpSquatTicks = 3, FullHopHeight = F(36), ShortHopHeight = F(14), DoubleJumpHeight = F(32), AirJumps = 2,
        LandingTicks = 4, HelplessLandingTicks = 10,
        AirDodgeSpeed = F("3.1"), AirDodgeDecay = F("0.9"), AirDodgeTicks = 25, AirDodgeIntangibilityTicks = 20, AirDodgeLandingTicks = 10,
        CanWallJump = true, WallJumpSpeedX = F("1.6"), WallJumpSpeedY = F("2.8"),
        LedgeGrabHalfWidth = F(9), LedgeGrabBottom = F(8), LedgeGrabTop = F(22),
        LedgeHangOffsetX = F(4), LedgeHangOffsetY = F(14), LedgeActionableTicks = 6,
        LedgeGetupTicks = 24, LedgeGetupDistance = F(10), LedgeGetupIntangibilityTicks = 20,
        LedgeRollTicks = 32, LedgeRollDistance = F(30), LedgeRollIntangibilityTicks = 24,
        LedgeJumpHeight = F(34),
        StandingBox = new BoxSize(F(8), F(16)), CrouchingBox = new BoxSize(F(8), F(10)),
        AirborneBox = new BoxSize(F(8), F(14)), LedgeBox = new BoxSize(F(6), F(14)),
        Hurtboxes =
        [
            new Capsule(F(0), F(5), F(0), F(12), F("4.5")),
            new Capsule(F(1), F(17), F(1), F(17), F("3.5")),
        ],
    };

    /// <summary>Heavy, 1 double jump, no Wall Jump.</summary>
    public static CharacterAttributes Heavy => Fast with
    {
        Name = "Heavy",
        Weight = F(115),
        WalkSpeed = F("0.9"), WalkAcceleration = F("0.12"),
        DashInitialSpeed = F("1.5"), DashTicks = 15,
        RunSpeed = F("1.7"), RunAcceleration = F("0.08"), RunTurnTicks = 10,
        Traction = F("0.06"),
        AirSpeed = F("0.8"), AirAcceleration = F("0.04"), AirFriction = F("0.015"),
        Gravity = F("0.11"), MaxFallSpeed = F("1.9"), FastFallSpeed = F("2.4"),
        JumpSquatTicks = 5, FullHopHeight = F(30), ShortHopHeight = F(12), DoubleJumpHeight = F(26), AirJumps = 1,
        LandingTicks = 6, HelplessLandingTicks = 14,
        AirDodgeSpeed = F("2.6"), AirDodgeTicks = 30, AirDodgeIntangibilityTicks = 22, AirDodgeLandingTicks = 12,
        CanWallJump = false, WallJumpSpeedX = F(0), WallJumpSpeedY = F(0),
        LedgeGrabHalfWidth = F(10), LedgeGrabBottom = F(9), LedgeGrabTop = F(26),
        LedgeHangOffsetX = F(5), LedgeHangOffsetY = F(17), LedgeActionableTicks = 8,
        LedgeGetupTicks = 32, LedgeGetupDistance = F(12), LedgeGetupIntangibilityTicks = 24,
        LedgeRollTicks = 40, LedgeRollDistance = F(30), LedgeRollIntangibilityTicks = 26,
        LedgeJumpHeight = F(28),
        StandingBox = new BoxSize(F(12), F(20)), CrouchingBox = new BoxSize(F(12), F(13)),
        AirborneBox = new BoxSize(F(12), F(18)), LedgeBox = new BoxSize(F(8), F(17)),
        Hurtboxes =
        [
            new Capsule(F(0), F(6), F(0), F(15), F(6)),
            new Capsule(F("1.5"), F(21), F("1.5"), F(21), F("4.5")),
        ],
    };
}

/// <summary>Short-hand for building inputs in scripts.</summary>
static class In
{
    public static PlayerInput Neutral => default;
    public static PlayerInput Stick(int x, int y = 0, ushort buttons = 0) => new() { StickX = (sbyte)x, StickY = (sbyte)y, Buttons = buttons };
    public static PlayerInput Right => Stick(127);
    public static PlayerInput Left => Stick(-127);
    public static PlayerInput Down => Stick(0, -127);
    public static PlayerInput Up => Stick(0, 127);
    public static PlayerInput Jump => Stick(0, 0, InputButtons.Jump);
    public static PlayerInput Shield => Stick(0, 0, InputButtons.Shield);
    public static PlayerInput With(this PlayerInput input, ushort buttons) => input with { Buttons = (ushort)(input.Buttons | buttons) };
}

/// <summary>A Match driven by scripted inputs, for behaviour tests.</summary>
sealed class Scenario
{
    readonly PlayerInput[] _inputs;

    public Scenario(MatchSettings? settings = null, params CharacterAttributes[] characters)
    {
        if (characters.Length == 0)
            characters = [TestData.Fast];
        Match = new Match(settings ?? TestData.Settings, TestData.Stage, characters);
        _inputs = new PlayerInput[characters.Length];
    }

    public static Scenario Of(params CharacterAttributes[] characters) => new(null, characters);

    public Match Match { get; }
    public ref CharacterState this[int character] => ref Match.State.Characters[character];
    public CharacterAction ActionOf(int character) => this[character].CurrentAction;

    /// <summary>Holds <paramref name="input"/> for Player 1 (others neutral) for <paramref name="ticks"/> Ticks.</summary>
    public Scenario Hold(PlayerInput input, int ticks = 1) => HoldFor(0, input, ticks);

    public Scenario HoldFor(int player, PlayerInput input, int ticks = 1)
    {
        for (var t = 0; t < ticks; t++)
        {
            Array.Clear(_inputs);
            _inputs[player] = input;
            Match.Step(_inputs);
        }
        return this;
    }

    /// <summary>Steps once with explicit inputs for every Player.</summary>
    public Scenario Step(params PlayerInput[] inputs)
    {
        Match.Step(inputs);
        return this;
    }

    /// <summary>Steps with neutral input until <paramref name="done"/> holds, up to <paramref name="maxTicks"/>.</summary>
    public int RunUntil(Func<Scenario, bool> done, PlayerInput input = default, int maxTicks = 600)
    {
        for (var t = 0; t < maxTicks; t++)
        {
            if (done(this))
                return t;
            Hold(input);
        }
        throw new Xunit.Sdk.XunitException($"Condition not met within {maxTicks} Ticks; Player 1 is {ActionOf(0)} at ({this[0].PositionX}, {this[0].PositionY}).");
    }

    /// <summary>Teleports a Character into the air at (x, y), at rest. Test setup only.</summary>
    public Scenario PlaceInAir(int character, Fixed x, Fixed y)
    {
        ref var c = ref this[character];
        c.PositionX = x;
        c.PositionY = y;
        c.VelocityX = Fixed.Zero;
        c.VelocityY = Fixed.Zero;
        c.Platform = -1;
        c.CurrentAction = CharacterAction.Airborne;
        c.ActionTick = 0;
        return this;
    }

    /// <summary>Teleports a Character onto a platform top at x, standing. Test setup only.</summary>
    public Scenario PlaceOnGround(int character, int platform, Fixed x, int facing = 1)
    {
        ref var c = ref this[character];
        c.PositionX = x;
        c.PositionY = Match.Stage.Platforms[platform].Top;
        c.VelocityX = Fixed.Zero;
        c.VelocityY = Fixed.Zero;
        c.Platform = platform;
        c.Facing = facing;
        c.CurrentAction = CharacterAction.Idle;
        c.ActionTick = 0;
        return this;
    }
}
