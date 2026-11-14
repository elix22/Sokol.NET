using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.GI;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>Baked lighting's authoring (M8a-1, examples/GameEditor/docs/BAKED_GI_DESIGN.md §3.1): a renderer's Contribute
    /// GI and Scale In Lightmap, a light's Mode and Indirect Multiplier, the scene's lighting settings - the defaults when
    /// absent (a scene of before reads the same), written only when changed, read back.</summary>
    [Collection("Sequential")]
    public sealed class LightingAuthoringTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        public LightingAuthoringTests()
        {
            World.Clear();
            SceneManager.NewScene("Lighting");
        }

        public void Dispose() => World.Clear();

        [Fact]
        public void Defaults_of_a_component_made_without_the_fields()
        {
            Assert.Equal(1f, default(MeshRenderer).ScaleInLightmap);
            Assert.False(default(MeshRenderer).ContributeGI);
            Assert.Equal(1f, default(LightComponent).IndirectMultiplier);
            Assert.Equal(LightMode.Realtime, default(LightComponent).Mode);
        }

        [Fact]
        public void Renderer_and_light_fields_are_written_only_when_set_and_read_back()
        {
            var scene = SceneManager.ActiveScene!;
            Entity plain = scene.CreateEntity("Plain");
            World.AddComponent(plain, new MeshRenderer { MeshPath = "builtin:cube", Visible = true });
            Entity baked = scene.CreateEntity("Baked");
            World.AddComponent(baked, new MeshRenderer { MeshPath = "builtin:cube", Visible = true, ContributeGI = true, ScaleInLightmap = 2.5f });
            Entity sun = scene.CreateEntity("Sun");
            World.AddComponent(sun, new LightComponent { Type = LightType.Directional, Intensity = 1f, Enabled = true });
            Entity lamp = scene.CreateEntity("Lamp");
            World.AddComponent(lamp, new LightComponent { Type = LightType.Point, Intensity = 2f, Range = 5f, Enabled = true, Mode = LightMode.Baked, IndirectMultiplier = 0f });

            string json = SceneSerializer.Serialize(scene);
            Assert.Equal(1, json.Split("\"GI\":true").Length - 1);
            Assert.Equal(1, json.Split("\"LmScale\":").Length - 1);
            Assert.Equal(1, json.Split("\"Mode\":\"Baked\"").Length - 1);
            Assert.Equal(1, json.Split("\"IndMul\":0").Length - 1);
            Assert.DoesNotContain("Realtime", json);

            World.Clear();
            SceneSerializer.Deserialize(json, scene);
            foreach (Entity e in World.Entities)
            {
                string name = World.GetComponent<NameTag>(e).Name;
                if (World.TryGetComponent<MeshRenderer>(e, out var mr))
                {
                    Assert.Equal(name == "Baked", mr.ContributeGI);
                    Assert.Equal(name == "Baked" ? 2.5f : 1f, mr.ScaleInLightmap);
                }
                if (World.TryGetComponent<LightComponent>(e, out var lt))
                {
                    Assert.Equal(name == "Lamp" ? LightMode.Baked : LightMode.Realtime, lt.Mode);
                    Assert.Equal(name == "Lamp" ? 0f : 1f, lt.IndirectMultiplier);
                }
            }
        }

        [Fact]
        public void Lighting_settings_are_the_defaults_when_absent_and_round_trip_through_the_scene()
        {
            var scene = SceneManager.ActiveScene!;
            var s = LightingSettings.Of(scene);
            Assert.True(s.BakedGI);
            Assert.Equal(2, s.MaxBounces);
            Assert.Equal(40f, s.Resolution);
            Assert.Equal(1024, s.MaxLightmapSize);
            Assert.Equal(DirectionalMode.Directional, s.DirectionalMode);
            Assert.Equal("{}", s.ToJson());

            s.ApplyTo(scene);
            Assert.DoesNotContain("\"lighting\"", SceneSerializer.Serialize(scene));   // all defaults: no block

            s.MaxBounces = 4;
            s.Resolution = 10f;
            s.DirectionalMode = DirectionalMode.NonDirectional;
            s.ApplyTo(scene);
            string json = SceneSerializer.Serialize(scene);
            Assert.Contains("\"lighting\":{\"maxBounces\":4,\"resolution\":10,\"directionalMode\":\"NonDirectional\"}", json);

            World.Clear();
            scene.Blocks.Clear();
            SceneSerializer.Deserialize(json, scene);
            var back = LightingSettings.Of(scene);
            Assert.Equal(4, back.MaxBounces);
            Assert.Equal(10f, back.Resolution);
            Assert.Equal(DirectionalMode.NonDirectional, back.DirectionalMode);
            Assert.Equal(512, back.IndirectSamples);
        }

        [Fact]
        public void Out_of_range_settings_are_clamped()
        {
            var s = LightingSettings.Parse("{\"maxBounces\":99,\"resolution\":-3,\"padding\":\"x\"}");
            Assert.Equal(16, s.MaxBounces);
            Assert.Equal(0.01f, s.Resolution);
            Assert.Equal(2, s.Padding);
        }
    }
}
