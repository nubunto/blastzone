using Blastzone.Simulation;
using static Blastzone.Tests.TestData;

namespace Blastzone.Tests.Movement;

/// <summary>Ledge grab, Ledge Hog, Ledge Trump, regrab Intangibility and the ledge options.</summary>
public class LedgeTests
{
    // The main platform's left ledge is at (-70, 0), Side -1. Hanging puts the Ground Probe at
    // (-70 - LedgeHangOffsetX, -LedgeHangOffsetY), facing the Stage.

    /// <summary>Player 1 (Fast) falling toward the left ledge from just outside and above it.</summary>
    static Scenario FallingTowardLeftLedge(MatchSettings? settings = null, params CharacterAttributes[] characters)
    {
        var s = new Scenario(settings, characters.Length == 0 ? [Fast] : characters);
        return s.PlaceInAir(0, F(-76), F(5));
    }

    static Scenario Hanging(MatchSettings? settings = null, CharacterAttributes? character = null)
    {
        var s = FallingTowardLeftLedge(settings, character ?? Fast);
        s.RunUntil(x => x.ActionOf(0) == CharacterAction.LedgeHang, maxTicks: 30);
        return s;
    }

    static Scenario HangingAndActionable(MatchSettings? settings = null, CharacterAttributes? character = null)
    {
        var s = Hanging(settings, character);
        s.Hold(In.Neutral, (character ?? Fast).LedgeActionableTicks);
        return s;
    }

    [Fact]
    public void Falling_past_a_ledge_grabs_it()
    {
        var s = FallingTowardLeftLedge();
        s[0].JumpsUsed = 2;
        s[0].AirDodgeUsed = 1;

        s.RunUntil(x => x.ActionOf(0) == CharacterAction.LedgeHang, maxTicks: 30);

        Assert.Equal(LeftLedge, s[0].Ledge);
        Assert.Equal(F(-70) - Fast.LedgeHangOffsetX, s[0].PositionX);
        Assert.Equal(-Fast.LedgeHangOffsetY, s[0].PositionY);
        Assert.Equal(F(0), s[0].VelocityX);
        Assert.Equal(F(0), s[0].VelocityY);
        Assert.Equal(1, s[0].Facing);
        Assert.Equal(0, s[0].JumpsUsed);
        Assert.Equal(0, s[0].AirDodgeUsed);
    }

