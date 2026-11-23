using System;
using System.Numerics;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.GI;
using GameEditor.Framework.Renderer.Server.Resources;
using AmbientSH = GameEditor.Framework.Renderer.Server.Lighting.AmbientSH;
using Xunit;

namespace Scene.Tests
{
    /// <summary>Textured surfaces in the light bake (examples/GameEditor/docs/BAKED_GI_DESIGN.md §3.8): a map reduced to at
    /// most 256 texels a side (each the mean of what it covers, linear), sampled bilinearly with the UVs repeating; a bounce
    /// takes the colour of the texel it lands on, not the material's mean.</summary>
    public sealed class BakeTextureTests
    {
        static unsafe BakeTexture Make(int w, int h, Func<int, int, (byte, byte, byte)> px, bool srgb = false)
        {
            var data = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var (r, g, b) = px(x, y);
                    int i = (y * w + x) * 4;
                    data[i] = r; data[i + 1] = g; data[i + 2] = b; data[i + 3] = 255;
                }
            fixed (byte* p = data) return BakeTexture.FromRgba8(p, w, h, srgb);
        }

        [Fact]
        public void A_map_is_sampled_bilinearly_between_texel_centres_with_the_UVs_repeating()
        {
            var t = Make(2, 1, (x, _) => x == 0 ? ((byte)255, (byte)0, (byte)0) : ((byte)0, (byte)255, (byte)0));
            Assert.Equal(new Vector3(1, 0, 0), t.Sample(new Vector2(0.25f, 0.5f)));          // texel 0's centre
            Assert.Equal(new Vector3(0, 1, 0), t.Sample(new Vector2(0.75f, 0.5f)));          // texel 1's centre
            var mid = t.Sample(new Vector2(0.5f, 0.5f));
            Assert.Equal(0.5f, mid.X, 4); Assert.Equal(0.5f, mid.Y, 4);
            var edge = t.Sample(new Vector2(0f, 0.5f));                                       // between texel 1 (wrapped) and texel 0
            Assert.Equal(0.5f, edge.X, 4); Assert.Equal(0.5f, edge.Y, 4);
            var a = t.Sample(new Vector2(0.3f, 0.2f)); var b = t.Sample(new Vector2(3.3f, -1.8f));    // repeat
            Assert.True(Vector3.Distance(a, b) < 1e-5f, $"{a} vs {b}");
        }

        [Fact]
        public void A_large_map_is_reduced_to_256_each_texel_the_linear_mean_of_its_block()
        {
            var t = Make(1024, 512, (x, y) => (x % 2 == 0) ? ((byte)255, (byte)255, (byte)255) : ((byte)0, (byte)0, (byte)0), srgb: true);
            Assert.Equal(256, t.Width); Assert.Equal(256, t.Height);
            Assert.All(t.Linear, c => Assert.Equal(0.5f, c.X, 3));                         // half white, half black: 0.5 linear (not sRGB's 0.5)
        }

        static ObjSubMesh Floor(float half)
        {
            // TEXCOORD_0 u runs with x (0 at -half, 1 at +half), v with z
            var v = new[]
            {
                new ObjVertex { Position = new(-half, 0, -half), Normal = Vector3.UnitY, Uv = new(0, 0) },
                new ObjVertex { Position = new( half, 0, -half), Normal = Vector3.UnitY, Uv = new(1, 0) },
                new ObjVertex { Position = new( half, 0,  half), Normal = Vector3.UnitY, Uv = new(1, 1) },
                new ObjVertex { Position = new(-half, 0,  half), Normal = Vector3.UnitY, Uv = new(0, 1) },
            };
            return new ObjSubMesh { MaterialName = "", Vertices = v, Indices = new uint[] { 0, 2, 1, 0, 3, 2 } };
        }

        [Fact]
        public void A_bounce_takes_the_colour_of_the_texel_it_lands_on()
        {
            // a floor half red (x < 0), half green, under a Baked sun from straight above, no sky: a probe just above each half
            // sees that half's colour from below - one mean colour a material would give both the same yellow
            var split = Make(64, 4, (x, _) => x < 32 ? ((byte)230, (byte)20, (byte)20) : ((byte)20, (byte)230, (byte)20), srgb: true);
            var input = new BakeInput { Settings = new LightingSettings { Resolution = 1f, IndirectSamples = 256, MaxBounces = 1, MaxLightmapSize = 64, Filtering = false } };
            input.Meshes.Add(new BakeMesh { SceneId = 1, Source = new[] { Floor(20f) }, Materials = new[] { new BakeMaterial { Albedo = Vector3.One, AlbedoMap = split } } });
            input.Lights.Add(new BakeLight { Type = LightType.Directional, Mode = LightMode.Baked, Direction = -Vector3.UnitY, Color = new Vector3(3f), Shadows = true, IndirectMultiplier = 1f });
            input.Probes.Add(new Vector3(-10f, 0.5f, 0f));
            input.Probes.Add(new Vector3(10f, 0.5f, 0f));
            var r = LightBaker.Bake(input);
            var red = AmbientSH.FromRadianceSH(r.Probes[0].Radiance).Evaluate(-Vector3.UnitY);
            var green = AmbientSH.FromRadianceSH(r.Probes[1].Radiance).Evaluate(-Vector3.UnitY);
            Assert.True(red.X > 4f * red.Y, $"over the red half: {red}");
            Assert.True(green.Y > 4f * green.X, $"over the green half: {green}");
        }

        [Fact]
        public void Emission_is_the_materials_times_its_map_at_the_point()
        {
            // a dark floor that glows white on its left half only: a probe over the left sees light from below, over the right none
            var half = Make(64, 4, (x, _) => x < 32 ? ((byte)255, (byte)255, (byte)255) : ((byte)0, (byte)0, (byte)0));
            var input = new BakeInput { Settings = new LightingSettings { Resolution = 1f, IndirectSamples = 256, MaxBounces = 0, MaxLightmapSize = 64, Filtering = false } };
            input.Meshes.Add(new BakeMesh { SceneId = 1, Source = new[] { Floor(20f) }, Materials = new[] { new BakeMaterial { Albedo = new Vector3(0.1f), Emission = new Vector3(2f), EmissionMap = half } } });
            input.Probes.Add(new Vector3(-10f, 0.5f, 0f));
            input.Probes.Add(new Vector3(10f, 0.5f, 0f));
            var r = LightBaker.Bake(input);
            float lit = AmbientSH.FromRadianceSH(r.Probes[0].Radiance).Evaluate(-Vector3.UnitY).X;
            float dark = AmbientSH.FromRadianceSH(r.Probes[1].Radiance).Evaluate(-Vector3.UnitY).X;
            Assert.InRange(lit, 1.6f, 2.4f);
            Assert.InRange(dark, -0.2f, 0.2f);
        }
    }
}
