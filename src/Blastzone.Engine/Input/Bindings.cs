using Blastzone.Simulation;
using Brutal.SdlApi;

namespace Blastzone.Engine.Input;

/// <summary>
/// Fixed default keyboard bindings for Player 1 (remapping is out of scope for the first vertical slice).
/// WASD moves, Left Shift tilts the stick partway (to walk), Space jumps, J/K attack/special, L shields.
/// </summary>
public static class KeyboardBindings
{
    /// <summary>Stick deflection while Left Shift is held: a walk, not a dash.</summary>
    public const int TiltDeflection = 60;

    public static PlayerInput Map(Func<SdlScancode, bool> held)
    {
        var deflection = held(SdlScancode.Lshift) ? TiltDeflection : sbyte.MaxValue;
        var x = (held(SdlScancode.D) ? deflection : 0) - (held(SdlScancode.A) ? deflection : 0);
        var y = (held(SdlScancode.W) ? deflection : 0) - (held(SdlScancode.S) ? deflection : 0);
        ushort buttons = 0;
        if (held(SdlScancode.Space)) buttons |= InputButtons.Jump;
        if (held(SdlScancode.J)) buttons |= InputButtons.Attack;
        if (held(SdlScancode.K)) buttons |= InputButtons.Special;
        if (held(SdlScancode.L)) buttons |= InputButtons.Shield;
        return new PlayerInput { StickX = (sbyte)x, StickY = (sbyte)y, Buttons = buttons };
    }
}

/// <summary>
/// Fixed default gamepad bindings for Player 2: left stick or d-pad moves, South/North jump, West attacks,
/// East is special, either shoulder or trigger shields.
/// </summary>
public static class GamepadBindings
{
    /// <summary>Analog trigger travel (of 32767) that counts as a Shield press.</summary>
    public const int TriggerPressed = 16384;

    public static PlayerInput Map(Func<SdlGamepadButton, bool> held, Func<SdlGamepadAxis, short> axis)
    {
        var x = Scale(axis(SdlGamepadAxis.Leftx));
        var y = -Scale(axis(SdlGamepadAxis.Lefty)); // SDL: down is positive; the Simulation: up is positive
        if (held(SdlGamepadButton.DpadLeft)) x = -sbyte.MaxValue;
        if (held(SdlGamepadButton.DpadRight)) x = sbyte.MaxValue;
        if (held(SdlGamepadButton.DpadUp)) y = sbyte.MaxValue;
        if (held(SdlGamepadButton.DpadDown)) y = -sbyte.MaxValue;

        ushort buttons = 0;
        if (held(SdlGamepadButton.South) || held(SdlGamepadButton.North)) buttons |= InputButtons.Jump;
        if (held(SdlGamepadButton.West)) buttons |= InputButtons.Attack;
        if (held(SdlGamepadButton.East)) buttons |= InputButtons.Special;
        if (held(SdlGamepadButton.LeftShoulder) || held(SdlGamepadButton.RightShoulder)
            || axis(SdlGamepadAxis.LeftTrigger) >= TriggerPressed || axis(SdlGamepadAxis.RightTrigger) >= TriggerPressed)
            buttons |= InputButtons.Shield;
        return new PlayerInput { StickX = (sbyte)x, StickY = (sbyte)y, Buttons = buttons };
    }

    /// <summary>SDL axis (-32768 to 32767) to stick units (-127 to 127).</summary>
    static int Scale(short value) => Math.Clamp(value / 258, -sbyte.MaxValue, sbyte.MaxValue);
}
