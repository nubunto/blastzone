using Blastzone.Simulation;
using Blastzone.Simulation.Numerics;
using static Blastzone.Tests.TestData;

namespace Blastzone.Tests.Movement;

public class JumpTests
{
    static Scenario Standing(CharacterAttributes a, int x = 0) => Scenario.Of(a).PlaceOnGround(0, MainPlatform, F(x));

    public static TheoryData<string> Characters => new() { "Fast", "Heavy" };
    static CharacterAttributes ByName(string name) => name == "Fast" ? Fast : Heavy;

    /// <summary>Highest Ground Probe height reached while holding <paramref name="input"/> until falling.</summary>
    static Fixed Peak(Scenario s, PlayerInput input)
    {
        var peak = s[0].PositionY;
        s.RunUntil(x =>
        {
            peak = FixedMath.Max(peak, x[0].PositionY);
            return x[0].VelocityY < F(0);
        }, input);
        return peak;
    }

    [Fact]
    public void Jump_squat_lasts_its_ticks_then_takes_off()
    {
        var s = Standing(Fast);

        s.Hold(In.Jump);
        Assert.Equal(CharacterAction.JumpSquat, s.ActionOf(0));
        s.Hold(In.Jump, Fast.JumpSquatTicks - 1);
        Assert.Equal(CharacterAction.JumpSquat, s.ActionOf(0));
        Assert.Equal(F(0), s[0].PositionY);

        s.Hold(In.Jump);
        Assert.Equal(CharacterAction.Airborne, s.ActionOf(0));
        Assert.Equal(-1, s[0].Platform);
        Assert.Equal(Fast.JumpVelocity(Fast.FullHopHeight), s[0].VelocityY);
        Assert.Equal(s[0].VelocityY, s[0].PositionY);
    }

    [Theory]
    [MemberData(nameof(Characters))]
    public void Holding_jump_through_jump_squat_is_a_full_hop_to_the_full_hop_height(string name)
    {
        var a = ByName(name);
        var s = Standing(a).Hold(In.Jump, a.JumpSquatTicks);

        var peak = Peak(s, In.Jump);

        Assert.True(FixedMath.Abs(peak - a.FullHopHeight) <= a.Gravity, $"peak {peak} vs {a.FullHopHeight}");
    }

    [Theory]
    [MemberData(nameof(Characters))]
    public void Releasing_jump_during_jump_squat_is_a_short_hop_to_the_short_hop_height(string name)
    {
        var a = ByName(name);
        var s = Standing(a).Hold(In.Jump).Hold(In.Neutral, a.JumpSquatTicks - 1);

        var peak = Peak(s, In.Neutral);

        Assert.True(FixedMath.Abs(peak - a.ShortHopHeight) <= a.Gravity, $"peak {peak} vs {a.ShortHopHeight}");
    }

    [Fact]
    public void A_jump_out_of_a_dash_keeps_the_ground_speed()
    {
        var s = Standing(Fast, -40);
        s.Hold(In.Right, 4).Hold(In.Right.With(InputButtons.Jump));
        Assert.Equal(CharacterAction.JumpSquat, s.ActionOf(0));

        s.RunUntil(x => x.ActionOf(0) == CharacterAction.Airborne, In.Right);

        Assert.True(s[0].VelocityX > Fast.AirSpeed, $"vx {s[0].VelocityX}");
    }

    [Fact]
    public void Air_drift_accelerates_to_air_speed_and_air_friction_slows_it_down()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(0), F(150));

        s.Hold(In.Right);
        Assert.Equal(Fast.AirAcceleration, s[0].VelocityX);
        s.Hold(In.Right, 30);
        Assert.Equal(Fast.AirSpeed, s[0].VelocityX);

        s.Hold(In.Neutral);
        Assert.Equal(Fast.AirSpeed - Fast.AirFriction, s[0].VelocityX);
    }

    [Theory]
    [MemberData(nameof(Characters))]
    public void Each_character_has_its_own_number_of_double_jumps(string name)
    {
        var a = ByName(name);
        var s = Scenario.Of(a).PlaceInAir(0, F(0), F(100));

        for (var jump = 1; jump <= a.AirJumps; jump++)
        {
            s.Hold(In.Neutral, 5).Hold(In.Jump);
            Assert.Equal(jump, s[0].JumpsUsed);
            Assert.Equal(a.JumpVelocity(a.DoubleJumpHeight) - a.Gravity * F(0), s[0].VelocityY);
        }

        s.Hold(In.Neutral, 5).Hold(In.Jump);
        Assert.Equal(a.AirJumps, s[0].JumpsUsed);
        Assert.True(s[0].VelocityY < a.JumpVelocity(a.DoubleJumpHeight));
    }

    [Fact]
    public void The_fast_character_has_three_jumps_and_the_heavy_one_two()
    {
        Assert.Equal(2, Fast.AirJumps);
        Assert.Equal(1, Heavy.AirJumps);
    }

    [Fact]
    public void A_double_jump_points_horizontal_speed_where_the_stick_is()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(0), F(100));
        s[0].VelocityX = F(2);

        s.Hold(In.Left.With(InputButtons.Jump));

        Assert.Equal(-Fast.AirSpeed, s[0].VelocityX);
    }

    [Fact]
    public void Double_jumps_come_back_on_landing()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(-65), F(20)); // nothing overhead to land on
        s.Hold(In.Jump).Hold(In.Neutral).Hold(In.Jump);
        Assert.Equal(2, s[0].JumpsUsed);

        s.RunUntil(x => x[0].Platform == MainPlatform);

        Assert.Equal(0, s[0].JumpsUsed);
        Assert.Equal(CharacterAction.Landing, s.ActionOf(0));
    }

    [Fact]
    public void Tapping_down_while_rising_does_not_fast_fall()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(0), F(100));
        s[0].VelocityY = F(2);

        s.Hold(In.Down);

        Assert.Equal(0, s[0].FastFalling);
        Assert.Equal(F(2) - Fast.Gravity, s[0].VelocityY);
    }

    [Fact]
    public void Tapping_down_after_the_peak_fast_falls()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(0), F(100));
        s.Hold(In.Neutral);

        s.Hold(In.Down);
        Assert.Equal(1, s[0].FastFalling);
        Assert.Equal(-Fast.FastFallSpeed, s[0].VelocityY);

        s.Hold(In.Neutral, 5);
        Assert.Equal(-Fast.FastFallSpeed, s[0].VelocityY);
    }

    [Fact]
    public void A_double_jump_cancels_a_fast_fall()
    {
        var s = Scenario.Of(Fast).PlaceInAir(0, F(0), F(100));
        s.Hold(In.Neutral).Hold(In.Down).Hold(In.Neutral);

        s.Hold(In.Jump);

        Assert.Equal(0, s[0].FastFalling);
        Assert.True(s[0].VelocityY > F(0));
    }

    [Fact]
    public void Running_off_an_edge_keeps_every_double_jump()
    {
        var s = Standing(Fast, 40);
        s.RunUntil(x => x[0].Platform == -1, In.Right, maxTicks: 40);

        s.Hold(In.Neutral).Hold(In.Jump);

        Assert.Equal(1, s[0].JumpsUsed);
        Assert.True(s[0].VelocityY > F(0));
    }
}
