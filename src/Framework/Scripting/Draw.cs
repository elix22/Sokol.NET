// Draw.cs — G8.4 D3 (examples/GameEditor/docs/TEXT3D_SVG_DRAW_DESIGN.md §5c): drawing from scripts.
//
// World lines — the reference editor's Debug.DrawLine / DrawRay and its wire gizmos: kept for `seconds` of game time (0:
// this frame), drawn in the scene, depth-tested unless asked not to; shown in the editor's Scene view, in its Game view
// when its Gizmos toggle is on, never in a built game. Screen shapes — Godot's _draw: in the UI's units (a screen 720
// high), drawn with the game's UI this frame, in built games too.

using System.Collections.Generic;
using System.Numerics;
using System;

namespace GameEditor.Framework.Scripting
{
    public static class Draw
    {
        internal struct WorldLine { public Vector3 A, B; public Vector4 Color; public float Until; public bool Depth; }
        internal enum ShapeKind { Line, Rect, Circle, Text }
        internal struct Shape { public ShapeKind Kind; public Vector2 A, B; public float Size; public bool Filled; public Vector4 Color; public string? Text; }

        internal static readonly List<WorldLine> Lines = new();
        internal static readonly List<Shape> Shapes = new();
        private static float _now;
        private const int MaxLines = 65536, MaxShapes = 4096;

        /// <summary>A line in the world, from <paramref name="a"/> to <paramref name="b"/>, for <paramref name="seconds"/> of game time (0: this frame).</summary>
        public static void Line(Vector3 a, Vector3 b, Vector4 color, float seconds = 0f, bool depthTest = true)
        {
            if (Lines.Count >= MaxLines) return;
            Lines.Add(new WorldLine { A = a, B = b, Color = color, Until = _now + MathF.Max(0f, seconds), Depth = depthTest });
        }

        /// <summary>A line from <paramref name="start"/> along <paramref name="direction"/> (its length is the line's).</summary>
        public static void Ray(Vector3 start, Vector3 direction, Vector4 color, float seconds = 0f, bool depthTest = true)
            => Line(start, start + direction, color, seconds, depthTest);

        /// <summary>The edges of a box, upright, around <paramref name="center"/>.</summary>
        public static void Box(Vector3 center, Vector3 size, Vector4 color, float seconds = 0f, bool depthTest = true)
        {
            var h = size * 0.5f;
            Span<Vector3> c = stackalloc Vector3[8];
            for (int i = 0; i < 8; i++)
                c[i] = center + new Vector3((i & 1) != 0 ? h.X : -h.X, (i & 2) != 0 ? h.Y : -h.Y, (i & 4) != 0 ? h.Z : -h.Z);
            for (int i = 0; i < 8; i++)
                for (int bit = 1; bit < 8; bit <<= 1)
                    if ((i & bit) == 0) Line(c[i], c[i | bit], color, seconds, depthTest);
        }

        /// <summary>A sphere's three great circles around <paramref name="center"/>.</summary>
        public static void Sphere(Vector3 center, float radius, Vector4 color, float seconds = 0f, bool depthTest = true)
        {
            const int n = 32;
            for (int axis = 0; axis < 3; axis++)
                for (int i = 0; i < n; i++)
                {
                    float a0 = i * MathF.Tau / n, a1 = (i + 1) * MathF.Tau / n;
                    Line(center + Round(axis, a0) * radius, center + Round(axis, a1) * radius, color, seconds, depthTest);
                }
        }

        private static Vector3 Round(int axis, float a) => axis switch
        {
            0 => new Vector3(0f, MathF.Cos(a), MathF.Sin(a)),
            1 => new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)),
            _ => new Vector3(MathF.Cos(a), MathF.Sin(a), 0f),
        };

        /// <summary>Shapes on the screen, in the UI's units (a screen 720 high; 0, 0 its top left), drawn with the game's UI this frame.</summary>
        public static class Screen
        {
            public static void Line(Vector2 a, Vector2 b, Vector4 color, float width = 2f) => Add(new Shape { Kind = ShapeKind.Line, A = a, B = b, Color = color, Size = width });
            public static void Rect(Vector2 position, Vector2 size, Vector4 color, bool filled = true, float width = 2f)
                => Add(new Shape { Kind = ShapeKind.Rect, A = position, B = size, Color = color, Filled = filled, Size = width });
            public static void Circle(Vector2 center, float radius, Vector4 color, bool filled = true, float width = 2f)
                => Add(new Shape { Kind = ShapeKind.Circle, A = center, B = new Vector2(radius, 0f), Color = color, Filled = filled, Size = width });
            public static void Text(Vector2 position, string text, Vector4 color, float size = 24f)
                => Add(new Shape { Kind = ShapeKind.Text, A = position, Color = color, Size = size, Text = text });
            private static void Add(Shape s) { if (Shapes.Count < MaxShapes) Shapes.Add(s); }
        }

        /// <summary>The world lines kept now, and the screen shapes asked for this frame.</summary>
        public static int LineCount => Lines.Count;
        public static int ShapeCount => Shapes.Count;

        /// <summary>
        /// Before the scripts of a frame (the engine calls it): game time on; what was drawn for the frame before and has had
        /// its time, gone.
        /// </summary>
        public static void BeginFrame(float deltaTime)
        {
            _now += MathF.Max(0f, deltaTime);
            Lines.RemoveAll(static l => l.Until < _now);
            Shapes.Clear();
        }

        /// <summary>The game stopped: nothing it drew is kept.</summary>
        public static void Clear() { Lines.Clear(); Shapes.Clear(); }
    }
}
