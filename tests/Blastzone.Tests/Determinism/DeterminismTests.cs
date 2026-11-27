using Blastzone.Simulation;

namespace Blastzone.Tests.Determinism;

public class DeterminismTests
{
    [Fact]
    public void Same_input_stream_run_twice_gives_identical_hashes_on_every_tick()
    {
        var stream = InputStreams.Generate();

        var first = TestMatches.StraightPlayback(stream);
        var second = TestMatches.StraightPlayback(stream);

        for (var tick = 0; tick < first.Length; tick++)
            Assert.True(first[tick] == second[tick], $"Hashes diverge at Tick {tick}");
    }

    [Fact]
    public void Different_input_streams_give_different_hashes()
    {
        var a = TestMatches.StraightPlayback(InputStreams.Generate(seed: 1), ticks: 600);
        var b = TestMatches.StraightPlayback(InputStreams.Generate(seed: 2), ticks: 600);

        Assert.NotEqual(a[^1], b[^1]);
    }

    [Fact]
    public void The_state_changes_every_tick()
    {
        // Guards against a hash that ignores the state: at least the Tick counter moves.
        var hashes = TestMatches.StraightPlayback(InputStreams.Generate(), ticks: 600);

        for (var tick = 1; tick < hashes.Length; tick++)
            Assert.NotEqual(hashes[tick - 1], hashes[tick]);
    }

    [Fact]
    public void More_inputs_than_characters_is_rejected()
    {
        var match = TestMatches.Create();
        var tooMany = new PlayerInput[InputStreams.DefaultPlayers + 1];

        Assert.Throws<ArgumentException>(() => match.Step(tooMany));
    }
}
