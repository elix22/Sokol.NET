using System.Text.Json;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>A game's component (as a project's DLL registers one), for the kept-blocks tests.</summary>
    public struct KeptCar { public float Speed; }

    /// <summary>
    /// A game's components are never lost to a save: the editor unloads a project's DLL before it loads the next build, and
    /// a build can fail - the scene's components of the DLL that went must be saved as they were (a scene saved after that
    /// had lost every traffic car, walker and way graph of its city plugin); and a block kept while no DLL knew its type is
    /// saved even when the type is registered later without the scene being read again.
    /// </summary>
    public sealed class KeptBlocksTests
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const string Scene1 = "{\"name\":\"K\",\"entities\":[{\"id\":3,\"c\":{\"NameTag\":{\"Name\":\"Car\"},\"ActiveFlag\":{\"Active\":true},"
                            + "\"KeptCar\":{\"Speed\":9}}}]}";

        public KeptBlocksTests() { World.Clear(); SceneSerializer.UnregisterComponentExtension("KeptCar"); }

        static void Register() =>
            SceneSerializer.RegisterComponentExtension("KeptCar",
                (w, e) => w.TryGetComponent<KeptCar>(e, out var c)
                    ? "{\"Speed\":" + c.Speed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}" : null,
                (w, e, el, _) => w.AddComponent(e, new KeptCar { Speed = el.GetProperty("Speed").GetSingle() }),
                (w, e) => w.RemoveComponent<KeptCar>(e));

        static string? Block(string sceneJson)
        {
            foreach (var e in JsonDocument.Parse(sceneJson).RootElement.GetProperty("entities").EnumerateArray())
                if (e.GetProperty("c").TryGetProperty("KeptCar", out var b)) return b.GetRawText();
            return null;
        }

        [Fact]
        public void LiveComponent_OfATypeUnregistered_IsSavedAsItWas()
        {
            Register();
            var scene = new GameEditor.Framework.Scene.Scene("k");
            SceneSerializer.Deserialize(Scene1, scene);
            Entity car = default;
            foreach (Entity e in World.Entities) if (World.HasComponent<KeptCar>(e)) car = e;
            Assert.False(car.IsNull);
            World.GetComponent<KeptCar>(car).Speed = 12f;              // edited in the editor before the DLL went

            SceneSerializer.UnregisterComponentExtension("KeptCar");    // the DLL unloaded; its next build failed
            Assert.Equal("{\"Speed\":12}", Block(SceneSerializer.Serialize(scene)));

            Register();                                                  // a later build: read again, the component is back
            SceneSerializer.Deserialize(SceneSerializer.Serialize(scene), scene);
            foreach (Entity e in World.Entities) if (World.HasComponent<KeptCar>(e)) car = e;
            Assert.Equal(12f, World.GetComponent<KeptCar>(car).Speed);
        }

        [Fact]
        public void KeptBlock_OfATypeRegisteredLater_IsStillSaved()
        {
            var scene = new GameEditor.Framework.Scene.Scene("k");
            SceneSerializer.Deserialize(Scene1, scene);                  // no DLL knows KeptCar: kept as it is
            Register();                                                  // registered, the scene not read again
            Assert.Equal("{\"Speed\":9}", Block(SceneSerializer.Serialize(scene)));
        }
    }
}