    [Fact]
    public void Rising_past_a_ledge_does_not_grab_it()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(-76), F(-14));
        s[0].VelocityY = F(3);

        s.Hold(In.Neutral);

        Assert.Equal(CharacterAction.Airborne, s.ActionOf(0));
    }

    [Fact]
    public void Grabbing_a_ledge_gives_ledge_intangibility()
    {
        var s = Hanging();

        Assert.Equal(Settings.Ledge.IntangibilityTicks, s[0].IntangibleTicks);
    }

    [Fact]
    public void Each_regrab_before_landing_gives_less_intangibility()
    {
        var s = HangingAndActionable();
        var shrink = Settings.Ledge.RegrabIntangibilityShrinkTicks;

        for (var regrab = 1; regrab <= 3; regrab++)
        {
            // Drop, double jump straight up, and fall back onto the same ledge.
            s.Hold(In.Down).Hold(In.Neutral, 2).Hold(In.Jump);
            s.RunUntil(x => x.ActionOf(0) == CharacterAction.LedgeHang, maxTicks: 120);
            Assert.Equal(Settings.Ledge.IntangibilityTicks - regrab * shrink, s[0].IntangibleTicks);
            s.Hold(In.Neutral, Fast.LedgeActionableTicks);
        }

        // Enough regrabs and there is none left.
        s.Hold(In.Down).Hold(In.Neutral, 2).Hold(In.Jump);
        s.RunUntil(x => x.ActionOf(0) == CharacterAction.LedgeHang, maxTicks: 120);
        Assert.Equal(0, s[0].IntangibleTicks);
    }

    [Fact]
    public void Regrab_intangibility_resets_after_getting_back_on_the_stage()
    {
        var s = HangingAndActionable();
        s.Hold(In.Down).Hold(In.Neutral, 2).Hold(In.Jump);
        s.RunUntil(x => x.ActionOf(0) == CharacterAction.LedgeHang, maxTicks: 120);
        Assert.Equal(2, s[0].Regrabs);

        s.Hold(In.Neutral, Fast.LedgeActionableTicks).Hold(In.Right);
        s.RunUntil(x => x.ActionOf(0) == CharacterAction.Idle, maxTicks: 60);
        Assert.Equal(0, s[0].Regrabs);
    }

    [Fact]
    public void Ledge_hog_an_occupied_ledge_cannot_be_grabbed_when_trump_is_off()
    {
        var s = new Scenario(Settings, Fast, Heavy);
        s.PlaceInAir(1, F(-76), F(5));
        s.RunUntil(x => x.ActionOf(1) == CharacterAction.LedgeHang, maxTicks: 30);

        s.PlaceInAir(0, F(-76), F(5));
        s.Hold(In.Neutral, 30);

        Assert.Equal(CharacterAction.Airborne, s.ActionOf(0));
        Assert.Equal(-1, s[0].Ledge);
        Assert.Equal(CharacterAction.LedgeHang, s.ActionOf(1));
        Assert.Equal(LeftLedge, s[1].Ledge);
    }

    [Fact]
    public void Ledge_trump_a_newcomer_takes_the_ledge_and_knocks_the_holder_off()
    {
        var settings = Settings with { Ledge = Settings.Ledge with { Trump = true } };
        var s = new Scenario(settings, Fast, Heavy);
        s.PlaceInAir(1, F(-76), F(5));
        s.RunUntil(x => x.ActionOf(1) == CharacterAction.LedgeHang, maxTicks: 30);

        s.PlaceInAir(0, F(-76), F(5));
        s.RunUntil(x => x.ActionOf(0) == CharacterAction.LedgeHang, maxTicks: 30);

        Assert.Equal(LeftLedge, s[0].Ledge);
        Assert.Equal(CharacterAction.Trumped, s.ActionOf(1));
        Assert.Equal(-1, s[1].Ledge);
        Assert.True(s[1].VelocityX < F(0), "pushed away from the Stage");
        Assert.True(s[1].VelocityY > F(0));

        s.Hold(In.Neutral, settings.Ledge.TrumpStunTicks);
        Assert.Equal(CharacterAction.Airborne, s.ActionOf(1));
    }

    [Fact]
    public void Ledge_options_wait_for_the_actionable_ticks()
    {
        var s = Hanging();

        s.Hold(In.Stick(0, -127), Fast.LedgeActionableTicks - 1);

        Assert.Equal(CharacterAction.LedgeHang, s.ActionOf(0));
    }

    [Fact]
    public void Getup_toward_the_stage_ends_standing_on_it()
    {
        var s = HangingAndActionable();

        s.Hold(In.Right);
        Assert.Equal(CharacterAction.LedgeGetup, s.ActionOf(0));
        Assert.Equal(Fast.LedgeGetupIntangibilityTicks, s[0].IntangibleTicks);
        s.Hold(In.Neutral, Fast.LedgeGetupTicks - 2);
        Assert.Equal(CharacterAction.LedgeGetup, s.ActionOf(0));

        s.Hold(In.Neutral);
        Assert.Equal(CharacterAction.Idle, s.ActionOf(0));
        Assert.Equal(MainPlatform, s[0].Platform);
        Assert.Equal(F(-70) + Fast.LedgeGetupDistance, s[0].PositionX);
        Assert.Equal(F(0), s[0].PositionY);
    }

    [Fact]
    public void Up_is_a_getup_too()
    {
        var s = HangingAndActionable();

        s.Hold(In.Up);

        Assert.Equal(CharacterAction.LedgeGetup, s.ActionOf(0));
    }

    [Fact]
    public void Shield_rolls_onto_the_stage()
    {
        var s = HangingAndActionable();

        s.Hold(In.Shield);
        Assert.Equal(CharacterAction.LedgeRoll, s.ActionOf(0));
        s.RunUntil(x => x.ActionOf(0) == CharacterAction.Idle, maxTicks: Fast.LedgeRollTicks);

        Assert.Equal(F(-70) + Fast.LedgeRollDistance, s[0].PositionX);
        Assert.Equal(MainPlatform, s[0].Platform);
    }

    [Fact]
    public void Jump_leaves_the_ledge_upward_and_keeps_double_jumps()
    {
        var s = HangingAndActionable();

        s.Hold(In.Jump);

        Assert.Equal(CharacterAction.Airborne, s.ActionOf(0));
        Assert.Equal(-1, s[0].Ledge);
        Assert.Equal(Fast.JumpVelocity(Fast.LedgeJumpHeight), s[0].VelocityY);
        Assert.Equal(0, s[0].JumpsUsed);
        Assert.Equal(Settings.Ledge.RegrabCooldownTicks, s[0].LedgeCooldown);
    }

    [Theory]
    [InlineData(0, -127)] // down
    [InlineData(-127, 0)] // away from the Stage
    public void Down_or_away_drops_off_the_ledge(int x, int y)
    {
        var s = HangingAndActionable();

        s.Hold(In.Stick(x, y));

        Assert.Equal(CharacterAction.Airborne, s.ActionOf(0));
        Assert.Equal(-1, s[0].Ledge);
        Assert.True(s[0].VelocityY < F(0));
        Assert.Equal(Settings.Ledge.RegrabCooldownTicks, s[0].LedgeCooldown);
    }

    [Fact]
    public void Without_exit_intangibility_dropping_ends_ledge_intangibility()
    {
        var s = HangingAndActionable();

        s.Hold(In.Down);

        Assert.Equal(0, s[0].IntangibleTicks);
    }

    [Fact]
    public void Ledge_exit_intangibility_caps_what_is_left_of_the_ledge_intangibility()
    {
        var settings = Settings with { Ledge = Settings.Ledge with { ExitIntangibilityTicks = 10 } };

        var early = HangingAndActionable(settings);
        early.Hold(In.Down);
        Assert.Equal(10, early[0].IntangibleTicks); // 24 left, capped at 10

        var late = Hanging(settings);
        late.Hold(In.Neutral, Settings.Ledge.IntangibilityTicks - 4);
        late.Hold(In.Jump);
        Assert.Equal(3, late[0].IntangibleTicks); // 4 left, minus the jump Tick itself
    }

    [Fact]
    public void A_ledgedash_keeps_intangibility_for_the_exit_ticks_and_lands_on_the_stage()
    {
        // Air Dodge Intangibility off, so only the ledge's carries over.
        const int exitTicks = 10;
        var settings = Settings with { Ledge = Settings.Ledge with { ExitIntangibilityTicks = exitTicks } };
        var character = Fast with { AirDodgeIntangibilityTicks = 0 };
        var s = HangingAndActionable(settings, character);

        // Drop, double jump, drift in over the Stage with a partial tilt (a full tap toward the wall
        // would Wall Jump), then air dodge down onto it: a ledgedash.
        var intangibleTicksAfterLeaving = 0;
        var script = new List<PlayerInput> { In.Down, In.Neutral, In.Jump };
        script.AddRange(Enumerable.Repeat(In.Stick(70), 14));
        script.Add(In.Stick(127, -127, InputButtons.Shield));
        script.AddRange(Enumerable.Repeat(In.Neutral, 20));
        foreach (var input in script)
        {
            s.Hold(input);
            if (s[0].IntangibleTicks > 0)
                intangibleTicksAfterLeaving++;
        }

        Assert.Equal(exitTicks, intangibleTicksAfterLeaving);
        Assert.Equal(MainPlatform, s[0].Platform);
        Assert.True(s[0].PositionX > F(-60), $"slid to {s[0].PositionX}");
    }
}
