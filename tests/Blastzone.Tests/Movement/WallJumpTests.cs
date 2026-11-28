using Blastzone.Simulation;
using Blastzone.Simulation.Numerics;
using static Blastzone.Tests.TestData;

namespace Blastzone.Tests.Movement;

/// <summary>Wall Jump: airborne next to a wall, tap the stick toward the wall to jump away from it.</summary>
public class WallJumpTests
{
    /// <summary>
    /// Airborne beside the main platform's left wall, with <paramref name="gap"/> between box and wall,
    /// low enough to be out of ledge grab range.
    /// </summary>
    static Scenario BesideLeftWall(CharacterAttributes a, Fixed gap, int y = -30)
    {
        var x = F(-70) - a.AirborneBox.Width / F(2) - gap;
        return Scenario.Of(a).PlaceInAir(0, x, F(y));
    }

    [Fact]
    public void Tapping_toward_a_touching_wall_jumps_away_from_it()
    {
        var s = BesideLeftWall(Fast, F(0));

        s.Hold(In.Right);

        Assert.Equal(-Fast.WallJumpSpeedX, s[0].VelocityX);
        Assert.Equal(Fast.WallJumpSpeedY, s[0].VelocityY);
        Assert.Equal(-1, s[0].Facing);
        Assert.Equal(0, s[0].JumpsUsed);
    }

    [Fact]
    public void A_wall_within_reach_counts()
    {
        var s = BesideLeftWall(Fast, Settings.WallJumpReach - F("0.5"));

        s.Hold(In.Right);

        Assert.Equal(Fast.WallJumpSpeedY, s[0].VelocityY);
    }

    [Fact]
    public void A_wall_out_of_reach_does_not()
    {
        var s = BesideLeftWall(Fast, Settings.WallJumpReach + F(1));

        s.Hold(In.Right);

        Assert.True(s[0].VelocityY < F(0));
    }

    [Fact]
    public void Tapping_away_from_the_wall_does_not_wall_jump()
    {
        var s = BesideLeftWall(Fast, F(0));

        s.Hold(In.Left);

        Assert.True(s[0].VelocityY < F(0));
    }

    [Fact]
    public void Only_the_wall_beside_the_collision_box_counts()
    {
        // Above the platform top, the box is beside no wall.
        var s = BesideLeftWall(Fast, F(0), y: 10);

        s.Hold(In.Right);

        Assert.True(s[0].VelocityY < F(0));
    }

    [Fact]
    public void A_character_without_wall_jump_cannot()
    {
        var s = BesideLeftWall(Heavy, F(0));

        s.Hold(In.Right);

        Assert.True(s[0].VelocityY < F(0));
    }
}
