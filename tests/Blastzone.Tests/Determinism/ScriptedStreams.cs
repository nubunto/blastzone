using Blastzone.Simulation;

namespace Blastzone.Tests.Determinism;

/// <summary>
/// A hand-written input stream for 4 Players that drives every movement option at least once
/// (checked by <see cref="MovementCoverage"/>), padded with the pseudo-random stream to a long run.
/// </summary>
public static class ScriptedStreams
{
    /// <summary>Settings for the scripted Match: Ledge Trump and ledge-exit Intangibility on, so they get covered too.</summary>
    public static MatchSettings Settings => TestData.Settings with
    {
        Ledge = TestData.Settings.Ledge with { Trump = true, ExitIntangibilityTicks = 10 },
    };

    public static Match CreateMatch() => new(Settings, TestData.Stage,
        [TestData.Fast, TestData.Heavy, TestData.Fast, TestData.Heavy]);

    sealed class Script
    {
        public readonly List<PlayerInput> Inputs = [];
        public Script Hold(PlayerInput input, int ticks = 1)
        {
            for (var i = 0; i < ticks; i++)
                Inputs.Add(input);
            return this;
        }
        public Script Repeat(int times, Action<Script> body)
        {
            for (var i = 0; i < times; i++)
                body(this);
            return this;
        }
    }

    static readonly PlayerInput N = In.Neutral;
    static PlayerInput S(int x, int y = 0, ushort b = 0) => In.Stick(x, y, b);
    const ushort J = InputButtons.Jump, Sh = InputButtons.Shield;

    /// <summary>Player 1 (Fast, spawns at x -35 on the main platform): ground movement, jumps, air dodges.</summary>
    static Script Player1() => new Script()
        .Hold(N, 10)
        .Hold(S(60), 20).Hold(N, 10)                                  // walk
        .Repeat(5, s => s.Hold(S(127), 3).Hold(S(-127), 3))          // dash, Dash Dance
        .Hold(N, 10)
        .Hold(S(127), 25)                                            // dash into run
        .Hold(S(-127), 14)                                           // run turn
        .Hold(N, 30)                                                 // slide to idle
        .Hold(S(0, 0, J)).Hold(N, 40)                                // short hop
        .Hold(S(0, 0, J), 30).Hold(N, 40)                            // full hop
        .Hold(S(0, 0, J)).Hold(N, 6).Hold(S(0, 0, J)).Hold(N, 6)     // double jumps
        .Hold(S(0, 0, J)).Hold(N, 20).Hold(S(0, -127)).Hold(N, 60)   // fast-fall
        .Hold(S(0, 0, J)).Hold(S(127, -127, Sh), 5).Hold(N, 40)      // wavedash right
        .Hold(S(0, 0, J)).Hold(S(-127, -127, Sh), 5).Hold(N, 40)     // wavedash left
        .Hold(S(0, 0, J)).Hold(N, 12).Hold(S(0, 127, Sh)).Hold(N, 90) // air dodge up, helpless, helpless landing
        .Hold(S(127), 90).Hold(N, 200);                              // run off the right edge, fall out, respawn

    /// <summary>Player 3 (Fast, spawns on the left platform): drop-through, Teeter, ledge options, Wall Jump, ledgedash.</summary>
    static Script Player3() => new Script()
        .Hold(N, 10)
        .Hold(S(0, -127)).Hold(N, 40)                                // drop through the left platform
        .Hold(S(-60), 60)                                            // walk to the left edge: Teeter
        .Hold(N, 5)
        .Hold(S(0, 0, J), 5).Hold(S(-50), 4).Hold(N, 50)             // hop off the edge back onto the ledge
        .Hold(N, 10).Hold(S(127)).Hold(N, 40)                        // getup
        .Hold(S(-60), 30).Hold(N, 5)
        .Hold(S(0, 0, J), 5).Hold(S(-50), 4).Hold(N, 50)
        .Hold(N, 10).Hold(S(0, 0, Sh)).Hold(N, 50)                   // roll
        .Hold(S(-60), 50).Hold(N, 5)
        .Hold(S(0, 0, J), 5).Hold(S(-50), 4).Hold(N, 50)
        .Hold(N, 10).Hold(S(0, 0, J)).Hold(N, 60)                    // ledge jump, then fall back to the ledge
        .Hold(N, 10).Hold(S(0, -127)).Hold(N, 8).Hold(S(127))        // drop, Wall Jump
        .Hold(S(0, 0, J)).Hold(S(70), 40).Hold(N, 40)                // recover to the Stage or ledge
        .Hold(S(-60), 50).Hold(N, 5)
        .Hold(S(0, 0, J), 5).Hold(S(-50), 4).Hold(N, 50)
        .Hold(N, 10).Hold(S(0, -127)).Hold(N).Hold(S(0, 0, J))       // ledgedash: drop, double jump,
        .Hold(S(70), 14).Hold(S(127, -127, Sh)).Hold(N, 40);         // drift in, air dodge down

