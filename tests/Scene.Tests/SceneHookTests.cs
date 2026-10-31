using System.Text.Json;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

// ECSWorld.Instance, SceneManager and the serializer's extension registry are process-global.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Scene.Tests
{
    /// <summary>A plugin component with a cross-entity reference, registered as "TestLink".</summary>
    public struct TestLink
    {
        public Entity Target;
        public float  Gain;
    }

    public sealed class SceneHookTests
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        static SceneHookTests()
        {
            SceneSerializer.RegisterComponentExtension("TestLink",
                (w, e) => w.TryGetComponent<TestLink>(e, out var l)
                    ? $"{{\"T\":{l.Target.GetHashCode()},\"G\":{l.Gain.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"
                    : null,
                (w, e, el, resolve) => w.AddComponent(e, new TestLink
                {
                    Target = resolve(el.GetProperty("T").GetInt32()),
                    Gain   = el.GetProperty("G").GetSingle(),
                }));
        }

        public SceneHookTests() => World.Clear();

        static JsonElement Components(string sceneJson, string entityName)
        {
            foreach (var e in JsonDocument.Parse(sceneJson).RootElement.GetProperty("entities").EnumerateArray())
            {
                var c = e.GetProperty("c");
                if (c.GetProperty("NameTag").GetProperty("Name").GetString() == entityName) return c;
            }
            throw new Xunit.Sdk.XunitException($"entity '{entityName}' not found");
        }

        static Entity Find(string name)
        {
            foreach (Entity e in World.Entities)
                if (World.TryGetComponent<NameTag>(e, out var nt) && nt.Name == name) return e;
            return Entity.Null;
        }

        [Fact]
        public void UnknownBlock_SurvivesLoadAndSave_Verbatim()
        {
            const string block = "{\"Mass\":1.5,\"Joint\":{\"type\":\"hinge\",\"range\":[-1,1]},\"Tags\":[\"a\",null,true]}";
            string json = "{\"name\":\"R\",\"physics\":\"mujoco\",\"entities\":[{\"id\":7,\"c\":{\"NameTag\":{\"Name\":\"Pole\"},"
                        + "\"ActiveFlag\":{\"Active\":true},\"MjBody\":" + block + "}}]}";
            var scene = new GameEditor.Framework.Scene.Scene("x");

            SceneSerializer.Deserialize(json, scene);
            string saved = SceneSerializer.Serialize(scene);

            Assert.Equal(block, Components(saved, "Pole").GetProperty("MjBody").GetRawText());
            Assert.Equal("mujoco", scene.PhysicsBackend);
            Assert.Equal("mujoco", JsonDocument.Parse(saved).RootElement.GetProperty("physics").GetString());

            // A second load/save round (e.g. Play snapshot → Stop) keeps it too.
            SceneSerializer.Deserialize(saved, scene);
            Assert.Equal(block, Components(SceneSerializer.Serialize(scene), "Pole").GetProperty("MjBody").GetRawText());
        }

        [Fact]
        public void PhysicsKey_AbsentMeansJoltAndIsNotWritten()
        {
            var scene = new GameEditor.Framework.Scene.Scene("x") { PhysicsBackend = "mujoco" };
            SceneSerializer.Deserialize("{\"name\":\"J\",\"entities\":[]}", scene);

            Assert.Null(scene.PhysicsBackend);
            Assert.Equal("{\"name\":\"J\",\"entities\":[]}", SceneSerializer.Serialize(scene));
        }

        [Fact]
        public void Extension_RoundTrips_AndResolvesEntityReferences()
        {
            var scene = new GameEditor.Framework.Scene.Scene("x");
            Entity motor = World.CreateEntity("Motor");
            Entity joint = World.CreateEntity("Joint");
            World.AddComponent(motor, new TestLink { Target = joint, Gain = 2.5f });

            string saved = SceneSerializer.Serialize(scene);
            SceneSerializer.Deserialize(saved, scene);

            Entity motor2 = Find("Motor"), joint2 = Find("Joint");
            Assert.True(World.TryGetComponent<TestLink>(motor2, out var link));
            Assert.Equal(joint2, link.Target);
            Assert.Equal(2.5f, link.Gain);
            Assert.False(World.TryGetComponent<PreservedComponents>(motor2, out _));
            Assert.False(World.TryGetComponent<TestLink>(joint2, out _));
        }

        [Fact]
        public void RegisteringABuiltInKey_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                SceneSerializer.RegisterComponentExtension("Transform", (_, _) => null, (_, _, _, _) => { }));
        }

        sealed class FakeSimulator : ISceneSimulator
        {
            public int Starts, Steps, Stops;
            public void Start(ECSWorld world) => Starts++;
            public void Step(float dt) => Steps++;
            public void Stop() => Stops++;
        }

        [Fact]
        public void Play_DispatchesToTheFactorySimulator_ForTheScenesBackend()
        {
            var sim = new FakeSimulator();
            string? asked = null;
            SceneManager.SimulatorFactory = backend => { asked = backend; return sim; };
            try
            {
                SceneManager.NewScene("Sim");
                SceneManager.ActiveScene!.PhysicsBackend = "fake";

                SceneManager.Play();
                SceneManager.UpdatePhysics(1f / 60f);
                SceneManager.UpdatePhysics(1f / 60f);
                SceneManager.Pause();
                SceneManager.UpdatePhysics(1f / 60f);   // paused: no step
                SceneManager.Stop();
                SceneManager.UpdatePhysics(1f / 60f);   // stopped: simulator released

                Assert.Equal("fake", asked);
                Assert.Equal((1, 2, 1), (sim.Starts, sim.Steps, sim.Stops));
                Assert.Equal("fake", SceneManager.ActiveScene!.PhysicsBackend); // restored from the Play snapshot
            }
            finally { SceneManager.SimulatorFactory = null; }
        }
    }
}
