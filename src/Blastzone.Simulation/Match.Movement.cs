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

    void Think(ref CharacterState c, CharacterAttributes a, int index, PlayerInput input)
    {
        switch (c.CurrentAction)
        {
            case CharacterAction.Landing:
                if (c.ActionTick >= c.ActionDuration)
                    Enter(ref c, CharacterAction.Idle);
                break;
        }

        if (CharacterActions.IsGrounded(c.CurrentAction))
            ThinkGround(ref c, a, input);
        else
            ThinkAir(ref c, a, input);
    }

    void ThinkGround(ref CharacterState c, CharacterAttributes a, PlayerInput input)
    {
        switch (c.CurrentAction)
        {
            default:
                ApplyTraction(ref c, a);
                break;
        }
    }

    void ThinkAir(ref CharacterState c, CharacterAttributes a, PlayerInput input)
    {
        ApplyGravity(ref c, a);
    }

    static void ApplyTraction(ref CharacterState c, CharacterAttributes a) =>
        c.VelocityX = FixedMath.MoveToward(c.VelocityX, Fixed.Zero, a.Traction);

    static void ApplyGravity(ref CharacterState c, CharacterAttributes a)
    {
        c.VelocityY = FixedMath.Max(c.VelocityY - a.Gravity, -a.MaxFallSpeed);
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
