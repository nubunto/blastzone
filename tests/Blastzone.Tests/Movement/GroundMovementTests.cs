using Blastzone.Simulation;
using Blastzone.Simulation.Numerics;
using static Blastzone.Tests.TestData;

namespace Blastzone.Tests.Movement;

public class GroundMovementTests
{
    static Scenario Standing(int x = 0, int facing = 1) => Scenario.Of(Fast).PlaceOnGround(0, MainPlatform, F(x), facing);

    static Fixed WalkTarget(CharacterAttributes a, int stick) => a.WalkSpeed * F(stick) / F(127);

    [Fact]
    public void A_partial_tilt_walks_with_speed_scaled_by_the_stick()
    {
        var s = Standing();

        s.Hold(In.Stick(60));
        Assert.Equal(CharacterAction.Walk, s.ActionOf(0));
        Assert.Equal(Fast.WalkAcceleration, s[0].VelocityX);

        s.Hold(In.Stick(60), 30);
        Assert.Equal(WalkTarget(Fast, 60), s[0].VelocityX);
    }

    [Fact]
    public void Walking_left_faces_left()
    {
        var s = Standing();

        s.Hold(In.Stick(-60), 30);

        Assert.Equal(-1, s[0].Facing);
        Assert.Equal(-WalkTarget(Fast, 60), s[0].VelocityX);
    }

    [Fact]
    public void Tilting_slowly_to_full_walks_instead_of_dashing()
    {
        var s = Standing();

        s.Hold(In.Stick(30)).Hold(In.Stick(60)).Hold(In.Stick(127), 30);

        Assert.Equal(CharacterAction.Walk, s.ActionOf(0));
        Assert.Equal(Fast.WalkSpeed, s[0].VelocityX);
    }

    [Fact]
    public void A_tap_dashes_at_the_initial_dash_speed()
    {
        var s = Standing(facing: -1);

        s.Hold(In.Right);

        Assert.Equal(CharacterAction.Dash, s.ActionOf(0));
        Assert.Equal(1, s[0].Facing);
        Assert.Equal(Fast.DashInitialSpeed, s[0].VelocityX);
    }

    [Fact]
    public void A_dash_lasts_its_dash_ticks_then_runs_while_the_stick_is_held()
    {
        var s = Standing(-40);

        s.Hold(In.Right, Fast.DashTicks);
        Assert.Equal(CharacterAction.Dash, s.ActionOf(0));
        s.Hold(In.Right);
        Assert.Equal(CharacterAction.Run, s.ActionOf(0));

        s.Hold(In.Right, 30);
        Assert.Equal(Fast.RunSpeed, s[0].VelocityX);
    }

    [Fact]
    public void A_dash_released_ends_in_idle()
    {
        var s = Standing();

        s.Hold(In.Right).Hold(In.Neutral, Fast.DashTicks);

        Assert.Equal(CharacterAction.Idle, s.ActionOf(0));
    }

    [Fact]
    public void Tapping_the_other_way_during_the_initial_dash_turns_the_dash_around()
    {
        var s = Standing();

        s.Hold(In.Right, 3).Hold(In.Left);
        Assert.Equal(CharacterAction.Dash, s.ActionOf(0));
        Assert.Equal(-1, s[0].Facing);
        Assert.Equal(-Fast.DashInitialSpeed, s[0].VelocityX);

        // Dash Dance: keep flicking back and forth and the Character stays near where it started.
        for (var i = 0; i < 10; i++)
            s.Hold(In.Right, 3).Hold(In.Left, 3);
        Assert.Equal(CharacterAction.Dash, s.ActionOf(0));
        Assert.True(FixedMath.Abs(s[0].PositionX) <= F(12), $"drifted to {s[0].PositionX}");
    }

    [Fact]
    public void Holding_back_during_a_run_turns_around_over_the_run_turn_ticks()
    {
        var s = Standing(-60);
        s.Hold(In.Right, Fast.DashTicks + 20);
        Assert.Equal(CharacterAction.Run, s.ActionOf(0));

        s.Hold(In.Left);
        Assert.Equal(CharacterAction.RunTurn, s.ActionOf(0));
        Assert.Equal(Fast.RunSpeed - Fast.Traction, s[0].VelocityX);

        s.Hold(In.Left, Fast.RunTurnTicks - 1);
        Assert.Equal(CharacterAction.RunTurn, s.ActionOf(0));
        s.Hold(In.Left);
        Assert.Equal(CharacterAction.Run, s.ActionOf(0));
        Assert.Equal(-1, s[0].Facing);
    }

    [Fact]
    public void Releasing_a_run_slides_to_a_stop_with_traction()
    {
        var s = Standing(-60);
        s.Hold(In.Right, Fast.DashTicks + 20);

        s.Hold(In.Neutral);
        Assert.Equal(CharacterAction.Idle, s.ActionOf(0));
        Assert.Equal(Fast.RunSpeed - Fast.Traction, s[0].VelocityX);
    }

    [Fact]
    public void Walking_slowly_into_an_edge_stops_there_in_teeter()
    {
        var s = Standing(64);

        s.Hold(In.Stick(60), 40);

        Assert.Equal(CharacterAction.Teeter, s.ActionOf(0));
        Assert.Equal(MainPlatform, s[0].Platform);
        Assert.Equal(F(70), s[0].PositionX);
        Assert.Equal(F(0), s[0].VelocityX);
    }

    [Fact]
    public void Teeter_works_on_drop_through_platform_edges_too()
    {
        var s = Scenario.Of(Fast).PlaceOnGround(0, LeftPlatform, F(-50), -1);

        s.Hold(In.Stick(-60), 40);

        Assert.Equal(CharacterAction.Teeter, s.ActionOf(0));
        Assert.Equal(F(-58), s[0].PositionX);
    }

    [Fact]
    public void Walking_away_from_the_edge_leaves_teeter()
    {
        var s = Standing(64);
        s.Hold(In.Stick(60), 40);

        s.Hold(In.Neutral).Hold(In.Stick(-60), 5);

        Assert.Equal(CharacterAction.Walk, s.ActionOf(0));
        Assert.True(s[0].PositionX < F(70));
    }

    [Fact]
    public void Dashing_off_an_edge_goes_airborne_instead_of_teetering()
    {
        var s = Standing(60);

        s.RunUntil(x => x[0].Platform == -1, In.Right, maxTicks: 20);

        Assert.Equal(CharacterAction.Airborne, s.ActionOf(0));
    }

    [Fact]
    public void The_heavy_character_walks_slower_than_the_fast_one()
    {
        var s = Scenario.Of(Fast, Heavy);
        var inputs = new[] { In.Stick(-60), In.Stick(-60) };
        for (var t = 0; t < 40; t++)
            s.Step(inputs);

        Assert.Equal(-WalkTarget(Fast, 60), s[0].VelocityX);
        Assert.Equal(-WalkTarget(Heavy, 60), s[1].VelocityX);
    }
}
