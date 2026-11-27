using System;
using Blastzone.Simulation.Numerics;

namespace Blastzone.Simulation;

/// <summary>
/// Advances a Match one Tick at a time from Player inputs alone (ADR 0001).
/// Milestone 1 stub: each Character is a point steered by its Player's stick. Real gameplay is milestone 2.
/// </summary>
public sealed class Match
{
    readonly MatchSettings _settings;

    public Match(MatchSettings settings)
    {
        _settings = settings;
        State = new MatchState(settings.MaxCharacters);
    }

    public MatchState State { get; }

    /// <summary>Simulates one Tick. <paramref name="inputs"/>[i] drives Character i.</summary>
    public void Step(ReadOnlySpan<PlayerInput> inputs)
    {
        if (inputs.Length > State.MaxCharacters)
            throw new ArgumentException($"{inputs.Length} inputs for {State.MaxCharacters} Characters.", nameof(inputs));

        var characters = State.Characters;
        var maxSpeedSquared = _settings.MaxSpeed * _settings.MaxSpeed;
        for (var i = 0; i < inputs.Length; i++)
        {
            ref var c = ref characters[i];
            c.VelocityX = c.VelocityX * _settings.Friction + Fixed.FromInt(inputs[i].StickX) * _settings.Acceleration;
            c.VelocityY = c.VelocityY * _settings.Friction + Fixed.FromInt(inputs[i].StickY) * _settings.Acceleration;

            var speedSquared = c.VelocityX * c.VelocityX + c.VelocityY * c.VelocityY;
            if (speedSquared > maxSpeedSquared)
            {
                var scale = _settings.MaxSpeed / FixedMath.Sqrt(speedSquared);
                c.VelocityX *= scale;
                c.VelocityY *= scale;
            }

            c.PositionX += c.VelocityX;
            c.PositionY += c.VelocityY;
        }
        State.Header.Tick++;
    }

    /// <summary>A state block with this Match's layout, for holding Rollback snapshots.</summary>
    public MatchState CreateSnapshotBuffer() => new(State.MaxCharacters);

    public void SaveSnapshot(MatchState into) => State.CopyTo(into);

    public void RestoreSnapshot(MatchState from) => from.CopyTo(State);
}
