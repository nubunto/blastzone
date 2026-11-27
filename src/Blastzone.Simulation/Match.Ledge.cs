using Blastzone.Simulation.Numerics;

namespace Blastzone.Simulation;

// Ledges: grab, Ledge Hog / Ledge Trump, regrab Intangibility and the ledge options.
public sealed partial class Match
{
    void TryGrabLedge(ref CharacterState c, CharacterAttributes a, int index)
    {
        if (c.VelocityY > Fixed.Zero || c.LedgeCooldown > 0)
            return;

        for (var l = 0; l < _ledges.Length; l++)
        {
            var ledge = _ledges[l];
            var dx = ledge.X - c.PositionX;
            var dy = ledge.Y - c.PositionY;
            // The ledge point must be inside the grab area, and the Character outside the Stage side of it.
            if (dx < -a.LedgeGrabHalfWidth || dx > a.LedgeGrabHalfWidth || dy < a.LedgeGrabBottom || dy > a.LedgeGrabTop)
                continue;
            if ((c.PositionX - ledge.X) * Fixed.FromInt(ledge.Side) < Fixed.Zero)
                continue;

            var holder = LedgeHolder(l, index);
            if (holder >= 0)
            {
                if (!_settings.Ledge.Trump)
                    continue; // Ledge Hog: one Character per ledge.
                Trump(ref State.Characters[holder], ledge);
            }
            GrabLedge(ref c, a, l, ledge);
            return;
        }
    }

    int LedgeHolder(int ledge, int except)
    {
        var characters = State.Characters;
        for (var i = 0; i < _characters.Length; i++)
            if (i != except && characters[i].Ledge == ledge && characters[i].CurrentAction == CharacterAction.LedgeHang)
                return i;
        return -1;
    }

    void Trump(ref CharacterState holder, Ledge ledge)
    {
        var settings = _settings.Ledge;
        holder.Ledge = -1;
        holder.VelocityX = Fixed.FromInt(ledge.Side) * settings.TrumpVelocityX;
        holder.VelocityY = settings.TrumpVelocityY;
        holder.LedgeCooldown = settings.TrumpRegrabCooldownTicks;
        Enter(ref holder, CharacterAction.Trumped, settings.TrumpStunTicks);
    }

    void GrabLedge(ref CharacterState c, CharacterAttributes a, int index, Ledge ledge)
    {
        var settings = _settings.Ledge;
        c.Ledge = index;
        c.PositionX = ledge.X + Fixed.FromInt(ledge.Side) * a.LedgeHangOffsetX;
        c.PositionY = ledge.Y - a.LedgeHangOffsetY;
        c.VelocityX = Fixed.Zero;
        c.VelocityY = Fixed.Zero;
        c.Facing = -ledge.Side;
        c.JumpsUsed = 0;
        c.AirDodgeUsed = 0;
        c.FastFalling = 0;
        // Intangibility shrinks with every regrab since the Character last stood on the Stage.
        var intangible = settings.IntangibilityTicks - c.Regrabs * settings.RegrabIntangibilityShrinkTicks;
        c.IntangibleTicks = intangible > 0 ? intangible : 0;
        c.Regrabs++;
        Enter(ref c, CharacterAction.LedgeHang);
    }

    void ThinkLedge(ref CharacterState c, CharacterAttributes a, PlayerInput input)
    {
        if (c.CurrentAction != CharacterAction.LedgeHang || c.ActionTick < a.LedgeActionableTicks)
            return;

        var threshold = _settings.Stick.LedgeThreshold;
        var towardStage = input.StickX * c.Facing;
        if (c.BufferedJump > 0)
        {
            c.BufferedJump = 0;
            LeaveLedge(ref c);
            c.VelocityY = a.JumpVelocity(a.LedgeJumpHeight);
        }
        else if (c.BufferedShield > 0)
        {
            c.BufferedShield = 0;
            StartLedgeClimb(ref c, CharacterAction.LedgeRoll, a.LedgeRollTicks, a.LedgeRollIntangibilityTicks);
        }
        else if (towardStage >= threshold || input.StickY >= threshold)
            StartLedgeClimb(ref c, CharacterAction.LedgeGetup, a.LedgeGetupTicks, a.LedgeGetupIntangibilityTicks);
        else if (towardStage <= -threshold || input.StickY <= -threshold)
        {
            // The stick press that dropped doesn't also count as a fast-fall or Wall Jump tap.
            c.BufferedTapDown = 0;
            ConsumeTap(ref c);
            LeaveLedge(ref c);
            ApplyGravity(ref c, a);
        }
    }

    /// <summary>Drop or jump: airborne at the hang position, keeping some of the ledge Intangibility.</summary>
    void LeaveLedge(ref CharacterState c)
    {
        var settings = _settings.Ledge;
        c.Ledge = -1;
        c.LedgeCooldown = settings.RegrabCooldownTicks;
        if (c.IntangibleTicks > settings.ExitIntangibilityTicks)
            c.IntangibleTicks = settings.ExitIntangibilityTicks;
        Enter(ref c, CharacterAction.Airborne);
    }

    /// <summary>Getup or roll: a scripted move from the hang position onto the Stage, released from the ledge.</summary>
    void StartLedgeClimb(ref CharacterState c, CharacterAction action, int ticks, int intangibleTicks)
    {
        c.ActionStartX = c.PositionX;
        c.ActionStartY = c.PositionY;
        c.IntangibleTicks = intangibleTicks;
        Enter(ref c, action, ticks);
    }

    void MoveOnLedge(ref CharacterState c, CharacterAttributes a)
    {
        if (c.CurrentAction == CharacterAction.LedgeHang)
            return;

        var ledge = _ledges[c.Ledge];
        var distance = c.CurrentAction == CharacterAction.LedgeRoll ? a.LedgeRollDistance : a.LedgeGetupDistance;
        var endX = ledge.X - Fixed.FromInt(ledge.Side) * distance;
        var endY = ledge.Y;
        var done = c.ActionTick + 1;
        if (done < c.ActionDuration)
        {
            var t = Fixed.FromInt(done) / Fixed.FromInt(c.ActionDuration);
            c.PositionX = c.ActionStartX + (endX - c.ActionStartX) * t;
            c.PositionY = c.ActionStartY + (endY - c.ActionStartY) * t;
            return;
        }

        c.PositionX = endX;
        c.PositionY = endY;
        c.Platform = ledge.Platform;
        c.Ledge = -1;
        c.Regrabs = 0;
        Enter(ref c, CharacterAction.Idle);
    }
}
