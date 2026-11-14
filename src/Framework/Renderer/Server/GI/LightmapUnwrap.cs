using System;
using System.Collections.Generic;
using System.Numerics;
using GameEditor.Framework.Renderer.Server.Resources;

namespace GameEditor.Framework.Renderer.Server.GI
{
    /// <summary>
    /// Lightmap UVs (M8a, docs BAKED_GI_DESIGN.md §3.2), made per entity in world units: its triangles grouped into charts
    /// (edge-connected, within <see cref="ChartAngle"/> of the chart's first normal), each laid flat on its plane, turned to
    /// its smallest box and packed with padding into the entity's rectangle (<see cref="Unwrap"/>); the rectangles then
    /// packed into square pages (<see cref="PackPages"/>). A chart splits vertices at its seams: the result is a new mesh
    /// whose <c>Uv1</c> is the lightmap UV.
    /// </summary>
    public static class LightmapUnwrap
    {
        /// <summary>Largest angle between a chart's triangles and its first one (degrees).</summary>
        public const float ChartAngle = 45f;

        /// <summary>An entity's unwrapped mesh: <c>Uv1</c> in texels inside its rectangle (0..Width, 0..Height).</summary>
        public sealed class Result
        {
            public ObjSubMesh[] SubMeshes = Array.Empty<ObjSubMesh>();
            public int Width, Height;
            public int Charts;
            /// <summary>Texels per world unit it was made at (lower than asked when it had to fit a page).</summary>
            public float TexelsPerUnit;
        }

