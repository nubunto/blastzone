using System;
using Blastzone.Simulation.Numerics;

namespace Blastzone.Simulation;

/// <summary>
/// Advances a Match one Tick at a time from Player inputs alone (ADR 0001). Settings, the Stage and
/// Character attributes are immutable data; everything that changes lives in <see cref="State"/>.
/// </summary>
public sealed partial class Match
{
    readonly MatchSettings _settings;
    readonly Stage _stage;
    readonly Platform[] _platforms;
    readonly Ledge[] _ledges;
    readonly CharacterAttributes[] _characters;

    /// <param name="characters">The Character each Player drives, by seat.</param>
    public Match(MatchSettings settings, Stage stage, CharacterAttributes[] characters)
    {
        if (characters.Length == 0 || characters.Length > settings.MaxCharacters)
            throw new ArgumentException($"A Match needs 1 to {settings.MaxCharacters} Characters.", nameof(characters));
        if (stage.Spawns.Length < characters.Length)
            throw new ArgumentException($"Stage '{stage.Name}' has {stage.Spawns.Length} spawn points for {characters.Length} Players.", nameof(stage));

        _settings = settings;
        _stage = stage;
        _platforms = stage.Platforms;
        _ledges = stage.Ledges;
        _characters = characters;
        State = new MatchState(settings.MaxCharacters);
        State.Header.CharacterCount = characters.Length;
        for (var i = 0; i < characters.Length; i++)
            Spawn(ref State.Characters[i], i);
    }

    public MatchState State { get; }
    public MatchSettings Settings => _settings;
    public Stage Stage => _stage;
    public ReadOnlySpan<Ledge> Ledges => _ledges;
    public int CharacterCount => _characters.Length;
    public CharacterAttributes Attributes(int character) => _characters[character];

    /// <summary>The Collision Box of a Character in its current action: (left, right, bottom, top).</summary>
    public Rect CollisionBox(int character)
    {
        ref readonly var c = ref State.Characters[character];
        return BoxAt(_characters[character], c.CurrentAction, c.PositionX, c.PositionY);
    }

    static Rect BoxAt(CharacterAttributes a, CharacterAction action, Fixed x, Fixed y)
    {
        var size = a.Box(CharacterActions.Box(action));
        var half = size.Width / Fixed.FromInt(2);
        return new Rect(x - half, x + half, y, y + size.Height);
    }

    /// <summary>Simulates one Tick. <paramref name="inputs"/>[i] drives Character i; missing inputs are neutral.</summary>
    public void Step(ReadOnlySpan<PlayerInput> inputs)
    {
        if (inputs.Length > State.MaxCharacters)
            throw new ArgumentException($"{inputs.Length} inputs for {State.MaxCharacters} Characters.", nameof(inputs));

        var characters = State.Characters;
        for (var i = 0; i < _characters.Length; i++)
        {
            var input = i < inputs.Length ? inputs[i] : default;
            StepCharacter(i, input);
        }
        State.Header.Tick++;
    }

    void StepCharacter(int index, PlayerInput input)
    {
        ref var c = ref State.Characters[index];
        var a = _characters[index];

        UpdateBuffers(ref c, input);
        Think(ref c, a, index, input);
        Move(ref c, a);
        if (!_stage.BlastZone.Contains(c.PositionX, c.PositionY))
            Spawn(ref c, index); // Milestone 2: no Knockouts or Stocks yet, just put the Character back.

        if (c.IntangibleTicks > 0)
            c.IntangibleTicks--;
        if (c.LedgeCooldown > 0)
            c.LedgeCooldown--;
        ExpireBuffers(ref c);
        c.PreviousStickX = input.StickX;
        c.PreviousStickY = input.StickY;
        c.PreviousButtons = input.Buttons;
        c.ActionTick++;
    }

    /// <summary>Puts a Character at its spawn point, standing if the spawn is on a platform top.</summary>
    void Spawn(ref CharacterState c, int index)
    {
        var spawn = _stage.Spawns[index];
        c = default;
        c.PositionX = spawn.X;
        c.PositionY = spawn.Y;
        c.Facing = spawn.Facing;
        c.Platform = -1;
        c.IgnoredPlatform = -1;
        c.Ledge = -1;
        c.CurrentAction = CharacterAction.Airborne;
        for (var p = 0; p < _platforms.Length; p++)
        {
            var platform = _platforms[p];
            if (spawn.Y == platform.Top && spawn.X >= platform.Left && spawn.X <= platform.Right)
            {
                c.Platform = p;
                c.CurrentAction = CharacterAction.Idle;
                break;
            }
        }
    }

    /// <summary>A state block with this Match's layout, for holding Rollback snapshots.</summary>
    public MatchState CreateSnapshotBuffer() => new(State.MaxCharacters);

    public void SaveSnapshot(MatchState into) => State.CopyTo(into);

    public void RestoreSnapshot(MatchState from) => from.CopyTo(State);
}
