using System.Runtime.InteropServices;
using Brutal.Numerics;

namespace Blastzone.Engine.Render;

/// <summary>What the renderer looks at: a world-space center and half the visible height.</summary>
public readonly record struct Camera(float CenterX, float CenterY, float HalfHeight);

public enum ShapeKind { Rect = 0, RectOutline = 1, Capsule = 2, CapsuleOutline = 3 }

/// <summary>
/// One flat shape in world units (+Y up). Rectangles use (X1, Y1)-(X2, Y2) as corners; capsules use them as
/// the segment ends with <see cref="Radius"/> (equal ends draw a circle). Outlines are <see cref="Width"/> thick.
/// </summary>
public readonly record struct Shape(ShapeKind Kind, float X1, float Y1, float X2, float Y2, float Radius, float Width, float4 Color)
{
    public static Shape Rect(float left, float bottom, float right, float top, float4 color) =>
        new(ShapeKind.Rect, left, bottom, right, top, 0, 0, color);

    public static Shape RectOutline(float left, float bottom, float right, float top, float width, float4 color) =>
        new(ShapeKind.RectOutline, left, bottom, right, top, 0, width, color);

    public static Shape Capsule(float x1, float y1, float x2, float y2, float radius, float4 color) =>
        new(ShapeKind.Capsule, x1, y1, x2, y2, radius, 0, color);

    public static Shape CapsuleOutline(float x1, float y1, float x2, float y2, float radius, float width, float4 color) =>
        new(ShapeKind.CapsuleOutline, x1, y1, x2, y2, radius, width, color);
}

/// <summary>The push-constant block, laid out like the shaders' <c>Shape</c> uniform (5 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
struct ShapeConstants
{
    public float4 Bounds;
    public float4 Segment;
    public float4 Color;
    public float4 Params;
    public float4 Camera;

    public static ShapeConstants From(in Shape s, float4 camera)
    {
        var minX = MathF.Min(s.X1, s.X2) - s.Radius;
        var minY = MathF.Min(s.Y1, s.Y2) - s.Radius;
        var maxX = MathF.Max(s.X1, s.X2) + s.Radius;
        var maxY = MathF.Max(s.Y1, s.Y2) + s.Radius;
        var isRect = s.Kind is ShapeKind.Rect or ShapeKind.RectOutline;
        return new ShapeConstants
        {
            Bounds = new float4(minX, minY, maxX, maxY),
            Segment = isRect ? new float4(minX, minY, maxX, maxY) : new float4(s.X1, s.Y1, s.X2, s.Y2),
            Color = s.Color,
            Params = new float4(s.Radius, (float)s.Kind, s.Kind is ShapeKind.Rect or ShapeKind.Capsule ? float.MaxValue : s.Width, 0),
            Camera = camera,
        };
    }
}
