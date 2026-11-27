using System;
using System.Globalization;

namespace Blastzone.Simulation.Numerics;

/// <summary>
/// A Q32.32 fixed-point number (ADR 0004): 32 integer bits, 32 fractional bits, stored in one
/// <see cref="long"/>. One unit of <see cref="Raw"/> is 2^-32. The range is [-2^31, 2^31 - 2^-32].
/// </summary>
/// <remarks>
/// Overflow behaviour is deterministic and cheap: everything wraps like a 64-bit integer.
/// <list type="bullet">
/// <item><c>+</c>, <c>-</c> and negation wrap on overflow (two's complement), exactly like <c>long</c>.</item>
/// <item><c>*</c> computes the exact 128-bit product, shifts it right by 32 (rounding toward negative
/// infinity) and keeps the low 64 bits, so out-of-range products wrap.</item>
/// <item><c>/</c> computes <c>(a &lt;&lt; 32) / b</c> in 128 bits, truncating toward zero, and keeps the
/// low 64 bits. Dividing by zero throws <see cref="DivideByZeroException"/>.</item>
/// </list>
/// Gameplay values are expected to stay far inside the range; wrapping keeps the Simulation
/// bit-identical across machines even when they do not.
/// </remarks>
public readonly struct Fixed : IEquatable<Fixed>, IComparable<Fixed>
{
    public const int FractionalBits = 32;
    const long OneRaw = 1L << FractionalBits;
    const ulong FractionMask = (1UL << FractionalBits) - 1;

    /// <summary>The underlying Q32.32 value: the number times 2^32.</summary>
    public readonly long Raw;

    Fixed(long raw) => Raw = raw;

    public static Fixed Zero => default;
    public static Fixed One => new(OneRaw);
    public static Fixed Half => new(OneRaw / 2);
    /// <summary>The smallest positive value, 2^-32.</summary>
    public static Fixed Epsilon => new(1);
    public static Fixed MaxValue => new(long.MaxValue);
    public static Fixed MinValue => new(long.MinValue);

    public static Fixed FromRaw(long raw) => new(raw);
    public static Fixed FromInt(int value) => new((long)value << FractionalBits);

    /// <summary><paramref name="numerator"/> / <paramref name="denominator"/>, truncated toward zero.</summary>
    public static Fixed FromRatio(long numerator, long denominator) =>
        new((long)(((Int128)numerator << FractionalBits) / denominator));

    /// <summary>The largest integer less than or equal to this value.</summary>
    public int FloorToInt() => (int)(Raw >> FractionalBits);

    public static Fixed operator +(Fixed a, Fixed b) => new(unchecked(a.Raw + b.Raw));
    public static Fixed operator -(Fixed a, Fixed b) => new(unchecked(a.Raw - b.Raw));
    public static Fixed operator -(Fixed a) => new(unchecked(-a.Raw));

    public static Fixed operator *(Fixed a, Fixed b) =>
        new(unchecked((long)(((Int128)a.Raw * b.Raw) >> FractionalBits)));

    public static Fixed operator /(Fixed a, Fixed b) =>
        new(unchecked((long)(((Int128)a.Raw << FractionalBits) / b.Raw)));

    public static bool operator ==(Fixed a, Fixed b) => a.Raw == b.Raw;
    public static bool operator !=(Fixed a, Fixed b) => a.Raw != b.Raw;
    public static bool operator <(Fixed a, Fixed b) => a.Raw < b.Raw;
    public static bool operator >(Fixed a, Fixed b) => a.Raw > b.Raw;
    public static bool operator <=(Fixed a, Fixed b) => a.Raw <= b.Raw;
    public static bool operator >=(Fixed a, Fixed b) => a.Raw >= b.Raw;

    public int CompareTo(Fixed other) => Raw.CompareTo(other.Raw);
    public bool Equals(Fixed other) => Raw == other.Raw;
    public override bool Equals(object? obj) => obj is Fixed other && Raw == other.Raw;
    public override int GetHashCode() => Raw.GetHashCode();

    /// <summary>Decimal text with up to 10 fractional digits, computed with integers only.</summary>
    public override string ToString()
    {
        var negative = Raw < 0;
        var magnitude = negative ? unchecked((ulong)-Raw) : (ulong)Raw;
        var text = (negative ? "-" : "") + (magnitude >> FractionalBits).ToString(CultureInfo.InvariantCulture);

        var fraction = magnitude & FractionMask;
        if (fraction == 0)
            return text;

        Span<char> digits = stackalloc char[10];
        var count = 0;
        while (fraction != 0 && count < digits.Length)
        {
            fraction *= 10;
            digits[count++] = (char)('0' + (int)(fraction >> FractionalBits));
            fraction &= FractionMask;
        }
        return text + "." + new string(digits[..count]);
    }
}
