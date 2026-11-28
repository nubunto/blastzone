using Blastzone.Simulation;

namespace Blastzone.Tests.Determinism;

/// <summary>Determinism and Rollback over a long stream that exercises the whole movement set.</summary>
public class ScriptedStreamTests
{
    const int RollbackTicks = 8;

    static ulong[] StraightPlayback(PlayerInput[] stream, int ticks)
    {
        var match = ScriptedStreams.CreateMatch();
        var hashes = new ulong[ticks];
        for (var tick = 0; tick < ticks; tick++)
        {
            match.Step(TestMatches.InputsAt(stream, tick));
            hashes[tick] = match.State.ComputeHash();
        }
        return hashes;
    }

    [Fact]
    public void The_script_covers_every_movement_option()
    {
        var stream = ScriptedStreams.Generate();
        var match = ScriptedStreams.CreateMatch();
        var coverage = new MovementCoverage(match.CharacterCount);

        for (var tick = 0; tick < ScriptedStreams.ScriptedTicks; tick++)
        {
            match.Step(TestMatches.InputsAt(stream, tick));
            coverage.Observe(match);
        }

        Assert.Empty(coverage.Missing);
    }

    [Fact]
    public void The_scripted_stream_run_twice_gives_identical_hashes_on_every_tick()
    {
        var stream = ScriptedStreams.Generate();

        var first = StraightPlayback(stream, InputStreams.DefaultTicks);
        var second = StraightPlayback(stream, InputStreams.DefaultTicks);

        for (var tick = 0; tick < first.Length; tick++)
            Assert.True(first[tick] == second[tick], $"Hashes diverge at Tick {tick}");
    }

    [Fact]
    public void Rolling_back_8_ticks_on_every_tick_of_the_scripted_stream_matches_straight_playback()
    {
        var stream = ScriptedStreams.Generate();
        var straight = StraightPlayback(stream, InputStreams.DefaultTicks);

        var match = ScriptedStreams.CreateMatch();
        var snapshots = new MatchState[RollbackTicks + 1];
        for (var i = 0; i < snapshots.Length; i++)
            snapshots[i] = match.CreateSnapshotBuffer();

        for (var tick = 0; tick < InputStreams.DefaultTicks; tick++)
        {
            match.SaveSnapshot(snapshots[tick % snapshots.Length]);
            match.Step(TestMatches.InputsAt(stream, tick));
            if (tick + 1 < RollbackTicks)
                continue;

            var from = tick + 1 - RollbackTicks;
            match.RestoreSnapshot(snapshots[from % snapshots.Length]);
            for (var t = from; t <= tick; t++)
                match.Step(TestMatches.InputsAt(stream, t));

            Assert.True(straight[tick] == match.State.ComputeHash(), $"Rollback diverges at Tick {tick}");
        }
    }
}
