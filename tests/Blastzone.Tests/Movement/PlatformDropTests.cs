using Blastzone.Simulation;
using static Blastzone.Tests.TestData;

namespace Blastzone.Tests.Movement;

public class PlatformDropTests
{
    static Scenario OnSidePlatform() => Scenario.Of(Fast).PlaceOnGround(0, RightPlatform, F(40));

    [Fact]
    public void Tapping_down_on_a_drop_through_platform_falls_through_it()
    {
        var s = OnSidePlatform();

        s.Hold(In.Down);
        Assert.Equal(CharacterAction.Airborne, s.ActionOf(0));
        Assert.Equal(-1, s[0].Platform);

        s.RunUntil(x => x[0].Platform >= 0);
        Assert.Equal(MainPlatform, s[0].Platform);
    }

    [Fact]
    public void Pushing_down_slowly_does_not_drop_through()
    {
        var s = OnSidePlatform();

        s.Hold(In.Stick(0, -50)).Hold(In.Down, 10);

        Assert.Equal(RightPlatform, s[0].Platform);
    }

    [Fact]
    public void Tapping_down_on_the_main_platform_does_nothing()
    {
        var s = Scenario.Of(Fast).PlaceOnGround(0, MainPlatform, F(0));

        s.Hold(In.Down, 10);

        Assert.Equal(MainPlatform, s[0].Platform);
        Assert.Equal(CharacterAction.Idle, s.ActionOf(0));
    }

    [Fact]
    public void A_dropped_through_platform_catches_the_character_again_once_it_is_below_it()
    {
        var s = OnSidePlatform();
        s.Hold(In.Down).Hold(In.Neutral, 3);

        s.Hold(In.Jump);
        Assert.True(s[0].VelocityY > F(0));
        s.RunUntil(x => x[0].Platform >= 0);

        Assert.Equal(RightPlatform, s[0].Platform);
    }
}
