using Blastzone.Simulation.Numerics;

namespace Blastzone.Simulation;

// Per-Tick Character logic: Think picks the action and sets velocity from input, Move applies velocity and
// resolves Stage collision.
public sealed partial class Match
{
    static void Enter(ref CharacterState c, CharacterAction action, int duration = 0)
    {
        c.CurrentAction = action;
        c.ActionTick = 0;
        c.ActionDuration = duration;
    }

    /// <summary>Records presses and taps of this Tick into the Buffer Window counters.</summary>
    void UpdateBuffers(ref CharacterState c, PlayerInput input)
    {
        var stick = _settings.Stick;
        if (input.StickX >= stick.TapThreshold && c.PreviousStickX < stick.TapOriginMax)
        {
            c.BufferedTapRight = BufferTicks(_settings.Buffer.Dash);
            c.BufferedTapLeft = 0;
        }
        if (input.StickX <= -stick.TapThreshold && -c.PreviousStickX < stick.TapOriginMax)
        {
            c.BufferedTapLeft = BufferTicks(_settings.Buffer.Dash);
            c.BufferedTapRight = 0;
        }
        if (input.StickY <= -stick.DownTapThreshold && -c.PreviousStickY < stick.TapOriginMax)
            c.BufferedTapDown = BufferTicks(_settings.Buffer.Down);
        if (input.Holds(InputButtons.Jump) && (c.PreviousButtons & InputButtons.Jump) == 0)
            c.BufferedJump = BufferTicks(_settings.Buffer.Jump);
        if (input.Holds(InputButtons.Shield) && (c.PreviousButtons & InputButtons.Shield) == 0)
            c.BufferedShield = BufferTicks(_settings.Buffer.Shield);
    }

    /// <summary>Counter value for a fresh press, so it stays usable for the Tick it happened on.</summary>
    static int BufferTicks(int window) => 1;

    static void ExpireBuffers(ref CharacterState c)
    {
        if (c.BufferedJump > 0) c.BufferedJump--;
        if (c.BufferedShield > 0) c.BufferedShield--;
        if (c.BufferedTapLeft > 0) c.BufferedTapLeft--;
        if (c.BufferedTapRight > 0) c.BufferedTapRight--;
        if (c.BufferedTapDown > 0) c.BufferedTapDown--;
    }

    /// <summary>The buffered horizontal tap direction (-1, +1), or 0 if none.</summary>
    static int BufferedTap(in CharacterState c) => c.BufferedTapRight > 0 ? 1 : c.BufferedTapLeft > 0 ? -1 : 0;

    static void ConsumeTap(ref CharacterState c)
    {
        c.BufferedTapLeft = 0;
        c.BufferedTapRight = 0;
    }

    void Think(ref CharacterState c, CharacterAttributes a, int index, PlayerInput input)
    {
        var walk = _settings.Stick.WalkThreshold;

        // Timed transitions first, so the next action already runs on this Tick.
        switch (c.CurrentAction)
        {
            case CharacterAction.Landing:
                if (c.ActionTick >= c.ActionDuration)
                    Enter(ref c, CharacterAction.Idle);
                break;
            case CharacterAction.Dash:
                if (c.ActionTick >= a.DashTicks)
                    Enter(ref c, input.StickX * c.Facing >= walk ? CharacterAction.Run : CharacterAction.Idle);
                break;
            case CharacterAction.RunTurn:
                if (c.ActionTick >= c.ActionDuration)
                {
                    c.Facing = -c.Facing;
                    Enter(ref c, input.StickX * c.Facing >= walk ? CharacterAction.Run : CharacterAction.Idle);
                }
                break;
            case CharacterAction.JumpSquat:
                if (c.ActionTick >= a.JumpSquatTicks)
                {
                    // Still holding jump at take-off: full hop. Released: short hop.
                    var height = input.Holds(InputButtons.Jump) ? a.FullHopHeight : a.ShortHopHeight;
                    c.VelocityY = a.JumpVelocity(height);
                    c.Platform = -1;
                    Enter(ref c, CharacterAction.Airborne);
                    return; // the take-off Tick moves at full jump speed, gravity starts next Tick
                }
                ApplyTraction(ref c, a);
                return;
        }

        if (CharacterActions.IsGrounded(c.CurrentAction))
            ThinkGround(ref c, a, input);
        else
            ThinkAir(ref c, a, input);
    }

