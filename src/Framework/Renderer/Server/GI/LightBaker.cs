using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Resources;

namespace GameEditor.Framework.Renderer.Server.GI
{
    /// <summary>A surface's look to the baker: linear albedo and emission; <see cref="Texture"/> (optional) multiplies the albedo
    /// at a TEXCOORD_0.</summary>
    public struct BakeMaterial
    {
        public Vector3 Albedo;
        public Vector3 Emission;
        public Func<Vector2, Vector3>? Texture;
    }

    /// <summary>A contributing renderer: its mesh (as the registry holds it), where it is, its sub-meshes' materials.</summary>
    public sealed class BakeMesh
    {
        public int SceneId;
        public Matrix4x4 World = Matrix4x4.Identity;
        public ObjSubMesh[] Source = Array.Empty<ObjSubMesh>();
        public BakeMaterial[] Materials = Array.Empty<BakeMaterial>();
        public float ScaleInLightmap = 1f;
    }

    /// <summary>A Mixed or Baked light, in world space; <see cref="Color"/> is colour × intensity (the engine's units).</summary>
    public struct BakeLight
    {
        public int SceneId;
        public LightType Type;
        public LightMode Mode;
        public Vector3 Position, Direction, Color;
        public float Range, InnerCos, OuterCos, IndirectMultiplier;
        public bool Shadows;
    }

    public sealed class BakeInput
    {
        public List<BakeMesh> Meshes = new();
        public List<BakeLight> Lights = new();
        /// <summary>The sky's linear radiance toward a world direction (the environment the scene is lit by).</summary>
        public Func<Vector3, Vector3> Sky = _ => Vector3.Zero;
        public LightingSettings Settings = new();
    }

    /// <summary>A lightmap page: per texel the light a white diffuse surface there gives back (irradiance / π, linear) and,
    /// directional, the dominant direction (xyz 0..1) and its rebalancing (w).</summary>
    public sealed class LightmapPage
    {
        public int Size;
        public Vector3[] Light = Array.Empty<Vector3>();
        public Vector4[] Direction = Array.Empty<Vector4>();
        public bool[] Covered = Array.Empty<bool>();   // a surface's texel (not padding)
    }

    public sealed class BakedEntity
    {
        public int SceneId;
        public int Page;
        /// <summary>Its mesh with the lightmap UV (0..1 on its page) in <c>Uv1</c>.</summary>
        public ObjSubMesh[] Mesh = Array.Empty<ObjSubMesh>();
    }

    public sealed class BakeResult
    {
        public List<LightmapPage> Pages = new();
        public List<BakedEntity> Entities = new();
        public long Rays;
        public double Seconds;
    }

    /// <summary>
    /// The lightmap baker (M8a, docs BAKED_GI_DESIGN.md §3.3): CPU path tracing on worker threads, deterministic per texel.
    /// Per texel: the direct light of Baked lights (shadow rays), the sky, and bounced light up to Max Bounces (next-event
    /// estimation of Baked and Mixed lights at each bounce, Russian roulette after the second); the bounce part filtered
    /// edge-aware, the charts' edges dilated into their padding.
    /// </summary>
    public static class LightBaker
    {
        private const float Eps = 1e-3f;

        public static BakeResult Bake(BakeInput input, Action<float>? progress = null, CancellationToken cancel = default)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            var s = input.Settings;
            var result = new BakeResult();

            // ── UVs: each mesh unwrapped, the rectangles packed into pages ──
            var unwrapped = new List<(BakeMesh Mesh, LightmapUnwrap.Result Uv)>();
            foreach (var m in input.Meshes)
            {
                var r = LightmapUnwrap.Unwrap(m.Source, m.World, s.Resolution * MathF.Max(0.01f, m.ScaleInLightmap), s.Padding, s.MaxLightmapSize);
                if (r.Charts > 0) unwrapped.Add((m, r));
            }
            var rects = new (int W, int H)[unwrapped.Count];
            for (int i = 0; i < rects.Length; i++) rects[i] = (unwrapped[i].Uv.Width, unwrapped[i].Uv.Height);
            var at = LightmapUnwrap.PackPages(rects, s.MaxLightmapSize, out var pageSizes);
            foreach (int size in pageSizes)
                result.Pages.Add(new LightmapPage { Size = size, Light = new Vector3[size * size], Direction = new Vector4[size * size], Covered = new bool[size * size] });

