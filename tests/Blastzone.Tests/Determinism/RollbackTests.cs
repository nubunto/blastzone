using Blastzone.Simulation;

namespace Blastzone.Tests.Determinism;

public class RollbackTests
{
    const int RollbackTicks = 8;

    [Fact]
    public void Restoring_a_snapshot_from_8_ticks_back_and_resimulating_matches_straight_playback()
    {
        var stream = InputStreams.Generate();
        var straight = TestMatches.StraightPlayback(stream);

        var match = TestMatches.Create();
        // Ring of snapshots: snapshots[t % N] holds the state *before* Tick t is simulated.
        var snapshots = new MatchState[RollbackTicks + 1];
        for (var i = 0; i < snapshots.Length; i++)
            snapshots[i] = match.CreateSnapshotBuffer();

        for (var tick = 0; tick < InputStreams.DefaultTicks; tick++)
        {
            match.SaveSnapshot(snapshots[tick % snapshots.Length]);
            match.Step(TestMatches.InputsAt(stream, tick));

            if (tick + 1 < RollbackTicks)
                continue;

            // Roll back to the start of Tick (tick - 7), i.e. 8 Ticks ago, and re-simulate up to now.
            var from = tick + 1 - RollbackTicks;
            match.RestoreSnapshot(snapshots[from % snapshots.Length]);
            for (var t = from; t <= tick; t++)
                match.Step(TestMatches.InputsAt(stream, t));

            Assert.True(straight[tick] == match.State.ComputeHash(), $"Rollback diverges at Tick {tick}");
        }
    }

    [Fact]
    public void Rollback_corrects_a_misprediction()
    {
        // The real use: remote inputs were predicted wrongly for 8 Ticks, then the true inputs arrive.
        var stream = InputStreams.Generate(ticks: 600);
        var straight = TestMatches.StraightPlayback(stream, ticks: 600);
        var match = TestMatches.Create();
        var snapshot = match.CreateSnapshotBuffer();
        const int divergeAt = 300;

        for (var tick = 0; tick < divergeAt; tick++)
            match.Step(TestMatches.InputsAt(stream, tick));
        match.SaveSnapshot(snapshot);

        var predicted = new PlayerInput[InputStreams.DefaultPlayers];
        for (var tick = divergeAt; tick < divergeAt + RollbackTicks; tick++)
        {
            TestMatches.InputsAt(stream, tick).CopyTo(predicted);
            predicted[3] = new PlayerInput { StickX = 100, StickY = -100 }; // wrong guess for remote Player 4
            match.Step(predicted);
        }
        Assert.NotEqual(straight[divergeAt + RollbackTicks - 1], match.State.ComputeHash());

        match.RestoreSnapshot(snapshot);
        for (var tick = divergeAt; tick < divergeAt + RollbackTicks; tick++)
            match.Step(TestMatches.InputsAt(stream, tick));

        Assert.Equal(straight[divergeAt + RollbackTicks - 1], match.State.ComputeHash());
    }

    [Fact]
    public void A_snapshot_is_an_independent_copy()
    {
        var stream = InputStreams.Generate(ticks: 100);
        var match = TestMatches.Create();
        for (var tick = 0; tick < 50; tick++)
            match.Step(TestMatches.InputsAt(stream, tick));

        var snapshot = match.CreateSnapshotBuffer();
        match.SaveSnapshot(snapshot);
        var savedHash = match.State.ComputeHash();
        Assert.Equal(savedHash, snapshot.ComputeHash());

        for (var tick = 50; tick < 100; tick++)
            match.Step(TestMatches.InputsAt(stream, tick));
        Assert.Equal(savedHash, snapshot.ComputeHash());

        match.RestoreSnapshot(snapshot);
        Assert.Equal(savedHash, match.State.ComputeHash());
        Assert.Equal(50, match.State.Header.Tick);
    }
}
