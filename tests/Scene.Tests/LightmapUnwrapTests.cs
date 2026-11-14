using System.Numerics;
using GameEditor.Framework.Renderer;
using GameEditor.Framework.Renderer.Server.GI;
using GameEditor.Framework.Renderer.Server.Resources;
using Xunit;

namespace Scene.Tests
{
    /// <summary>Lightmap UVs (M8a-2, examples/GameEditor/docs/BAKED_GI_DESIGN.md §3.2): charts, texel density, no overlap,
    /// padding kept, a big entity made to fit a page, pages packed.</summary>
    public sealed class LightmapUnwrapTests
    {
        static ObjSubMesh[] Mesh(string path)
        {
            Assert.True(PrimitiveMeshSpec.TryParse(path, out var spec));
            var (p, ix) = PrimitiveMeshGeometry.GetMeshTriangles(spec);
            var v = new ObjVertex[p.Length];
            for (int i = 0; i < p.Length; i++) v[i] = new ObjVertex { Position = p[i], Normal = Vector3.Normalize(p[i] + new Vector3(1e-6f)) };
            return new[] { new ObjSubMesh { MaterialName = "", Vertices = v, Indices = ix } };
        }

        static float Area2(Vector2 a, Vector2 b, Vector2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        // Each texel centre a triangle covers, the chart (triangle's first new vertex' chart is unknown: use the triangle's
        // connected component by shared vertices) - here simply: which triangle group (its welded new vertices) claims it.
        static int[,] Coverage(LightmapUnwrap.Result r, out int overlaps)
        {
            var owner = new int[r.Width + 1, r.Height + 1];
            overlaps = 0;
            var sm = r.SubMeshes[0];
            // group triangles by shared vertices (a chart's triangles share its vertices; charts never share one)
            var parent = new int[sm.Vertices.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            for (int t = 0; t < sm.Indices.Length; t += 3)
            {
                parent[Find((int)sm.Indices[t + 1])] = Find((int)sm.Indices[t]);
                parent[Find((int)sm.Indices[t + 2])] = Find((int)sm.Indices[t]);
            }
            for (int t = 0; t < sm.Indices.Length; t += 3)
            {
                var a = sm.Vertices[sm.Indices[t]].Uv1; var b = sm.Vertices[sm.Indices[t + 1]].Uv1; var c = sm.Vertices[sm.Indices[t + 2]].Uv1;
                int group = Find((int)sm.Indices[t]) + 1;
                float area = Area2(a, b, c);
                for (int y = 0; y <= r.Height; y++)
                    for (int x = 0; x <= r.Width; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float w0 = Area2(b, c, p), w1 = Area2(c, a, p), w2 = Area2(a, b, p);
                        bool inside = area > 0 ? (w0 >= 0 && w1 >= 0 && w2 >= 0) : (w0 <= 0 && w1 <= 0 && w2 <= 0);
                        if (!inside) continue;
                        if (owner[x, y] != 0 && owner[x, y] != group) overlaps++;
                        owner[x, y] = group;
                    }
            }
            return owner;
        }

        [Fact]
        public void A_box_is_six_charts_at_the_asked_density_without_overlap_and_with_its_padding()
        {
            var world = Matrix4x4.CreateScale(2f);   // 2 m faces
            var r = LightmapUnwrap.Unwrap(Mesh("prim:box;w=1;h=1;d=1;tiles=1"), world, 10f, 2, 1024);
            Assert.Equal(6, r.Charts);
            Assert.Equal(10f, r.TexelsPerUnit);
            var sm = r.SubMeshes[0];
            Assert.Equal(36, sm.Indices.Length);
            Assert.Equal(24, sm.Vertices.Length);   // 4 per face: split at the charts' seams
            float uvArea = 0f;
            for (int t = 0; t < sm.Indices.Length; t += 3)
            {
                var a = sm.Vertices[sm.Indices[t]].Uv1; var b = sm.Vertices[sm.Indices[t + 1]].Uv1; var c = sm.Vertices[sm.Indices[t + 2]].Uv1;
                uvArea += System.MathF.Abs(Area2(a, b, c)) * 0.5f;
                foreach (var uv in new[] { a, b, c })
                    Assert.True(uv.X >= 0 && uv.Y >= 0 && uv.X <= r.Width && uv.Y <= r.Height, $"{uv} in {r.Width}x{r.Height}");
            }
            Assert.InRange(uvArea, 6 * 4 * 100 * 0.99f, 6 * 4 * 100 * 1.01f);   // 6 faces of 4 m² at 10 texels per metre

            var owner = Coverage(r, out int overlaps);
            Assert.Equal(0, overlaps);
            // Padding: two texels of different charts are at least 2 apart (in x or y).
            for (int y = 0; y <= r.Height; y++)
                for (int x = 0; x <= r.Width; x++)
                {
                    if (owner[x, y] == 0) continue;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx > r.Width || ny > r.Height) continue;
                            Assert.True(owner[nx, ny] == 0 || owner[nx, ny] == owner[x, y], $"charts touch at {x},{y}");
                        }
                }
        }

