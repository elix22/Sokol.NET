using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The camera's Anti-aliasing (G8.9c, examples/GameEditor/docs/POST_EFFECTS_DESIGN.md): None by default, the scene
    /// block's "AA": "FXAA" written only when it is on (a scene of before is unchanged) and read back.</summary>
    [Collection("Sequential")]
    public sealed class FxaaTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        public FxaaTests()
        {
            World.Clear();
            SceneManager.NewScene("Fxaa");
        }

        public void Dispose() => World.Clear();

        static Entity Camera(string name, AntiAliasing aa)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity(name);
            World.AddComponent(e, new CameraComponent { Fov = 60f, NearZ = 0.1f, FarZ = 100f, IsMain = aa == AntiAliasing.Fxaa, Enabled = true, AntiAliasing = aa });
            return e;
        }

        [Fact]
        public void The_cameras_FXAA_is_written_only_when_on_and_read_back()
        {
            Assert.Equal(AntiAliasing.None, default(CameraComponent).AntiAliasing);
            Camera("Plain", AntiAliasing.None);
            Camera("Smooth", AntiAliasing.Fxaa);
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            Assert.Equal(1, json.Split("\"AA\":\"FXAA\"").Length - 1);
            Assert.DoesNotContain("\"AA\":\"None\"", json);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            foreach (Entity e in World.Entities)
            {
                if (!World.TryGetComponent<CameraComponent>(e, out var cam)) continue;
                string name = World.GetComponent<NameTag>(e).Name;
                Assert.Equal(name == "Smooth" ? AntiAliasing.Fxaa : AntiAliasing.None, cam.AntiAliasing);
            }
        }
    }
}
