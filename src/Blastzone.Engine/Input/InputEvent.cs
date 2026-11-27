using Brutal.SdlApi;

namespace Blastzone.Engine.Input;

public enum InputEventKind { Key, GamepadButton, GamepadAxis }

/// <summary>A device event with its SDL timestamp, independent of SDL's event union so it can be queued and tested.</summary>
public readonly record struct InputEvent(ulong TimestampNs, InputEventKind Kind, uint Device, int Code, int Value)
{
    public static InputEvent Key(ulong timestampNs, SdlScancode key, bool down) =>
        new(timestampNs, InputEventKind.Key, 0, (int)key, down ? 1 : 0);

    public static InputEvent Button(ulong timestampNs, uint gamepad, SdlGamepadButton button, bool down) =>
        new(timestampNs, InputEventKind.GamepadButton, gamepad, (int)button, down ? 1 : 0);

    public static InputEvent Axis(ulong timestampNs, uint gamepad, SdlGamepadAxis axis, short value) =>
        new(timestampNs, InputEventKind.GamepadAxis, gamepad, (int)axis, value);

    /// <summary>Converts an SDL event, or returns null for events that aren't Player input.</summary>
    public static InputEvent? FromSdl(in SdlEvent e) => e.type switch
    {
        SdlEventType.KeyDown or SdlEventType.KeyUp when e.key.repeat == 0 =>
            Key(e.key.timestamp, e.key.scancode, e.type == SdlEventType.KeyDown),
        SdlEventType.GamepadButtonDown or SdlEventType.GamepadButtonUp =>
            Button(e.gbutton.timestamp, e.gbutton.which, (SdlGamepadButton)e.gbutton.button, e.type == SdlEventType.GamepadButtonDown),
        SdlEventType.GamepadAxisMotion =>
            Axis(e.gaxis.timestamp, e.gaxis.which, (SdlGamepadAxis)e.gaxis.axis, e.gaxis.value),
        _ => null,
    };
}
