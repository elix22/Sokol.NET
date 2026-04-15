using System;

namespace Sokol.GUI;

/// <summary>A 2D point or size in logical pixels.</summary>
public readonly record struct Vector2(float X, float Y)
{
    public static readonly Vector2 Zero  = new(0f, 0f);
    public static readonly Vector2 One   = new(1f, 1f);
    public static readonly Vector2 UnitX = new(1f, 0f);
    public static readonly Vector2 UnitY = new(0f, 1f);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 v, float s)   => new(v.X * s, v.Y * s);
    public static Vector2 operator *(float s,   Vector2 v) => new(v.X * s, v.Y * s);
    public static Vector2 operator /(Vector2 v, float s)   => new(v.X / s, v.Y / s);
    public static Vector2 operator -(Vector2 v)            => new(-v.X, -v.Y);

    public float LengthSquared => X * X + Y * Y;
    public float Length        => MathF.Sqrt(LengthSquared);

    public static float  Distance(Vector2 a, Vector2 b)          => (a - b).Length;
    public static float  DistanceSquared(Vector2 a, Vector2 b)   => (a - b).LengthSquared;
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t)    => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    public static float  Dot(Vector2 a, Vector2 b)               => a.X * b.X + a.Y * b.Y;
    public static Vector2 Normalize(Vector2 v) { float l = v.Length; return l > 0f ? v / l : Zero; }
    public static Vector2 Min(Vector2 a, Vector2 b) => new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y));
    public static Vector2 Max(Vector2 a, Vector2 b) => new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));

    public override string ToString() => $"({X}, {Y})";
}