            // ── each entity's final mesh (UV on its page) and the scene's triangles ──
            var corners = new List<Vector3>();
            var triMaterial = new List<BakeMaterial>();
            var triUv0 = new List<(Vector2, Vector2, Vector2)>();
            for (int i = 0; i < unwrapped.Count; i++)
            {
                var (m, uv) = unwrapped[i];
                int size = pageSizes[at[i].Page];
                var mesh = new ObjSubMesh[uv.SubMeshes.Length];
                for (int sIdx = 0; sIdx < mesh.Length; sIdx++)
                {
                    var src = uv.SubMeshes[sIdx];
                    var v = (ObjVertex[])src.Vertices.Clone();
                    for (int k = 0; k < v.Length; k++)
                        v[k].Uv1 = new Vector2((at[i].X + v[k].Uv1.X) / size, (at[i].Y + v[k].Uv1.Y) / size);
                    mesh[sIdx] = new ObjSubMesh { MaterialName = src.MaterialName, Vertices = v, Indices = src.Indices };
                    var mat = sIdx < m.Materials.Length ? m.Materials[sIdx] : new BakeMaterial { Albedo = new Vector3(0.8f) };
                    for (int t = 0; t + 2 < src.Indices.Length; t += 3)
                    {
                        for (int e = 0; e < 3; e++) corners.Add(Vector3.Transform(v[src.Indices[t + e]].Position, m.World));
                        triMaterial.Add(mat);
                        triUv0.Add((v[src.Indices[t]].Uv, v[src.Indices[t + 1]].Uv, v[src.Indices[t + 2]].Uv));
                    }
                }
                result.Entities.Add(new BakedEntity { SceneId = m.SceneId, Page = at[i].Page, Mesh = mesh });
            }
            var bvh = new Bvh(corners.ToArray());
            var ctx = new Context(input, bvh, triMaterial.ToArray(), triUv0.ToArray());

            // ── texels: each surface's position and normal where its triangles cover a texel centre ──
            var pages = new List<Texels>();
            for (int p = 0; p < result.Pages.Count; p++) pages.Add(new Texels(result.Pages[p].Size));
            for (int i = 0; i < unwrapped.Count; i++)
            {
                var m = unwrapped[i].Mesh;
                var e = result.Entities[i];
                var tx = pages[e.Page];
                var nm = Matrix4x4.Invert(m.World, out var inv) ? Matrix4x4.Transpose(inv) : Matrix4x4.Identity;
                foreach (var sm in e.Mesh)
                    for (int t = 0; t + 2 < sm.Indices.Length; t += 3)
                        Raster(tx, i, sm.Vertices[sm.Indices[t]], sm.Vertices[sm.Indices[t + 1]], sm.Vertices[sm.Indices[t + 2]], m.World, nm);
            }

            // ── light per texel ──
            long totalRows = 0, doneRows = 0, rays = 0;
            foreach (var tx in pages) totalRows += tx.Size;
            bool directional = s.DirectionalMode == DirectionalMode.Directional;
            for (int p = 0; p < pages.Count; p++)
            {
                var tx = pages[p];
                var page = result.Pages[p];
                var direct = new Vector3[tx.Pos.Length];
                var bounce = new Vector3[tx.Pos.Length];
                int pageIndex = p;
                Parallel.For(0, tx.Size, new ParallelOptions { CancellationToken = cancel }, y =>
                {
                    long localRays = 0;
                    for (int x = 0; x < tx.Size; x++)
                    {
                        int i = y * tx.Size + x;
                        if (tx.Owner[i] < 0) continue;
                        var rng = new Rng((uint)(pageIndex * 73856093) ^ (uint)(i * 19349663) ^ 0x9E3779B9u);
                        Texel(ctx, tx.Pos[i], tx.Nrm[i], ref rng, out direct[i], out bounce[i], out var dir, ref localRays);
                        if (directional)
                        {
                            float w = 0.5f * Vector3.Dot(tx.Nrm[i], dir) + 0.5f;
                            page.Direction[i] = new Vector4(dir * 0.5f + new Vector3(0.5f), MathF.Max(1e-4f, w));
                        }
                    }
                    Interlocked.Add(ref rays, localRays);
                    long d = Interlocked.Increment(ref doneRows);
                    progress?.Invoke((float)d / Math.Max(1, totalRows));
                });
                cancel.ThrowIfCancellationRequested();
                if (s.Filtering) bounce = Filter(tx, bounce);
                for (int i = 0; i < page.Light.Length; i++)
                {
                    if (tx.Owner[i] < 0) continue;
                    page.Light[i] = direct[i] + bounce[i];
                    page.Covered[i] = true;
                }
                Dilate(tx, page, s.Padding + 2);
            }
            progress?.Invoke(1f);
            result.Rays = rays;
            result.Seconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds;
            return result;
        }

