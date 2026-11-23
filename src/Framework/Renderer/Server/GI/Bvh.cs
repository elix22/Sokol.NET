using System;
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.GI
{
    /// <summary>
    /// A bounding volume hierarchy over world-space triangles for the lightmap baker (M8a, docs BAKED_GI_DESIGN.md §3.3):
    /// binned SAH build, flat nodes, closest hit and any hit. Read-only once built: many threads trace it at once.
    /// </summary>
    public sealed class Bvh
    {
        private struct Node
        {
            public Vector3 Min, Max;
            public int Left;     // inner: the left child (the right is Left + 1); leaf: first triangle in _order
            public int Count;    // 0: inner node; > 0: a leaf of Count triangles
        }

        private struct Tri { public Vector3 A, E1, E2; }   // a corner and two edges
        private readonly Vector3[] _a, _e1, _e2;   // per triangle (its own index), while building
        private readonly int[] _order;
        private readonly Tri[] _tris;              // per leaf slot: a leaf's triangles side by side in memory (one fetch, not three far apart)
        private readonly int[] _slot;              // a triangle's leaf slot
        private Node[] _nodes;
        private int _nodeCount;

        public int TriangleCount => _tris.Length;

        /// <summary>A hit: distance along the ray, the triangle, and its barycentrics (u for the second corner, v the third).</summary>
        public struct Hit { public float T; public int Triangle; public float U, V; }

        /// <summary>Builds over triangles given as 3 corners each (<paramref name="corners"/>.Length = 3 × triangles).</summary>
        public Bvh(ReadOnlySpan<Vector3> corners)
        {
            int n = corners.Length / 3;
            _a = new Vector3[n]; _e1 = new Vector3[n]; _e2 = new Vector3[n];
            _order = new int[n];
            var cmin = new Vector3[n]; var cmax = new Vector3[n]; var centre = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 a = corners[i * 3], b = corners[i * 3 + 1], c = corners[i * 3 + 2];
                _a[i] = a; _e1[i] = b - a; _e2[i] = c - a;
                cmin[i] = Vector3.Min(a, Vector3.Min(b, c));
                cmax[i] = Vector3.Max(a, Vector3.Max(b, c));
                centre[i] = (cmin[i] + cmax[i]) * 0.5f;
                _order[i] = i;
            }
            _nodes = new Node[Math.Max(1, 2 * n)];
            _nodeCount = 1;
            Build(0, 0, n, cmin, cmax, centre);
            _tris = new Tri[n];
            _slot = new int[n];
            for (int i = 0; i < n; i++) { int t = _order[i]; _tris[i] = new Tri { A = _a[t], E1 = _e1[t], E2 = _e2[t] }; _slot[t] = i; }
            _a = _e1 = _e2 = Array.Empty<Vector3>();
        }

        private const int Bins = 12, LeafSize = 4;

        private void Build(int node, int first, int count, Vector3[] cmin, Vector3[] cmax, Vector3[] centre)
        {
            Vector3 bmin = new(float.MaxValue), bmax = new(float.MinValue), kmin = new(float.MaxValue), kmax = new(float.MinValue);
            for (int i = first; i < first + count; i++)
            {
                int t = _order[i];
                bmin = Vector3.Min(bmin, cmin[t]); bmax = Vector3.Max(bmax, cmax[t]);
                kmin = Vector3.Min(kmin, centre[t]); kmax = Vector3.Max(kmax, centre[t]);
            }
            _nodes[node].Min = bmin; _nodes[node].Max = bmax;
            if (count <= LeafSize) { _nodes[node].Left = first; _nodes[node].Count = count; return; }

            // The cheapest split of the centres' extent into Bins, on any axis (surface area heuristic).
            int bestAxis = -1, bestBin = 0; float bestCost = float.MaxValue;
            Span<int> binCount = stackalloc int[Bins];
            Span<Vector3> binMin = stackalloc Vector3[Bins], binMax = stackalloc Vector3[Bins];
            Span<float> leftArea = stackalloc float[Bins]; Span<int> leftCount = stackalloc int[Bins];
            for (int axis = 0; axis < 3; axis++)
            {
                float lo = Get(kmin, axis), hi = Get(kmax, axis);
                if (hi - lo < 1e-9f) continue;
                binCount.Clear();
                for (int b = 0; b < Bins; b++) { binMin[b] = new Vector3(float.MaxValue); binMax[b] = new Vector3(float.MinValue); }
                float scale = Bins / (hi - lo);
                for (int i = first; i < first + count; i++)
                {
                    int t = _order[i];
                    int b = Math.Min(Bins - 1, (int)((Get(centre[t], axis) - lo) * scale));
                    binCount[b]++; binMin[b] = Vector3.Min(binMin[b], cmin[t]); binMax[b] = Vector3.Max(binMax[b], cmax[t]);
                }
                Vector3 lmin = new(float.MaxValue), lmax = new(float.MinValue); int lc = 0;
                for (int b = 0; b < Bins - 1; b++)
                {
                    lc += binCount[b];
                    if (binCount[b] > 0) { lmin = Vector3.Min(lmin, binMin[b]); lmax = Vector3.Max(lmax, binMax[b]); }
                    leftCount[b] = lc; leftArea[b] = lc > 0 ? Area(lmin, lmax) : 0f;
                }
                Vector3 rmin = new(float.MaxValue), rmax = new(float.MinValue); int rc = 0;
                for (int b = Bins - 1; b > 0; b--)
                {
                    rc += binCount[b];
                    if (binCount[b] > 0) { rmin = Vector3.Min(rmin, binMin[b]); rmax = Vector3.Max(rmax, binMax[b]); }
                    if (leftCount[b - 1] == 0 || rc == 0) continue;
                    float cost = leftArea[b - 1] * leftCount[b - 1] + Area(rmin, rmax) * rc;
                    if (cost < bestCost) { bestCost = cost; bestAxis = axis; bestBin = b; }
                }
            }

            int mid;
            if (bestAxis < 0)
                mid = first + count / 2;   // every centre in one place: halve the list
            else
            {
                float lo = Get(kmin, bestAxis), scale = Bins / (Get(kmax, bestAxis) - lo);
                int i = first, j = first + count - 1;
                while (i <= j)
                {
                    int b = Math.Min(Bins - 1, (int)((Get(centre[_order[i]], bestAxis) - lo) * scale));
                    if (b < bestBin) i++;
                    else { (_order[i], _order[j]) = (_order[j], _order[i]); j--; }
                }
                mid = i;
                if (mid == first || mid == first + count) mid = first + count / 2;
            }
            int left = _nodeCount;
            _nodeCount += 2;
            if (_nodeCount > _nodes.Length) Array.Resize(ref _nodes, _nodes.Length * 2);
            _nodes[node].Left = left; _nodes[node].Count = 0;
            Build(left, first, mid - first, cmin, cmax, centre);
            Build(left + 1, mid, first + count - mid, cmin, cmax, centre);
        }

        private static float Get(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;
        private static float Area(Vector3 mn, Vector3 mx) { var d = mx - mn; return d.X * d.Y + d.Y * d.Z + d.Z * d.X; }

        /// <summary>
        /// The tree as flat arrays for a GPU (Retopology's texture bake): two vec4 a node - (min, first child or first leaf
        /// slot), (max, triangle count: 0 an inner node), the integers' bits in w; three a leaf slot - (corner, the
        /// triangle's own index), (edge 1, 0), (edge 2, 0). A hit's barycentrics: u for the second corner, v the third.
        /// </summary>
        public void Flatten(out Vector4[] nodes, out Vector4[] triangles)
        {
            nodes = new Vector4[_nodeCount * 2];
            for (int i = 0; i < _nodeCount; i++)
            {
                ref Node n = ref _nodes[i];
                nodes[i * 2] = new Vector4(n.Min, BitConverter.Int32BitsToSingle(n.Left));
                nodes[i * 2 + 1] = new Vector4(n.Max, BitConverter.Int32BitsToSingle(n.Count));
            }
            triangles = new Vector4[_tris.Length * 3];
            for (int i = 0; i < _tris.Length; i++)
            {
                ref Tri t = ref _tris[i];
                triangles[i * 3] = new Vector4(t.A, BitConverter.Int32BitsToSingle(_order[i]));
                triangles[i * 3 + 1] = new Vector4(t.E1, 0f);
                triangles[i * 3 + 2] = new Vector4(t.E2, 0f);
            }
        }

        /// <summary>The nearest triangle the ray meets before <paramref name="tMax"/> (both faces), or false.</summary>
        public bool Intersect(Vector3 origin, Vector3 dir, float tMax, out Hit hit) => Trace(origin, dir, tMax, false, out hit);

        /// <summary>True when any triangle lies on the ray before <paramref name="tMax"/>.</summary>
        public bool Occluded(Vector3 origin, Vector3 dir, float tMax) => Trace(origin, dir, tMax, true, out _);

        private bool Trace(Vector3 o, Vector3 d, float tMax, bool any, out Hit hit)
        {
            hit = default; hit.Triangle = -1; hit.T = tMax;
            if (_tris.Length == 0) return false;
            var inv = new Vector3(1f / (MathF.Abs(d.X) > 1e-12f ? d.X : 1e-12f), 1f / (MathF.Abs(d.Y) > 1e-12f ? d.Y : 1e-12f), 1f / (MathF.Abs(d.Z) > 1e-12f ? d.Z : 1e-12f));
            Span<int> stack = stackalloc int[128];   // a binned SAH tree over a scene stays far below 64 levels
            int sp = 0;
            stack[sp++] = 0;
            while (sp > 0)
            {
                ref Node nd = ref _nodes[stack[--sp]];
                if (!Slab(nd.Min, nd.Max, o, inv, hit.T)) continue;
                if (nd.Count > 0)
                {
                    for (int i = nd.Left; i < nd.Left + nd.Count; i++)
                    {
                        if (Triangle(in _tris[i], o, d, hit.T, out float tt, out float u, out float v))
                        {
                            hit.T = tt; hit.Triangle = _order[i]; hit.U = u; hit.V = v;
                            if (any) return true;
                        }
                    }
                }
                else if (sp + 2 <= stack.Length)
                {
                    stack[sp++] = nd.Left + 1;
                    stack[sp++] = nd.Left;
                }
            }
            return hit.Triangle >= 0;
        }

        private static bool Slab(Vector3 mn, Vector3 mx, Vector3 o, Vector3 inv, float tMax)
        {
            var t0 = (mn - o) * inv; var t1 = (mx - o) * inv;
            var lo = Vector3.Min(t0, t1); var hi = Vector3.Max(t0, t1);
            float enter = MathF.Max(MathF.Max(lo.X, lo.Y), MathF.Max(lo.Z, 0f));
            float exit = MathF.Min(MathF.Min(hi.X, hi.Y), MathF.Min(hi.Z, tMax));
            return enter <= exit;
        }

        // Möller–Trumbore, both faces.
        private static bool Triangle(in Tri tr, Vector3 o, Vector3 d, float tMax, out float tt, out float u, out float v)
        {
            tt = u = v = 0f;
            Vector3 e1 = tr.E1, e2 = tr.E2;
            var p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (MathF.Abs(det) < 1e-12f) return false;
            float id = 1f / det;
            var s = o - tr.A;
            u = Vector3.Dot(s, p) * id;
            if (u < 0f || u > 1f) return false;
            var q = Vector3.Cross(s, e1);
            v = Vector3.Dot(d, q) * id;
            if (v < 0f || u + v > 1f) return false;
            tt = Vector3.Dot(e2, q) * id;
            return tt > 1e-6f && tt < tMax;
        }

        /// <summary>A triangle's geometric normal (unit; its corners' winding).</summary>
        public Vector3 Normal(int t) { ref Tri tr = ref _tris[_slot[t]]; return Vector3.Normalize(Vector3.Cross(tr.E1, tr.E2)); }
    }
}
