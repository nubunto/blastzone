using Blastzone.Simulation;
using Brutal.SdlApi;

namespace Blastzone.Engine.Input;

/// <summary>Logical inputs of the two local devices for one Tick.</summary>
public readonly record struct LocalInputs(PlayerInput Keyboard, PlayerInput Gamepad);

/// <summary>
/// Assigns device events to Ticks by their SDL timestamp. Events are queued as they are polled; sampling
/// Tick k applies exactly the events that happened before Tick k's interval ended. A press released within
/// the same Tick still counts as held for that Tick, so quick taps are never lost.
/// </summary>
public sealed class InputTimeline(TickClock clock, uint gamepadId)
{
    readonly Queue<InputEvent> _pending = new();
    readonly HashSet<SdlScancode> _keys = [];
    readonly HashSet<SdlScancode> _keysPressedThisTick = [];
    readonly HashSet<SdlGamepadButton> _buttons = [];
    readonly HashSet<SdlGamepadButton> _buttonsPressedThisTick = [];
    readonly Dictionary<SdlGamepadAxis, short> _axes = [];

    /// <summary>The SDL instance id of the gamepad driving Player 2; 0 for none.</summary>
    public uint GamepadId { get; set; } = gamepadId;

    public TickClock Clock => clock;

    /// <summary>Queues an event. SDL delivers events in timestamp order.</summary>
    public void Add(InputEvent e) => _pending.Enqueue(e);

    public LocalInputs Sample(long tick)
    {
        var end = clock.TickEndNs(tick);
        while (_pending.Count > 0 && _pending.Peek().TimestampNs < end)
            Apply(_pending.Dequeue());

        var keyboard = KeyboardBindings.Map(k => _keys.Contains(k) || _keysPressedThisTick.Contains(k));
        var gamepad = GamepadBindings.Map(
            b => _buttons.Contains(b) || _buttonsPressedThisTick.Contains(b),
            a => _axes.TryGetValue(a, out var v) ? v : (short)0);
        _keysPressedThisTick.Clear();
        _buttonsPressedThisTick.Clear();
        return new LocalInputs(keyboard, gamepad);
    }

    void Apply(InputEvent e)
    {
        switch (e.Kind)
        {
            case InputEventKind.Key:
                var key = (SdlScancode)e.Code;
                if (e.Value != 0)
                {
                    _keys.Add(key);
                    _keysPressedThisTick.Add(key);
                }
                else
                    _keys.Remove(key);
                break;
            case InputEventKind.GamepadButton when e.Device == GamepadId:
                var button = (SdlGamepadButton)e.Code;
                if (e.Value != 0)
                {
                    _buttons.Add(button);
                    _buttonsPressedThisTick.Add(button);
                }
                else
                    _buttons.Remove(button);
                break;
            case InputEventKind.GamepadAxis when e.Device == GamepadId:
                _axes[(SdlGamepadAxis)e.Code] = (short)e.Value;
                break;
        }
    }
}
