using Blastzone.Simulation;
using static Blastzone.Tests.TestData;

namespace Blastzone.Tests.Movement;

/// <summary>A press during Landing Lag runs on the first actionable Tick if it is within the Buffer Window.</summary>
public class BufferTests
{
    const int Lag = 20;

    /// <summary>Standing in 20 Ticks of Landing Lag: Ticks 0 to 19 are lag, Tick 20 is the first actionable one.</summary>
    static Scenario InLandingLag(MatchSettings? settings = null)
    {
        var s = new Scenario(settings ?? Settings, Fast).PlaceOnGround(0, MainPlatform, F(0));
        s[0].CurrentAction = CharacterAction.Landing;
        s[0].ActionDuration = Lag;
        return s;
    }

    /// <summary>Presses <paramref name="press"/> on Tick <paramref name="pressTick"/>, holds it, and returns the action on Tick 20.</summary>
    static CharacterAction ActionAtFirstActionableTick(Scenario s, int pressTick, PlayerInput press)
    {
        s.Hold(In.Neutral, pressTick).Hold(press, Lag + 1 - pressTick);
        return s.ActionOf(0);
    }

    [Fact]
    public void A_jump_pressed_6_ticks_early_is_buffered()
    {
        Assert.Equal(CharacterAction.JumpSquat, ActionAtFirstActionableTick(InLandingLag(), Lag - 6, In.Jump));
    }

    [Fact]
    public void A_jump_pressed_7_ticks_early_is_dropped()
    {
        Assert.Equal(CharacterAction.Idle, ActionAtFirstActionableTick(InLandingLag(), Lag - 7, In.Jump));
    }

    [Fact]
    public void The_buffer_window_can_be_overridden_per_action_type()
    {
        var settings = Settings with { Buffer = Settings.Buffer with { Jump = 2 } };

        Assert.Equal(CharacterAction.Idle, ActionAtFirstActionableTick(InLandingLag(settings), Lag - 3, In.Jump));
        Assert.Equal(CharacterAction.JumpSquat, ActionAtFirstActionableTick(InLandingLag(settings), Lag - 2, In.Jump));
        // Dash taps keep the default window.
        Assert.Equal(CharacterAction.Dash, ActionAtFirstActionableTick(InLandingLag(settings), Lag - 6, In.Right));
    }

    [Fact]
    public void A_dash_tap_during_landing_lag_dashes_when_the_lag_ends()
    {
        Assert.Equal(CharacterAction.Dash, ActionAtFirstActionableTick(InLandingLag(), Lag - 4, In.Right));
    }

    [Fact]
    public void A_down_tap_during_landing_lag_drops_through_when_the_lag_ends()
    {
        var s = Scenario.Of(Fast).PlaceOnGround(0, RightPlatform, F(40));
        s[0].CurrentAction = CharacterAction.Landing;
        s[0].ActionDuration = Lag;

        Assert.Equal(CharacterAction.Airborne, ActionAtFirstActionableTick(s, Lag - 3, In.Down));
    }

    [Fact]
    public void An_air_dodge_pressed_during_jump_squat_comes_out_on_the_first_airborne_tick()
    {
        // The usual way to input a wavedash: jump, then Shield while still in jump squat.
        var s = Scenario.Of(Fast).PlaceOnGround(0, MainPlatform, F(0));
        s.Hold(In.Jump).Hold(In.Stick(127, -127, InputButtons.Shield), Fast.JumpSquatTicks - 1);
        Assert.Equal(CharacterAction.JumpSquat, s.ActionOf(0));

        s.Hold(In.Stick(127, -127, InputButtons.Shield)); // take-off
        s.Hold(In.Stick(127, -127, InputButtons.Shield)); // first airborne Tick
        Assert.True(s.ActionOf(0) is CharacterAction.AirDodge or CharacterAction.Landing, $"{s.ActionOf(0)}");

        s.RunUntil(x => x[0].Platform == MainPlatform, maxTicks: 3);
        Assert.Equal(Fast.AirDodgeLandingTicks, s[0].ActionDuration);
    }

    [Fact]
    public void A_held_button_is_not_buffered_again()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(-65), F(60));

        s.Hold(In.Jump, 30);

        Assert.Equal(1, s[0].JumpsUsed);
    }
}
