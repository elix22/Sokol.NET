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
                    ? $"{{\"T\":{(l.Target.IsNull ? -1 : w.GetSceneId(l.Target))},\"G\":{l.Gain.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"
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

        static int IdOf(string sceneJson, string entityName)
        {
            foreach (var e in JsonDocument.Parse(sceneJson).RootElement.GetProperty("entities").EnumerateArray())
                if (e.GetProperty("c").GetProperty("NameTag").GetProperty("Name").GetString() == entityName)
                    return e.GetProperty("id").GetInt32();
            throw new Xunit.Sdk.XunitException($"entity '{entityName}' not found");
        }

        static string Ent(int id, string name, string extra = "") =>
            $"{{\"id\":{id},\"c\":{{\"NameTag\":{{\"Name\":\"{name}\"}},\"ActiveFlag\":{{\"Active\":true}}{extra}}}}}";

        [Fact]
        public void SceneIds_SurviveLoadAndSave_IncludingBuiltInReferences()
        {
            string json = "{\"name\":\"S\",\"entities\":[" + Ent(10, "Root") + ","
                        + Ent(20, "Child", ",\"Transform\":{\"P\":[0,0,0],\"E\":[0,0,0],\"S\":[1,1,1],\"Par\":10}") + "],\"nextId\":25}";
            var scene = new GameEditor.Framework.Scene.Scene("x");

            SceneSerializer.Deserialize(json, scene);
            string saved = SceneSerializer.Serialize(scene);

            Assert.Equal(10, IdOf(saved, "Root"));
            Assert.Equal(20, IdOf(saved, "Child"));
            Assert.Equal(10, Components(saved, "Child").GetProperty("Transform").GetProperty("Par").GetInt32());
            Assert.Equal(25, JsonDocument.Parse(saved).RootElement.GetProperty("nextId").GetInt32());
            Assert.True(World.TryGetEntityBySceneId(20, out var child) && World.GetComponent<NameTag>(child).Name == "Child");
        }

        [Fact]
        public void PreservedBlockReference_StaysValid_ThroughARoundTripWithoutTheExtension()
        {
            // "MjActuatorX" is not registered here (a build without robotics): its block is opaque and
            // names entity 3. The round trip must keep entity 3 as 3 so the reference still means "Joint".
            string json = "{\"name\":\"R\",\"entities\":[" + Ent(5, "Motor", ",\"MjActuatorX\":{\"Joint\":3}") + ","
                        + Ent(3, "Joint") + "]}";
            var scene = new GameEditor.Framework.Scene.Scene("x");

            SceneSerializer.Deserialize(json, scene);
            World.CreateEntity("Added");                      // edits in that editor allocate fresh ids
            string saved = SceneSerializer.Serialize(scene);
            SceneSerializer.Deserialize(saved, scene);        // the Play snapshot → Stop path, too
            saved = SceneSerializer.Serialize(scene);

            Assert.Equal("{\"Joint\":3}", Components(saved, "Motor").GetProperty("MjActuatorX").GetRawText());
            Assert.Equal(3, IdOf(saved, "Joint"));
            Assert.Equal(6, IdOf(saved, "Added"));
        }

        [Fact]
        public void ExtensionReference_ToAnUnknownId_IsSkipped()
        {
            string json = "{\"name\":\"R\",\"entities\":[" + Ent(1, "Motor", ",\"TestLink\":{\"T\":99,\"G\":1}") + "]}";
            var scene = new GameEditor.Framework.Scene.Scene("x");

            SceneSerializer.Deserialize(json, scene);

            Assert.True(World.TryGetComponent<TestLink>(Find("Motor"), out var link));
            Assert.True(link.Target.IsNull);
        }

        [Fact]
        public void DeletedEntitysId_IsNeverReused_SoItsReferencesStayUnresolved()
        {
            string json = "{\"name\":\"R\",\"entities\":[" + Ent(1, "Motor", ",\"TestLink\":{\"T\":2,\"G\":1}") + ","
                        + Ent(2, "Joint") + "]}";
            var scene = new GameEditor.Framework.Scene.Scene("x");
            SceneSerializer.Deserialize(json, scene);
            Assert.Equal(Find("Joint"), World.GetComponent<TestLink>(Find("Motor")).Target);

            // Delete the joint in an editor that keeps the motor's block opaque, then add an entity and save.
            string opaque = json.Replace("TestLink", "TestLinkX");
            SceneSerializer.Deserialize(opaque, scene);
            World.DestroyEntity(Find("Joint"));
            World.CreateEntity("Newcomer");
            string saved = SceneSerializer.Serialize(scene);
            Assert.Equal(3, IdOf(saved, "Newcomer"));

            // Reload the saved file with the extension: the newcomer is saved, reloaded, then a second
            // newcomer is created — neither may take id 2.
            SceneSerializer.Deserialize(saved.Replace("TestLinkX", "TestLink"), scene);
            Entity second = World.CreateEntity("Second");
            Assert.Equal(4, World.GetSceneId(second));
            Assert.True(World.GetComponent<TestLink>(Find("Motor")).Target.IsNull);
        }

        [Fact]
        public void NewScene_CountsIdsFromOne_AndAnEmptySceneWritesNoCounter()
        {
            var scene = new GameEditor.Framework.Scene.Scene("x");
            World.CreateEntity("A");
            World.CreateEntity("B");
            string saved = SceneSerializer.Serialize(scene);

            Assert.Equal((1, 2), (IdOf(saved, "A"), IdOf(saved, "B")));
            Assert.Equal(3, JsonDocument.Parse(saved).RootElement.GetProperty("nextId").GetInt32());
        }

        [Fact]
        public void DuplicateIdsInAFile_SecondEntityGetsAFreshId()
        {
            string json = "{\"name\":\"D\",\"entities\":[" + Ent(4, "First") + "," + Ent(4, "Second") + "]}";
            var scene = new GameEditor.Framework.Scene.Scene("x");

            SceneSerializer.Deserialize(json, scene);
            string saved = SceneSerializer.Serialize(scene);

            Assert.Equal(4, IdOf(saved, "First"));
            Assert.Equal(5, IdOf(saved, "Second"));
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
                int bodyId = World.GetSceneId(SceneManager.ActiveScene.CreateEntity("Body"));

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
                Assert.True(World.TryGetEntityBySceneId(bodyId, out var body));   // same id after Stop's reload
                Assert.Equal("Body", World.GetComponent<NameTag>(body).Name);
            }
            finally { SceneManager.SimulatorFactory = null; }
        }
    }
}
