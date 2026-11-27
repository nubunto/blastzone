using Blastzone.Simulation;

namespace Blastzone.Tests.Determinism;

/// <summary>
/// Deterministic pseudo-random input streams for determinism tests. Uses its own xorshift
/// generator rather than System.Random so the stream is identical on every runtime and OS.
/// </summary>
public static class InputStreams
{
    public const int DefaultTicks = 10_000;
    public const int DefaultPlayers = 4;
    public const ulong DefaultSeed = 0xB1A5_7203_E5EE_D001;

    /// <summary>Inputs laid out tick-major: inputs[tick * players + player].</summary>
    public static PlayerInput[] Generate(ulong seed = DefaultSeed, int ticks = DefaultTicks, int players = DefaultPlayers)
    {
        var inputs = new PlayerInput[ticks * players];
        var state = seed;
        var held = new PlayerInput[players];
        for (var tick = 0; tick < ticks; tick++)
        {
            for (var player = 0; player < players; player++)
            {
                // Players hold an input for a while, like people do, and change it about every 8 Ticks.
                if ((Next(ref state) & 7) == 0)
                {
                    var r = Next(ref state);
                    held[player] = new PlayerInput
                    {
                        StickX = (sbyte)(r % 255 - 127),
                        StickY = (sbyte)((r >> 8) % 255 - 127),
                        Buttons = (ushort)(r >> 16),
                    };
                }
                inputs[tick * players + player] = held[player];
            }
        }
        return inputs;
    }

    static ulong Next(ref ulong state)
    {
        state ^= state << 13;
        state ^= state >> 7;
        state ^= state << 17;
        return state;
    }
}