    /// <summary>Player 2 (Heavy, spawns at x 35): grabs the right ledge and holds it.</summary>
    static Script Player2() => new Script()
        .Hold(N, 10)
        .Hold(S(30)).Hold(S(60)).Hold(S(120), 60).Hold(N, 5)         // walk (no tap) to the right edge: Teeter
        .Hold(S(0, 0, J), 7).Hold(S(50), 4).Hold(N, 200)             // hop onto the right ledge and hang
        .Hold(N, 60);

    /// <summary>Player 4 (Heavy, spawns on the right platform): Ledge Trumps Player 2.</summary>
    static Script Player4() => new Script()
        .Hold(N, 100)
        .Hold(S(0, -127)).Hold(N, 40)                                // drop through the right platform
        .Hold(S(30)).Hold(S(60)).Hold(S(120), 50).Hold(N, 5)
        .Hold(S(0, 0, J), 7).Hold(S(50), 4).Hold(N, 100);            // grab the occupied right ledge: Ledge Trump

    /// <summary>The scripted part, then pseudo-random inputs up to <paramref name="ticks"/>. Tick-major, 4 Players.</summary>
    public static PlayerInput[] Generate(int ticks = InputStreams.DefaultTicks)
    {
        var scripts = new[] { Player1(), Player2(), Player3(), Player4() };
        var scripted = scripts.Max(s => s.Inputs.Count);
        if (ticks < scripted)
            throw new ArgumentOutOfRangeException(nameof(ticks), $"The script alone is {scripted} Ticks.");

        var random = InputStreams.Generate(ticks: ticks);
        var stream = new PlayerInput[ticks * 4];
        for (var tick = 0; tick < ticks; tick++)
            for (var p = 0; p < 4; p++)
            {
                var script = scripts[p].Inputs;
                stream[tick * 4 + p] = tick < scripted
                    ? (tick < script.Count ? script[tick] : default)
                    : random[tick * 4 + p];
            }
        return stream;
    }

    public static int ScriptedTicks => new[] { Player1(), Player2(), Player3(), Player4() }.Max(s => s.Inputs.Count);
}

/// <summary>Which movement options a Match went through, observed from its state after every Tick.</summary>
public sealed class MovementCoverage
{
    readonly HashSet<string> _seen = [];
    readonly CharacterState[] _previous;

    public MovementCoverage(int characters) => _previous = new CharacterState[characters];

    public static readonly string[] All =
    [
        .. Enum.GetNames<CharacterAction>(),
        "DoubleJump", "ShortHop", "FullHop", "FastFall", "DropThrough", "Wavedash", "HelplessLanding",
        "WallJump", "LedgeRegrab", "LedgeExitIntangibility", "RunOffEdge",
    ];

    public IEnumerable<string> Missing => All.Where(o => !_seen.Contains(o));

    public void Observe(Match match)
    {
        var characters = match.State.Characters;
        for (var i = 0; i < match.CharacterCount; i++)
        {
            ref readonly var c = ref characters[i];
            var before = _previous[i];
            var a = match.Attributes(i);
            _seen.Add(c.CurrentAction.ToString());
            if (c.JumpsUsed > before.JumpsUsed)
                _seen.Add("DoubleJump");
            if (before.CurrentAction == CharacterAction.JumpSquat && c.CurrentAction == CharacterAction.Airborne)
                _seen.Add(c.VelocityY == a.JumpVelocity(a.FullHopHeight) ? "FullHop" : "ShortHop");
            if (c.FastFalling != 0)
                _seen.Add("FastFall");
            if (c.IgnoredPlatform >= 0)
                _seen.Add("DropThrough");
            if (c.CurrentAction == CharacterAction.Landing && before.CurrentAction == CharacterAction.AirDodge)
                _seen.Add("Wavedash");
            if (c.CurrentAction == CharacterAction.Landing && before.CurrentAction == CharacterAction.Helpless)
                _seen.Add("HelplessLanding");
            if (a.CanWallJump && c.VelocityY == a.WallJumpSpeedY && before.VelocityY != a.WallJumpSpeedY)
                _seen.Add("WallJump");
            if (c.CurrentAction == CharacterAction.LedgeHang && c.Regrabs >= 2)
                _seen.Add("LedgeRegrab");
            if (CharacterActions.IsOnLedge(before.CurrentAction) && c.CurrentAction == CharacterAction.Airborne && c.IntangibleTicks > 0)
                _seen.Add("LedgeExitIntangibility");
            if (before.CurrentAction is CharacterAction.Run or CharacterAction.Dash && c.CurrentAction == CharacterAction.Airborne && before.Platform >= 0)
                _seen.Add("RunOffEdge");
            _previous[i] = c;
        }
    }
}
