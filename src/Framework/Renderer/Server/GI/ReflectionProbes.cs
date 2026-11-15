using System;
using System.Collections.Generic;
using System.Numerics;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.GI
{
    /// <summary>A baked reflection probe as LightingData.json holds it (M8c).</summary>
    public sealed class ReflectionProbeData
    {
        public int Id;
        public Vector3 Position;          // its capture point
        public Vector3 Min, Max;          // its box (world)
        public bool BoxProjection;
        public float Intensity = 1f, Importance = 1f;
        public int Size;                  // face size
        public string[] Faces = new string[6];   // +X, -X, +Y, -Y, +Z, -Z
    }

    /// <summary>
    /// The loaded bake's reflection probes at run time (M8c, docs BAKED_GI_DESIGN.md §4.4): each a mipped cube; an object
    /// reflects the probe whose box holds its bounds' centre - the higher importance, then the smaller box - else the sky.
    /// </summary>
    public static class ReflectionProbes
    {
        private struct Loaded { public ReflectionProbeData Data; public sg_image Image; public sg_view View; public int Mips; }
        private static readonly List<Loaded> _probes = new();

        public static int Count => _probes.Count;
        public static ReflectionProbeData Data(int i) => _probes[i].Data;
        public static sg_view View(int i) => _probes[i].View;
        public static int MipCount(int i) => _probes[i].Mips;

        /// <summary>The probe an object at <paramref name="at"/> reflects (-1: the sky).</summary>
        public static int Choose(Vector3 at)
        {
            int best = -1;
            for (int i = 0; i < _probes.Count; i++)
                if (Better(_probes[i].Data, at, best < 0 ? null : _probes[best].Data)) best = i;
            return best;
        }

        /// <summary>The choice's rule (unit-tested): inside the box; the higher importance; then the smaller box.</summary>
        public static bool Better(ReflectionProbeData p, Vector3 at, ReflectionProbeData? than)
        {
            if (at.X < p.Min.X || at.Y < p.Min.Y || at.Z < p.Min.Z || at.X > p.Max.X || at.Y > p.Max.Y || at.Z > p.Max.Z) return false;
            if (than == null) return true;
            if (p.Importance != than.Importance) return p.Importance > than.Importance;
            static float Volume(ReflectionProbeData d) { var s = d.Max - d.Min; return s.X * s.Y * s.Z; }
            return Volume(p) < Volume(than);
        }

        /// <summary>Box projection (the shader's boxProject): the ray from <paramref name="p"/> along <paramref name="r"/> met
        /// with the box's far side, as a direction from the capture point.</summary>
        public static Vector3 BoxProject(Vector3 p, Vector3 r, Vector3 min, Vector3 max, Vector3 capture)
        {
            // a slab's far side is the larger of its two distances (whatever r's sign, ±0 and infinities included)
            static float Far(float lo, float hi, float pp, float rr) => MathF.Max((hi - pp) / rr, (lo - pp) / rr);
            float t = MathF.Min(MathF.Min(Far(min.X, max.X, p.X, r.X), Far(min.Y, max.Y, p.Y, r.Y)), Far(min.Z, max.Z, p.Z, r.Z));
            return p + r * t - capture;
        }

        internal static void Load(string label, List<ReflectionProbeData> probes, List<byte[][]> facesRgba)
        {
            Unload();
            for (int i = 0; i < probes.Count; i++)
            {
                if (facesRgba[i] is not { Length: 6 } faces) continue;
                var (img, mips) = Lighting.EnvironmentMap.MippedCube(faces, probes[i].Size, $"{label}-reflection-{probes[i].Id}");
                var view = sg_make_view(new sg_view_desc { texture = new sg_texture_view_desc { image = img }, label = $"{label}-reflection-{probes[i].Id}-view" });
                _probes.Add(new Loaded { Data = probes[i], Image = img, View = view, Mips = mips });
            }
        }

        internal static void Unload()
        {
            foreach (var p in _probes)
            {
                if (p.View.id != 0) sg_destroy_view(p.View);
                if (p.Image.id != 0) sg_destroy_image(p.Image);
            }
            _probes.Clear();
        }
    }
}