    void ThinkGround(ref CharacterState c, CharacterAttributes a, PlayerInput input)
    {
        var walk = _settings.Stick.WalkThreshold;
        var action = c.CurrentAction;
        var actionable = action is CharacterAction.Idle or CharacterAction.Walk or CharacterAction.Teeter;

        if (c.BufferedJump > 0 && (actionable || action is CharacterAction.Dash or CharacterAction.Run or CharacterAction.RunTurn))
        {
            c.BufferedJump = 0;
            Enter(ref c, CharacterAction.JumpSquat);
            ApplyTraction(ref c, a);
            return;
        }

        if (actionable || action == CharacterAction.Dash)
        {
            var tap = BufferedTap(c);
            if (tap != 0 && (actionable || tap == -c.Facing))
            {
                // A dash, or a Dash Dance turnaround during the initial dash.
                ConsumeTap(ref c);
                c.Facing = tap;
                c.VelocityX = Fixed.FromInt(tap) * a.DashInitialSpeed;
                Enter(ref c, CharacterAction.Dash);
                return;
            }
        }

        switch (action)
        {
            case CharacterAction.Idle:
                if (input.StickX >= walk || input.StickX <= -walk)
                {
                    Enter(ref c, CharacterAction.Walk);
                    Walk(ref c, a, input);
                }
                else
                    ApplyTraction(ref c, a);
                break;
            case CharacterAction.Walk:
                if (input.StickX >= walk || input.StickX <= -walk)
                    Walk(ref c, a, input);
                else
                {
                    Enter(ref c, CharacterAction.Idle);
                    ApplyTraction(ref c, a);
                }
                break;
            case CharacterAction.Teeter:
                // Standing at an edge. Only walking away from it (or a dash, jump...) leaves Teeter.
                if (input.StickX * TeeterSide(c) <= -walk)
                {
                    Enter(ref c, CharacterAction.Walk);
                    Walk(ref c, a, input);
                }
                else
                    c.VelocityX = Fixed.Zero;
                break;
            case CharacterAction.Dash:
                if (c.ActionTick > 0)
                    c.VelocityX = FixedMath.MoveToward(c.VelocityX, Fixed.FromInt(c.Facing) * a.RunSpeed, a.RunAcceleration);
                break;
            case CharacterAction.Run:
                if (input.StickX * c.Facing >= walk)
                    c.VelocityX = FixedMath.MoveToward(c.VelocityX, Fixed.FromInt(c.Facing) * a.RunSpeed, a.RunAcceleration);
                else
                {
                    Enter(ref c, input.StickX * c.Facing <= -walk ? CharacterAction.RunTurn : CharacterAction.Idle, a.RunTurnTicks);
                    ApplyTraction(ref c, a);
                }
                break;
            default:
                ApplyTraction(ref c, a);
                break;
        }
    }

    /// <summary>Which edge of its platform a teetering Character stands at: -1 left, +1 right.</summary>
    int TeeterSide(in CharacterState c) => c.PositionX >= _platforms[c.Platform].Right ? 1 : -1;

    static void Walk(ref CharacterState c, CharacterAttributes a, PlayerInput input)
    {
        var direction = input.StickX > 0 ? 1 : -1;
        c.Facing = direction;
        var deflection = input.StickX > 0 ? input.StickX : -input.StickX;
        var target = Fixed.FromInt(direction) * a.WalkSpeed * Fixed.FromInt(deflection) / Fixed.FromInt(sbyte.MaxValue);
        var faster = direction > 0 ? c.VelocityX > target : c.VelocityX < target;
        c.VelocityX = FixedMath.MoveToward(c.VelocityX, target, faster ? a.Traction : a.WalkAcceleration);
    }

    void ThinkAir(ref CharacterState c, CharacterAttributes a, PlayerInput input)
    {
        if (c.CurrentAction == CharacterAction.Airborne)
        {
            if (c.BufferedJump > 0 && c.JumpsUsed < a.AirJumps)
            {
                c.BufferedJump = 0;
                c.JumpsUsed++;
                c.FastFalling = 0;
                c.VelocityY = a.JumpVelocity(a.DoubleJumpHeight);
                c.VelocityX = DriftTarget(a, input);
                return; // like take-off, the jump Tick moves at full jump speed
            }
            if (c.BufferedTapDown > 0 && c.VelocityY <= Fixed.Zero && c.FastFalling == 0)
            {
                c.BufferedTapDown = 0;
                c.FastFalling = 1;
            }
        }

        Drift(ref c, a, input);
        ApplyGravity(ref c, a);
    }

    Fixed DriftTarget(CharacterAttributes a, PlayerInput input)
    {
        var walk = _settings.Stick.WalkThreshold;
        if (input.StickX < walk && input.StickX > -walk)
            return Fixed.Zero;
        return a.AirSpeed * Fixed.FromInt(input.StickX) / Fixed.FromInt(sbyte.MaxValue);
    }

    /// <summary>Air control: accelerate toward the stick's share of air speed, air friction above it or with no stick.</summary>
    void Drift(ref CharacterState c, CharacterAttributes a, PlayerInput input)
    {
        var target = DriftTarget(a, input);
        var accelerating = target != Fixed.Zero
            && (target > Fixed.Zero ? c.VelocityX < target : c.VelocityX > target);
        c.VelocityX = FixedMath.MoveToward(c.VelocityX, target, accelerating ? a.AirAcceleration : a.AirFriction);
    }

    static void ApplyTraction(ref CharacterState c, CharacterAttributes a) =>
        c.VelocityX = FixedMath.MoveToward(c.VelocityX, Fixed.Zero, a.Traction);

