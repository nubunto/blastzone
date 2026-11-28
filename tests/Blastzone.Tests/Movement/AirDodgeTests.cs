using Blastzone.Simulation;
using Blastzone.Simulation.Numerics;
using static Blastzone.Tests.TestData;

namespace Blastzone.Tests.Movement;

/// <summary>Air Dodge knobs and the presets built from them. Wavedash and waveland come out of the physics.</summary>
public class AirDodgeTests
{
    static Scenario InAir(AirDodgeSettings preset, int x = 0, int y = 100) =>
        new Scenario(Settings with { AirDodge = preset }, Fast).PlaceInAir(0, F(x), F(y));

    static Fixed Diagonal(Fixed speed) => speed * FixedMath.Sqrt(Fixed.Half);

    static PlayerInput DodgeToward(int x, int y) => In.Stick(x, y, InputButtons.Shield);

    [Fact]
    public void Melee_air_dodge_moves_in_one_of_eight_directions_at_air_dodge_speed()
    {
        var s = InAir(Melee);
        s.Hold(DodgeToward(127, -127));
        Assert.Equal(CharacterAction.AirDodge, s.ActionOf(0));
        Assert.Equal(Diagonal(Fast.AirDodgeSpeed), s[0].VelocityX);
        Assert.Equal(-Diagonal(Fast.AirDodgeSpeed), s[0].VelocityY);

        // A shallow angle snaps to the nearest of the 8 directions: straight left.
        s = InAir(Melee);
        s.Hold(DodgeToward(-127, -30));
        Assert.Equal(-Fast.AirDodgeSpeed, s[0].VelocityX);
        Assert.Equal(F(0), s[0].VelocityY);
    }

    [Fact]
    public void A_small_stick_deflection_is_a_neutral_air_dodge()
    {
        var s = InAir(Melee);
        s[0].VelocityX = F(1);

        s.Hold(DodgeToward(20, 10));

        Assert.Equal(F(0), s[0].VelocityX);
        Assert.Equal(F(0), s[0].VelocityY);
    }

    [Fact]
    public void Air_dodge_velocity_decays_without_gravity_and_grants_intangibility()
    {
        var s = InAir(Melee);
        s.Hold(DodgeToward(127, 0));
        Assert.True(s[0].IntangibleTicks > 0);

        s.Hold(In.Neutral);
        Assert.Equal(Fast.AirDodgeSpeed * Fast.AirDodgeDecay, s[0].VelocityX);
        Assert.Equal(F(0), s[0].VelocityY);

        s.Hold(In.Neutral, Fast.AirDodgeIntangibilityTicks);
        Assert.Equal(0, s[0].IntangibleTicks);
    }

    [Fact]
    public void Melee_preset_leaves_the_character_helpless_until_landing()
    {
        var s = InAir(Melee, x: -65, y: 60);
        s.Hold(DodgeToward(0, 127)).Hold(In.Neutral, Fast.AirDodgeTicks);
        Assert.Equal(CharacterAction.Helpless, s.ActionOf(0));

        s.Hold(In.Jump);
        Assert.Equal(0, s[0].JumpsUsed);

        s.RunUntil(x => x[0].Platform == MainPlatform);
        Assert.Equal(CharacterAction.Landing, s.ActionOf(0));
        Assert.Equal(Fast.HelplessLandingTicks, s[0].ActionDuration);
    }

    [Fact]
    public void Ultimate_preset_is_neutral_only_and_actionable_afterwards()
    {
        var s = InAir(Ultimate);
        s[0].VelocityX = F(1);
        s.Hold(DodgeToward(127, -127));
        var y = s[0].PositionY;

        s.Hold(In.Neutral, Fast.AirDodgeTicks - 1);
        Assert.Equal(CharacterAction.AirDodge, s.ActionOf(0));
        Assert.Equal(y, s[0].PositionY);
        Assert.Equal(F(0), s[0].PositionX);

        s.Hold(In.Neutral);
        Assert.Equal(CharacterAction.Airborne, s.ActionOf(0));
        s.Hold(In.Jump);
        Assert.Equal(1, s[0].JumpsUsed);
    }

    [Fact]
    public void Rivals2_preset_is_directional_zeroes_velocity_at_the_end_and_is_actionable()
    {
        var s = InAir(Rivals2);
        s.Hold(DodgeToward(127, 0));
        Assert.Equal(Fast.AirDodgeSpeed, s[0].VelocityX);

        s.Hold(In.Neutral, Fast.AirDodgeTicks);
        Assert.Equal(CharacterAction.Airborne, s.ActionOf(0));
        Assert.Equal(F(0), s[0].VelocityX);
        Assert.Equal(-Fast.Gravity, s[0].VelocityY); // velocity zeroed, then this Tick's gravity
    }

    [Fact]
    public void Only_one_air_dodge_per_airtime_when_not_helpless()
    {
        var s = InAir(Rivals2, x: -65, y: 40);
        s.Hold(DodgeToward(0, 127)).Hold(In.Neutral, Fast.AirDodgeTicks);

        s.Hold(DodgeToward(0, 127));
        Assert.Equal(CharacterAction.Airborne, s.ActionOf(0));

        s.RunUntil(x => x.ActionOf(0) == CharacterAction.Idle);
        s.Hold(In.Jump).Hold(In.Neutral, 10).Hold(DodgeToward(0, 127));
        Assert.Equal(CharacterAction.AirDodge, s.ActionOf(0));
    }

    [Fact]
    public void Wavedash_an_air_dodge_into_the_ground_right_after_take_off_slides_along_it()
    {
        var s = new Scenario(Settings, Fast).PlaceOnGround(0, MainPlatform, F(0));
        s.Hold(In.Jump).Hold(In.Neutral, Fast.JumpSquatTicks);
        Assert.Equal(CharacterAction.Airborne, s.ActionOf(0));

        s.Hold(DodgeToward(127, -127));
        s.RunUntil(x => x[0].Platform == MainPlatform, maxTicks: 3);

        Assert.Equal(CharacterAction.Landing, s.ActionOf(0));
        Assert.Equal(Fast.AirDodgeLandingTicks, s[0].ActionDuration);
        Assert.True(s[0].VelocityX > Fast.WalkSpeed, $"slides at {s[0].VelocityX}");

        // Traction alone slows the slide; the Character ends up well away from where it jumped.
        s.RunUntil(x => x[0].VelocityX == F(0));
        Assert.True(s[0].PositionX > F(20), $"slid to {s[0].PositionX}");
        Assert.Equal(MainPlatform, s[0].Platform);
    }

    [Fact]
    public void Waveland_an_air_dodge_onto_a_drop_through_platform_lands_and_slides()
    {
        var s = new Scenario(Settings, Fast).PlaceInAir(0, F(30), F(34));

        s.Hold(DodgeToward(127, -127));
        s.RunUntil(x => x[0].Platform >= 0, maxTicks: 5);

        Assert.Equal(RightPlatform, s[0].Platform);
        Assert.Equal(Fast.AirDodgeLandingTicks, s[0].ActionDuration);
        Assert.True(s[0].VelocityX > F(0));
    }

    [Fact]
    public void An_air_dodge_straight_down_does_not_pass_through_the_main_platform()
    {
        var s = new Scenario(Settings, Fast).PlaceInAir(0, F(0), F(2));

        s.Hold(DodgeToward(0, -127));

        Assert.Equal(MainPlatform, s[0].Platform);
        Assert.Equal(F(0), s[0].PositionY);
    }
}
