using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// Prefab instances in a scene file: an instance is stored as a reference plus the values that differ, resolved
    /// from the prefab file on load (SceneSerializer.Prefabs.cs, PrefabLibrary).
    /// </summary>
    public sealed class PrefabTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const string EnemyKey = "Prefabs/Enemy.prefab";

        readonly Dictionary<string, string> _files = new();

        public PrefabTests()
        {
            RuntimeHelpers.RunClassConstructor(typeof(SceneHookTests).TypeHandle);   // registers the "TestLink" extension
            SceneSerializer.RegisterEntityReferenceFields("TestLink", "T");
            World.Clear();
            PrefabLibrary.Clear();
            PrefabLibrary.Reader = key => _files.TryGetValue(key, out string? text) ? text : null;
        }

        public void Dispose()
        {
            PrefabLibrary.Reader = null;
            PrefabLibrary.Clear();
            World.Clear();
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────────

        static GameEditor.Framework.Scene.Scene NewScene()
        {
            World.Clear();
            return new GameEditor.Framework.Scene.Scene("t");
        }

        static GameEditor.Framework.Scene.Scene Load(string json)
        {
            var s = new GameEditor.Framework.Scene.Scene("x");
            SceneSerializer.Deserialize(json, s);
            return s;
        }

        static Entity Child(GameEditor.Framework.Scene.Scene s, string name, Entity parent)
        {
            Entity e = s.CreateEntity(name);
            var tr = World.GetComponent<Transform>(e);
            tr.Parent = parent;
            World.AddComponent(e, tr);
            return e;
        }

        /// <summary>The world as a prefab file (what saving an open prefab writes), then an empty world.</summary>
        static string AsPrefab(GameEditor.Framework.Scene.Scene s, string name)
        {
            s.Name = name;
            s.Blocks["prefab"] = "1";
            string text = SceneSerializer.Serialize(s);
            World.Clear();
            return text;
        }

        // Enemy (1: body, script) ─ Body (2: mesh) ─ Lamp (3: light, a link to Body) ─ Hinge (4: Enemy to Body)
        static string EnemyPrefab(float lamp = 1.5f, float mass = 2f, string hp = "10")
        {
            var s = NewScene();
            Entity root = s.CreateEntity("Enemy");
            World.AddComponent(root, new RigidbodyComponent { MotionType = RigidbodyMotionType.Dynamic, Mass = mass, UseGravity = true, Friction = 0.5f, Shapes = [ShapeEntry.Default()], Layer = 1, LayerMask = 0xFFFF });
            World.AddComponent(root, new ScriptComponent { TypeName = "EnemyAI", Properties = new() { ["Speed"] = "3", ["Hp"] = hp } });
            Entity body = Child(s, "Body", root);
            World.AddComponent(body, new MeshRenderer { MeshPath = "prim:box", MaterialPath = "", Visible = true, ReceivesShadows = true, CastsShadows = true });
            Entity lampE = Child(s, "Lamp", root);
            World.AddComponent(lampE, new LightComponent { Type = LightType.Point, Color = Vector3.One, Intensity = lamp, Range = 10f, InnerAngle = 25f, OuterAngle = 35f, CastsShadows = true, Enabled = true });
            World.AddComponent(lampE, new TestLink { Target = body, Gain = 1f });
            Entity hinge = Child(s, "Hinge", root);
            World.AddComponent(hinge, new ConstraintComponent { Type = ConstraintType.Hinge, BodyA = root, BodyB = body, LocalAxisA = Vector3.UnitY, LocalAxisB = Vector3.UnitY });
            return AsPrefab(s, "Enemy");
        }

        static Entity Instantiate(string key)
        {
            Entity root = SceneSerializer.InstantiatePrefab(key, out string? error);
            Assert.Null(error);
            Assert.False(root.IsNull);
            return root;
        }

        static string NameOf(Entity e) => World.GetComponent<NameTag>(e).Name;

        static Entity Find(string name)
        {
            foreach (Entity e in World.Entities)
                if (NameOf(e) == name) return e;
            throw new Xunit.Sdk.XunitException($"entity '{name}' not found");
        }

        /// <summary>The entity called <paramref name="name"/> of the instance whose root is <paramref name="root"/>.</summary>
        static Entity Member(Entity root, string name)
        {
            foreach (Entity e in World.Entities)
                if (NameOf(e) == name && World.TryGetComponent<PrefabLink>(e, out var l) && l.Root.Equals(root)) return e;
            throw new Xunit.Sdk.XunitException($"'{name}' not found in the instance");
        }

        static JsonElement Lines(string sceneJson) => JsonDocument.Parse(sceneJson).RootElement.GetProperty("entities");

        static JsonElement Line(string sceneJson, int id)
        {
            foreach (var e in Lines(sceneJson).EnumerateArray())
                if (e.GetProperty("id").GetInt32() == id) return e;
            throw new Xunit.Sdk.XunitException($"no entry with id {id}");
        }

        static string[] Keys(JsonElement obj)
        {
            var keys = new List<string>();
            foreach (var p in obj.EnumerateObject()) keys.Add(p.Name);
            return keys.ToArray();
        }

        static void SetLight(Entity e, float intensity)
        {
            var l = World.GetComponent<LightComponent>(e);
            l.Intensity = intensity;
            World.AddComponent(e, l);
        }

        static void MoveTo(Entity e, Vector3 position)
        {
            var tr = World.GetComponent<Transform>(e);
            tr.Position = position;
            World.AddComponent(e, tr);
        }

        static string Edit(string json, Action<JsonObject> edit)
        {
            var node = JsonNode.Parse(json)!.AsObject();
            edit(node);
            return node.ToJsonString();
        }

        static string Plain(int id, string name, int parent = 0) =>
            $"{{\"id\":{id},\"c\":{{\"NameTag\":{{\"Name\":\"{name}\"}},\"ActiveFlag\":{{\"Active\":true}}," +
            $"\"Transform\":{{\"P\":[0,0,0],\"E\":[0,0,0],\"Q\":[0,0,0,1],\"S\":[1,1,1]{(parent > 0 ? $",\"Par\":{parent}" : "")}}}}}}}";

        // ── A new instance ──────────────────────────────────────────────────────────────

        [Fact]
        public void Instantiate_MakesThePrefabsEntities_WithReferencesInsideTheInstance()
        {
            _files[EnemyKey] = EnemyPrefab();
            NewScene();
            var created = new List<Entity>();
            Entity root = SceneSerializer.InstantiatePrefab(EnemyKey, out string? error, created);

            Assert.Null(error);
            Assert.Equal(4, created.Count);
            Assert.Equal(root, created[0]);
            Assert.Equal("Enemy", NameOf(root));
            Assert.Equal(2f, World.GetComponent<RigidbodyComponent>(root).Mass);
            Assert.False(World.GetComponent<Transform>(root).Parent.HasValue);

            Entity body = Member(root, "Body"), lamp = Member(root, "Lamp"), hinge = Member(root, "Hinge");
            Assert.Equal(root, World.GetComponent<Transform>(body).Parent);
            Assert.Equal(body, World.GetComponent<TestLink>(lamp).Target);                 // an extension's reference
            Assert.Equal(root, World.GetComponent<ConstraintComponent>(hinge).BodyA);
            Assert.Equal(body, World.GetComponent<ConstraintComponent>(hinge).BodyB);

            var link = World.GetComponent<PrefabLink>(root);
            Assert.True(link.IsRoot);
            Assert.Equal(EnemyKey, link.Asset);
            Assert.Empty(link.Path);
            Assert.Equal(new[] { 2 }, World.GetComponent<PrefabLink>(body).Path);
            Assert.False(World.GetComponent<PrefabLink>(body).IsRoot);
        }

        [Fact]
        public void Instantiate_OfAPrefabThatIsNotThere_IsAnError()
        {
            NewScene();
            Entity root = SceneSerializer.InstantiatePrefab("Prefabs/Nothing.prefab", out string? error);
            Assert.True(root.IsNull);
            Assert.Contains("Prefabs/Nothing.prefab", error);
            Assert.Empty(World.Entities);
        }

        [Fact]
        public void AnUntouchedInstance_IsWrittenAsAddresses_WithTheRootsNameAndPlace()
        {
            _files[EnemyKey] = EnemyPrefab();
            var s = NewScene();
            Entity root = Instantiate(EnemyKey);
            int rootId = World.GetSceneId(root);

            string json = SceneSerializer.Serialize(s);

            var rootLine = Line(json, rootId);
            Assert.Equal(EnemyKey, rootLine.GetProperty("src").GetProperty("prefab").GetString());
            Assert.Equal(new[] { "NameTag", "Transform" }, Keys(rootLine.GetProperty("c")));
            Assert.Equal(new[] { "P", "E", "Q" }, Keys(rootLine.GetProperty("c").GetProperty("Transform")));
            Assert.False(rootLine.TryGetProperty("removed", out _));

            int n = 0;
            foreach (var line in Lines(json).EnumerateArray())
            {
                if (line.GetProperty("id").GetInt32() == rootId) continue;
                n++;
                Assert.Equal(new[] { "id", "src" }, Keys(line));                                   // no "c": nothing differs
                Assert.Equal(rootId, line.GetProperty("src").GetProperty("in").GetInt32());
                Assert.Equal(1, line.GetProperty("src").GetProperty("e").GetArrayLength());
            }
            Assert.Equal(3, n);
        }

        [Fact]
        public void SerializeEntity_OfAnInstanceEntity_HasAllItsValues()
        {
            _files[EnemyKey] = EnemyPrefab();
            NewScene();
            Entity lamp = Member(Instantiate(EnemyKey), "Lamp");

            using var doc = JsonDocument.Parse(SceneSerializer.SerializeEntity(lamp));

            Assert.False(doc.RootElement.TryGetProperty("src", out _));
            Assert.Equal(1.5f, doc.RootElement.GetProperty("c").GetProperty("Light").GetProperty("Int").GetSingle());
        }

        [Fact]
        public void AScene_WithNoInstance_IsWrittenAsBefore()
        {
            var s = NewScene();
            Entity a = s.CreateEntity("A");
            Child(s, "B", a);
            string json = SceneSerializer.Serialize(s);
            Assert.DoesNotContain("\"src\"", json);
            Load(json);
            Assert.Equal(json, SceneSerializer.Serialize(Load(json)));
            Assert.False(World.HasAnyComponent<PrefabLink>());
        }

        // ── Save and load ───────────────────────────────────────────────────────────────

        [Fact]
        public void SaveLoadSave_IsIdentical_AndEachInstanceKeepsItsOwnValuesIdsAndReferences()
        {
            _files[EnemyKey] = EnemyPrefab();
            var s = NewScene();
            Entity ground = s.CreateEntity("Ground");
            Entity first = Instantiate(EnemyKey);
            Entity second = Instantiate(EnemyKey);
            World.AddComponent(second, new NameTag { Name = "Enemy 2" });
            var tr = World.GetComponent<Transform>(second);
            tr.Position = new Vector3(4f, 0f, 2f);
            tr.Rotation = Quaternion.CreateFromYawPitchRoll(0.7f, 0.1f, 0f);
            tr.Parent = ground;
            World.AddComponent(second, tr);
            SetLight(Member(second, "Lamp"), 3f);
            int firstId = World.GetSceneId(first), secondId = World.GetSceneId(second);
            int lamp2Id = World.GetSceneId(Member(second, "Lamp")), body2Id = World.GetSceneId(Member(second, "Body"));

            string json1 = SceneSerializer.Serialize(s);
            Assert.Equal("{\"Light\":{\"Int\":3}}", Line(json1, lamp2Id).GetProperty("c").GetRawText());

            var loaded = Load(json1);
            string json2 = SceneSerializer.Serialize(loaded);
            Assert.Equal(json1, json2);
            Assert.Equal(json1, SceneSerializer.Serialize(Load(json2)));                    // Play → Stop goes this way

            Assert.True(World.TryGetEntityBySceneId(firstId, out first));
            Assert.True(World.TryGetEntityBySceneId(secondId, out second));
            Assert.Equal("Enemy", NameOf(first));
            Assert.Equal("Enemy 2", NameOf(second));
            Assert.Equal(new Vector3(4f, 0f, 2f), World.GetComponent<Transform>(second).Position);
            Assert.Equal(Find("Ground"), World.GetComponent<Transform>(second).Parent);
            Assert.Equal(1.5f, World.GetComponent<LightComponent>(Member(first, "Lamp")).Intensity);
            Assert.Equal(3f, World.GetComponent<LightComponent>(Member(second, "Lamp")).Intensity);
            Assert.Equal(lamp2Id, World.GetSceneId(Member(second, "Lamp")));
            Assert.Equal(body2Id, World.GetSceneId(Member(second, "Body")));

            foreach (Entity root in new[] { first, second })
            {
                var hinge = World.GetComponent<ConstraintComponent>(Member(root, "Hinge"));
                Assert.Equal(root, hinge.BodyA);
                Assert.Equal(Member(root, "Body"), hinge.BodyB);
                Assert.Equal(Member(root, "Body"), World.GetComponent<TestLink>(Member(root, "Lamp")).Target);
            }
        }

        [Fact]
        public void APrefabChange_ReachesEveryInstance_ExceptWhatAnInstanceChangedItself()
        {
            _files[EnemyKey] = EnemyPrefab();
            var s = NewScene();
            Entity root = Instantiate(EnemyKey);
            var script = World.GetComponent<ScriptComponent>(root);
            script.Properties!["Speed"] = "5";
            SetLight(Member(root, "Lamp"), 3f);
            int rootId = World.GetSceneId(root);
            string json = SceneSerializer.Serialize(s);
            Assert.Equal("{\"Props\":{\"Speed\":\"5\"}}", Line(json, rootId).GetProperty("c").GetProperty("Script").GetRawText());

            _files[EnemyKey] = EnemyPrefab(lamp: 9f, mass: 7f, hp: "20");
            PrefabLibrary.Invalidate(EnemyKey);
            Load(json);

            root = Find("Enemy");
            Assert.Equal(7f, World.GetComponent<RigidbodyComponent>(root).Mass);            // follows the prefab
            var props = World.GetComponent<ScriptComponent>(root).Properties!;
            Assert.Equal("5", props["Speed"]);                                              // the instance's own
            Assert.Equal("20", props["Hp"]);                                                // follows, in the same block
            Assert.Equal(3f, World.GetComponent<LightComponent>(Member(root, "Lamp")).Intensity);
        }

        [Fact]
        public void AValueSetBackToThePrefabs_IsNoLongerWritten()
        {
            _files[EnemyKey] = EnemyPrefab();
            var s = NewScene();
            Entity lamp = Member(Instantiate(EnemyKey), "Lamp");
            SetLight(lamp, 3f);
            Assert.True(Line(SceneSerializer.Serialize(s), World.GetSceneId(lamp)).TryGetProperty("c", out _));
            SetLight(lamp, 1.5f);
            Assert.False(Line(SceneSerializer.Serialize(s), World.GetSceneId(lamp)).TryGetProperty("c", out _));
        }

        [Fact]
        public void AddedAndRemovedComponents_AndAnAddedChild_AreKept()
        {
            _files[EnemyKey] = EnemyPrefab();
            var s = NewScene();
            Entity root = Instantiate(EnemyKey);
            Entity body = Member(root, "Body");
            World.AddComponent(body, new LightComponent { Type = LightType.Spot, Color = Vector3.One, Intensity = 2f, Range = 5f, InnerAngle = 25f, OuterAngle = 35f, CastsShadows = false, Enabled = true });
            World.RemoveComponent<RigidbodyComponent>(root);
            Entity hat = Child(s, "Hat", body);
            World.AddComponent(hat, new MeshRenderer { MeshPath = "prim:cone", MaterialPath = "", Visible = true });
            int rootId = World.GetSceneId(root), bodyId = World.GetSceneId(body), hatId = World.GetSceneId(hat);

            string json = SceneSerializer.Serialize(s);

            Assert.Equal(new[] { "Light" }, Keys(Line(json, bodyId).GetProperty("c")));
            Assert.Equal("Spot", Line(json, bodyId).GetProperty("c").GetProperty("Light").GetProperty("Type").GetString());
            Assert.Equal("[\"Rigidbody\"]", Line(json, rootId).GetProperty("removed").GetRawText());
            Assert.False(Line(json, hatId).TryGetProperty("src", out _));
            Assert.Equal(bodyId, Line(json, hatId).GetProperty("c").GetProperty("Transform").GetProperty("Par").GetInt32());

            var loaded = Load(json);
            root = Find("Enemy"); body = Member(root, "Body"); hat = Find("Hat");
            Assert.False(World.HasComponent<RigidbodyComponent>(root));
            Assert.Equal(LightType.Spot, World.GetComponent<LightComponent>(body).Type);
            Assert.True(World.HasComponent<MeshRenderer>(body));
            Assert.Equal(body, World.GetComponent<Transform>(hat).Parent);
            Assert.False(World.HasComponent<PrefabLink>(hat));
            Assert.Equal(json, SceneSerializer.Serialize(loaded));
        }

        [Fact]
        public void AMembersRotation_IsWrittenAsEAndQTogether_AndNothingElse()
        {
            _files[EnemyKey] = EnemyPrefab();
            var s = NewScene();
            Entity body = Member(Instantiate(EnemyKey), "Body");
            var tr = World.GetComponent<Transform>(body);
            tr.Rotation = Quaternion.CreateFromYawPitchRoll(0.5f, 0f, 0f);
            World.AddComponent(body, tr);
            int bodyId = World.GetSceneId(body);

            string json = SceneSerializer.Serialize(s);
            Assert.Equal(new[] { "E", "Q" }, Keys(Line(json, bodyId).GetProperty("c").GetProperty("Transform")));

            var loaded = Load(json);
            Assert.True(World.TryGetEntityBySceneId(bodyId, out body));
            Assert.Equal(tr.Rotation, World.GetComponent<Transform>(body).Rotation);
            Assert.Equal(json, SceneSerializer.Serialize(loaded));
        }

        [Fact]
        public void AnInstancesReference_ToAnEntityOfTheScene_IsKept()
        {
            _files[EnemyKey] = EnemyPrefab();
            var s = NewScene();
            Entity wall = s.CreateEntity("Wall");
            Entity root = Instantiate(EnemyKey);
            Entity hinge = Member(root, "Hinge");
            var c = World.GetComponent<ConstraintComponent>(hinge);
            c.BodyB = wall;
            World.AddComponent(hinge, c);
            int hingeId = World.GetSceneId(hinge), wallId = World.GetSceneId(wall);

            string json = SceneSerializer.Serialize(s);
            Assert.Equal($"{{\"Constraint\":{{\"B\":{wallId}}}}}", Line(json, hingeId).GetProperty("c").GetRawText());

            Load(json);
            root = Find("Enemy");
            c = World.GetComponent<ConstraintComponent>(Member(root, "Hinge"));
            Assert.Equal(root, c.BodyA);
            Assert.Equal(Find("Wall"), c.BodyB);
        }

        [Fact]
        public void UnknownMembersOfSrc_AreWrittenBack()
        {
            _files[EnemyKey] = EnemyPrefab();
            string json = "{\"name\":\"t\",\"entities\":[{\"id\":5,\"src\":{\"prefab\":\"" + EnemyKey + "\",\"later\":{\"a\":[1,2]}}}]}";

            string saved = SceneSerializer.Serialize(Load(json));

            Assert.Equal("{\"prefab\":\"" + EnemyKey + "\",\"later\":{\"a\":[1,2]}}", Line(saved, 5).GetProperty("src").GetRawText());
            Assert.Equal(4, World.Entities.Count);
        }

        // ── The prefab changed its entities ─────────────────────────────────────────────

        [Fact]
        public void AnEntityThePrefabGained_Appears_AndOneItLost_GoesWithItsEntry()
        {
            _files[EnemyKey] = EnemyPrefab();
            var s = NewScene();
            Entity root = Instantiate(EnemyKey);
            Entity hat = Child(s, "Hat", Member(root, "Lamp"));
            int rootId = World.GetSceneId(root), bodyId = World.GetSceneId(Member(root, "Body"));
            int lampId = World.GetSceneId(Member(root, "Lamp")), hatId = World.GetSceneId(hat);
            string json = SceneSerializer.Serialize(s);
            int highest = World.NextSceneId - 1;

            // The prefab loses Lamp (3) and gains Antenna (5) under the root.
            _files[EnemyKey] = Edit(_files[EnemyKey], prefab =>
            {
                var entities = prefab["entities"]!.AsArray();
                for (int i = 0; i < entities.Count; i++)
                    if (entities[i]!["id"]!.GetValue<int>() == 3) { entities.RemoveAt(i); break; }
                entities.Add(JsonNode.Parse(Plain(5, "Antenna", parent: 1)));
                prefab["nextId"] = 6;
            });
            PrefabLibrary.Invalidate(EnemyKey);
            Load(json);

            Assert.True(World.TryGetEntityBySceneId(rootId, out root));
            Assert.False(World.TryGetEntityBySceneId(lampId, out _));
            Assert.Equal(bodyId, World.GetSceneId(Member(root, "Body")));
            Entity antenna = Member(root, "Antenna");
            Assert.True(World.GetSceneId(antenna) > highest);                              // a new id, never one in use
            Assert.Equal(new[] { 5 }, World.GetComponent<PrefabLink>(antenna).Path);
            Assert.Equal(root, World.GetComponent<Transform>(antenna).Parent);
            Assert.True(World.TryGetEntityBySceneId(hatId, out hat));
            Assert.Equal(root, World.GetComponent<Transform>(hat).Parent);                 // was under Lamp: moved to the root

            string again = SceneSerializer.Serialize(new GameEditor.Framework.Scene.Scene("t"));
            Assert.Equal(again, SceneSerializer.Serialize(Load(again)));
            Assert.DoesNotContain($"\"id\":{lampId},", again);
        }

        // ── A prefab that is not there ──────────────────────────────────────────────────

        [Fact]
        public void AMissingPrefab_KeepsTheInstancesEntries_AndTheFileComingBackRepairsIt()
        {
            _files[EnemyKey] = EnemyPrefab();
            var s = NewScene();
            Entity root = Instantiate(EnemyKey);
            MoveTo(root, new Vector3(1f, 2f, 3f));
            SetLight(Member(root, "Lamp"), 3f);
            Child(s, "Hat", Member(root, "Body"));
            string json = SceneSerializer.Serialize(s);
            string text = _files[EnemyKey];

            _files.Remove(EnemyKey);
            PrefabLibrary.Invalidate();
            var loaded = Load(json);

            Assert.Equal(5, World.Entities.Count);
            root = Find("Enemy");
            var link = World.GetComponent<PrefabLink>(root);
            Assert.True(link.Missing);
            Assert.Equal(EnemyKey, link.Asset);
            Assert.Equal(new Vector3(1f, 2f, 3f), World.GetComponent<Transform>(root).Position);
            Assert.False(World.HasComponent<RigidbodyComponent>(root));
            Assert.True(World.GetComponent<Transform>(Find("Hat")).Parent.HasValue);
            Assert.Equal(json, SceneSerializer.Serialize(loaded));                          // nothing lost

            _files[EnemyKey] = text;
            PrefabLibrary.Invalidate();
            loaded = Load(json);
            root = Find("Enemy");
            Assert.False(World.GetComponent<PrefabLink>(root).Missing);
            Assert.Equal(2f, World.GetComponent<RigidbodyComponent>(root).Mass);
            Assert.Equal(3f, World.GetComponent<LightComponent>(Member(root, "Lamp")).Intensity);
            Assert.Equal(json, SceneSerializer.Serialize(loaded));
        }

        [Fact]
        public void APrefabDeletedWhileTheSceneIsOpen_IsSavedWithAllItsValues()
        {
            _files[EnemyKey] = EnemyPrefab();
            var s = NewScene();
            Entity root = Instantiate(EnemyKey);
            int lampId = World.GetSceneId(Member(root, "Lamp"));

            _files.Remove(EnemyKey);
            PrefabLibrary.Invalidate();
            string json = SceneSerializer.Serialize(s);

            var lamp = Line(json, lampId);
            Assert.True(lamp.TryGetProperty("src", out _));
            Assert.Equal(1.5f, lamp.GetProperty("c").GetProperty("Light").GetProperty("Int").GetSingle());
            Assert.True(lamp.GetProperty("c").TryGetProperty("Transform", out _));
        }

        [Fact]
        public void APrefabThatContainsItself_IsNotLoaded()
        {
            _files["A.prefab"] = "{\"prefab\":1,\"entities\":[" + Plain(1, "A") + ",{\"id\":2,\"src\":{\"prefab\":\"B.prefab\"},\"c\":{\"Transform\":{\"Par\":1}}}]}";
            _files["B.prefab"] = "{\"prefab\":1,\"entities\":[" + Plain(1, "B") + ",{\"id\":2,\"src\":{\"prefab\":\"A.prefab\"},\"c\":{\"Transform\":{\"Par\":1}}}]}";

            Assert.Null(PrefabLibrary.Get("A.prefab"));
            Assert.Contains("contains itself", PrefabLibrary.ErrorOf("A.prefab"));
            Assert.Null(PrefabLibrary.Get("B.prefab"));

            string json = "{\"name\":\"t\",\"entities\":[{\"id\":1,\"src\":{\"prefab\":\"A.prefab\"},\"c\":{\"NameTag\":{\"Name\":\"Loop\"}}}],\"nextId\":2}";
            var loaded = Load(json);
            Assert.Single(World.Entities);
            Assert.True(World.GetComponent<PrefabLink>(Find("Loop")).Missing);
            Assert.Equal(json, SceneSerializer.Serialize(loaded));
        }

        [Fact]
        public void APrefabWithTwoRoots_IsNotLoaded()
        {
            _files["Two.prefab"] = "{\"prefab\":1,\"entities\":[" + Plain(1, "A") + "," + Plain(2, "B") + "]}";
            Assert.Null(PrefabLibrary.Get("Two.prefab"));
            Assert.Contains("more than one root", PrefabLibrary.ErrorOf("Two.prefab"));
        }

        // ── Nested prefabs ──────────────────────────────────────────────────────────────

        const string WheelKey = "Prefabs/Wheel.prefab", CarKey = "Prefabs/Car.prefab";

        // Wheel (1: mesh) ─ Hub (2: light)
        static string WheelPrefab(float hub = 1f)
        {
            var s = NewScene();
            Entity wheel = s.CreateEntity("Wheel");
            World.AddComponent(wheel, new MeshRenderer { MeshPath = "prim:cylinder", MaterialPath = "", Visible = true });
            Entity hubE = Child(s, "Hub", wheel);
            World.AddComponent(hubE, new LightComponent { Type = LightType.Point, Color = Vector3.One, Intensity = hub, Range = 10f, InnerAngle = 25f, OuterAngle = 35f, CastsShadows = true, Enabled = true });
            return AsPrefab(s, "Wheel");
        }

        // Car (1) ─ Seat (2) ─ WheelL (3; its Hub 4) ─ WheelR (5; its Hub 6, light 4 in the car)
        static string CarPrefab()
        {
            var s = NewScene();
            Entity car = s.CreateEntity("Car");
            Child(s, "Seat", car);
            foreach (string name in new[] { "WheelL", "WheelR" })
            {
                Entity wheel = Instantiate(WheelKey);
                World.AddComponent(wheel, new NameTag { Name = name });
                var tr = World.GetComponent<Transform>(wheel);
                tr.Parent = car;
                tr.Position = new Vector3(name == "WheelL" ? -1f : 1f, 0f, 0f);
                World.AddComponent(wheel, tr);
                if (name == "WheelR") SetLight(Member(wheel, "Hub"), 4f);
            }
            return AsPrefab(s, "Car");
        }

        /// <summary>The entity of the car instance at <paramref name="path"/>.</summary>
        static Entity At(Entity carRoot, params int[] path)
        {
            foreach (Entity e in World.Entities)
                if (World.TryGetComponent<PrefabLink>(e, out var l) && l.Root.Equals(carRoot) && l.Path.AsSpan().SequenceEqual(path)) return e;
            throw new Xunit.Sdk.XunitException($"no entity at {string.Join('/', path)}");
        }

        [Fact]
        public void ANestedPrefab_IsAddressedByPath_AndEachLevelsValuesApplyInOrder()
        {
            _files[WheelKey] = WheelPrefab();
            _files[CarKey] = CarPrefab();
            var s = NewScene();
            Entity car = Instantiate(CarKey);

            Assert.Equal(6, World.Entities.Count);
            Assert.Equal("WheelL", NameOf(At(car, 3)));                                    // the nested root keeps the car's name for it
            Assert.Equal(car, World.GetComponent<Transform>(At(car, 3)).Parent);
            Assert.Equal(new Vector3(1f, 0f, 0f), World.GetComponent<Transform>(At(car, 5)).Position);
            Assert.Equal(At(car, 3), World.GetComponent<Transform>(At(car, 3, 2)).Parent);
            Assert.Equal(1f, World.GetComponent<LightComponent>(At(car, 3, 2)).Intensity); // the wheel's value
            Assert.Equal(4f, World.GetComponent<LightComponent>(At(car, 5, 2)).Intensity); // the car's value for its right wheel

            SetLight(At(car, 3, 2), 7f);                                                    // the scene's value
            int hubId = World.GetSceneId(At(car, 3, 2));
            string json = SceneSerializer.Serialize(s);
            Assert.Equal("[3,2]", Line(json, hubId).GetProperty("src").GetProperty("e").GetRawText());
            Assert.Equal("{\"Light\":{\"Int\":7}}", Line(json, hubId).GetProperty("c").GetRawText());
            Assert.False(Line(json, World.GetSceneId(At(car, 5, 2))).TryGetProperty("c", out _));
            Assert.Equal(json, SceneSerializer.Serialize(Load(json)));

            // The wheel changes: every value that nobody above overrode follows.
            _files[WheelKey] = Edit(WheelPrefab(hub: 2f), wheel =>
            {
                wheel["entities"]!.AsArray().Add(JsonNode.Parse(Plain(3, "Cap", parent: 1)));
                wheel["nextId"] = 4;
            });
            PrefabLibrary.Invalidate(WheelKey);
            Load(json);
            car = Find("Car");
            Assert.Equal(8, World.Entities.Count);                                          // a Cap in each wheel
            Assert.Equal("Cap", NameOf(At(car, 3, 3)));
            Assert.Equal(At(car, 5), World.GetComponent<Transform>(At(car, 5, 3)).Parent);
            Assert.Equal(hubId, World.GetSceneId(At(car, 3, 2)));
            Assert.Equal(7f, World.GetComponent<LightComponent>(At(car, 3, 2)).Intensity);
            Assert.Equal(4f, World.GetComponent<LightComponent>(At(car, 5, 2)).Intensity);

            string again = SceneSerializer.Serialize(new GameEditor.Framework.Scene.Scene("t"));
            Assert.Equal(again, SceneSerializer.Serialize(Load(again)));
        }

        [Fact]
        public void APrefab_IsNotLoaded_WhileAPrefabInsideItIsMissing_SoNothingOfItIsDropped()
        {
            _files[WheelKey] = WheelPrefab();
            _files[CarKey] = CarPrefab();
            var s = NewScene();
            Entity car = Instantiate(CarKey);
            SetLight(At(car, 3, 2), 7f);
            string json = SceneSerializer.Serialize(s);

            _files.Remove(WheelKey);
            PrefabLibrary.Invalidate();
            var loaded = Load(json);

            Assert.Null(PrefabLibrary.Get(CarKey));
            Assert.Contains(WheelKey, PrefabLibrary.ErrorOf(CarKey));
            Assert.Equal(json, SceneSerializer.Serialize(loaded));
        }

        [Fact]
        public void APrefabWhoseRootIsAnInstance_IsAVariant()
        {
            _files[EnemyKey] = EnemyPrefab();
            const string bossKey = "Prefabs/Boss.prefab";
            var b = NewScene();
            Entity enemy = Instantiate(EnemyKey);
            World.AddComponent(enemy, new NameTag { Name = "Boss" });
            var rb = World.GetComponent<RigidbodyComponent>(enemy);
            rb.Mass = 50f;
            World.AddComponent(enemy, rb);
            Child(b, "Crown", enemy);
            _files[bossKey] = AsPrefab(b, "Boss");

            var s = NewScene();
            Entity boss = Instantiate(bossKey);
            Assert.Equal(5, World.Entities.Count);
            Assert.Equal("Boss", NameOf(boss));
            Assert.Equal(50f, World.GetComponent<RigidbodyComponent>(boss).Mass);
            Assert.Equal(boss, World.GetComponent<Transform>(Member(boss, "Crown")).Parent);
            Assert.Equal(new[] { 1, 3 }, World.GetComponent<PrefabLink>(Member(boss, "Lamp")).Path);
            Assert.Equal(boss, World.GetComponent<ConstraintComponent>(Member(boss, "Hinge")).BodyA);
            string json = SceneSerializer.Serialize(s);
            Assert.Equal(json, SceneSerializer.Serialize(Load(json)));

            _files[EnemyKey] = EnemyPrefab(lamp: 9f, mass: 3f);
            PrefabLibrary.Invalidate(EnemyKey);                                            // the variant depends on it
            Load(json);
            boss = Find("Boss");
            Assert.Equal(9f, World.GetComponent<LightComponent>(Member(boss, "Lamp")).Intensity);
            Assert.Equal(50f, World.GetComponent<RigidbodyComponent>(boss).Mass);
        }

        // ── Editing: writing a prefab file, applying, reverting, unpacking, refreshing ──

        static void SetLinks(Dictionary<Entity, PrefabLink> links)
        {
            foreach (var (e, link) in links) World.AddComponent(e, link);
        }

        static void SetMass(Entity e, float mass)
        {
            var rb = World.GetComponent<RigidbodyComponent>(e);
            rb.Mass = mass;
            World.AddComponent(e, rb);
        }

        static JsonElement Describe(Entity e) => JsonDocument.Parse(SceneSerializer.DescribePrefabLink(e)!).RootElement;

        [Fact]
        public void APrefabMadeFromEntities_HasThemWithNewIds_AndTheEntitiesBecomeAnInstanceWithNothingChanged()
        {
            _files[EnemyKey] = EnemyPrefab();
            const string robotKey = "Prefabs/Robot.prefab";
            var s = NewScene();
            Entity floor = s.CreateEntity("Floor");
            Entity robot = Child(s, "Robot", floor);
            MoveTo(robot, new Vector3(2f, 0f, 0f));
            Entity arm = Child(s, "Arm", robot);
            World.AddComponent(arm, new ConstraintComponent { Type = ConstraintType.Fixed, BodyA = robot, BodyB = floor });   // B is outside
            Entity pet = Instantiate(EnemyKey);                                              // an instance among the entities
            var tr = World.GetComponent<Transform>(pet);
            tr.Parent = robot;
            World.AddComponent(pet, tr);
            SetLight(Member(pet, "Lamp"), 3f);

            string? text = SceneSerializer.WritePrefabFile(robot, "Robot", robotKey, apply: false, out var links, out string? error);

            Assert.Null(error);
            Assert.Equal(6, links.Count);
            using (var doc = JsonDocument.Parse(text!))
            {
                Assert.Equal(1, doc.RootElement.GetProperty("prefab").GetInt32());
                Assert.Equal("Robot", doc.RootElement.GetProperty("name").GetString());
                Assert.Equal(7, doc.RootElement.GetProperty("nextId").GetInt32());
                var lines = doc.RootElement.GetProperty("entities");
                Assert.Equal(1, lines[0].GetProperty("id").GetInt32());
                Assert.False(lines[0].GetProperty("c").GetProperty("Transform").TryGetProperty("Par", out _));   // the root has no parent
                var armC = lines[1].GetProperty("c");
                Assert.Equal(1, armC.GetProperty("Transform").GetProperty("Par").GetInt32());
                Assert.Equal(1, armC.GetProperty("Constraint").GetProperty("A").GetInt32());
                Assert.Equal(-1, armC.GetProperty("Constraint").GetProperty("B").GetInt32());   // outside the prefab: nothing
                Assert.Equal(EnemyKey, lines[2].GetProperty("src").GetProperty("prefab").GetString());
            }

            _files[robotKey] = text!;
            SetLinks(links);
            Assert.Equal(robotKey, World.GetComponent<PrefabLink>(robot).Asset);
            Assert.Null(World.GetComponent<PrefabLink>(pet).Asset);                          // a part of the robot now
            Assert.Equal(robot, World.GetComponent<PrefabLink>(Find("Lamp")).Root);
            Assert.Equal(new[] { 3, 3 }, World.GetComponent<PrefabLink>(Find("Lamp")).Path);

            string json = SceneSerializer.Serialize(s);
            foreach (var line in Lines(json).EnumerateArray())
            {
                if (!line.TryGetProperty("src", out var src)) continue;
                if (src.TryGetProperty("prefab", out _)) Assert.Equal(new[] { "NameTag", "Transform" }, Keys(line.GetProperty("c")));
                else if (line.GetProperty("id").GetInt32() == World.GetSceneId(arm))
                    Assert.Equal("{\"Constraint\":{\"B\":" + World.GetSceneId(floor) + "}}", line.GetProperty("c").GetRawText());   // its own again
                else Assert.False(line.TryGetProperty("c", out _));
            }
            Assert.Equal(json, SceneSerializer.Serialize(Load(json)));

            Entity second = Instantiate(robotKey);
            Assert.Equal(13, World.Entities.Count);
            Assert.Equal(3f, World.GetComponent<LightComponent>(Member(second, "Lamp")).Intensity);   // the robot's value for its pet
        }

        [Fact]
        public void APrefab_CannotBeMadeFromAPartOfAnInstance()
        {
            _files[EnemyKey] = EnemyPrefab();
            NewScene();
            Entity body = Member(Instantiate(EnemyKey), "Body");
            Assert.Null(SceneSerializer.WritePrefabFile(body, "Body", "Prefabs/Body.prefab", apply: false, out _, out string? error));
            Assert.Contains("unpack", error);
        }

        [Fact]
        public void APrefabMadeFromAnInstancesRoot_IsAVariantOfItsPrefab()
        {
            _files[EnemyKey] = EnemyPrefab();
            NewScene();
            Entity enemy = Instantiate(EnemyKey);
            SetMass(enemy, 50f);

            string? text = SceneSerializer.WritePrefabFile(enemy, "Boss", "Prefabs/Boss.prefab", apply: false, out var links, out _);

            using var doc = JsonDocument.Parse(text!);
            var rootLine = doc.RootElement.GetProperty("entities")[0];
            Assert.Equal(EnemyKey, rootLine.GetProperty("src").GetProperty("prefab").GetString());
            Assert.Equal(50f, rootLine.GetProperty("c").GetProperty("Rigidbody").GetProperty("Mass").GetSingle());
            Assert.Equal(new[] { 1, 3 }, links[Member(enemy, "Lamp")].Path);
        }

        [Fact]
        public void Apply_WritesTheInstancesValuesIntoThePrefab_AndTheOtherInstancesFollow_ThereAndBack()
        {
            _files[EnemyKey] = EnemyPrefab();
            string before = _files[EnemyKey];
            var s = NewScene();
            Entity first = Instantiate(EnemyKey), second = Instantiate(EnemyKey);
            World.AddComponent(first, new NameTag { Name = "Boss" });
            MoveTo(first, new Vector3(5f, 0f, 0f));
            MoveTo(second, new Vector3(-5f, 0f, 0f));
            SetMass(first, 5f);
            SetLight(Member(first, "Lamp"), 3f);
            Entity hat = Child(s, "Hat", Member(first, "Body"));
            int secondLampId = World.GetSceneId(Member(second, "Lamp"));
            Entity secondLamp = Member(second, "Lamp");

            string? after = SceneSerializer.WritePrefabFile(first, "", "", apply: true, out var links, out string? error);
            Assert.Null(error);
            using (var doc = JsonDocument.Parse(after!))
            {
                var root = doc.RootElement;
                Assert.Equal(1, root.GetProperty("prefab").GetInt32());
                Assert.Equal(6, root.GetProperty("nextId").GetInt32());
                var rootC = root.GetProperty("entities")[0].GetProperty("c");
                Assert.Equal("Enemy", rootC.GetProperty("NameTag").GetProperty("Name").GetString());            // not "Boss"
                Assert.Equal("[0,0,0]", rootC.GetProperty("Transform").GetProperty("P").GetRawText());           // not moved
                Assert.Equal(5f, rootC.GetProperty("Rigidbody").GetProperty("Mass").GetSingle());
                Assert.Equal(5, root.GetProperty("entities")[4].GetProperty("id").GetInt32());                   // Hat, a new id
                Assert.Equal(2, root.GetProperty("entities")[4].GetProperty("c").GetProperty("Transform").GetProperty("Par").GetInt32());
            }

            var memo = new PrefabRefreshMemo();
            SetLinks(links);
            SceneSerializer.RefreshPrefabInstances(() => _files[EnemyKey] = after!, except: first, memo);

            Assert.Equal(new[] { 5 }, World.GetComponent<PrefabLink>(hat).Path);            // the hat is part of the prefab now
            Assert.Equal(5f, World.GetComponent<RigidbodyComponent>(second).Mass);
            Assert.Equal(secondLamp, Member(second, "Lamp"));                                // the same entity, new values
            Assert.Equal(3f, World.GetComponent<LightComponent>(secondLamp).Intensity);
            Assert.Equal("Enemy", NameOf(second));
            Assert.Equal(new Vector3(-5f, 0f, 0f), World.GetComponent<Transform>(second).Position);
            Entity secondHat = Member(second, "Hat");
            Assert.Equal(Member(second, "Body"), World.GetComponent<Transform>(secondHat).Parent);
            int secondHatId = World.GetSceneId(secondHat);
            Assert.Equal(10, World.Entities.Count);

            string json = SceneSerializer.Serialize(s);                                      // nothing differs from the prefab any more
            foreach (var line in Lines(json).EnumerateArray())
                if (line.GetProperty("src").TryGetProperty("in", out _)) Assert.False(line.TryGetProperty("c", out _));
            Assert.Equal(json, SceneSerializer.Serialize(Load(json)));

            // Back (an undo restores the file; the editor also restores the first instance's own entities).
            Load(json);
            first = Find("Boss"); second = Find("Enemy");
            SceneSerializer.RefreshPrefabInstances(() => _files[EnemyKey] = before, except: first, memo);
            Assert.Equal(2f, World.GetComponent<RigidbodyComponent>(second).Mass);
            Assert.Equal(1.5f, World.GetComponent<LightComponent>(Member(second, "Lamp")).Intensity);
            Assert.Equal(secondLampId, World.GetSceneId(Member(second, "Lamp")));
            Assert.False(World.TryGetEntityBySceneId(secondHatId, out _));
            Assert.Equal(9, World.Entities.Count);

            // And again (a redo): the same hat comes back, with the id it had.
            SceneSerializer.RefreshPrefabInstances(() => _files[EnemyKey] = after!, except: first, memo);
            Assert.True(World.TryGetEntityBySceneId(secondHatId, out secondHat));
            Assert.Equal("Hat", NameOf(secondHat));
            Assert.Equal(Member(second, "Body"), World.GetComponent<Transform>(secondHat).Parent);
            Assert.Equal(10, World.Entities.Count);
            memo.Discard();
        }

        [Fact]
        public void Apply_OfOneComponent_LeavesTheRestOfThePrefabAsItWas()
        {
            _files[EnemyKey] = EnemyPrefab();
            var s = NewScene();
            Entity first = Instantiate(EnemyKey);
            SetMass(first, 5f);
            Entity lamp = Member(first, "Lamp");
            SetLight(lamp, 3f);
            Child(s, "Hat", first);

            string? text = SceneSerializer.WritePrefabFile(first, "", "", apply: true, out _, out string? error, onlyEntity: lamp, onlyComponent: "Light");
            Assert.Null(error);
            _files[EnemyKey] = text!;
            PrefabLibrary.Invalidate();

            var prefab = PrefabLibrary.Get(EnemyKey)!;
            Assert.Equal(4, prefab.Entities.Count);                                          // no hat
            Assert.Equal(3f, prefab.ById(3)!.Components.GetProperty("Light").GetProperty("Int").GetSingle());
            Assert.Equal(2f, prefab.Root.Components.GetProperty("Rigidbody").GetProperty("Mass").GetSingle());
            Assert.Equal(2, prefab.ById(3)!.Components.GetProperty("TestLink").GetProperty("T").GetInt32());
        }

        [Fact]
        public void Apply_OnANestedInstance_WritesTheOuterPrefabsValueForIt_AndLeavesTheInnerPrefabAlone()
        {
            _files[WheelKey] = WheelPrefab();
            _files[CarKey] = CarPrefab();
            string wheelText = _files[WheelKey];
            NewScene();
            Entity car = Instantiate(CarKey);
            SetLight(At(car, 3, 2), 7f);

            string? text = SceneSerializer.WritePrefabFile(car, "", "", apply: true, out _, out string? error);
            Assert.Null(error);

            using var doc = JsonDocument.Parse(text!);
            var lines = doc.RootElement.GetProperty("entities");
            Assert.Equal(6, lines.GetArrayLength());
            foreach (var line in lines.EnumerateArray())
            {
                int id = line.GetProperty("id").GetInt32();
                if (id == 4) Assert.Equal("{\"Light\":{\"Int\":7}}", line.GetProperty("c").GetRawText());
                if (id == 6) Assert.Equal("{\"Light\":{\"Int\":4}}", line.GetProperty("c").GetRawText());
                if (id is 4 or 6) Assert.Equal("[2]", line.GetProperty("src").GetProperty("e").GetRawText());
                if (id is 3 or 5) Assert.Equal(WheelKey, line.GetProperty("src").GetProperty("prefab").GetString());
            }
            Assert.Equal(wheelText, _files[WheelKey]);
        }

        [Fact]
        public void Describe_SaysWhatAnInstanceEntityChanged_AndNotWhatIsTheRootsOwn()
        {
            _files[EnemyKey] = EnemyPrefab();
            var s = NewScene();
            Entity root = Instantiate(EnemyKey);
            World.AddComponent(root, new NameTag { Name = "Other" });
            MoveTo(root, new Vector3(1f, 2f, 3f));
            Entity lamp = Member(root, "Lamp"), body = Member(root, "Body");

            var d = Describe(root);
            Assert.Equal(EnemyKey, d.GetProperty("asset").GetString());
            Assert.Equal(World.GetSceneId(root), d.GetProperty("root").GetInt32());
            Assert.Equal("{}", d.GetProperty("overrides").GetRawText());                      // moved and renamed: its own
            Assert.Equal("{}", Describe(lamp).GetProperty("overrides").GetRawText());
            Assert.Equal("[3]", Describe(lamp).GetProperty("path").GetRawText());

            SetLight(lamp, 3f);
            World.RemoveComponent<TestLink>(lamp);
            World.AddComponent(body, new LightComponent { Type = LightType.Spot, Color = Vector3.One, Intensity = 2f, Range = 5f, InnerAngle = 25f, OuterAngle = 35f, Enabled = true });
            var tr = World.GetComponent<Transform>(root);
            tr.Scale = new Vector3(2f, 2f, 2f);
            World.AddComponent(root, tr);

            Assert.Equal("{\"Light\":{\"Int\":3}}", Describe(lamp).GetProperty("overrides").GetRawText());
            Assert.Equal("[\"TestLink\"]", Describe(lamp).GetProperty("removed").GetRawText());
            Assert.Equal("[\"Light\"]", Describe(body).GetProperty("added").GetRawText());
            Assert.Equal("{\"Transform\":{\"S\":[2,2,2]}}", Describe(root).GetProperty("overrides").GetRawText());
            Assert.Null(SceneSerializer.DescribePrefabLink(s.CreateEntity("Plain")));
        }

        [Fact]
        public void Revert_TakesBackAField_AComponent_OrEverything_AndKeepsTheRootsNameAndPlace()
        {
            _files[EnemyKey] = EnemyPrefab();
            NewScene();
            Entity root = Instantiate(EnemyKey);
            World.AddComponent(root, new NameTag { Name = "Other" });
            MoveTo(root, new Vector3(1f, 2f, 3f));
            Entity lamp = Member(root, "Lamp"), body = Member(root, "Body");

            // A field: the others the instance changed stay.
            var light = World.GetComponent<LightComponent>(lamp);
            light.Intensity = 3f; light.Range = 44f;
            World.AddComponent(lamp, light);
            Assert.Null(SceneSerializer.RevertToPrefab(lamp, "Light", "Int"));
            Assert.Equal(1.5f, World.GetComponent<LightComponent>(lamp).Intensity);
            Assert.Equal(44f, World.GetComponent<LightComponent>(lamp).Range);
            Assert.NotNull(SceneSerializer.RevertToPrefab(lamp, "Light", "Nothing"));

            // A component: one that was changed, one that was removed, one that was added.
            Assert.Null(SceneSerializer.RevertToPrefab(lamp, "Light"));
            Assert.Equal(10f, World.GetComponent<LightComponent>(lamp).Range);
            World.RemoveComponent<RigidbodyComponent>(root);
            Assert.Null(SceneSerializer.RevertToPrefab(root, "Rigidbody"));
            Assert.Equal(2f, World.GetComponent<RigidbodyComponent>(root).Mass);
            World.AddComponent(body, new LightComponent { Type = LightType.Spot, Intensity = 2f, Range = 5f, Enabled = true });
            Assert.Null(SceneSerializer.RevertToPrefab(body, "Light"));
            Assert.False(World.HasComponent<LightComponent>(body));
            Assert.NotNull(SceneSerializer.RevertToPrefab(body, "Camera"));

            // Everything of an entity. The root keeps its name, place and parent.
            SetMass(root, 9f);
            var tr = World.GetComponent<Transform>(root);
            tr.Scale = new Vector3(2f, 2f, 2f);
            World.AddComponent(root, tr);
            World.AddComponent(root, new CameraComponent { Fov = 60f, NearZ = 0.1f, FarZ = 100f });
            Assert.NotNull(SceneSerializer.RevertToPrefab(root, "Transform", "P"));
            Assert.Null(SceneSerializer.RevertToPrefab(root));
            Assert.Equal(2f, World.GetComponent<RigidbodyComponent>(root).Mass);
            Assert.False(World.HasComponent<CameraComponent>(root));
            Assert.Equal(Vector3.One, World.GetComponent<Transform>(root).Scale);
            Assert.Equal(new Vector3(1f, 2f, 3f), World.GetComponent<Transform>(root).Position);
            Assert.Equal("Other", NameOf(root));
            Assert.Equal("{}", Describe(root).GetProperty("overrides").GetRawText());
            Assert.Equal(root, World.GetComponent<ConstraintComponent>(Member(root, "Hinge")).BodyA);

            Assert.NotNull(SceneSerializer.RevertToPrefab(World.CreateEntity("Plain")));
        }

        [Fact]
        public void Refresh_GivesOpenInstancesAChangedPrefabsValues_OnTheSameEntities()
        {
            string changed = EnemyPrefab(lamp: 9f, mass: 7f);                                // (building a prefab text empties the world)
            _files[EnemyKey] = EnemyPrefab();
            NewScene();
            Entity root = Instantiate(EnemyKey);
            Entity lamp = Member(root, "Lamp");
            SetMass(root, 5f);
            int lampId = World.GetSceneId(lamp);

            _files[EnemyKey] = changed;
            SceneSerializer.RefreshPrefabInstances();

            Assert.Equal(lamp, Member(root, "Lamp"));
            Assert.Equal(lampId, World.GetSceneId(lamp));
            Assert.Equal(9f, World.GetComponent<LightComponent>(lamp).Intensity);
            Assert.Equal(5f, World.GetComponent<RigidbodyComponent>(root).Mass);             // its own
            Assert.Equal(Member(root, "Body"), World.GetComponent<TestLink>(lamp).Target);
            Assert.Equal(4, World.Entities.Count);
        }

        [Fact]
        public void Unpack_LeavesOrdinaryEntities_AndNestedInstancesStayInstances_UnlessCompletely()
        {
            _files[WheelKey] = WheelPrefab();
            _files[CarKey] = CarPrefab();
            var s = NewScene();
            Entity car = Instantiate(CarKey);
            Entity wheelR = At(car, 5), hubR = At(car, 5, 2), seat = At(car, 2);

            var completely = SceneSerializer.UnpackedLinks(car, completely: true);
            Assert.Equal(6, completely.Count);
            Assert.All(completely.Values, l => Assert.Null(l));

            var links = SceneSerializer.UnpackedLinks(car, completely: false);
            Assert.Null(links[car]);
            Assert.Null(links[seat]);
            Assert.Equal(WheelKey, links[wheelR]!.Value.Asset);
            Assert.Equal(wheelR, links[wheelR]!.Value.Root);
            Assert.Equal(wheelR, links[hubR]!.Value.Root);
            Assert.Equal(new[] { 2 }, links[hubR]!.Value.Path);

            foreach (var (e, link) in links)
                if (link is { } l) World.AddComponent(e, l); else World.RemoveComponent<PrefabLink>(e);
            string json = SceneSerializer.Serialize(s);
            Assert.Equal(WheelKey, Line(json, World.GetSceneId(wheelR)).GetProperty("src").GetProperty("prefab").GetString());
            Assert.Equal("{\"Light\":{\"Int\":4}}", Line(json, World.GetSceneId(hubR)).GetProperty("c").GetRawText());   // the car's value, the scene's now
            Assert.False(Line(json, World.GetSceneId(seat)).TryGetProperty("src", out _));
            Assert.Equal(json, SceneSerializer.Serialize(Load(json)));
            Assert.Equal(4f, World.GetComponent<LightComponent>(Find("Hub", 1)).Intensity);
        }

        static Entity Find(string name, int index)
        {
            foreach (Entity e in World.Entities)
                if (NameOf(e) == name && index-- == 0) return e;
            throw new Xunit.Sdk.XunitException($"entity '{name}' not found");
        }
    }
}