        // ── the scene, read-only while threads trace it ─────────────────────────────────
        private sealed class Context
        {
            public readonly BakeInput Input;
            public readonly Bvh Bvh;
            public readonly BakeMaterial[] Material;
            public readonly (Vector2 A, Vector2 B, Vector2 C)[] Uv0;
            public readonly int MaxBounces;
            public readonly int Samples;
            public readonly float IndirectIntensity, AlbedoBoost;

            public Context(BakeInput input, Bvh bvh, BakeMaterial[] material, (Vector2, Vector2, Vector2)[] uv0)
            {
                Input = input; Bvh = bvh; Material = material; Uv0 = uv0;
                MaxBounces = input.Settings.MaxBounces;
                Samples = Math.Max(1, input.Settings.IndirectSamples);
                IndirectIntensity = input.Settings.IndirectIntensity;
                AlbedoBoost = input.Settings.AlbedoBoost;
            }

            public Vector3 Albedo(int tri, float u, float v)
            {
                var m = Material[tri];
                var a = m.Albedo;
                if (m.Texture != null)
                {
                    var (ua, ub, uc) = Uv0[tri];
                    a *= m.Texture(ua * (1f - u - v) + ub * u + uc * v);
                }
                return Vector3.Min(a * AlbedoBoost, new Vector3(1f));
            }
        }

        private struct Rng
        {
            private uint _s;
            public Rng(uint seed) { _s = seed == 0 ? 0x1234567u : seed; Next(); Next(); }
            public uint Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
            public float Float() => (Next() >> 8) * (1f / 16777216f);
        }

        private static Vector3 Cosine(Vector3 n, ref Rng rng)
        {
            float r1 = rng.Float(), r2 = rng.Float();
            float phi = 2f * MathF.PI * r1, r = MathF.Sqrt(r2);
            Vector3 t = MathF.Abs(n.X) > 0.5f ? Vector3.Normalize(Vector3.Cross(n, Vector3.UnitY)) : Vector3.Normalize(Vector3.Cross(n, Vector3.UnitX));
            Vector3 b = Vector3.Cross(n, t);
            return Vector3.Normalize(t * (MathF.Cos(phi) * r) + b * (MathF.Sin(phi) * r) + n * MathF.Sqrt(MathF.Max(0f, 1f - r2)));
        }

        private static float Luminance(Vector3 c) => 0.2126f * c.X + 0.7152f * c.Y + 0.0722f * c.Z;

        // The light of the scene's lights at a point (radiance a white diffuse surface gives back): Baked lights, and Mixed
        // ones too for a bounce (scaled by their Indirect Multiplier). dirAcc: luminance-weighted directions.
        private static Vector3 Lights(Context ctx, Vector3 p, Vector3 n, bool bounce, ref Vector3 dirAcc, ref long rays)
        {
            var sum = Vector3.Zero;
            foreach (var l in ctx.Input.Lights)
            {
                if (l.Mode == LightMode.Realtime || (l.Mode == LightMode.Mixed && !bounce)) continue;
                Vector3 toL; float dist, att;
                if (l.Type == LightType.Directional)
                {
                    toL = -Vector3.Normalize(l.Direction); dist = 1e6f; att = 1f;
                }
                else
                {
                    var d = l.Position - p;
                    dist = d.Length();
                    if (dist < 1e-6f) continue;
                    toL = d / dist;
                    att = l.Range < 0f ? 1f : MathF.Max(MathF.Min(1f - MathF.Pow(dist / MathF.Max(l.Range, 1e-4f), 4f), 1f), 0f) / MathF.Max(dist * dist, 1e-4f);
                    if (l.Type == LightType.Spot)
                    {
                        float theta = Vector3.Dot(-toL, Vector3.Normalize(l.Direction));
                        att *= Math.Clamp((theta - l.OuterCos) / MathF.Max(l.InnerCos - l.OuterCos, 1e-4f), 0f, 1f);
                    }
                }
                float ndl = Vector3.Dot(n, toL);
                if (ndl <= 0f || att <= 0f) continue;
                if (l.Shadows)
                {
                    rays++;
                    if (ctx.Bvh.Occluded(p + n * Eps, toL, dist - 2f * Eps)) continue;
                }
                var c = l.Color * (ndl * att) * (bounce ? l.IndirectMultiplier : 1f);
                sum += c;
                dirAcc += toL * Luminance(c);
            }
            return sum;
        }

