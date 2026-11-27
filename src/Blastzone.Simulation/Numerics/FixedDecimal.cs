using System;

namespace Blastzone.Simulation.Numerics;

/// <summary>
/// Converts decimal text (as written in JSON data) to <see cref="Fixed"/> with integer arithmetic only,
/// so loading data is deterministic too (ADR 0004). Values are rounded to the nearest 2^-32.
/// </summary>
public static class FixedDecimal
{
    const int MaxDigits = 18; // 10^18 < 2^63, so the digits always fit in a long

    /// <summary>Parses <c>-?digits(.digits)?</c>. Throws <see cref="FormatException"/> on anything else or out of range.</summary>
    public static Fixed Parse(ReadOnlySpan<byte> utf8)
    {
        var i = 0;
        var negative = i < utf8.Length && utf8[i] == (byte)'-';
        if (negative)
            i++;

        long digits = 0, scale = 1;
        int count = 0, integerDigits = 0;
        var seenPoint = false;
        var fractionDigits = 0;
        for (; i < utf8.Length; i++)
        {
            var b = utf8[i];
            if (b == (byte)'.' && !seenPoint)
            {
                seenPoint = true;
                continue;
            }
            if (b < (byte)'0' || b > (byte)'9')
                throw new FormatException($"'{System.Text.Encoding.UTF8.GetString(utf8)}' is not a plain decimal number.");
            if (count == MaxDigits)
                throw new FormatException("Too many digits.");
            digits = digits * 10 + (b - '0');
            count++;
            if (seenPoint)
            {
                scale *= 10;
                fractionDigits++;
            }
            else
                integerDigits++;
        }
        if (integerDigits == 0 || (seenPoint && fractionDigits == 0))
            throw new FormatException($"'{System.Text.Encoding.UTF8.GetString(utf8)}' is not a plain decimal number.");

        // round(digits * 2^32 / scale), computed exactly in 128 bits.
        var raw = (((Int128)digits << Fixed.FractionalBits) + scale / 2) / scale;
        if (raw > long.MaxValue)
            throw new FormatException("Value is out of the Q32.32 range.");
        return Fixed.FromRaw(negative ? -(long)raw : (long)raw);
    }
}
