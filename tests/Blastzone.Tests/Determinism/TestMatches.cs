using Blastzone.Simulation;
using Blastzone.Simulation.Numerics;

namespace Blastzone.Tests.Determinism;

static class TestMatches
{
    public static MatchSettings Settings => new()
    {
        MaxCharacters = InputStreams.DefaultPlayers,
        Acceleration = Fixed.FromRatio(1, 64),
        Friction = Fixed.FromRatio(15, 16),
        MaxSpeed = Fixed.FromRatio(3, 2),
    };

    public static Match Create() => new(Settings);

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
