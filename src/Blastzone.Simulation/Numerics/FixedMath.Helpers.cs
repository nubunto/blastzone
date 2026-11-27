namespace Blastzone.Simulation.Numerics;

public static partial class FixedMath
{
    public static Fixed Abs(Fixed value) => value.Raw < 0 ? -value : value;

    public static Fixed Min(Fixed a, Fixed b) => a <= b ? a : b;

    public static Fixed Max(Fixed a, Fixed b) => a >= b ? a : b;

    public static Fixed Clamp(Fixed value, Fixed min, Fixed max) => value < min ? min : value > max ? max : value;

    /// <summary>-1, 0 or 1.</summary>
    public static int Sign(Fixed value) => value.Raw < 0 ? -1 : value.Raw > 0 ? 1 : 0;

    /// <summary>Moves <paramref name="value"/> toward <paramref name="target"/> by at most <paramref name="step"/> (non-negative), without overshooting.</summary>
    public static Fixed MoveToward(Fixed value, Fixed target, Fixed step)
    {
        if (value < target)
            return Min(value + step, target);
        if (value > target)
            return Max(value - step, target);
        return value;
    }
}
