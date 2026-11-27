using Blastzone.Simulation.Numerics;

namespace Blastzone.Tests.FixedPoint;

public class FixedMathTests
{
    const double RawPerUnit = 4294967296.0; // 2^32

    static double ToDouble(Fixed f) => f.Raw / RawPerUnit;
    static Fixed FromDouble(double d) => Fixed.FromRaw((long)Math.Round(d * RawPerUnit));

    // Linear interpolation over a 4096-step quarter wave has a worst-case error near 1.8e-8.
    const double TrigTolerance = 1.0 / (1 << 24); // ~6e-8

    [Fact]
    public void Pi_constants_are_correctly_rounded()
    {
        Assert.Equal(13493037705L, FixedMath.Pi.Raw);
        Assert.Equal(26986075409L, FixedMath.TwoPi.Raw);
        Assert.Equal(6746518852L, FixedMath.HalfPi.Raw);
    }

    [Fact]
    public void Sin_and_cos_are_exact_at_quadrant_boundaries()
    {
        Assert.Equal(Fixed.Zero, FixedMath.Sin(Fixed.Zero));
        Assert.Equal(Fixed.One, FixedMath.Cos(Fixed.Zero));
        Assert.Equal(Fixed.One, FixedMath.Sin(FixedMath.HalfPi));
    }

    [Fact]
    public void Sin_and_cos_match_double_precision_over_a_sweep()
    {
        // -50 to +50 radians, including several wraps of the range reduction.
        for (var i = -5000; i <= 5000; i++)
        {
            var x = FromDouble(i * 0.01 + 0.0012345);
            var angle = ToDouble(x);
            Assert.InRange(ToDouble(FixedMath.Sin(x)) - Math.Sin(angle), -TrigTolerance, TrigTolerance);
            Assert.InRange(ToDouble(FixedMath.Cos(x)) - Math.Cos(angle), -TrigTolerance, TrigTolerance);
        }
    }

    [Fact]
    public void Sin_survives_extreme_angles()
    {
        // Range reduction must not overflow at the ends of the Q32.32 range.
        var s1 = ToDouble(FixedMath.Sin(Fixed.MaxValue));
        var s2 = ToDouble(FixedMath.Cos(Fixed.MinValue));
        Assert.InRange(s1, -1.0, 1.0);
        Assert.InRange(s2, -1.0, 1.0);
    }

    [Fact]
    public void Sin_table_matches_correctly_rounded_double_sine()
    {
        var table = FixedMath.QuarterSineTable;
        Assert.Equal(FixedMath.TableSteps + 1, table.Length);
        for (var i = 0; i < table.Length; i++)
        {
            var expected = (long)Math.Round(Math.Sin(i * (Math.PI / 2) / FixedMath.TableSteps) * RawPerUnit);
            Assert.InRange(table[i] - expected, -1, 1);
        }
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(9, 3)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    [InlineData(1073741824, 32768)]
    public void Sqrt_of_perfect_squares_is_exact(int value, int root)
    {
        Assert.Equal(Fixed.FromInt(root), FixedMath.Sqrt(Fixed.FromInt(value)));
    }

    [Fact]
    public void Sqrt_rounds_down_to_the_nearest_unit()
    {
        Assert.Equal(6074000999L, FixedMath.Sqrt(Fixed.FromInt(2)).Raw);
        Assert.Equal(65536L, FixedMath.Sqrt(Fixed.Epsilon).Raw); // sqrt(2^-32) = 2^-16
        Assert.Equal(Fixed.Half, FixedMath.Sqrt(Fixed.FromRatio(1, 4)));
    }

    [Fact]
    public void Sqrt_of_max_value_does_not_overflow()
    {
        var root = ToDouble(FixedMath.Sqrt(Fixed.MaxValue));
        Assert.InRange(root - Math.Sqrt(ToDouble(Fixed.MaxValue)), -1e-9, 1e-9);
    }

    [Fact]
    public void Sqrt_of_negative_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FixedMath.Sqrt(-Fixed.One));
    }
}
