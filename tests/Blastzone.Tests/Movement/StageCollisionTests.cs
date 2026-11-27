using Blastzone.Simulation;
using static Blastzone.Tests.TestData;

namespace Blastzone.Tests.Movement;

/// <summary>Collision Box against walls and ceilings, Ground Probe against floors and edges (ADR 0009).</summary>
public class StageCollisionTests
{
    [Fact]
    public void An_airborne_character_accelerates_by_gravity_up_to_max_fall_speed()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(-120), F(100));

        s.Hold(In.Neutral);
        Assert.Equal(-Fast.Gravity, s[0].VelocityY);
        Assert.Equal(F(100) - Fast.Gravity, s[0].PositionY);

        s.Hold(In.Neutral, 40);
        Assert.Equal(-Fast.MaxFallSpeed, s[0].VelocityY);
    }

    [Fact]
    public void Landing_happens_when_the_ground_probe_crosses_a_platform_top()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(0), F(10));

        s.RunUntil(x => x[0].Platform == MainPlatform);

        Assert.Equal(F(0), s[0].PositionY);
        Assert.Equal(F(0), s[0].VelocityY);
        Assert.Equal(CharacterAction.Landing, s.ActionOf(0));
    }

    [Fact]
    public void Landing_lag_lasts_the_characters_landing_ticks()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(0), F(10));
        s.RunUntil(x => x.ActionOf(0) == CharacterAction.Landing);

        s.Hold(In.Neutral, Fast.LandingTicks - 1);
        Assert.Equal(CharacterAction.Landing, s.ActionOf(0));
        s.Hold(In.Neutral);
        Assert.Equal(CharacterAction.Idle, s.ActionOf(0));
    }

    [Fact]
    public void A_drop_through_platform_catches_a_character_falling_from_above()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(40), F(40));

        s.RunUntil(x => x[0].Platform >= 0);

        Assert.Equal(RightPlatform, s[0].Platform);
        Assert.Equal(F(30), s[0].PositionY);
    }

    [Fact]
    public void A_drop_through_platform_lets_a_rising_character_pass_and_catches_it_on_the_way_down()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(40), F(20));
        s[0].VelocityY = F(4);

        var wentAbove = false;
        s.RunUntil(x =>
        {
            wentAbove |= x[0].PositionY > F(30) && x[0].Platform == -1;
            return x[0].Platform >= 0;
        });

        Assert.True(wentAbove);
        Assert.Equal(RightPlatform, s[0].Platform);
        Assert.Equal(F(30), s[0].PositionY);
    }

    [Fact]
    public void The_collision_box_stops_at_the_main_platform_wall()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(-80), F(-10));
        s[0].VelocityX = F(2);

        s.Hold(In.Neutral, 10);

        var halfWidth = Fast.AirborneBox.Width / F(2);
        Assert.Equal(F(-70) - halfWidth, s[0].PositionX);
        Assert.Equal(F(0), s[0].VelocityX);
    }

    [Fact]
    public void The_collision_box_stops_at_the_main_platform_ceiling()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(0), F(-40));
        s[0].VelocityY = F(3);

        var hitCeiling = s.RunUntil(x => x[0].VelocityY <= F(0));

        Assert.True(hitCeiling < 10, "should stop on the ceiling, not at the top of its arc");
        Assert.Equal(F(-20) - Fast.AirborneBox.Height, s[0].PositionY);
    }

    [Fact]
    public void A_box_overhanging_the_platform_top_does_not_stand_on_it_when_the_probe_is_outside()
    {
        // Ground Probe 3 units left of the ledge, Collision Box overlapping the platform top.
        var s = Scenario.Of(Fast).PlaceInAir(0, F(-73), F(5));

        s.Hold(In.Neutral, 30);

        Assert.Equal(-1, s[0].Platform);
        Assert.True(s[0].PositionY < F(0));
        Assert.True(s[0].PositionX <= F(-70) - Fast.AirborneBox.Width / F(2));
    }

    [Fact]
    public void A_character_stays_grounded_while_its_ground_probe_is_on_the_platform()
    {
        var s = Scenario.Of(Fast).PlaceOnGround(0, MainPlatform, F("69.5"));

        s.Hold(In.Neutral, 30);

        Assert.Equal(MainPlatform, s[0].Platform);
        Assert.Equal(CharacterAction.Idle, s.ActionOf(0));
    }

    [Fact]
    public void A_fast_sliding_character_leaves_the_edge_when_its_ground_probe_does()
    {
        var s = Scenario.Of(Fast).PlaceOnGround(0, MainPlatform, F(66));
        s[0].VelocityX = F(3);

        s.Hold(In.Neutral);
        Assert.Equal(MainPlatform, s[0].Platform); // 66 + 2.92 is still on the platform
        s.Hold(In.Neutral);
        Assert.Equal(-1, s[0].Platform);
        Assert.Equal(CharacterAction.Airborne, s.ActionOf(0));
        Assert.True(s[0].PositionX > F(70));
    }

    [Fact]
    public void Traction_slows_a_sliding_character_to_a_stop()
    {
        var s = Scenario.Of(Fast).PlaceOnGround(0, MainPlatform, F(0));
        s[0].VelocityX = F(1);

        s.Hold(In.Neutral);
        Assert.Equal(F(1) - Fast.Traction, s[0].VelocityX);
        s.Hold(In.Neutral, 20);
        Assert.Equal(F(0), s[0].VelocityX);
    }

    [Fact]
    public void Leaving_the_blast_zone_resets_the_character_to_its_spawn_point()
    {
        // Milestone 2 has no Knockouts or Stocks yet: the Character is simply put back at its spawn.
        var s = Scenario.Of(Fast).PlaceInAir(0, F(-120), F(-140));
        s[0].JumpsUsed = 2;

        s.RunUntil(x => x[0].Platform == MainPlatform);

        Assert.Equal(TestData.Stage.Spawns[0].X, s[0].PositionX);
        Assert.Equal(F(0), s[0].PositionY);
        Assert.Equal(F(0), s[0].VelocityX);
        Assert.Equal(0, s[0].JumpsUsed);
        Assert.Equal(CharacterAction.Idle, s.ActionOf(0));
    }
}
