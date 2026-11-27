using System;
using System.Numerics;

namespace Blastzone.Simulation.Numerics;

/// <summary>
/// Deterministic math on <see cref="Fixed"/> values (ADR 0004). Uses integer arithmetic only:
/// trigonometry interpolates a generated quarter-wave table, and square roots are exact integer roots.
/// Angles are in radians.
/// </summary>
public static partial class FixedMath
{
    /// <summary>Number of interpolation steps in a quarter wave (<see cref="QuarterSineTable"/> has one more entry).</summary>
    public const int TableSteps = 4096;
    const int StepBits = 12;
    const int QuarterMask = TableSteps - 1;
    const int FullWaveMask = 4 * TableSteps - 1;

    const long PiRaw = 13493037705L;      // round(pi * 2^32)
    const long TwoPiRaw = 26986075409L;   // round(2pi * 2^32)
    const long HalfPiRaw = 6746518852L;   // round(pi/2 * 2^32)
    const long StepsPerRadianRaw = 11199533475044L; // round(2 * TableSteps / pi * 2^32)

    public static Fixed Pi => Fixed.FromRaw(PiRaw);
    public static Fixed TwoPi => Fixed.FromRaw(TwoPiRaw);
    public static Fixed HalfPi => Fixed.FromRaw(HalfPiRaw);

    public static Fixed Sin(Fixed angle) => SinOfTablePosition(TablePosition(angle));

    public static Fixed Cos(Fixed angle) =>
        SinOfTablePosition(TablePosition(angle) + ((long)TableSteps << Fixed.FractionalBits));

    /// <summary>The angle reduced to [0, 2pi), as a table position with 32 fractional bits.</summary>
    static long TablePosition(Fixed angle)
    {
        var reduced = angle.Raw % TwoPiRaw;
        if (reduced < 0)
            reduced += TwoPiRaw;
        // reduced < 2^35 and StepsPerRadianRaw < 2^44, so the product fits easily in 128 bits.
        return (long)(((UInt128)(ulong)reduced * (ulong)StepsPerRadianRaw) >> Fixed.FractionalBits);
    }

    static Fixed SinOfTablePosition(long position)
    {
        var index = (int)(position >> Fixed.FractionalBits) & FullWaveMask;
        var fraction = position & 0xFFFFFFFFL;
        var quadrant = index >> StepBits;
        var step = index & QuarterMask;
        var table = QuarterSineTable;

        // Quadrants 1 and 3 walk the quarter wave backwards; quadrants 2 and 3 are negated.
        long from, to;
        if ((quadrant & 1) == 0)
        {
            from = table[step];
            to = table[step + 1];
        }
        else
        {
            from = table[TableSteps - step];
            to = table[TableSteps - step - 1];
        }
        // |to - from| < 2^23 and fraction < 2^32, so this cannot overflow. Round to nearest.
        var value = from + (((to - from) * fraction + (1L << 31)) >> Fixed.FractionalBits);
        return Fixed.FromRaw(quadrant >= 2 ? -value : value);
    }

    /// <summary>The square root, rounded down to the nearest 2^-32. Throws for negative input.</summary>
    public static Fixed Sqrt(Fixed value)
    {
        if (value.Raw < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Square root of a negative number.");

        // sqrt(raw / 2^32) * 2^32 = sqrt(raw * 2^32): an exact integer square root of a 96-bit number,
        // computed digit by digit, two input bits per step. The remainder never exceeds 2 * root < 2^49,
        // so everything fits in 64-bit registers.
        var raw = (ulong)value.Raw;
        if (raw == 0)
            return Fixed.Zero;
        ulong remainder = 0, root = 0;
        for (var pair = (63 - BitOperations.LeadingZeroCount(raw)) / 2; pair >= 0; pair--)
            SqrtStep((raw >> (2 * pair)) & 3, ref remainder, ref root);
        for (var pair = 0; pair < Fixed.FractionalBits / 2; pair++)
            SqrtStep(0, ref remainder, ref root);
        return Fixed.FromRaw((long)root);
    }

    static void SqrtStep(ulong nextTwoBits, ref ulong remainder, ref ulong root)
    {
        // Branchless: the comparison is data-dependent and mispredicts about half the time.
        remainder = (remainder << 2) | nextTwoBits;
        var trial = (root << 2) | 1;
        var fits = remainder >= trial ? 1UL : 0UL;
        remainder -= trial & (0UL - fits);
        root = (root << 1) | fits;
    }
}
