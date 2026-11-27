using Blastzone.Simulation.Numerics;

namespace Blastzone.Tests.FixedPoint;

public class FixedArithmeticTests
{
    const long OneRaw = 1L << 32;

    [Fact]
    public void FromInt_places_the_integer_in_the_high_32_bits()
    {
        Assert.Equal(OneRaw, Fixed.FromInt(1).Raw);
        Assert.Equal(-3 * OneRaw, Fixed.FromInt(-3).Raw);
        Assert.Equal(0, Fixed.Zero.Raw);
        Assert.Equal(OneRaw, Fixed.One.Raw);
        Assert.Equal(OneRaw / 2, Fixed.Half.Raw);
    }

    [Fact]
    public void FromRatio_is_exact_for_dyadic_fractions()
    {
        Assert.Equal(Fixed.FromInt(3) / Fixed.FromInt(2), Fixed.FromRatio(3, 2));
        Assert.Equal(OneRaw / 4, Fixed.FromRatio(1, 4).Raw);
        Assert.Equal(-OneRaw / 4, Fixed.FromRatio(-1, 4).Raw);
    }

    [Fact]
    public void Add_and_subtract()
    {
        Assert.Equal(Fixed.FromInt(5), Fixed.FromInt(2) + Fixed.FromInt(3));
        Assert.Equal(Fixed.FromInt(-1), Fixed.FromInt(2) - Fixed.FromInt(3));
        Assert.Equal(Fixed.FromRatio(3, 4), Fixed.Half + Fixed.FromRatio(1, 4));
        Assert.Equal(Fixed.FromInt(-7), -Fixed.FromInt(7));
    }

    [Fact]
    public void Add_wraps_on_overflow_like_a_64_bit_integer()
    {
        Assert.Equal(Fixed.MinValue, Fixed.MaxValue + Fixed.Epsilon);
        Assert.Equal(Fixed.MaxValue, Fixed.MinValue - Fixed.Epsilon);
    }

    [Theory]
    [InlineData(3, 2, 2, 1, 3, 1)]       // 1.5 * 2 = 3
    [InlineData(-3, 2, 2, 1, -3, 1)]     // -1.5 * 2 = -3
    [InlineData(1, 2, 1, 2, 1, 4)]       // 0.5 * 0.5 = 0.25
    [InlineData(-1, 2, -1, 2, 1, 4)]     // -0.5 * -0.5 = 0.25
    [InlineData(30000, 1, 1, 2, 15000, 1)] // intermediate needs more than 64 bits
    [InlineData(46340, 1, 46340, 1, 2147395600, 1)] // near the top of the range
    public void Multiply_known_values(long an, long ad, long bn, long bd, long en, long ed)
    {
        Assert.Equal(Fixed.FromRatio(en, ed), Fixed.FromRatio(an, ad) * Fixed.FromRatio(bn, bd));
    }

    [Fact]
    public void Multiply_rounds_toward_negative_infinity()
    {
        // 2^-32 * 2^-32 = 2^-64, below one unit: floors to 0 when positive, -1 unit when negative.
        Assert.Equal(Fixed.Zero, Fixed.Epsilon * Fixed.Epsilon);
        Assert.Equal(-Fixed.Epsilon, -Fixed.Epsilon * Fixed.Epsilon);
    }

    [Fact]
    public void Multiply_wraps_to_the_low_64_bits_on_overflow()
    {
        // 65536 * 65536 = 2^32, whose raw value 2^64 wraps to 0.
        Assert.Equal(Fixed.Zero, Fixed.FromInt(65536) * Fixed.FromInt(65536));
    }

    [Fact]
    public void Divide_known_values()
    {
        Assert.Equal(Fixed.FromRatio(3, 2), Fixed.FromInt(3) / Fixed.FromInt(2));
        Assert.Equal(Fixed.FromRatio(-3, 2), Fixed.FromInt(-3) / Fixed.FromInt(2));
        Assert.Equal(Fixed.FromInt(40000), Fixed.FromInt(20000) / Fixed.Half); // intermediate needs 128 bits
        Assert.Equal(Fixed.FromRatio(1, 4), Fixed.Half / Fixed.FromInt(2));
    }

    [Fact]
    public void Divide_truncates_toward_zero()
    {
        Assert.Equal(1431655765L, (Fixed.One / Fixed.FromInt(3)).Raw);
        Assert.Equal(-1431655765L, (-Fixed.One / Fixed.FromInt(3)).Raw);
    }

    [Fact]
    public void Divide_by_zero_throws()
    {
        Assert.Throws<DivideByZeroException>(() => Fixed.One / Fixed.Zero);
    }

    [Fact]
    public void Comparison_and_equality()
    {
        var a = Fixed.FromInt(1);
        var b = Fixed.FromInt(2);
        Assert.True(a < b);
        Assert.True(b > a);
        var alsoOne = Fixed.One;
        Assert.True(a <= alsoOne);
        Assert.True(a >= alsoOne);
        Assert.True(a != b);
        Assert.True(a == Fixed.One);
        Assert.True(-b < a);
        Assert.Equal(-1, a.CompareTo(b));
        Assert.Equal(0, a.CompareTo(Fixed.One));
        Assert.Equal(1, b.CompareTo(a));
        Assert.True(a.Equals(Fixed.One));
        Assert.Equal(a.GetHashCode(), Fixed.One.GetHashCode());
    }

    [Fact]
    public void Floor_and_integer_part()
    {
        Assert.Equal(1, Fixed.FromRatio(3, 2).FloorToInt());
        Assert.Equal(-2, Fixed.FromRatio(-3, 2).FloorToInt());
    }

    [Fact]
    public void ToString_prints_decimal_digits_without_floating_point()
    {
        Assert.Equal("1.5", Fixed.FromRatio(3, 2).ToString());
        Assert.Equal("-0.25", Fixed.FromRatio(-1, 4).ToString());
        Assert.Equal("3", Fixed.FromInt(3).ToString());
    }
}
