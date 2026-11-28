using System.Text;
using Blastzone.Simulation.Numerics;

namespace Blastzone.Tests.FixedPoint;

public class FixedDecimalTests
{
    static Fixed Parse(string text) => FixedDecimal.Parse(Encoding.UTF8.GetBytes(text));

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("1", 1L << 32)]
    [InlineData("-3", -3L << 32)]
    [InlineData("0.5", 1L << 31)]
    [InlineData("-0.25", -(1L << 30))]
    [InlineData("12.75", (12L << 32) + (3L << 30))]
    public void Parses_exactly_representable_decimals_exactly(string text, long raw)
    {
        Assert.Equal(raw, Parse(text).Raw);
    }

    [Fact]
    public void Rounds_inexact_decimals_to_nearest()
    {
        // 0.1 * 2^32 = 429496729.6, so the nearest raw value is 429496730.
        Assert.Equal(429496730L, Parse("0.1").Raw);
        Assert.Equal(-429496730L, Parse("-0.1").Raw);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("1.")]
    [InlineData("1e3")]
    [InlineData("abc")]
    [InlineData("3000000000")]
    public void Rejects_malformed_or_out_of_range_text(string text)
    {
        Assert.Throws<FormatException>(() => Parse(text));
    }

    [Fact]
    public void Helpers_behave_like_their_integer_counterparts()
    {
        var two = Fixed.FromInt(2);
        var three = Fixed.FromInt(3);
        Assert.Equal(two, FixedMath.Abs(-two));
        Assert.Equal(two, FixedMath.Min(two, three));
        Assert.Equal(three, FixedMath.Max(two, three));
        Assert.Equal(two, FixedMath.Clamp(Fixed.One, two, three));
        Assert.Equal(-1, FixedMath.Sign(-three));
        Assert.Equal(0, FixedMath.Sign(Fixed.Zero));
        Assert.Equal(Fixed.FromRatio(5, 2), FixedMath.MoveToward(two, three, Fixed.Half));
        Assert.Equal(three, FixedMath.MoveToward(two, three, Fixed.FromInt(5)));
        Assert.Equal(Fixed.FromRatio(3, 2), FixedMath.MoveToward(two, -three, Fixed.Half));
    }
}
