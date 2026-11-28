using Blastzone.Engine.Input;
using Blastzone.Simulation;
using Brutal.SdlApi;

namespace Blastzone.Tests.Input;

public class InputTimelineTests
{
    const ulong Start = 1_000_000_000; // SDL nanosecond clock at the first Tick
    static ulong At(double tick) => Start + (ulong)(tick * 1_000_000_000 / 60);

    const uint Pad = 7;

    static InputTimeline Timeline() => new(new TickClock(Start), gamepadId: Pad);

    [Fact]
    public void The_clock_runs_60_ticks_per_second_and_reports_how_far_into_the_next_tick_it_is()
    {
        var clock = new TickClock(Start);

        Assert.Equal(0, clock.CompletedTicks(Start));
        Assert.Equal(0, clock.CompletedTicks(At(0.99)));
        Assert.Equal(1, clock.CompletedTicks(At(1)));
        Assert.Equal(60, clock.CompletedTicks(Start + 1_000_000_000));
        Assert.Equal(0.5, clock.Interpolation(At(10.5), completedTicks: 10), 3);
    }

    [Fact]
    public void An_event_belongs_to_the_tick_whose_interval_contains_its_timestamp()
    {
        var timeline = Timeline();
        timeline.Add(InputEvent.Key(At(2.5), SdlScancode.D, down: true));

        Assert.Equal(0, timeline.Sample(0).Keyboard.StickX);
        Assert.Equal(0, timeline.Sample(1).Keyboard.StickX);
        Assert.Equal(127, timeline.Sample(2).Keyboard.StickX);
        Assert.Equal(127, timeline.Sample(3).Keyboard.StickX);
    }

    [Fact]
    public void Events_arriving_in_one_frame_are_still_split_across_the_ticks_they_happened_in()
    {
        // A slow frame: 3 Ticks are simulated at once, but press and release land on their own Ticks.
        var timeline = Timeline();
        timeline.Add(InputEvent.Key(At(0.2), SdlScancode.Space, down: true));
        timeline.Add(InputEvent.Key(At(1.4), SdlScancode.Space, down: false));

        Assert.True(timeline.Sample(0).Keyboard.Holds(InputButtons.Jump));
        Assert.False(timeline.Sample(1).Keyboard.Holds(InputButtons.Jump));
        Assert.False(timeline.Sample(2).Keyboard.Holds(InputButtons.Jump));
    }

    [Fact]
    public void A_press_and_release_inside_one_tick_still_counts_for_that_tick()
    {
        var timeline = Timeline();
        timeline.Add(InputEvent.Key(At(0.2), SdlScancode.Space, down: true));
        timeline.Add(InputEvent.Key(At(0.6), SdlScancode.Space, down: false));

        Assert.True(timeline.Sample(0).Keyboard.Holds(InputButtons.Jump));
        Assert.False(timeline.Sample(1).Keyboard.Holds(InputButtons.Jump));
    }

    [Theory]
    [InlineData(new[] { SdlScancode.D }, 127, 0, (ushort)0)]
    [InlineData(new[] { SdlScancode.A }, -127, 0, (ushort)0)]
    [InlineData(new[] { SdlScancode.A, SdlScancode.D }, 0, 0, (ushort)0)]
    [InlineData(new[] { SdlScancode.W }, 0, 127, (ushort)0)]
    [InlineData(new[] { SdlScancode.S }, 0, -127, (ushort)0)]
    [InlineData(new[] { SdlScancode.Lshift, SdlScancode.D }, KeyboardBindings.TiltDeflection, 0, (ushort)0)]
    [InlineData(new[] { SdlScancode.Space }, 0, 0, InputButtons.Jump)]
    [InlineData(new[] { SdlScancode.J }, 0, 0, InputButtons.Attack)]
    [InlineData(new[] { SdlScancode.K }, 0, 0, InputButtons.Special)]
    [InlineData(new[] { SdlScancode.L }, 0, 0, InputButtons.Shield)]
    public void Keyboard_default_bindings(SdlScancode[] keys, int x, int y, ushort buttons)
    {
        var timeline = Timeline();
        foreach (var key in keys)
            timeline.Add(InputEvent.Key(At(0.1), key, down: true));

        var input = timeline.Sample(0).Keyboard;

        Assert.Equal(x, input.StickX);
        Assert.Equal(y, input.StickY);
        Assert.Equal(buttons, input.Buttons);
    }

    [Fact]
    public void Gamepad_left_stick_maps_to_the_logical_stick_with_up_positive()
    {
        var timeline = Timeline();
        timeline.Add(InputEvent.Axis(At(0.1), Pad, SdlGamepadAxis.Leftx, short.MaxValue));
        timeline.Add(InputEvent.Axis(At(0.1), Pad, SdlGamepadAxis.Lefty, short.MinValue)); // SDL: negative is up

        var input = timeline.Sample(0).Gamepad;

        Assert.Equal(127, input.StickX);
        Assert.Equal(127, input.StickY);
    }

    [Fact]
    public void Gamepad_default_buttons_and_triggers()
    {
        var timeline = Timeline();
        timeline.Add(InputEvent.Button(At(0.1), Pad, SdlGamepadButton.South, down: true));
        timeline.Add(InputEvent.Axis(At(0.1), Pad, SdlGamepadAxis.RightTrigger, short.MaxValue));
        Assert.Equal(InputButtons.Jump | InputButtons.Shield, timeline.Sample(0).Gamepad.Buttons);

        timeline.Add(InputEvent.Button(At(1.1), Pad, SdlGamepadButton.South, down: false));
        timeline.Add(InputEvent.Axis(At(1.1), Pad, SdlGamepadAxis.RightTrigger, 0));
        timeline.Add(InputEvent.Button(At(1.1), Pad, SdlGamepadButton.West, down: true));
        timeline.Add(InputEvent.Button(At(1.1), Pad, SdlGamepadButton.RightShoulder, down: true));
        Assert.Equal(InputButtons.Attack | InputButtons.Shield, timeline.Sample(1).Gamepad.Buttons);
    }

    [Fact]
    public void The_d_pad_works_as_a_digital_stick()
    {
        var timeline = Timeline();
        timeline.Add(InputEvent.Button(At(0.1), Pad, SdlGamepadButton.DpadLeft, down: true));

        Assert.Equal(-127, timeline.Sample(0).Gamepad.StickX);
    }

    [Fact]
    public void Only_the_assigned_gamepad_drives_player_2()
    {
        var timeline = Timeline();
        timeline.Add(InputEvent.Button(At(0.1), Pad + 1, SdlGamepadButton.South, down: true));

        Assert.Equal(0, timeline.Sample(0).Gamepad.Buttons);
    }
}
