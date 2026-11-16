using System;
using System.Numerics;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer;
using GameEditor.Framework.Renderer.Server.GI;
using GameEditor.Framework.Renderer.Server.Resources;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The lightmap baker (M8a-3, examples/GameEditor/docs/BAKED_GI_DESIGN.md §3.3, §5) against answers known
    /// beforehand: the BVH against brute force, an open sky, the furnace (bounces add up), a baked sun and its shadow,
    /// the same bytes twice.</summary>
    public sealed class LightBakerTests
    {
        static ObjSubMesh Plane(float half)
        {
            var v = new[]
            {
                new ObjVertex { Position = new(-half, 0, -half), Normal = Vector3.UnitY },
                new ObjVertex { Position = new( half, 0, -half), Normal = Vector3.UnitY },
                new ObjVertex { Position = new( half, 0,  half), Normal = Vector3.UnitY },
                new ObjVertex { Position = new(-half, 0,  half), Normal = Vector3.UnitY },
            };
            return new ObjSubMesh { MaterialName = "", Vertices = v, Indices = new uint[] { 0, 2, 1, 0, 3, 2 } };   // facing +Y
        }

        // A closed box seen from inside: winding and normals inward (the collider triangles' winding already faces in).
        static ObjSubMesh InsideBox()
        {
            Assert.True(PrimitiveMeshSpec.TryParse("prim:box;w=1;h=1;d=1;tiles=1", out var spec));
            var (p, ix) = PrimitiveMeshGeometry.GetMeshTriangles(spec);
            var v = new ObjVertex[p.Length];
            for (int i = 0; i < p.Length; i++) v[i] = new ObjVertex { Position = p[i], Normal = -Vector3.Normalize(p[i]) };
            var a = p[ix[0]]; var b = p[ix[1]]; var c = p[ix[2]];
            Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), a) < 0f, "the triangles face in");
            return new ObjSubMesh { MaterialName = "", Vertices = v, Indices = ix };
        }

        static (float Mean, float Min, float Max) Covered(LightmapPage page, Func<Vector3, float> f)
        {
            double sum = 0; int n = 0; float mn = float.MaxValue, mx = float.MinValue;
            for (int i = 0; i < page.Light.Length; i++)
            {
                if (!page.Covered[i]) continue;
                float v = f(page.Light[i]);
                sum += v; n++; mn = MathF.Min(mn, v); mx = MathF.Max(mx, v);
            }
            Assert.True(n > 0);
            return ((float)(sum / n), mn, mx);
        }

        static LightingSettings Quick(int samples, int bounces) => new() { Resolution = 4f, Padding = 2, IndirectSamples = samples, MaxBounces = bounces, MaxLightmapSize = 256, Filtering = false };

        [Fact]
        public void The_bvh_finds_the_same_nearest_triangle_as_trying_them_all()
        {
            var rng = new Random(7);
            var corners = new Vector3[300 * 3];
            for (int i = 0; i < 300; i++)
            {
                var c = new Vector3(rng.NextSingle() * 20 - 10, rng.NextSingle() * 20 - 10, rng.NextSingle() * 20 - 10);
                for (int k = 0; k < 3; k++) corners[i * 3 + k] = c + new Vector3(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1);
            }
            var bvh = new Bvh(corners);
            int hits = 0;
            for (int r = 0; r < 500; r++)
            {
                var o = new Vector3(rng.NextSingle() * 30 - 15, rng.NextSingle() * 30 - 15, rng.NextSingle() * 30 - 15);
                var d = Vector3.Normalize(new Vector3(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1));
                float best = float.MaxValue;
                for (int t = 0; t < 300; t++)
                {
                    var one = new Bvh(corners.AsSpan(t * 3, 3));
                    if (one.Intersect(o, d, best, out var h)) best = h.T;
                }
                bool got = bvh.Intersect(o, d, 1e30f, out var hit);
                Assert.Equal(best < float.MaxValue, got);
                if (got) { hits++; Assert.InRange(hit.T, best - 1e-3f, best + 1e-3f); Assert.True(bvh.Occluded(o, d, best + 1e-3f)); }
            }
            Assert.True(hits > 20, $"rays that hit: {hits}");
        }

        [Fact]
        public void A_plane_under_an_even_sky_gets_the_skys_radiance()
        {
            var input = new BakeInput { Settings = Quick(256, 2), Sky = _ => new Vector3(0.7f, 0.5f, 0.3f) };
            input.Meshes.Add(new BakeMesh { SceneId = 1, Source = new[] { Plane(2f) }, Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.8f) } } });
            var r = LightBaker.Bake(input);
            Assert.Single(r.Pages);
            var red = Covered(r.Pages[0], c => c.X);
            Assert.InRange(red.Mean, 0.7f * 0.99f, 0.7f * 1.01f);   // nothing above: every ray sees the sky
            var blue = Covered(r.Pages[0], c => c.Z);
            Assert.InRange(blue.Mean, 0.3f * 0.99f, 0.3f * 1.01f);
        }

        [Fact]
        public void The_furnace_an_enclosure_glowing_one_with_albedo_one_half_gives_two_and_one_without_bounces()
        {
            var input = new BakeInput { Settings = Quick(256, 16) };
            input.Meshes.Add(new BakeMesh { SceneId = 1, World = Matrix4x4.CreateScale(4f), Source = new[] { InsideBox() },
                                            Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.5f), Emission = new Vector3(1f) } } });
            var r = LightBaker.Bake(input);
            var lit = Covered(r.Pages[0], c => c.Y);
            Assert.InRange(lit.Mean, 2f * 0.95f, 2f * 1.05f);          // E / (1 - ρ)

            input.Settings = Quick(64, 0);
            var none = Covered(LightBaker.Bake(input).Pages[0], c => c.Y);
            Assert.InRange(none.Mean, 0.999f, 1.001f);                 // its emission only

            input.Settings = Quick(256, 1);
            var one = Covered(LightBaker.Bake(input).Pages[0], c => c.Y);
            Assert.InRange(one.Mean, 1.5f * 0.97f, 1.5f * 1.03f);       // one bounce: E + ρE
        }

        [Fact]
        public void A_baked_sun_lights_the_plane_and_a_box_casts_its_shadow_a_mixed_one_bakes_no_direct_light()
        {
            var input = new BakeInput { Settings = Quick(16, 0) };
            input.Meshes.Add(new BakeMesh { SceneId = 1, Source = new[] { Plane(3f) }, Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.8f) } } });
            input.Lights.Add(new BakeLight { Type = LightType.Directional, Mode = LightMode.Baked, Direction = new Vector3(0, -1, 0), Color = new Vector3(2f), Shadows = true, IndirectMultiplier = 1f });
            var open = LightBaker.Bake(input);
            var lit = Covered(open.Pages[0], c => c.X);
            Assert.InRange(lit.Min, 1.999f, 2.001f);                    // NdotL 1 × intensity 2, everywhere
            Assert.InRange(lit.Max, 1.999f, 2.001f);

            Assert.True(PrimitiveMeshSpec.TryParse("prim:box;w=1;h=1;d=1;tiles=1", out var spec));
            var (p, ix) = PrimitiveMeshGeometry.GetMeshTriangles(spec);
            var bv = new ObjVertex[p.Length];
            for (int i = 0; i < p.Length; i++) bv[i] = new ObjVertex { Position = p[i], Normal = Vector3.Normalize(p[i]) };
            input.Meshes.Add(new BakeMesh { SceneId = 2, World = Matrix4x4.CreateTranslation(0, 1f, 0), Source = new[] { new ObjSubMesh { MaterialName = "", Vertices = bv, Indices = ix } },
                                            Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.8f) } } });
            var shaded = LightBaker.Bake(input);
            int planeIndex = shaded.Entities.FindIndex(e => e.SceneId == 1);
            var page = shaded.Pages[shaded.Entities[planeIndex].Page];
            var floor = Covered(page, c => c.X);
            Assert.True(floor.Min < 0.01f, $"the box's shadow on the plane: darkest {floor.Min}");

            input.Lights[0] = input.Lights[0] with { Mode = LightMode.Mixed };
            input.Meshes.RemoveAt(1);
            var mixed = Covered(LightBaker.Bake(input).Pages[0], c => c.X);
            Assert.InRange(mixed.Max, -0.001f, 0.001f);                 // a Mixed light's direct light is drawn, not baked
        }

        // The light a baked page gives at a world point of the plane (y = 0): the texel whose rasterised position is nearest.
        static Vector3 At(BakeResult r, int sceneId, Vector3 world)
        {
            var e = r.Entities.Find(x => x.SceneId == sceneId)!;
            var page = r.Pages[e.Page];
            var sm = e.Mesh[0];
            // the plane's own UV at that point: barycentric in the triangle holding it (x, z)
            for (int t = 0; t < sm.Indices.Length; t += 3)
            {
                var a = sm.Vertices[sm.Indices[t]]; var b = sm.Vertices[sm.Indices[t + 1]]; var c = sm.Vertices[sm.Indices[t + 2]];
                Vector2 P = new(world.X, world.Z), A = new(a.Position.X, a.Position.Z), B = new(b.Position.X, b.Position.Z), C = new(c.Position.X, c.Position.Z);
                float d = (B.Y - C.Y) * (A.X - C.X) + (C.X - B.X) * (A.Y - C.Y);
                float w0 = ((B.Y - C.Y) * (P.X - C.X) + (C.X - B.X) * (P.Y - C.Y)) / d;
                float w1 = ((C.Y - A.Y) * (P.X - C.X) + (A.X - C.X) * (P.Y - C.Y)) / d;
                float w2 = 1 - w0 - w1;
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                var uv = a.Uv1 * w0 + b.Uv1 * w1 + c.Uv1 * w2;
                int x = (int)(uv.X * page.Size), y = (int)(uv.Y * page.Size);
                return page.Light[y * page.Size + x];
            }
            throw new Exception($"{world} not on the plane");
        }

        // The shadowmask's value at a world point of the plane (y = 0), as At reads the light (its texel), or filtered
        // between the four nearest texels as the GPU reads it.
        static float MaskAt(BakeResult r, int sceneId, Vector3 world, bool bilinear = false)
        {
            var e = r.Entities.Find(x => x.SceneId == sceneId)!;
            var page = r.Pages[e.Page];
            var sm = e.Mesh[0];
            for (int t = 0; t < sm.Indices.Length; t += 3)
            {
                var a = sm.Vertices[sm.Indices[t]]; var b = sm.Vertices[sm.Indices[t + 1]]; var c = sm.Vertices[sm.Indices[t + 2]];
                Vector2 P = new(world.X, world.Z), A = new(a.Position.X, a.Position.Z), B = new(b.Position.X, b.Position.Z), C = new(c.Position.X, c.Position.Z);
                float d = (B.Y - C.Y) * (A.X - C.X) + (C.X - B.X) * (A.Y - C.Y);
                float w0 = ((B.Y - C.Y) * (P.X - C.X) + (C.X - B.X) * (P.Y - C.Y)) / d;
                float w1 = ((C.Y - A.Y) * (P.X - C.X) + (A.X - C.X) * (P.Y - C.Y)) / d;
                float w2 = 1 - w0 - w1;
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                var uv = a.Uv1 * w0 + b.Uv1 * w1 + c.Uv1 * w2;
                int ms = page.MaskSize;
                if (!bilinear) return page.Mask[(int)(uv.Y * ms) * ms + (int)(uv.X * ms)];
                float fx = uv.X * ms - 0.5f, fy = uv.Y * ms - 0.5f;
                int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
                float tx = fx - x0, ty = fy - y0;
                float M(int x, int y) => page.Mask[Math.Clamp(y, 0, ms - 1) * ms + Math.Clamp(x, 0, ms - 1)];
                return (M(x0, y0) * (1 - tx) + M(x0 + 1, y0) * tx) * (1 - ty) + (M(x0, y0 + 1) * (1 - tx) + M(x0 + 1, y0 + 1) * tx) * ty;
            }
            throw new Exception($"{world} not on the plane");
        }

        // A floor (3 x 3, scene id 1), a 1 m box on it (2) and a Mixed sun (9) toward +X and down: the box's shadow on
        // the floor's +X side, from x = 0.5 to 1.5 (|z| < 0.5) - in the mask only.
        static BakeInput SunAndBox(int shadowmaskScale)
        {
            var input = new BakeInput { Settings = Quick(16, 0) };
            input.Settings.ShadowmaskScale = shadowmaskScale;
            input.Meshes.Add(new BakeMesh { SceneId = 1, Source = new[] { Plane(3f) }, Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.8f) } } });
            Assert.True(PrimitiveMeshSpec.TryParse("prim:box;w=1;h=1;d=1;tiles=1", out var spec));
            var (p, ix) = PrimitiveMeshGeometry.GetMeshTriangles(spec);
            var bv = new ObjVertex[p.Length];
            for (int i = 0; i < p.Length; i++) bv[i] = new ObjVertex { Position = p[i], Normal = Vector3.Normalize(p[i]) };
            input.Meshes.Add(new BakeMesh { SceneId = 2, World = Matrix4x4.CreateTranslation(0, 0.5f, 0), Source = new[] { new ObjSubMesh { MaterialName = "", Vertices = bv, Indices = ix } },
                                            Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.8f) } } });
            input.Lights.Add(new BakeLight { SceneId = 9, Type = LightType.Directional, Mode = LightMode.Mixed, Direction = Vector3.Normalize(new Vector3(1, -1, 0)), Color = new Vector3(1f), Shadows = true, IndirectMultiplier = 1f });
            return input;
        }

        [Fact]
        public void A_mixed_suns_shadowmask_is_dark_in_the_boxs_shadow_and_bright_in_the_open_and_bakes_no_direct_light()
        {
            var input = SunAndBox(1);   // the mask's texels the light's (Covered below is the light page's)
            var none = LightBaker.Bake(input);
            Assert.Equal(-1, none.ShadowmaskLight);                      // not asked for: no mask
            Assert.Empty(none.Pages[0].Mask);

            input.ShadowmaskLight = 9;
            var r = LightBaker.Bake(input);
            Assert.Equal(9, r.ShadowmaskLight);
            float shadowed = MaskAt(r, 1, new Vector3(1.0f, 0, 0)), open = MaskAt(r, 1, new Vector3(-1.0f, 0, 0));
            Assert.True(shadowed < 0.05f && open > 0.95f, $"the mask: +X side {shadowed}, -X side {open}");
            Assert.True(At(r, 1, new Vector3(-1.0f, 0, 0)).X < 0.001f, "the Mixed sun's direct light is drawn, not baked");
            // the padding carries the mask too (no dark seam where bilinear filtering reads it)
            var page = r.Pages[0];
            int padded = 0;
            for (int i = 0; i < page.Mask.Length; i++) if (!page.Covered[i] && page.Mask[i] > 0.5f) padded++;
            Assert.True(padded > 0, "the open floor's mask spreads into its padding");

            // a Realtime or Baked light is no shadowmask's
            input.Lights[0] = input.Lights[0] with { Mode = LightMode.Baked };
            Assert.Equal(-1, LightBaker.Bake(input).ShadowmaskLight);
        }

        [Fact]
        public void A_shadowmask_scale_of_two_bakes_the_mask_at_twice_the_lights_texels_with_a_sharper_edge()
        {
            float EdgeWidth(int scale, out BakeResult r)
            {
                var input = SunAndBox(scale);
                input.ShadowmaskLight = 9;
                r = LightBaker.Bake(input);
                // across the shadow's side edge (z = 0.5, at x = 1), filtered as the GPU reads it: the run of partly lit
                float width = 0f;
                for (float z = 0.1f; z <= 0.9f; z += 0.002f)
                {
                    float m = MaskAt(r, 1, new Vector3(1.0f, 0, z), bilinear: true);
                    if (m > 0.1f && m < 0.9f) width += 0.002f;
                }
                return width;
            }
            float one = EdgeWidth(1, out var r1), two = EdgeWidth(2, out var r2);
            Assert.Equal(r1.Pages[0].Size, r1.Pages[0].MaskSize);
            Assert.Equal(2 * r2.Pages[0].Size, r2.Pages[0].MaskSize);
            Assert.Equal(r2.Pages[0].MaskSize * r2.Pages[0].MaskSize, r2.Pages[0].Mask.Length);
            Assert.Equal(r1.Pages[0].Light, r2.Pages[0].Light);   // the light pages are not changed by it
            Assert.True(one > 0f && two < 0.7f * one, $"the edge's width: x1 {one:F3}, x2 {two:F3} (world units)");
            Assert.True(MaskAt(r2, 1, new Vector3(1.0f, 0, 0)) < 0.05f && MaskAt(r2, 1, new Vector3(-1.0f, 0, 0)) > 0.95f, "x2: dark in the shadow, lit in the open");
        }

        [Fact]
        public void Instances_of_a_mesh_at_one_scale_share_a_lightmap_layout_each_with_its_own_place()
        {
            var input = new BakeInput { Settings = Quick(4, 0) };
            Assert.True(PrimitiveMeshSpec.TryParse("prim:box;w=1;h=1;d=1;tiles=1", out var spec));
            var (p, ix) = PrimitiveMeshGeometry.GetMeshTriangles(spec);
            var bv = new ObjVertex[p.Length];
            for (int i = 0; i < p.Length; i++) bv[i] = new ObjVertex { Position = p[i], Normal = Vector3.Normalize(p[i]) };
            var box = new[] { new ObjSubMesh { MaterialName = "", Vertices = bv, Indices = ix } };   // one source, as the registry's copy
            var mat = new[] { new BakeMaterial { Albedo = new Vector3(0.8f) } };
            input.Meshes.Add(new BakeMesh { SceneId = 1, World = Matrix4x4.CreateTranslation(-3, 0.5f, 0), Source = box, Materials = mat });
            input.Meshes.Add(new BakeMesh { SceneId = 2, World = Matrix4x4.CreateRotationY(0.7f) * Matrix4x4.CreateTranslation(3, 0.5f, 2), Source = box, Materials = mat });
            input.Meshes.Add(new BakeMesh { SceneId = 3, World = Matrix4x4.CreateScale(2f) * Matrix4x4.CreateTranslation(0, 1f, -4), Source = box, Materials = mat });
            var r = LightBaker.Bake(input);
            var e1 = r.Entities.Find(e => e.SceneId == 1)!; var e2 = r.Entities.Find(e => e.SceneId == 2)!; var e3 = r.Entities.Find(e => e.SceneId == 3)!;
            Assert.Equal(e1.Group, e2.Group);                 // moved and turned: the same layout (drawn instanced)
            Assert.NotEqual(e1.Group, e3.Group);              // twice the size: a layout of its own (twice the texels)
            Assert.Equal(2, r.Groups.Count);
            Assert.NotEqual(e1.Offset, e2.Offset);            // each its own place
            // each entity's page UV = the layout's UV (texels) + its place, over the page's size
            foreach (var e in new[] { e1, e2, e3 })
            {
                float size = r.Pages[e.Page].Size;
                var layout = r.Groups[e.Group][0].Vertices; var own = e.Mesh[0].Vertices;
                for (int k = 0; k < own.Length; k++)
                {
                    var want = (layout[k].Uv1 + e.Offset) / size;
                    Assert.True(Vector2.Distance(own[k].Uv1, want) < 1e-5f, $"entity {e.SceneId} vertex {k}: {own[k].Uv1} vs {want}");
                }
            }
        }

        [Fact]
        public void A_shadow_falls_where_the_sun_sends_it_in_the_world()
        {
            var input = new BakeInput { Settings = Quick(16, 0) };
            input.Meshes.Add(new BakeMesh { SceneId = 1, Source = new[] { Plane(3f) }, Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.8f) } } });
            Assert.True(PrimitiveMeshSpec.TryParse("prim:box;w=1;h=1;d=1;tiles=1", out var spec));
            var (p, ix) = PrimitiveMeshGeometry.GetMeshTriangles(spec);
            var bv = new ObjVertex[p.Length];
            for (int i = 0; i < p.Length; i++) bv[i] = new ObjVertex { Position = p[i], Normal = Vector3.Normalize(p[i]) };
            input.Meshes.Add(new BakeMesh { SceneId = 2, World = Matrix4x4.CreateTranslation(0, 0.5f, 0), Source = new[] { new ObjSubMesh { MaterialName = "", Vertices = bv, Indices = ix } },
                                            Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.8f) } } });
            // the sun travels toward +X and down: the shadow lies on the box's +X side
            input.Lights.Add(new BakeLight { Type = LightType.Directional, Mode = LightMode.Baked, Direction = Vector3.Normalize(new Vector3(1, -1, 0)), Color = new Vector3(1f), Shadows = true, IndirectMultiplier = 1f });
            var r = LightBaker.Bake(input);
            float plusX = At(r, 1, new Vector3(1.0f, 0, 0)).X, minusX = At(r, 1, new Vector3(-1.0f, 0, 0)).X;
            Assert.True(plusX < 0.05f && minusX > 0.6f, $"+X side {plusX}, -X side {minusX}");
        }

        [Fact]
        public void The_padding_beside_a_chart_carries_its_light_no_dark_seams()
        {
            var input = new BakeInput { Settings = Quick(16, 0), Sky = _ => new Vector3(0.5f) };
            input.Meshes.Add(new BakeMesh { SceneId = 1, Source = new[] { Plane(2f) }, Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.8f) } } });
            var page = LightBaker.Bake(input).Pages[0];
            int n = page.Size, edge = 0;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    if (page.Covered[i]) continue;
                    bool besideChart = false;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < n && ny < n && page.Covered[ny * n + nx]) besideChart = true;
                        }
                    if (!besideChart) continue;
                    edge++;
                    Assert.True(page.Light[i].X > 0.45f, $"padding texel {x},{y} beside the chart: {page.Light[i]}");
                }
            Assert.True(edge > 8, $"padding texels beside the chart: {edge}");
        }

        [Fact]
        public void The_same_scene_bakes_to_the_same_light_twice()
        {
            BakeResult Once()
            {
                var input = new BakeInput { Settings = Quick(32, 2), Sky = d => new Vector3(0.2f, 0.3f, 0.5f) * MathF.Max(0f, d.Y) };
                input.Meshes.Add(new BakeMesh { SceneId = 1, Source = new[] { Plane(2f) }, Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.8f) } } });
                input.Meshes.Add(new BakeMesh { SceneId = 2, World = Matrix4x4.CreateTranslation(0, 0.5f, 0), Source = new[] { InsideBox() }, Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.6f, 0.2f, 0.2f) } } });
                return LightBaker.Bake(input);
            }
            var a = Once(); var b = Once();
            Assert.Equal(a.Pages.Count, b.Pages.Count);
            for (int p = 0; p < a.Pages.Count; p++) Assert.Equal(a.Pages[p].Light, b.Pages[p].Light);
        }
    }
}