        // The radiance arriving at o from direction w: the sky when nothing is hit (sky: on the first ray); else the
        // surface's emission and, while bounces are left, the light it reflects (its lights + the next bounce).
        private static Vector3 Radiance(Context ctx, Vector3 o, Vector3 w, ref Rng rng, ref long rays, out bool sky)
        {
            sky = false;
            var throughput = Vector3.One;
            var sum = Vector3.Zero;
            for (int bounce = 1; ; bounce++)
            {
                rays++;
                if (!ctx.Bvh.Intersect(o, w, 1e30f, out var hit))
                {
                    sky = bounce == 1;
                    return sum + throughput * ctx.Input.Sky(w);
                }
                int tri = hit.Triangle;
                var n = ctx.Bvh.Normal(tri);
                if (Vector3.Dot(n, w) > 0f) n = -n;
                sum += throughput * ctx.Material[tri].Emission;
                if (bounce > ctx.MaxBounces) return sum;
                var albedo = ctx.Albedo(tri, hit.U, hit.V);
                var p = o + w * hit.T + n * Eps;
                Vector3 unused = default;
                sum += throughput * albedo * Lights(ctx, p, n, true, ref unused, ref rays);
                throughput *= albedo;
                if (bounce >= 2)
                {
                    float keep = Math.Clamp(MathF.Max(throughput.X, MathF.Max(throughput.Y, throughput.Z)), 0.05f, 0.95f);
                    if (rng.Float() > keep) return sum;
                    throughput /= keep;
                }
                o = p;
                w = Cosine(n, ref rng);
            }
        }

        // A texel's light: direct (Baked lights) and the rest (sky + bounces, the bounces × Indirect Intensity); dir: the
        // luminance-weighted mean direction it came from (length 1 when from one direction, shorter when spread).
        private static void Texel(Context ctx, Vector3 pos, Vector3 n, ref Rng rng, out Vector3 direct, out Vector3 bounce, out Vector3 dir, ref long rays)
        {
            var o = pos + n * Eps;
            var dirDirect = Vector3.Zero;
            direct = Lights(ctx, o, n, false, ref dirDirect, ref rays);
            var sum = Vector3.Zero; var dirSamples = Vector3.Zero;
            for (int k = 0; k < ctx.Samples; k++)
            {
                var w = Cosine(n, ref rng);
                var l = Radiance(ctx, o, w, ref rng, ref rays, out bool sky);
                if (!sky) l *= ctx.IndirectIntensity;
                sum += l;
                dirSamples += w * Luminance(l);
            }
            float inv = 1f / ctx.Samples;
            bounce = sum * inv;
            float total = Luminance(direct) + Luminance(bounce);
            dir = total > 1e-8f ? (dirDirect + dirSamples * inv) / total : n;
            float len = dir.Length();
            if (len > 1f) dir /= len;
        }

        // ── texel coverage ──────────────────────────────────────────────────────────────
        private sealed class Texels
        {
            public readonly int Size;
            public readonly Vector3[] Pos, Nrm;
            public readonly int[] Owner;   // the entity (index in the bake) whose surface the texel is; -1 none
            public int Count;
            public Texels(int size)
            {
                Size = size;
                Pos = new Vector3[size * size]; Nrm = new Vector3[size * size];
                Owner = new int[size * size];
                Array.Fill(Owner, -1);
            }
        }

