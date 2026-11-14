// ClusterBuilder.cs — Phase G8.2 (examples/GameEditor/docs/CLUSTERED_LIGHTING_DESIGN.md): Forward+'s cluster grid.
//
// The view frustum of a perspective camera is cut into 16 × 9 screen tiles (in NDC, the same on every backend) and 24
// depth slices spaced exponentially between its near and far planes; each cluster gets a bit mask of the lights that
// reach it. The fragment shader (pbr.glsl, CLUSTERED) finds its own cluster with ClusterOf's formula and shades only
// those lights. A light is put in every cluster its sphere (centre, range) touches — exact where a light ends, as its
// attenuation is 0 at its range; a directional light, or one whose range never ends (< 0), in every cluster; range 0
// (no light) in none. Zero allocation after construction: the cluster boxes are kept while the projection is the same.

using System;
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    public sealed class ClusterBuilder
    {
        public const int TilesX = 16, TilesY = 9, Slices = 24;
        public const int Count = TilesX * TilesY * Slices;

        /// <summary>The most lights a view holds; a mask has a bit for each.</summary>
        public int MaxLights { get; }
        /// <summary>16-bit words in a cluster's mask.</summary>
        public int Words { get; }
        /// <summary>The masks: cluster c's word w is <c>Masks[c * Words + w]</c>; bit b of word w is light 16·w + b.</summary>
        public readonly ushort[] Masks;

        /// <summary>The view-projection the clusters were built for (the shader's <c>cluster_view_proj</c>).</summary>
        public Matrix4x4 ViewProj { get; private set; }
        public float Near { get; private set; }
        public float Far { get; private set; }
        /// <summary>Slices ÷ ln(far ÷ near): a depth's slice is ⌊ln(depth ÷ near) · SliceScale⌋.</summary>
        public float SliceScale { get; private set; }
        /// <summary>Lights put in the grid by the last <see cref="Build"/>.</summary>
        public int LightCount { get; private set; }

        // The clusters' view-space boxes: x by (slice, tile x), y by (slice, tile y), depth by slice (view space looks
        // down -z; depth = -z).
        private readonly float[] _xMin = new float[Slices * TilesX], _xMax = new float[Slices * TilesX];
        private readonly float[] _yMin = new float[Slices * TilesY], _yMax = new float[Slices * TilesY];
        private readonly float[] _dMin = new float[Slices], _dMax = new float[Slices];
        private float _m11 = float.NaN, _m22, _m31, _m32, _boxNear, _boxFar;

        public ClusterBuilder(int maxLights)
        {
            MaxLights = Math.Max(1, maxLights);
            Words = (MaxLights + 15) / 16;
            Masks = new ushort[Count * Words];
        }

        /// <summary>
        /// Puts <paramref name="lights"/> (at most <see cref="MaxLights"/>, in the order the shader reads them) in the
        /// clusters of the view (<paramref name="view"/>, a right-handed perspective <paramref name="proj"/> whose w is
        /// the view depth). False, and nothing built, for a projection that is not such a perspective one.
        /// </summary>
        public bool Build(in Matrix4x4 view, in Matrix4x4 proj, float near, float far, ReadOnlySpan<GpuLight> lights)
        {
            if (MathF.Abs(proj.M34 + 1f) > 1e-5f || proj.M44 != 0f || !(near > 0f) || !(far > near)) return false;
            Near = near; Far = far;
            SliceScale = Slices / MathF.Log(far / near);
            ViewProj = view * proj;
            if (proj.M11 != _m11 || proj.M22 != _m22 || proj.M31 != _m31 || proj.M32 != _m32 || near != _boxNear || far != _boxFar)
                MakeBoxes(proj.M11, proj.M22, proj.M31, proj.M32, near, far);

            Array.Clear(Masks);
            int n = Math.Min(lights.Length, MaxLights);
            LightCount = n;
            for (int li = 0; li < n; li++)
            {
                ref readonly var l = ref lights[li];
                int type = (int)(l.PositionType.W + 0.5f);
                float range = l.DirectionRange.W;
                int word = li >> 4;
                ushort bit = (ushort)(1 << (li & 15));
                if (type == (int)LightType.Directional || range < 0f)
                {
                    for (int c = 0; c < Count; c++) Masks[c * Words + word] |= bit;
                    continue;
                }
                if (range == 0f) continue;
                var centre = Vector3.Transform(new Vector3(l.PositionType.X, l.PositionType.Y, l.PositionType.Z), view);
                PutSphere(centre, range, word, bit);
            }
            return true;
        }

        private void PutSphere(Vector3 c, float r, int word, ushort bit)
        {
            float depth = -c.Z, r2 = r * r;
            if (depth + r < _dMin[0] || depth - r > _dMax[Slices - 1]) return;
            int k0 = SliceOf(depth - r), k1 = SliceOf(depth + r);
            for (int k = k0; k <= k1; k++)
            {
                // The part of the sphere's box inside this slice's depths, and the tiles it can cover.
                float da = MathF.Max(_dMin[k], depth - r), db = MathF.Min(_dMax[k], depth + r);
                if (db < da) continue;
                NdcRange(c.X - r, c.X + r, da, db, _m11, _m31, out float nx0, out float nx1);
                NdcRange(c.Y - r, c.Y + r, da, db, _m22, _m32, out float ny0, out float ny1);
                int i0 = Tile(nx0, TilesX), i1 = Tile(nx1, TilesX);
                int j0 = Tile(ny0, TilesY), j1 = Tile(ny1, TilesY);
                // The depth part of the distance from the centre to this slice's boxes.
                float dz = depth < _dMin[k] ? _dMin[k] - depth : depth > _dMax[k] ? depth - _dMax[k] : 0f;
                float dz2 = dz * dz;
                if (dz2 > r2) continue;
                for (int j = j0; j <= j1; j++)
                {
                    float yMin = _yMin[k * TilesY + j], yMax = _yMax[k * TilesY + j];
                    float dy = c.Y < yMin ? yMin - c.Y : c.Y > yMax ? c.Y - yMax : 0f;
                    float dyz2 = dz2 + dy * dy;
                    if (dyz2 > r2) continue;
                    for (int i = i0; i <= i1; i++)
                    {
                        float xMin = _xMin[k * TilesX + i], xMax = _xMax[k * TilesX + i];
                        float dx = c.X < xMin ? xMin - c.X : c.X > xMax ? c.X - xMax : 0f;
                        if (dyz2 + dx * dx > r2) continue;
                        Masks[((k * TilesY + j) * TilesX + i) * Words + word] |= bit;
                    }
                }
            }
        }

        // The NDC range of view-space values v0..v1 (x or y) at depths da..db (> 0): ndc = v · m / d − offset, monotonic
        // in d for a given v, so its ends are among the four corners.
        private static void NdcRange(float v0, float v1, float da, float db, float m, float offset, out float lo, out float hi)
        {
            float a = v0 * m / da, b = v0 * m / db, c = v1 * m / da, d = v1 * m / db;
            lo = MathF.Min(MathF.Min(a, b), MathF.Min(c, d)) - offset;
            hi = MathF.Max(MathF.Max(a, b), MathF.Max(c, d)) - offset;
        }

        private static int Tile(float ndc, int tiles)
            => Math.Clamp((int)MathF.Floor((ndc * 0.5f + 0.5f) * tiles), 0, tiles - 1);

        private int SliceOf(float depth)
            => depth <= Near ? 0 : Math.Clamp((int)MathF.Floor(MathF.Log(depth / Near) * SliceScale), 0, Slices - 1);

        private void MakeBoxes(float m11, float m22, float m31, float m32, float near, float far)
        {
            _m11 = m11; _m22 = m22; _m31 = m31; _m32 = m32; _boxNear = near; _boxFar = far;
            for (int k = 0; k < Slices; k++)
            {
                // Slice k holds depths near·(far/near)^(k/24) .. ^((k+1)/24); the first reaches a little nearer and the
                // last a little farther than the planes, and every box is widened a little, for the GPU's rounding.
                float d0 = k == 0 ? near * 0.999f : near * MathF.Pow(far / near, k / (float)Slices);
                float d1 = k == Slices - 1 ? far * 1.001f : near * MathF.Pow(far / near, (k + 1) / (float)Slices);
                float pad = d1 * 1e-4f;
                _dMin[k] = d0 - pad; _dMax[k] = d1 + pad;
                for (int i = 0; i < TilesX; i++)
                    Extent(i, TilesX, d0, d1, m11, m31, pad, out _xMin[k * TilesX + i], out _xMax[k * TilesX + i]);
                for (int j = 0; j < TilesY; j++)
                    Extent(j, TilesY, d0, d1, m22, m32, pad, out _yMin[k * TilesY + j], out _yMax[k * TilesY + j]);
            }
        }

        // A tile's view-space extent (x or y) between depths d0 and d1: v = d · (ndc + offset) / m at its four corners;
        // the edge tiles reach past the screen's edge.
        private static void Extent(int t, int tiles, float d0, float d1, float m, float offset, float pad, out float lo, out float hi)
        {
            float n0 = -1f + 2f * t / tiles, n1 = -1f + 2f * (t + 1) / tiles;
            if (t == 0) n0 -= 0.01f;
            if (t == tiles - 1) n1 += 0.01f;
            float a = d0 * (n0 + offset) / m, b = d1 * (n0 + offset) / m, c = d0 * (n1 + offset) / m, d = d1 * (n1 + offset) / m;
            lo = MathF.Min(MathF.Min(a, b), MathF.Min(c, d)) - pad;
            hi = MathF.Max(MathF.Max(a, b), MathF.Max(c, d)) + pad;
        }

        /// <summary>
        /// The cluster a world point is in — the shader's formula (pbr.glsl, CLUSTERED): its clip position through
        /// <see cref="ViewProj"/>, the tile from NDC, the slice from the view depth (clip w).
        /// </summary>
        public int ClusterOf(Vector3 world)
        {
            var clip = Vector4.Transform(new Vector4(world, 1f), ViewProj);
            float w = MathF.Max(clip.W, 1e-6f);
            int i = Tile(clip.X / w, TilesX), j = Tile(clip.Y / w, TilesY);
            int k = Math.Clamp((int)MathF.Floor(MathF.Log(w / Near) * SliceScale), 0, Slices - 1);
            return (k * TilesY + j) * TilesX + i;
        }

        /// <summary>True when light <paramref name="light"/> is in cluster <paramref name="cluster"/>.</summary>
        public bool Has(int cluster, int light)
            => (Masks[cluster * Words + (light >> 4)] & (1 << (light & 15))) != 0;
    }
}
