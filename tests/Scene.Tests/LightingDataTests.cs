using System;
using System.IO;
using System.Numerics;
using GameEditor.Framework.Renderer.Server.GI;
using GameEditor.Framework.Renderer.Server.Resources;
using Xunit;

namespace Scene.Tests
{
    /// <summary>What a bake writes (M8a-3, examples/GameEditor/docs/BAKED_GI_DESIGN.md §3.4): RGBM within its precision,
    /// LightingData.json and LightmapMeshes.bin read back as written.</summary>
    public sealed class LightingDataTests
    {
        [Fact]
        public void Rgbm_keeps_light_from_dark_to_eight_within_its_precision()
        {
            var light = new[] { new Vector3(0f), new Vector3(0.02f, 0.01f, 0.005f), new Vector3(0.5f, 0.25f, 0.1f), new Vector3(1f), new Vector3(3f, 1.5f, 0.2f), new Vector3(7.9f, 4f, 1f), new Vector3(20f) };
            var b = LightingData.EncodeRgbm(light);
            for (int i = 0; i < light.Length; i++)
            {
                var back = LightingData.DecodeRgbm(b[i * 4], b[i * 4 + 1], b[i * 4 + 2], b[i * 4 + 3]);
                var want = Vector3.Min(light[i], new Vector3(LightingData.RgbmRange));
                float m = MathF.Max(want.X, MathF.Max(want.Y, want.Z));
                Assert.True((back - want).Length() <= MathF.Max(0.002f, m * 0.01f), $"{light[i]} -> {back}");
            }
        }

        [Fact]
        public void A_bake_written_reads_back_the_same()
        {
            string dir = Path.Combine(Path.GetTempPath(), "lightingdata_" + Guid.NewGuid().ToString("N"));
            try
            {
                var page = new LightmapPage { Size = 4, Light = new Vector3[16], Direction = new Vector4[16], Covered = new bool[16] };
                for (int i = 0; i < 16; i++) { page.Light[i] = new Vector3(i * 0.1f); page.Direction[i] = new Vector4(0.5f, 1f, 0.5f, 1f); }
                var mesh = new[] { new ObjSubMesh { MaterialName = "Wall", Vertices = new[] { new ObjVertex { Position = new Vector3(1, 2, 3), Uv1 = new Vector2(0.25f, 0.75f) }, new ObjVertex(), new ObjVertex() }, Indices = new uint[] { 0, 1, 2 } } };
                var bake = new BakeResult();
                bake.Pages.Add(page);
                bake.Entities.Add(new BakedEntity { SceneId = 42, Page = 0, Mesh = mesh });
                LightingData.Write(dir, bake, new[] { 7 }, directional: true);

                Assert.True(File.Exists(Path.Combine(dir, "Lightmap-0_light.png")) && File.Exists(Path.Combine(dir, "Lightmap-0_dir.png")));
                // RGBA PNGs: the alpha (RGBM's multiplier, the direction's rebalancing) kept - colour type 6 in IHDR
                Assert.Equal(6, File.ReadAllBytes(Path.Combine(dir, "Lightmap-0_light.png"))[25]);
                Assert.Equal(6, File.ReadAllBytes(Path.Combine(dir, "Lightmap-0_dir.png"))[25]);
                var data = LightingData.Parse(File.ReadAllText(Path.Combine(dir, LightingData.JsonFile)));
                Assert.NotNull(data);
                Assert.True(data!.Directional);
                Assert.Equal(4, data.Pages[0].Size);
                Assert.Equal("Lightmap-0_dir.png", data.Pages[0].Direction);
                Assert.Equal(0, data.EntityPage[42]);
                Assert.Equal(new[] { 7 }, data.BakedLights);
                Assert.True(data.ReadMeshes(File.ReadAllBytes(Path.Combine(dir, LightingData.MeshFile))));
                var back = data.Meshes[42];
                Assert.Equal("Wall", back[0].MaterialName);
                Assert.Equal(new Vector3(1, 2, 3), back[0].Vertices[0].Position);
                Assert.Equal(new Vector2(0.25f, 0.75f), back[0].Vertices[0].Uv1);
                Assert.Equal(new uint[] { 0, 1, 2 }, back[0].Indices);

                // A bake of before the shared layouts: each entity its own mesh, its UV the page's (place 1, 0, 0).
                Assert.False(data.EntityMesh.ContainsKey(42));
                // Shared layouts: the layouts as the meshes, each entity's layout and place on its page.
                var groups = new BakeResult();
                groups.Pages.Add(new LightmapPage { Size = 64, Light = new Vector3[64 * 64], Direction = new Vector4[64 * 64], Covered = new bool[64 * 64] });
                groups.Groups.Add(mesh);
                groups.Entities.Add(new BakedEntity { SceneId = 5, Page = 0, Group = 0, Offset = new Vector2(16, 32) });
                groups.Entities.Add(new BakedEntity { SceneId = 6, Page = 0, Group = 0, Offset = new Vector2(48, 0) });
                string gdir = dir + "_groups";
                try
                {
                    LightingData.Write(gdir, groups, Array.Empty<int>(), directional: false);
                    var gd = LightingData.Parse(File.ReadAllText(Path.Combine(gdir, LightingData.JsonFile)))!;
                    Assert.True(gd.ReadMeshes(File.ReadAllBytes(Path.Combine(gdir, LightingData.MeshFile))));
                    Assert.Single(gd.Meshes);                               // one layout for both
                    Assert.Equal((0, new Vector3(1f / 64, 0.25f, 0.5f)), gd.EntityMesh[5]);
                    Assert.Equal((0, new Vector3(1f / 64, 0.75f, 0f)), gd.EntityMesh[6]);
                }
                finally { if (Directory.Exists(gdir)) Directory.Delete(gdir, true); }

                // A shadowmask: a page of its own (grey, opaque), named in the json with its light.
                Assert.Equal(-1, data.ShadowmaskLight);
                Assert.Equal("", data.Pages[0].Mask);
                page.Mask = new float[16];
                for (int i = 0; i < 16; i++) page.Mask[i] = i < 8 ? 0f : 1f;
                bake.ShadowmaskLight = 5;
                LightingData.Write(dir, bake, new[] { 7 }, directional: true);
                var masked = LightingData.Parse(File.ReadAllText(Path.Combine(dir, LightingData.JsonFile)))!;
                Assert.Equal(5, masked.ShadowmaskLight);
                Assert.Equal("Lightmap-0_shadowmask.png", masked.Pages[0].Mask);
                Assert.True(File.Exists(Path.Combine(dir, "Lightmap-0_shadowmask.png")));
                var bytes = LightingData.EncodeMask(page.Mask);
                Assert.Equal(new byte[] { 0, 0, 0, 255 }, bytes[..4]);
                Assert.Equal(new byte[] { 255, 255, 255, 255 }, bytes[^4..]);

                // A second bake replaces the first's files (none of an older page left behind).
                bake.Pages.Clear(); bake.Pages.Add(new LightmapPage { Size = 2, Light = new Vector3[4], Direction = new Vector4[4], Covered = new bool[4] }); bake.ShadowmaskLight = -1;
                LightingData.Write(dir, bake, Array.Empty<int>(), directional: false);
                Assert.False(File.Exists(Path.Combine(dir, "Lightmap-0_dir.png")));
                Assert.False(File.Exists(Path.Combine(dir, "Lightmap-0_shadowmask.png")));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
    }
}