        private static void Raster(Texels tx, int owner, ObjVertex a, ObjVertex b, ObjVertex c, Matrix4x4 world, Matrix4x4 normalMatrix)
        {
            int size = tx.Size;
            Vector2 pa = a.Uv1 * size, pb = b.Uv1 * size, pc = c.Uv1 * size;
            float area = (pb.X - pa.X) * (pc.Y - pa.Y) - (pb.Y - pa.Y) * (pc.X - pa.X);
            if (MathF.Abs(area) < 1e-9f) return;
            Vector3 wa = Vector3.Transform(a.Position, world), wb = Vector3.Transform(b.Position, world), wc = Vector3.Transform(c.Position, world);
            Vector3 na = Vector3.TransformNormal(a.Normal, normalMatrix), nb = Vector3.TransformNormal(b.Normal, normalMatrix), nc = Vector3.TransformNormal(c.Normal, normalMatrix);
            var geo = Vector3.Cross(wb - wa, wc - wa);
            geo = geo.LengthSquared() > 0 ? Vector3.Normalize(geo) : Vector3.UnitY;
            int x0 = Math.Max(0, (int)MathF.Floor(MathF.Min(pa.X, MathF.Min(pb.X, pc.X)))), x1 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(pa.X, MathF.Max(pb.X, pc.X))));
            int y0 = Math.Max(0, (int)MathF.Floor(MathF.Min(pa.Y, MathF.Min(pb.Y, pc.Y)))), y1 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(pa.Y, MathF.Max(pb.Y, pc.Y))));
            float inv = 1f / area;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = ((pb.X - p.X) * (pc.Y - p.Y) - (pb.Y - p.Y) * (pc.X - p.X)) * inv;
                    float w1 = ((pc.X - p.X) * (pa.Y - p.Y) - (pc.Y - p.Y) * (pa.X - p.X)) * inv;
                    float w2 = 1f - w0 - w1;
                    if (w0 < -1e-5f || w1 < -1e-5f || w2 < -1e-5f) continue;
                    int i = y * size + x;
                    if (tx.Owner[i] < 0) tx.Count++;
                    tx.Owner[i] = owner;
                    tx.Pos[i] = wa * w0 + wb * w1 + wc * w2;
                    var n = na * w0 + nb * w1 + nc * w2;
                    n = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : geo;
                    if (Vector3.Dot(n, geo) < 0f) n = geo;   // a shading normal facing away: the face's own
                    tx.Nrm[i] = n;
                }
        }

        // ── edge-aware blur of the bounce light, within one entity's texels ──────────────
        private static Vector3[] Filter(Texels tx, Vector3[] src)
        {
            var dst = new Vector3[src.Length];
            int size = tx.Size;
            Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    if (tx.Owner[i] < 0) continue;
                    var sum = Vector3.Zero; float wsum = 0f;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                            int j = ny * size + nx;
                            if (tx.Owner[j] != tx.Owner[i]) continue;
                            float nd = MathF.Max(0f, Vector3.Dot(tx.Nrm[i], tx.Nrm[j]));
                            float w = MathF.Exp(-(dx * dx + dy * dy) / 4.5f) * MathF.Pow(nd, 8f);
                            sum += src[j] * w; wsum += w;
                        }
                    dst[i] = wsum > 0f ? sum / wsum : src[i];
                }
            });
            return dst;
        }

        // ── the charts' edges spread into their padding (no dark seams with bilinear filtering) ──
        private static void Dilate(Texels tx, LightmapPage page, int iterations)
        {
            int size = tx.Size;
            var filled = (bool[])page.Covered.Clone();
            for (int it = 0; it < iterations; it++)
            {
                var next = (bool[])filled.Clone();
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int i = y * size + x;
                        if (filled[i]) continue;
                        var sum = Vector3.Zero; var dsum = Vector4.Zero; int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                                int j = ny * size + nx;
                                if (!filled[j]) continue;
                                sum += page.Light[j]; dsum += page.Direction[j]; n++;
                            }
                        if (n == 0) continue;
                        page.Light[i] = sum / n; page.Direction[i] = dsum / n;
                        next[i] = true;
                    }
                filled = next;
            }
        }
    }
}
