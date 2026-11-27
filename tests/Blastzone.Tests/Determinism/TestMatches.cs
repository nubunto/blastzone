using Blastzone.Simulation;

namespace Blastzone.Tests.Determinism;

static class TestMatches
{
    /// <summary>Four Players alternating the two test Characters.</summary>
    public static Match Create() => new(TestData.Settings, TestData.Stage,
        [TestData.Fast, TestData.Heavy, TestData.Fast, TestData.Heavy]);

    public static ReadOnlySpan<PlayerInput> InputsAt(PlayerInput[] stream, int tick, int players = InputStreams.DefaultPlayers) =>
        stream.AsSpan(tick * players, players);

    /// <summary>Plays the whole stream from a fresh Match and returns the state hash after every Tick.</summary>
    public static ulong[] StraightPlayback(PlayerInput[] stream, int ticks = InputStreams.DefaultTicks)
    {
        var match = Create();
        var hashes = new ulong[ticks];
        for (var tick = 0; tick < ticks; tick++)
        {
            match.Step(InputsAt(stream, tick));
            hashes[tick] = match.State.ComputeHash();
        }
        return hashes;
    }
}
