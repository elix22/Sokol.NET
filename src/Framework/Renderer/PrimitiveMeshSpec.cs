using System;
using System.Globalization;

namespace GameEditor.Framework.Renderer
{
    public enum PrimitiveKind
    {
        Box,
        Sphere,
        Plane,
        Cylinder,
        Cone,
        Ring,
        Pyramid
    }

    public struct PrimitiveMeshSpec
    {
        public PrimitiveKind Kind;

        public float Width;
        public float Height;
        public float Depth;

        public float Radius;
        public float RingRadius;

        public int Slices;
        public int Stacks;
        public int Sides;
        public int Rings;
        public int Tiles;

        public static PrimitiveMeshSpec Default(PrimitiveKind kind)
        {
            return kind switch
            {
                PrimitiveKind.Box => new PrimitiveMeshSpec
                {
                    Kind = kind,
                    Width = 1f,
                    Height = 1f,
                    Depth = 1f,
                    Tiles = 1
                },
                PrimitiveKind.Sphere => new PrimitiveMeshSpec
                {
                    Kind = kind,
                    Radius = 0.5f,
                    Slices = 36,
                    Stacks = 20
                },
                PrimitiveKind.Plane => new PrimitiveMeshSpec
                {
                    Kind = kind,
                    Width = 1f,
                    Depth = 1f,
                    Tiles = 1
                },
                PrimitiveKind.Cylinder => new PrimitiveMeshSpec
                {
                    Kind = kind,
                    Radius = 0.5f,
                    Height = 1f,
                    Slices = 24,
                    Stacks = 1
                },
                PrimitiveKind.Cone => new PrimitiveMeshSpec
                {
                    Kind = kind,
                    Radius = 0.5f,
                    Height = 1f,
                    Slices = 24
                },
                PrimitiveKind.Ring => new PrimitiveMeshSpec
                {
                    Kind = kind,
                    Radius = 0.38f,
                    RingRadius = 0.14f,
                    Sides = 20,
                    Rings = 36
                },
                PrimitiveKind.Pyramid => new PrimitiveMeshSpec
                {
                    Kind = kind,
                    Radius = 0.5f,
                    Height = 1f,
                    Sides = 4
                },
                _ => Default(PrimitiveKind.Box)
            };
        }

        public static bool TryParse(string? meshPath, out PrimitiveMeshSpec spec)
        {
            spec = default;
            if (string.IsNullOrEmpty(meshPath))
            {
                spec = Default(PrimitiveKind.Box);
                return true;
            }

            if (meshPath == "prim:box") { spec = Default(PrimitiveKind.Box); return true; }
            if (meshPath == "prim:cube") { spec = Default(PrimitiveKind.Box); return true; }
            if (meshPath == "prim:sphere") { spec = Default(PrimitiveKind.Sphere); return true; }
            if (meshPath == "prim:plane") { spec = Default(PrimitiveKind.Plane); return true; }
            if (meshPath == "prim:cylinder") { spec = Default(PrimitiveKind.Cylinder); return true; }
            if (meshPath == "prim:cone") { spec = Default(PrimitiveKind.Cone); return true; }
            if (meshPath == "prim:ring") { spec = Default(PrimitiveKind.Ring); return true; }
            if (meshPath == "prim:pyramid") { spec = Default(PrimitiveKind.Pyramid); return true; }

            if (!meshPath.StartsWith("prim:", StringComparison.OrdinalIgnoreCase))
                return false;

            string[] tokens = meshPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return false;

            string head = tokens[0].Substring(5).Trim().ToLowerInvariant();
            PrimitiveKind kind = head switch
            {
                "box" => PrimitiveKind.Box,
                "cube" => PrimitiveKind.Box,
                "sphere" => PrimitiveKind.Sphere,
                "plane" => PrimitiveKind.Plane,
                "cylinder" => PrimitiveKind.Cylinder,
                "cone" => PrimitiveKind.Cone,
                "ring" => PrimitiveKind.Ring,
                "pyramid" => PrimitiveKind.Pyramid,
                _ => PrimitiveKind.Box
            };

            spec = Default(kind);

            for (int i = 1; i < tokens.Length; i++)
            {
                string[] kv = tokens[i].Split('=', 2);
                if (kv.Length != 2) continue;
                string key = kv[0].Trim().ToLowerInvariant();
                string val = kv[1].Trim();

                if (TryParseFloat(val, out float fv))
                {
                    switch (key)
                    {
                        case "w": spec.Width = fv; break;
                        case "h": spec.Height = fv; break;
                        case "d": spec.Depth = fv; break;
                        case "r": spec.Radius = fv; break;
                        case "rr": spec.RingRadius = fv; break;
                    }
                }
                if (TryParseInt(val, out int iv))
                {
                    switch (key)
                    {
                        case "slices": spec.Slices = iv; break;
                        case "stacks": spec.Stacks = iv; break;
                        case "sides": spec.Sides = iv; break;
                        case "rings": spec.Rings = iv; break;
                        case "tiles": spec.Tiles = iv; break;
                    }
                }
            }

            Clamp(ref spec);
            return true;
        }

        public static string ToMeshPath(in PrimitiveMeshSpec spec)
        {
            PrimitiveMeshSpec s = spec;
            Clamp(ref s);

            string head = s.Kind switch
            {
                PrimitiveKind.Box => "prim:box",
                PrimitiveKind.Sphere => "prim:sphere",
                PrimitiveKind.Plane => "prim:plane",
                PrimitiveKind.Cylinder => "prim:cylinder",
                PrimitiveKind.Cone => "prim:cone",
                PrimitiveKind.Ring => "prim:ring",
                PrimitiveKind.Pyramid => "prim:pyramid",
                _ => "prim:box"
            };

            return s.Kind switch
            {
                PrimitiveKind.Box => $"{head};w={F(s.Width)};h={F(s.Height)};d={F(s.Depth)};tiles={s.Tiles}",
                PrimitiveKind.Sphere => $"{head};r={F(s.Radius)};slices={s.Slices};stacks={s.Stacks}",
                PrimitiveKind.Plane => $"{head};w={F(s.Width)};d={F(s.Depth)};tiles={s.Tiles}",
                PrimitiveKind.Cylinder => $"{head};r={F(s.Radius)};h={F(s.Height)};slices={s.Slices};stacks={s.Stacks}",
                PrimitiveKind.Cone => $"{head};r={F(s.Radius)};h={F(s.Height)};slices={s.Slices}",
                PrimitiveKind.Ring => $"{head};r={F(s.Radius)};rr={F(s.RingRadius)};sides={s.Sides};rings={s.Rings}",
                PrimitiveKind.Pyramid => $"{head};r={F(s.Radius)};h={F(s.Height)};sides={s.Sides}",
                _ => head
            };
        }

        private static void Clamp(ref PrimitiveMeshSpec s)
        {
            s.Width = MathF.Max(0.01f, s.Width);
            s.Height = MathF.Max(0.01f, s.Height);
            s.Depth = MathF.Max(0.01f, s.Depth);
            s.Radius = MathF.Max(0.01f, s.Radius);
            s.RingRadius = MathF.Max(0.01f, s.RingRadius);

            s.Slices = Math.Max(3, s.Slices);
            s.Stacks = Math.Max(1, s.Stacks);
            s.Sides = Math.Max(3, s.Sides);
            s.Rings = Math.Max(3, s.Rings);
            s.Tiles = Math.Max(1, s.Tiles);
        }

        private static bool TryParseFloat(string s, out float v)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        private static bool TryParseInt(string s, out int v)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);

        private static string F(float v)
            => v.ToString("G", CultureInfo.InvariantCulture);
    }
}