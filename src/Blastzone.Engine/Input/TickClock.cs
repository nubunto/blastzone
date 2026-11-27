namespace Blastzone.Engine.Input;

/// <summary>
/// Maps SDL's nanosecond clock to Ticks. Tick k covers [start + k/60 s, start + (k+1)/60 s) and is
/// simulated once that interval has ended, so every input event inside it is known by then.
/// </summary>
public sealed class TickClock(ulong startNs)
{
    public const int TicksPerSecond = 60; // glossary: exactly 60 Ticks per second
    const ulong NsPerSecond = 1_000_000_000;

    public ulong StartNs { get; } = startNs;

    /// <summary>End of Tick <paramref name="tick"/>'s interval, in SDL nanoseconds.</summary>
    public ulong TickEndNs(long tick) => StartNs + (ulong)(tick + 1) * NsPerSecond / TicksPerSecond;

    /// <summary>How many Ticks have fully elapsed at <paramref name="nowNs"/>.</summary>
    public long CompletedTicks(ulong nowNs)
    {
        if (nowNs <= StartNs)
            return 0;
        // Consistent with TickEndNs's rounding: Tick k is complete once nowNs reaches TickEndNs(k).
        var ticks = (long)((nowNs - StartNs) * TicksPerSecond / NsPerSecond);
        return TickEndNs(ticks) <= nowNs ? ticks + 1 : ticks;
    }

    /// <summary>Fraction (0 to 1) of the way from the last simulated Tick to the next, for render interpolation.</summary>
    public double Interpolation(ulong nowNs, long completedTicks)
    {
        var last = completedTicks == 0 ? StartNs : TickEndNs(completedTicks - 1);
        if (nowNs <= last)
            return 0;
        var fraction = (nowNs - last) / (double)(NsPerSecond / TicksPerSecond);
        return fraction > 1 ? 1 : fraction;
    }
}
