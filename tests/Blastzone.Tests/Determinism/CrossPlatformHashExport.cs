using System.Globalization;
using System.Text;
using Blastzone.Simulation.Numerics;

namespace Blastzone.Tests.Determinism;

/// <summary>
/// Produces the file CI compares between macOS ARM64 and Windows x64: a digest of fixed-point results
/// plus the state hash after every Tick of the fixed input stream. Any difference between the two
/// files is a cross-platform desync. Set BLASTZONE_HASH_OUT to a file path to write it.
/// </summary>
public class CrossPlatformHashExport
{
    public const string OutputVariable = "BLASTZONE_HASH_OUT";

    [Fact]
    public void Writes_determinism_hashes_when_requested()
    {
        var text = Build();
        Assert.Equal(text, Build()); // same process, same answer: a cheap sanity check

        var path = Environment.GetEnvironmentVariable(OutputVariable);
        if (!string.IsNullOrEmpty(path))
            File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    static string Build()
    {
        var builder = new StringBuilder();
        builder.Append("# Blastzone determinism hashes, format 2\n");
        builder.Append("fixed-point ").Append(FixedPointDigest().ToString("x16", CultureInfo.InvariantCulture)).Append('\n');

        var hashes = TestMatches.StraightPlayback(InputStreams.Generate());
        for (var tick = 0; tick < hashes.Length; tick++)
            builder.Append("tick ").Append(tick.ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(hashes[tick].ToString("x16", CultureInfo.InvariantCulture)).Append('\n');

        // The scripted stream exercises every movement option (see ScriptedStreamTests).
        var scripted = ScriptedStreams.Generate();
        var match = ScriptedStreams.CreateMatch();
        for (var tick = 0; tick < InputStreams.DefaultTicks; tick++)
        {
            match.Step(TestMatches.InputsAt(scripted, tick));
            builder.Append("scripted ").Append(tick.ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(match.State.ComputeHash().ToString("x16", CultureInfo.InvariantCulture)).Append('\n');
        }
        return builder.ToString();
    }

    /// <summary>Folds the raw results of every Fixed operation over a sweep of inputs into one hash.</summary>
    static ulong FixedPointDigest()
    {
        var hash = 14695981039346656037UL;
        void Mix(Fixed value) => hash = unchecked((hash ^ (ulong)value.Raw) * 1099511628211UL);

        for (var i = -2000; i <= 2000; i++)
        {
            var a = Fixed.FromRatio(i * 7919L, 1000);
            var b = Fixed.FromRatio(i * 104729L + 13, 77);
            Mix(a * b);
            if (b != Fixed.Zero)
                Mix(a / b);
            Mix(FixedMath.Sin(a));
            Mix(FixedMath.Cos(b));
            Mix(FixedMath.Sqrt(a * a));
        }
        return hash;
    }
}
