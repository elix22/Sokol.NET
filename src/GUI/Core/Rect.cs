using System;

namespace Sokol.GUI;

/// <summary>An axis-aligned rectangle in logical pixels.</summary>
public readonly record struct Rect(float X, float Y, float Width, float Height)
{
    public static readonly Rect Empty = new(0f, 0f, 0f, 0f);

    public float Left   => X;
    public float Top    => Y;
    public float Right  => X + Width;
    public float Bottom => Y + Height;

    public Vector2 Position => new(X, Y);
    public Vector2 Size     => new(Width, Height);
    public Vector2 Center   => new(X + Width * 0.5f, Y + Height * 0.5f);

    public bool IsEmpty => Width <= 0f || Height <= 0f;

    /// <summary>Create a rect from left/top/right/bottom coordinates.</summary>
    public static Rect FromLTRB(float l, float t, float r, float b) => new(l, t, r - l, b - t);

    /// <summary>Create a rect centered at a point with given size.</summary>
    public static Rect FromCenter(Vector2 center, Vector2 size) =>
        new(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f, size.X, size.Y);

    /// <summary>Returns true if the point is inside (inclusive of edges).</summary>
    public bool Contains(Vector2 point) =>
        point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;

    /// <summary>Returns true if other rect is fully inside this rect.</summary>
    public bool Contains(Rect other) =>
        other.Left >= Left && other.Right <= Right && other.Top >= Top && other.Bottom <= Bottom;

    /// <summary>Returns true if this rect overlaps with other.</summary>
    public bool Intersects(Rect other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

    /// <summary>Returns the intersection rect; Empty if no overlap.</summary>
    public Rect Intersection(Rect other)
    {
        float l = MathF.Max(Left,  other.Left);
        float t = MathF.Max(Top,   other.Top);
        float r = MathF.Min(Right, other.Right);
        float b = MathF.Min(Bottom,other.Bottom);
        return r > l && b > t ? FromLTRB(l, t, r, b) : Empty;
    }

    /// <summary>Returns the smallest rect containing both rects.</summary>
    public Rect Union(Rect other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;
        float l = MathF.Min(Left,  other.Left);
        float t = MathF.Min(Top,   other.Top);
        float r = MathF.Max(Right, other.Right);
        float b = MathF.Max(Bottom,other.Bottom);
        return FromLTRB(l, t, r, b);
    }

    /// <summary>Expand the rect by a Thickness (positive = grow, negative = shrink).</summary>
    public Rect Inflate(Thickness t) =>
        FromLTRB(Left - t.Left, Top - t.Top, Right + t.Right, Bottom + t.Bottom);

    /// <summary>Inset (shrink) the rect by a Thickness.</summary>
    public Rect Deflate(Thickness t) =>
        FromLTRB(Left + t.Left, Top + t.Top, Right - t.Right, Bottom - t.Bottom);

    /// <summary>Move the rect by an offset.</summary>
    public Rect Offset(Vector2 offset) => new(X + offset.X, Y + offset.Y, Width, Height);
    public Rect Offset(float dx, float dy) => new(X + dx, Y + dy, Width, Height);

    /// <summary>Return a rect with a new position.</summary>
    public Rect WithPosition(Vector2 pos) => new(pos.X, pos.Y, Width, Height);
    public Rect WithSize(Vector2 size)    => new(X, Y, size.X, size.Y);

    public override string ToString() => $"({X}, {Y}, {Width}x{Height})";
}
