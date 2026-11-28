using System;
using Blastzone.Simulation.Numerics;

namespace Blastzone.Simulation;

/// <summary>An axis-aligned platform. Solid platforms have walls and a ceiling; the rest are drop-through.</summary>
public readonly record struct Platform(Fixed Left, Fixed Right, Fixed Top, Fixed Bottom, bool Solid, bool GrabbableLedges);

/// <summary>A grabbable ledge: a top corner of a solid platform. <see cref="Side"/> is -1 on the left, +1 on the right.</summary>
public readonly record struct Ledge(Fixed X, Fixed Y, int Side, int Platform);

public readonly record struct SpawnPoint(Fixed X, Fixed Y, int Facing);

public readonly record struct Rect(Fixed Left, Fixed Right, Fixed Bottom, Fixed Top)
{
    public bool Contains(Fixed x, Fixed y) => x >= Left && x <= Right && y >= Bottom && y <= Top;
}

/// <summary>The arena a Match is played on: its platforms, ledges, spawn points and Blast Zone.</summary>
public sealed record Stage
{
    public required string Name { get; init; }
    public required Platform[] Platforms { get; init; }
    /// <summary>Spawn point per Player seat.</summary>
    public required SpawnPoint[] Spawns { get; init; }
    public required Rect BlastZone { get; init; }

    /// <summary>Ledges in a fixed order: per platform, left then right.</summary>
    public Ledge[] Ledges
    {
        get
        {
            var count = 0;
            foreach (var p in Platforms)
                if (p.Solid && p.GrabbableLedges)
                    count += 2;
            var ledges = new Ledge[count];
            var n = 0;
            for (var i = 0; i < Platforms.Length; i++)
            {
                var p = Platforms[i];
                if (!p.Solid || !p.GrabbableLedges)
                    continue;
                ledges[n++] = new Ledge(p.Left, p.Top, -1, i);
                ledges[n++] = new Ledge(p.Right, p.Top, 1, i);
            }
            return ledges;
        }
    }
}
