using Blastzone.Simulation;
using Blastzone.Simulation.Numerics;
using Brutal.Numerics;

namespace Blastzone.Engine.Render;

/// <summary>
/// Turns Simulation state into shapes to draw. Read-only (ADR 0008): it interpolates Character positions
/// between the last two snapshots and never writes back. Characters are capsules generated from their
/// Hurtboxes; the debug display adds Hurtbox outlines, Collision Boxes, Ground Probes and ledges.
/// </summary>
public static class MatchView
{
    static readonly float4 SolidPlatform = new(0.38f, 0.40f, 0.46f, 1);
    static readonly float4 PassPlatform = new(0.55f, 0.58f, 0.64f, 1);
    static readonly float4[] PlayerColors =
    [
        new(0.90f, 0.30f, 0.28f, 1),
        new(0.27f, 0.52f, 0.92f, 1),
        new(0.32f, 0.78f, 0.40f, 1),
        new(0.93f, 0.78f, 0.25f, 1),
    ];
    static readonly float4 TeeterMarker = new(1.0f, 0.62f, 0.10f, 1);
    static readonly float4 HurtboxDebug = new(1.0f, 0.95f, 0.30f, 1);
    static readonly float4 CollisionBoxDebug = new(0.25f, 1.0f, 0.45f, 1);
    static readonly float4 GroundProbeDebug = new(1.0f, 0.25f, 0.95f, 1);
    static readonly float4 LedgeDebug = new(0.30f, 0.95f, 1.0f, 1);
    const float DropThroughThickness = 1.5f;
    const float DebugLine = 0.45f;

    public static float ToFloat(Fixed value) => (float)(value.Raw / 4294967296.0);

    public static void Build(Match match, MatchState previous, double alpha, bool debug, List<Shape> shapes)
    {
        shapes.Clear();
        foreach (var p in match.Stage.Platforms)
        {
            var top = ToFloat(p.Top);
            shapes.Add(p.Solid
                ? Shape.Rect(ToFloat(p.Left), ToFloat(p.Bottom), ToFloat(p.Right), top, SolidPlatform)
                : Shape.Rect(ToFloat(p.Left), top - DropThroughThickness, ToFloat(p.Right), top, PassPlatform));
        }

        var current = match.State.Characters;
        var before = previous.Characters;
        var t = (float)alpha;
        for (var i = 0; i < match.CharacterCount; i++)
        {
            ref readonly var c = ref current[i];
            ref readonly var b = ref before[i];
            var x = Lerp(ToFloat(b.PositionX), ToFloat(c.PositionX), t);
            var y = Lerp(ToFloat(b.PositionY), ToFloat(c.PositionY), t);
            var a = match.Attributes(i);
            var color = PlayerColors[i % PlayerColors.Length];
            if (c.IntangibleTicks > 0)
                color = Mix(color, new float4(1, 1, 1, 1), 0.55f); // Intangibility: washed out toward white

            foreach (var h in a.Hurtboxes)
            {
                var (x1, y1, x2, y2) = HurtboxEnds(h, c.Facing, x, y);
                shapes.Add(Shape.Capsule(x1, y1, x2, y2, ToFloat(h.Radius), color));
            }

            if (c.CurrentAction == CharacterAction.Teeter)
            {
                // No animation yet: a marker above the head says "teetering at an edge".
                var height = ToFloat(a.StandingBox.Height);
                shapes.Add(Shape.Capsule(x, y + height + 3, x, y + height + 3, 1.6f, TeeterMarker));
            }

            if (!debug)
                continue;
            foreach (var h in a.Hurtboxes)
            {
                var (x1, y1, x2, y2) = HurtboxEnds(h, c.Facing, x, y);
                shapes.Add(Shape.CapsuleOutline(x1, y1, x2, y2, ToFloat(h.Radius), DebugLine, HurtboxDebug));
            }
            var box = a.Box(CharacterActions.Box(c.CurrentAction));
            var half = ToFloat(box.Width) / 2;
            shapes.Add(Shape.RectOutline(x - half, y, x + half, y + ToFloat(box.Height), DebugLine, CollisionBoxDebug));
            shapes.Add(Shape.Rect(x - 0.9f, y - 0.9f, x + 0.9f, y + 0.9f, GroundProbeDebug));
        }

        if (debug)
            foreach (var ledge in match.Ledges)
            {
                var lx = ToFloat(ledge.X);
                var ly = ToFloat(ledge.Y);
                shapes.Add(Shape.RectOutline(lx - 1.2f, ly - 1.2f, lx + 1.2f, ly + 1.2f, DebugLine, LedgeDebug));
            }
    }

    static (float, float, float, float) HurtboxEnds(Capsule h, int facing, float x, float y) =>
        (x + ToFloat(h.X1) * facing, y + ToFloat(h.Y1), x + ToFloat(h.X2) * facing, y + ToFloat(h.Y2));

    static float Lerp(float a, float b, float t) => a + (b - a) * t;

    static float4 Mix(float4 a, float4 b, float t) =>
        new(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t), Lerp(a.Z, b.Z, t), 1);
}