        /// <summary>
        /// The mesh's triangles in world space (<paramref name="world"/>) charted and packed at <paramref name="texelsPerUnit"/>,
        /// <paramref name="padding"/> texels between charts; made smaller to fit <paramref name="maxSize"/> when it would not.
        /// </summary>
        public static Result Unwrap(ObjSubMesh[] subMeshes, Matrix4x4 world, float texelsPerUnit, int padding, int maxSize)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var r = UnwrapAt(subMeshes, world, texelsPerUnit, padding);
                int big = Math.Max(r.Width, r.Height);
                if (big <= maxSize) return r;
                texelsPerUnit *= 0.95f * maxSize / big;
            }
            return UnwrapAt(subMeshes, world, texelsPerUnit, padding);
        }

        private struct Chart
        {
            public int Sub;
            public List<int> Triangles;    // triangle indices (first index / 3) in its sub-mesh
            public Vector2[] Corners;      // per triangle corner, projected (texels), in Triangles' order
            public float W, H;             // its box after turning (texels)
            public int X, Y;               // its place in the entity's rectangle (texels)
        }

        private static Result UnwrapAt(ObjSubMesh[] subMeshes, Matrix4x4 world, float tpu, int padding)
        {
            var charts = new List<Chart>();
            var nworld = Matrix4x4.Transpose(Matrix4x4.Invert(world, out var inv) ? inv : Matrix4x4.Identity);
            for (int s = 0; s < subMeshes.Length; s++)
            {
                var sm = subMeshes[s];
                int triCount = sm.Indices.Length / 3;
                if (triCount == 0) continue;
                var wp = new Vector3[sm.Vertices.Length];
                for (int i = 0; i < wp.Length; i++) wp[i] = Vector3.Transform(sm.Vertices[i].Position, world);

                // Welded corners (a mesh splits vertices at its UV seams and hard edges): neighbours across those too.
                var weld = new int[wp.Length];
                var byPos = new Dictionary<(int, int, int), int>();
                for (int i = 0; i < wp.Length; i++)
                {
                    var k = ((int)MathF.Round(wp[i].X * 1e4f), (int)MathF.Round(wp[i].Y * 1e4f), (int)MathF.Round(wp[i].Z * 1e4f));
                    if (!byPos.TryGetValue(k, out int w)) byPos[k] = w = i;
                    weld[i] = w;
                }
                var normal = new Vector3[triCount];
                var area = new float[triCount];
                for (int t = 0; t < triCount; t++)
                {
                    var a = wp[sm.Indices[t * 3]]; var b = wp[sm.Indices[t * 3 + 1]]; var c = wp[sm.Indices[t * 3 + 2]];
                    var n = Vector3.Cross(b - a, c - a);
                    float len = n.Length();
                    area[t] = len * 0.5f;
                    normal[t] = len > 1e-12f ? n / len : Vector3.UnitY;
                }
                var byEdge = new Dictionary<(int, int), List<int>>();
                for (int t = 0; t < triCount; t++)
                    for (int e = 0; e < 3; e++)
                    {
                        int p = weld[sm.Indices[t * 3 + e]], q = weld[sm.Indices[t * 3 + (e + 1) % 3]];
                        var key = p < q ? (p, q) : (q, p);
                        if (!byEdge.TryGetValue(key, out var list)) byEdge[key] = list = new List<int>(2);
                        list.Add(t);
                    }

                // Charts: grown from the largest triangle left, across shared edges, within ChartAngle of the seed.
                var order = new int[triCount];
                for (int t = 0; t < triCount; t++) order[t] = t;
                Array.Sort(order, (x, y) => area[y].CompareTo(area[x]));
                var chartOf = new int[triCount];
                Array.Fill(chartOf, -1);
                float cosLimit = MathF.Cos(ChartAngle * MathF.PI / 180f);
                var queue = new Queue<int>();
                foreach (int seed in order)
                {
                    if (chartOf[seed] >= 0 || area[seed] <= 0f) continue;
                    var tris = new List<int>();
                    int id = charts.Count;
                    Vector3 sn = normal[seed];
                    chartOf[seed] = id; queue.Enqueue(seed);
                    while (queue.Count > 0)
                    {
                        int t = queue.Dequeue();
                        tris.Add(t);
                        for (int e = 0; e < 3; e++)
                        {
                            int p = weld[sm.Indices[t * 3 + e]], q = weld[sm.Indices[t * 3 + (e + 1) % 3]];
                            foreach (int o in byEdge[p < q ? (p, q) : (q, p)])
                                if (chartOf[o] < 0 && area[o] > 0f && Vector3.Dot(normal[o], sn) >= cosLimit)
                                {
                                    chartOf[o] = id;
                                    queue.Enqueue(o);
                                }
                        }
                    }
                    charts.Add(Flatten(sm, s, tris, wp, sn, tpu));
                }
            }

            // Charts into the entity's rectangle: shelves, tallest first.
            int pad = Math.Max(0, padding);
            var placed = charts.ToArray();
            var sizes = new (int W, int H)[placed.Length];
            for (int i = 0; i < placed.Length; i++) sizes[i] = ((int)MathF.Ceiling(placed[i].W) + pad + 1, (int)MathF.Ceiling(placed[i].H) + pad + 1);
            var at = Shelf(sizes, out int width, out int height);
            for (int i = 0; i < placed.Length; i++) { placed[i].X = at[i].X; placed[i].Y = at[i].Y; }

            // The new mesh: a vertex per (source vertex, chart), its Uv1 the chart's place.
            var result = new Result { Width = width, Height = height, Charts = placed.Length, TexelsPerUnit = tpu, SubMeshes = new ObjSubMesh[subMeshes.Length] };
            for (int s = 0; s < subMeshes.Length; s++)
            {
                var sm = subMeshes[s];
                var verts = new List<ObjVertex>(sm.Vertices.Length);
                var idx = new List<uint>(sm.Indices.Length);
                var map = new Dictionary<(uint, int), uint>();
                for (int c = 0; c < placed.Length; c++)
                {
                    if (placed[c].Sub != s) continue;
                    var ch = placed[c];
                    float ox = ch.X + pad * 0.5f + 0.5f, oy = ch.Y + pad * 0.5f + 0.5f;
                    for (int k = 0; k < ch.Triangles.Count; k++)
                        for (int e = 0; e < 3; e++)
                        {
                            uint src = sm.Indices[ch.Triangles[k] * 3 + e];
                            if (!map.TryGetValue((src, c), out uint ni))
                            {
                                var v = sm.Vertices[src];
                                var uv = ch.Corners[k * 3 + e];
                                v.Uv1 = new Vector2(ox + uv.X, oy + uv.Y);
                                ni = (uint)verts.Count;
                                verts.Add(v);
                                map[(src, c)] = ni;
                            }
                            idx.Add(ni);
                        }
                }
                result.SubMeshes[s] = new ObjSubMesh { MaterialName = sm.MaterialName, Vertices = verts.ToArray(), Indices = idx.ToArray() };
            }
            return result;
        }

        // A chart's corners laid flat on the plane of its seed normal, in texels, turned to its smallest box, from (0, 0).
        private static Chart Flatten(ObjSubMesh sm, int sub, List<int> tris, Vector3[] wp, Vector3 n, float tpu)
        {
            Vector3 helper = MathF.Abs(n.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 tx = Vector3.Normalize(Vector3.Cross(helper, n));
            Vector3 ty = Vector3.Cross(n, tx);
            var corners = new Vector2[tris.Count * 3];
            for (int k = 0; k < tris.Count; k++)
                for (int e = 0; e < 3; e++)
                {
                    var p = wp[sm.Indices[tris[k] * 3 + e]];
                    corners[k * 3 + e] = new Vector2(Vector3.Dot(p, tx), Vector3.Dot(p, ty)) * tpu;
                }

            // Smallest box: try each convex-hull edge's direction.
            var hull = Hull(corners);
            float best = float.MaxValue, bestAngle = 0f;
            for (int i = 0; i < hull.Count; i++)
            {
                var d = hull[(i + 1) % hull.Count] - hull[i];
                if (d.LengthSquared() < 1e-12f) continue;
                float angle = -MathF.Atan2(d.Y, d.X);
                Box(hull, angle, out var mn, out var mx);
                float a = (mx.X - mn.X) * (mx.Y - mn.Y);
                if (a < best) { best = a; bestAngle = angle; }
            }
            Box(corners, bestAngle, out var lo, out var hi);
            float cs = MathF.Cos(bestAngle), sn = MathF.Sin(bestAngle);
            for (int i = 0; i < corners.Length; i++)
            {
                var c = corners[i];
                corners[i] = new Vector2(c.X * cs - c.Y * sn - lo.X, c.X * sn + c.Y * cs - lo.Y);
            }
            return new Chart { Sub = sub, Triangles = tris, Corners = corners, W = hi.X - lo.X, H = hi.Y - lo.Y };
        }

        private static void Box(IReadOnlyList<Vector2> pts, float angle, out Vector2 mn, out Vector2 mx)
        {
            float cs = MathF.Cos(angle), sn = MathF.Sin(angle);
            mn = new Vector2(float.MaxValue); mx = new Vector2(float.MinValue);
            for (int i = 0; i < pts.Count; i++)
            {
                var p = new Vector2(pts[i].X * cs - pts[i].Y * sn, pts[i].X * sn + pts[i].Y * cs);
                mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p);
            }
        }

        // Andrew's monotone chain.
        private static List<Vector2> Hull(Vector2[] pts)
        {
            var p = (Vector2[])pts.Clone();
            Array.Sort(p, (a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            var h = new List<Vector2>(p.Length + 1);
            static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
            for (int i = 0; i < p.Length; i++)
            {
                while (h.Count >= 2 && Cross(h[^2], h[^1], p[i]) <= 0) h.RemoveAt(h.Count - 1);
                h.Add(p[i]);
            }
            int lower = h.Count + 1;
            for (int i = p.Length - 2; i >= 0; i--)
            {
                while (h.Count >= lower && Cross(h[^2], h[^1], p[i]) <= 0) h.RemoveAt(h.Count - 1);
                h.Add(p[i]);
            }
            if (h.Count > 1) h.RemoveAt(h.Count - 1);
            return h;
        }

        /// <summary>Shelf packing, tallest first, into a width near the square root of the total area: each rectangle's
        /// place, and the width and height used.</summary>
        public static (int X, int Y)[] Shelf((int W, int H)[] sizes, out int width, out int height)
        {
            long total = 0; int widest = 1;
            foreach (var s in sizes) { total += (long)s.W * s.H; widest = Math.Max(widest, s.W); }
            int limit = Math.Max(widest, (int)MathF.Ceiling(MathF.Sqrt(total) * 1.15f));
            var order = new int[sizes.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => sizes[b].H != sizes[a].H ? sizes[b].H.CompareTo(sizes[a].H) : sizes[b].W.CompareTo(sizes[a].W));
            var at = new (int X, int Y)[sizes.Length];
            int x = 0, y = 0, shelf = 0;
            width = 0;
            foreach (int i in order)
            {
                if (x > 0 && x + sizes[i].W > limit) { y += shelf; x = 0; shelf = 0; }
                at[i] = (x, y);
                x += sizes[i].W;
                shelf = Math.Max(shelf, sizes[i].H);
                width = Math.Max(width, x);
            }
            height = y + shelf;
            return at;
        }

        /// <summary>
        /// Entity rectangles into square pages of at most <paramref name="maxSize"/> texels (shelves, tallest first), each
        /// page then made as small as the power of two that holds what it got: each rectangle's page and place, and the
        /// pages' sizes.
        /// </summary>
        public static (int Page, int X, int Y)[] PackPages((int W, int H)[] rects, int maxSize, out List<int> pageSizes)
        {
            var order = new int[rects.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => rects[b].H != rects[a].H ? rects[b].H.CompareTo(rects[a].H) : rects[b].W.CompareTo(rects[a].W));
            var at = new (int Page, int X, int Y)[rects.Length];
            var used = new List<(int W, int H)>();
            int page = -1, x = 0, y = 0, shelf = 0;
            foreach (int i in order)
            {
                var r = rects[i];
                if (page >= 0 && x > 0 && x + r.W > maxSize) { y += shelf; x = 0; shelf = 0; }   // the next shelf
                if (page < 0 || y + r.H > maxSize) { page++; used.Add((0, 0)); x = 0; y = 0; shelf = 0; }   // a new page
                at[i] = (page, x, y);
                x += r.W;
                shelf = Math.Max(shelf, r.H);
                used[page] = (Math.Max(used[page].W, x), Math.Max(used[page].H, y + shelf));
            }
            pageSizes = new List<int>(used.Count);
            foreach (var u in used)
            {
                int size = 32;
                while (size < Math.Max(u.W, u.H) && size < maxSize) size *= 2;
                pageSizes.Add(size);
            }
            return at;
        }
    }
}