    static void ApplyGravity(ref CharacterState c, CharacterAttributes a)
    {
        c.VelocityY = c.FastFalling != 0
            ? -a.FastFallSpeed
            : FixedMath.Max(c.VelocityY - a.Gravity, -a.MaxFallSpeed);
    }

    void Move(ref CharacterState c, CharacterAttributes a)
    {
        if (CharacterActions.IsGrounded(c.CurrentAction))
            MoveGrounded(ref c, a);
        else
            MoveAirborne(ref c, a);
    }

    void MoveGrounded(ref CharacterState c, CharacterAttributes a)
    {
        c.PositionX += c.VelocityX;
        PushOutOfWalls(ref c, a);

        var platform = _platforms[c.Platform];
        if (c.PositionX >= platform.Left && c.PositionX <= platform.Right)
            return;

        // Slow movement stops at the edge in Teeter; dashes, runs and fast slides go off it.
        if (c.CurrentAction is not (CharacterAction.Dash or CharacterAction.Run)
            && FixedMath.Abs(c.VelocityX) <= _settings.TeeterMaxSpeed)
        {
            c.PositionX = c.PositionX < platform.Left ? platform.Left : platform.Right;
            c.VelocityX = Fixed.Zero;
            if (c.CurrentAction != CharacterAction.Teeter)
                Enter(ref c, CharacterAction.Teeter);
            return;
        }

        // The Ground Probe left the platform: the Character leaves the edge (ADR 0009).
        c.Platform = -1;
        c.VelocityY = Fixed.Zero;
        Enter(ref c, CharacterAction.Airborne);
    }

    void MoveAirborne(ref CharacterState c, CharacterAttributes a)
    {
        c.PositionX += c.VelocityX;
        PushOutOfWalls(ref c, a);

        var previousY = c.PositionY;
        var previousTop = BoxAt(a, c.CurrentAction, c.PositionX, previousY).Top;
        c.PositionY += c.VelocityY;

        if (c.VelocityY <= Fixed.Zero)
        {
            var landOn = -1;
            for (var p = 0; p < _platforms.Length; p++)
            {
                var platform = _platforms[p];
                if (p == c.IgnoredPlatform || c.PositionX < platform.Left || c.PositionX > platform.Right)
                    continue;
                if (previousY >= platform.Top && c.PositionY <= platform.Top
                    && (landOn < 0 || platform.Top > _platforms[landOn].Top))
                    landOn = p;
            }
            if (landOn >= 0)
            {
                Land(ref c, a, landOn);
                return;
            }
        }
        else
        {
            var box = BoxAt(a, c.CurrentAction, c.PositionX, c.PositionY);
            foreach (var platform in _platforms)
            {
                if (platform.Solid && Overlaps(box, platform) && previousTop <= platform.Bottom)
                {
                    c.PositionY = platform.Bottom - (box.Top - box.Bottom);
                    c.VelocityY = Fixed.Zero;
                    box = BoxAt(a, c.CurrentAction, c.PositionX, c.PositionY);
                }
            }
        }

        if (c.IgnoredPlatform >= 0 && c.PositionY < _platforms[c.IgnoredPlatform].Top)
            c.IgnoredPlatform = -1;

        // Falling past a corner with the Ground Probe outside the platform: the box slides off the side.
        PushOutOfWalls(ref c, a);
    }

    void Land(ref CharacterState c, CharacterAttributes a, int platform)
    {
        c.PositionY = _platforms[platform].Top;
        c.VelocityY = Fixed.Zero;
        c.Platform = platform;
        c.IgnoredPlatform = -1;
        c.JumpsUsed = 0;
        c.AirDodgeUsed = 0;
        c.FastFalling = 0;
        c.Regrabs = 0;

        var lag = a.LandingTicks;
        if (lag > 0)
            Enter(ref c, CharacterAction.Landing, lag);
        else
            Enter(ref c, CharacterAction.Idle);
    }

    static bool Overlaps(Rect box, Platform p) =>
        box.Right > p.Left && box.Left < p.Right && box.Top > p.Bottom && box.Bottom < p.Top;

    /// <summary>Pushes the Collision Box sideways out of any solid platform it overlaps.</summary>
    void PushOutOfWalls(ref CharacterState c, CharacterAttributes a)
    {
        foreach (var platform in _platforms)
        {
            if (!platform.Solid)
                continue;
            var box = BoxAt(a, c.CurrentAction, c.PositionX, c.PositionY);
            if (!Overlaps(box, platform))
                continue;
            var half = (box.Right - box.Left) / Fixed.FromInt(2);
            var center = (platform.Left + platform.Right) / Fixed.FromInt(2);
            if (c.PositionX < center)
            {
                c.PositionX = platform.Left - half;
                if (c.VelocityX > Fixed.Zero)
                    c.VelocityX = Fixed.Zero;
            }
            else
            {
                c.PositionX = platform.Right + half;
                if (c.VelocityX < Fixed.Zero)
                    c.VelocityX = Fixed.Zero;
            }
        }
    }
}