        [Fact]
        public void A_sphere_unwraps_into_several_charts_none_folded_none_overlapping()
        {
            var r = LightmapUnwrap.Unwrap(Mesh("prim:sphere"), Matrix4x4.CreateScale(2f), 20f, 2, 1024);
            Assert.InRange(r.Charts, 6, 40);
            var sm = r.SubMeshes[0];
            int positive = 0, negative = 0;
            for (int t = 0; t < sm.Indices.Length; t += 3)
            {
                float a = Area2(sm.Vertices[sm.Indices[t]].Uv1, sm.Vertices[sm.Indices[t + 1]].Uv1, sm.Vertices[sm.Indices[t + 2]].Uv1);
                if (a > 1e-6f) positive++; else if (a < -1e-6f) negative++;
            }
            Assert.True(positive == 0 || negative == 0, $"every triangle keeps its winding: {positive} / {negative}");
            Coverage(r, out int overlaps);
            Assert.Equal(0, overlaps);
        }

        [Fact]
        public void A_big_entity_is_made_to_fit_the_page()
        {
            var r = LightmapUnwrap.Unwrap(Mesh("prim:box;w=1;h=1;d=1;tiles=1"), Matrix4x4.CreateScale(100f), 40f, 2, 256);
            Assert.True(r.Width <= 256 && r.Height <= 256, $"{r.Width}x{r.Height}");
            Assert.True(r.TexelsPerUnit < 40f);
        }

        [Fact]
        public void Rectangles_go_into_square_power_of_two_pages_without_overlap()
        {
            var rects = new (int W, int H)[40];
            for (int i = 0; i < rects.Length; i++) rects[i] = (20 + i * 3 % 50, 15 + i * 7 % 40);
            var at = LightmapUnwrap.PackPages(rects, 128, out var sizes);
            Assert.True(sizes.Count >= 2);
            foreach (int s in sizes) Assert.True(s <= 128 && (s & (s - 1)) == 0, $"page size {s}");
            for (int i = 0; i < rects.Length; i++)
            {
                Assert.True(at[i].X + rects[i].W <= sizes[at[i].Page] && at[i].Y + rects[i].H <= sizes[at[i].Page], $"rect {i} inside its page");
                for (int j = i + 1; j < rects.Length; j++)
                {
                    if (at[i].Page != at[j].Page) continue;
                    bool apart = at[i].X + rects[i].W <= at[j].X || at[j].X + rects[j].W <= at[i].X
                              || at[i].Y + rects[i].H <= at[j].Y || at[j].Y + rects[j].H <= at[i].Y;
                    Assert.True(apart, $"rects {i} and {j} overlap");
                }
            }
        }
    }
}
