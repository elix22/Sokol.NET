using System;
using System.Numerics;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer;
using GameEditor.Framework.Renderer.Server.GI;
using AmbientSH = GameEditor.Framework.Renderer.Server.Lighting.AmbientSH;
using GameEditor.Framework.Renderer.Server.Resources;
using Xunit;

namespace Scene.Tests
{
    /// <summary>Light probes (M8b, examples/GameEditor/docs/BAKED_GI_DESIGN.md §4): a probe under an even sky gives the sky,
    /// one in the furnace E/(1-ρ), a Baked sun the shader's light facing it; the blend exact at a probe, continuous between.</summary>
    public sealed class LightProbeTests
    {
        static ObjSubMesh Plane(float half, float y)
        {
            var v = new[]
            {
                new ObjVertex { Position = new(-half, y, -half), Normal = Vector3.UnitY },
                new ObjVertex { Position = new( half, y, -half), Normal = Vector3.UnitY },
                new ObjVertex { Position = new( half, y,  half), Normal = Vector3.UnitY },
                new ObjVertex { Position = new(-half, y,  half), Normal = Vector3.UnitY },
            };
            return new ObjSubMesh { MaterialName = "", Vertices = v, Indices = new uint[] { 0, 2, 1, 0, 3, 2 } };
        }

        static LightingSettings Quick(int samples, int bounces) => new() { Resolution = 2f, IndirectSamples = samples, MaxBounces = bounces, MaxLightmapSize = 128, Filtering = false };

        static AmbientSH Of(BakeResult r, int i) => AmbientSH.FromRadianceSH(r.Probes[i].Radiance);

        [Fact]
        public void A_probe_under_an_even_sky_gives_the_sky_in_every_direction()
        {
            // a small plane far below: the sky all around the probe (the plane hides almost none of it)
            var input = new BakeInput { Settings = Quick(1024, 0), Sky = _ => new Vector3(0.6f, 0.4f, 0.2f) };
            input.Meshes.Add(new BakeMesh { SceneId = 1, Source = new[] { Plane(0.1f, -100f) }, Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.5f) } } });
            input.Probes.Add(Vector3.Zero);
            var sh = Of(LightBaker.Bake(input), 0);
            foreach (var n in new[] { Vector3.UnitY, -Vector3.UnitY, Vector3.UnitX, Vector3.Normalize(new Vector3(1, 1, -1)) })
            {
                var e = sh.Evaluate(n);
                Assert.InRange(e.X, 0.6f * 0.97f, 0.6f * 1.03f);
                Assert.InRange(e.Z, 0.2f * 0.97f, 0.2f * 1.03f);
            }
        }

        [Fact]
        public void A_probe_in_the_furnace_gives_its_light_E_over_one_minus_rho()
        {
            Assert.True(PrimitiveMeshSpec.TryParse("prim:box;w=1;h=1;d=1;tiles=1", out var spec));
            var (p, ix) = PrimitiveMeshGeometry.GetMeshTriangles(spec);
            var v = new ObjVertex[p.Length];
            for (int i = 0; i < p.Length; i++) v[i] = new ObjVertex { Position = p[i], Normal = -Vector3.Normalize(p[i]) };
            var input = new BakeInput { Settings = Quick(512, 16) };
            input.Meshes.Add(new BakeMesh { SceneId = 1, World = Matrix4x4.CreateScale(4f), Source = new[] { new ObjSubMesh { MaterialName = "", Vertices = v, Indices = ix } },
                                            Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.5f), Emission = new Vector3(1f) } } });
            input.Probes.Add(new Vector3(0.3f, -0.2f, 0.5f));
            var e = Of(LightBaker.Bake(input), 0).Evaluate(Vector3.UnitX);
            Assert.InRange(e.Y, 2f * 0.95f, 2f * 1.05f);
        }

        [Fact]
        public void A_baked_sun_gives_a_probe_the_shaders_light_facing_it_and_none_from_behind()
        {
            var input = new BakeInput { Settings = Quick(64, 0) };
            input.Meshes.Add(new BakeMesh { SceneId = 1, Source = new[] { Plane(0.1f, -100f) }, Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.5f) } } });
            input.Lights.Add(new BakeLight { Type = LightType.Directional, Mode = LightMode.Baked, Direction = new Vector3(0, -1, 0), Color = new Vector3(2f), Shadows = true, IndirectMultiplier = 1f });
            input.Probes.Add(Vector3.Zero);
            var sh = Of(LightBaker.Bake(input), 0);
            Assert.InRange(sh.Evaluate(Vector3.UnitY).X, 2f * 0.88f, 2f * 1.12f);    // NdotL 1 × 2 (L2's own error ~10 %)
            Assert.InRange(sh.Evaluate(-Vector3.UnitY).X, -0.25f, 0.25f);           // facing away: (almost) none

            input.Lights[0] = input.Lights[0] with { Mode = LightMode.Mixed };
            Assert.InRange(Of(LightBaker.Bake(input), 0).Evaluate(Vector3.UnitY).X, -0.01f, 0.01f);   // a Mixed light's direct light is drawn
        }

        [Fact]
        public void The_blend_is_a_probes_own_light_at_it_and_changes_smoothly_between_two()
        {
            var pos = new[] { new Vector3(0, 0, 0), new Vector3(4, 0, 0) };
            var packed = new Vector4[18];
            // two flat lights: c = 1 and c = 3, no direction
            packed[0] = new Vector4(1, 0, 0, 0); packed[3] = packed[0]; packed[6] = packed[0];
            packed[9] = new Vector4(3, 0, 0, 0); packed[12] = packed[9]; packed[15] = packed[9];
            Span<Vector4> d = stackalloc Vector4[9];
            Lightmaps.Blend(pos, packed, pos[0], d);
            Assert.InRange(d[0].X, 1f, 1.001f);
            Assert.Equal(1f, d[2].W);
            Lightmaps.Blend(pos, packed, pos[1], d);
            Assert.InRange(d[0].X, 2.999f, 3f);
            float last = 1f, biggestStep = 0f;
            for (int i = 1; i <= 160; i++)
            {
                Lightmaps.Blend(pos, packed, new Vector3(i * 0.025f, 0.3f, 0), d);
                Assert.True(d[0].X >= last - 1e-5f, $"never darker toward the brighter probe: {d[0].X} after {last} at {i}");
                biggestStep = MathF.Max(biggestStep, d[0].X - last);
                last = d[0].X;
            }
            Assert.True(biggestStep < 0.15f, $"no jump: the biggest step over 2.5 cm {biggestStep}");
        }
    }
}
