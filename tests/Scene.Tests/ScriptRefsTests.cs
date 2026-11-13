using System;
using System.Collections.Generic;
using System.Text.Json;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// A script's field that points to another entity of the scene (docs SCRIPT_FIELDS_DESIGN.md §2.5): kept as
    /// ScriptComponent.Refs, written as scene ids ("Refs"), read back to the same entities, mapped like any reference
    /// when a prefab is placed, and given to the running script — through its generated setter or by name.
    /// </summary>
    public sealed class ScriptRefsTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        public sealed class Follower : GameBehaviour
        {
            public Entity Target;
            public Entity Other;
        }

        readonly Dictionary<string, string> _files = new();

        public ScriptRefsTests()
        {
            World.Clear();
            PrefabLibrary.Clear();
            PrefabLibrary.Reader = key => _files.TryGetValue(key, out string? text) ? text : null;
        }

        public void Dispose()
        {
            ScriptSystem.StopAll();
            ScriptSystem.ClearFactories();
            PrefabLibrary.Reader = null;
            PrefabLibrary.Clear();
            World.Clear();
        }

        static Entity Named(string name)
        {
            foreach (Entity e in World.Entities)
                if (World.GetComponent<NameTag>(e).Name == name) return e;
            return Entity.Null;
        }

        static Entity Child(GameEditor.Framework.Scene.Scene s, string name, Entity parent)
        {
            Entity e = s.CreateEntity(name);
            var tr = World.GetComponent<Transform>(e);
            tr.Parent = parent;
            World.AddComponent(e, tr);
            return e;
        }

        static GameEditor.Framework.Scene.Scene Load(string json)
        {
            World.Clear();
            var s = new GameEditor.Framework.Scene.Scene("x");
            SceneSerializer.Deserialize(json, s);
            return s;
        }

        static JsonElement ScriptBlock(string json, string name, string key = "Script")
        {
            foreach (var line in JsonDocument.Parse(json).RootElement.GetProperty("entities").EnumerateArray())
            {
                var c = line.GetProperty("c");
                if (c.TryGetProperty("NameTag", out var tag) && tag.GetProperty("Name").GetString() == name) return c.GetProperty(key).Clone();
            }
            throw new InvalidOperationException(name);
        }

        [Fact]
        public void Refs_AreWrittenAsSceneIds_AndReadBackToTheSameEntities()
        {
            var s = new GameEditor.Framework.Scene.Scene("t");
            Entity camera = s.CreateEntity("Camera");
            Entity player = s.CreateEntity("Player");
            Entity gone = s.CreateEntity("Gone");
            World.AddComponent(camera, new ScriptComponent { TypeName = "Follow", Refs = new() { ["Target"] = player, ["Other"] = gone } });
            World.AddComponent(camera, new ScriptCollectionComponent { Scripts = [new ScriptComponent { TypeName = "Aim", Refs = new() { ["Target"] = camera } }] });
            World.DestroyEntity(gone);

            string json = SceneSerializer.Serialize(s);
            var block = ScriptBlock(json, "Camera");
            Assert.Equal(World.GetSceneId(player), block.GetProperty("Refs").GetProperty("Target").GetInt32());
            Assert.False(block.GetProperty("Refs").TryGetProperty("Other", out _));          // an entity that is gone is not written
            Assert.Equal(World.GetSceneId(camera), ScriptBlock(json, "Camera", "Scripts")[0].GetProperty("Refs").GetProperty("Target").GetInt32());

            Load(json);
            Entity camera2 = Named("Camera"), player2 = Named("Player");
            var sc = World.GetComponent<ScriptComponent>(camera2);
            Assert.Equal(player2, sc.Refs!["Target"]);
            Assert.False(sc.Refs.ContainsKey("Other"));
            Assert.Equal(camera2, World.GetComponent<ScriptCollectionComponent>(camera2).Scripts![0].Refs!["Target"]);
        }

        [Fact]
        public void APrefabsRefs_PointToTheInstancesOwnEntities_AndNotOutsideIt()
        {
            var s = new GameEditor.Framework.Scene.Scene("t");
            Entity turret = s.CreateEntity("Turret");
            Entity barrel = Child(s, "Barrel", turret);
            Entity outside = s.CreateEntity("Outside");
            World.AddComponent(turret, new ScriptComponent { TypeName = "Aim", Refs = new() { ["Target"] = barrel, ["Other"] = outside } });
            World.AddComponent(turret, new ScriptCollectionComponent { Scripts = [new ScriptComponent { TypeName = "Aim", Refs = new() { ["Target"] = turret } }] });
            World.DestroyEntity(outside);       // a prefab holds only what is under its root
            s.Name = "Turret";
            s.Blocks["prefab"] = "1";
            _files["Prefabs/Turret.prefab"] = SceneSerializer.Serialize(s);
            World.Clear();

            Entity a = SceneSerializer.InstantiatePrefab("Prefabs/Turret.prefab", out string? error);
            Assert.Null(error);
            Entity b = SceneSerializer.InstantiatePrefab("Prefabs/Turret.prefab", out error);
            Assert.Null(error);
            foreach (Entity root in new[] { a, b })
            {
                Entity myBarrel = Entity.Null;
                foreach (Entity c in World.GetChildren(root)) myBarrel = c;
                var sc = World.GetComponent<ScriptComponent>(root);
                Assert.Equal(myBarrel, sc.Refs!["Target"]);
                Assert.Equal(root, World.GetComponent<ScriptCollectionComponent>(root).Scripts![0].Refs!["Target"]);
            }
            Assert.NotEqual(World.GetComponent<ScriptComponent>(a).Refs!["Target"], World.GetComponent<ScriptComponent>(b).Refs!["Target"]);
        }

        [Fact]
        public void AScenesInstance_KeepsItsRefs_ThroughASaveAndALoad()
        {
            var p = new GameEditor.Framework.Scene.Scene("p");
            Entity turret = p.CreateEntity("Turret");
            Entity barrel = Child(p, "Barrel", turret);
            World.AddComponent(turret, new ScriptComponent { TypeName = "Aim", Refs = new() { ["Target"] = barrel } });
            World.AddComponent(turret, new ScriptCollectionComponent { Scripts = [new ScriptComponent { TypeName = "Aim", Refs = new() { ["Target"] = barrel } }] });
            p.Name = "Turret";
            p.Blocks["prefab"] = "1";
            _files["Prefabs/Turret.prefab"] = SceneSerializer.Serialize(p);
            World.Clear();

            // Entities before the instances, and two instances: in the scene file the instances' ids are not the
            // prefab's, so a reference that is not mapped would point to another entity.
            var s = new GameEditor.Framework.Scene.Scene("t");
            s.CreateEntity("First");
            s.CreateEntity("Second");
            s.CreateEntity("Third");
            Entity a = SceneSerializer.InstantiatePrefab("Prefabs/Turret.prefab", out _);
            Entity b = SceneSerializer.InstantiatePrefab("Prefabs/Turret.prefab", out _);
            World.AddComponent(b, new NameTag { Name = "Turret2" });
            string json = SceneSerializer.Serialize(s);
            // An instance whose scripts point where the prefab's do has changed nothing of them: no "Script" or
            // "Scripts" of its own in the file (its references are the prefab's, mapped to its entities).
            int instances = 0;
            foreach (var line in JsonDocument.Parse(json).RootElement.GetProperty("entities").EnumerateArray())
            {
                if (!line.TryGetProperty("src", out _)) continue;
                instances++;
                Assert.False(line.TryGetProperty("c", out var c) && (c.TryGetProperty("Script", out _) || c.TryGetProperty("Scripts", out _)), line.GetRawText());
            }
            Assert.Equal(4, instances);
            Load(json);
            foreach (string name in new[] { "Turret", "Turret2" })
            {
                Entity root = Named(name);
                Assert.False(root.IsNull);
                Entity myBarrel = Entity.Null;
                foreach (Entity c in World.GetChildren(root)) myBarrel = c;
                Assert.False(myBarrel.IsNull);
                Assert.Equal(myBarrel, World.GetComponent<ScriptComponent>(root).Refs!["Target"]);
                Assert.Equal(myBarrel, World.GetComponent<ScriptCollectionComponent>(root).Scripts![0].Refs!["Target"]);
            }
        }

        [Fact]
        public void AnInstanceThatChangesOneRef_KeepsThePrefabsOthers_PointingToItsOwnEntities()
        {
            var p = new GameEditor.Framework.Scene.Scene("p");
            Entity turret = p.CreateEntity("Turret");
            Entity barrel = Child(p, "Barrel", turret);
            World.AddComponent(turret, new ScriptComponent { TypeName = "Aim", Refs = new() { ["Target"] = barrel } });
            p.Name = "Turret";
            p.Blocks["prefab"] = "1";
            _files["Prefabs/Turret.prefab"] = SceneSerializer.Serialize(p);
            World.Clear();

            var s = new GameEditor.Framework.Scene.Scene("t");
            s.CreateEntity("First");
            Entity player = s.CreateEntity("Player");
            s.CreateEntity("Third");
            Entity root = SceneSerializer.InstantiatePrefab("Prefabs/Turret.prefab", out _);
            var sc = World.GetComponent<ScriptComponent>(root);
            sc.Refs = new Dictionary<string, Entity>(sc.Refs!) { ["Spare"] = player };      // one reference of its own
            World.AddComponent(root, sc);
            Load(SceneSerializer.Serialize(s));

            Entity root2 = Named("Turret"), barrel2 = Named("Barrel"), player2 = Named("Player");
            var refs = World.GetComponent<ScriptComponent>(root2).Refs!;
            Assert.Equal(barrel2, refs["Target"]);      // the prefab's, mapped to the instance's own child
            Assert.Equal(player2, refs["Spare"]);       // the instance's own
        }

        [Fact]
        public void ARefOutsideThePrefab_IsNotWritten_AndAPlacedInstanceChangesNothing()
        {
            // A prefab made from entities of a scene, as the editor makes one: the script points outside them.
            var p = new GameEditor.Framework.Scene.Scene("p");
            Entity turret = p.CreateEntity("Turret");
            Entity outside = p.CreateEntity("Outside");
            World.AddComponent(turret, new ScriptComponent { TypeName = "Aim", Refs = new() { ["Spare"] = outside } });
            string? prefab = SceneSerializer.WritePrefabFile(turret, "Turret", "Prefabs/Turret.prefab", apply: false, out _, out string? error);
            Assert.Null(error);
            Assert.False(ScriptBlock(prefab!, "Turret").TryGetProperty("Refs", out var written), written.ToString());
            _files["Prefabs/Turret.prefab"] = prefab!;
            World.Clear();

            var s = new GameEditor.Framework.Scene.Scene("t");
            SceneSerializer.InstantiatePrefab("Prefabs/Turret.prefab", out _);
            foreach (var line in JsonDocument.Parse(SceneSerializer.Serialize(s)).RootElement.GetProperty("entities").EnumerateArray())
                Assert.False(line.TryGetProperty("c", out var c) && c.TryGetProperty("Script", out _), line.GetRawText());
        }

        static bool Set(GameBehaviour script, string field, in ScriptFieldValue value)
        {
            var f = (Follower)script;
            switch (field)
            {
                case "Target": return ScriptValue.Set(ref f.Target, value);
                case "Other": return ScriptValue.Set(ref f.Other, value);
                default: return false;
            }
        }

        static Follower Started(Entity e)
        {
            Follower? found = null;
            ScriptSystem.PopulateFromScene(World);
            ScriptSystem.StartAll();
            foreach (var (entity, behaviour) in Running()) if (entity == e && behaviour is Follower f) found = f;
            return found ?? throw new InvalidOperationException("no Follower started");
        }

        // The running behaviours, read through the script system's own list (internal state: reflection is fine in a test).
        static IEnumerable<(Entity, GameBehaviour)> Running()
        {
            var field = typeof(ScriptSystem).GetField("_running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            return (List<(Entity, GameBehaviour)>)field.GetValue(null)!;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AScript_GetsTheEntity_ByItsSetter_OrByName(bool generated)
        {
            var s = new GameEditor.Framework.Scene.Scene("t");
            Entity camera = s.CreateEntity("Camera");
            Entity player = s.CreateEntity("Player");
            Entity gone = s.CreateEntity("Gone");
            World.AddComponent(camera, new ScriptComponent { TypeName = nameof(Follower), Refs = new() { ["Target"] = player, ["Other"] = gone } });
            World.DestroyEntity(gone);
            if (generated) ScriptSystem.RegisterType<Follower>(Set);
            else ScriptSystem.RegisterType<Follower>();

            int before = ScriptSystem.FieldsSetByGeneratedCode;
            var follower = Started(camera);
            Assert.Equal(player, follower.Target);
            Assert.True(follower.Other.IsNull);                // an entity that is gone is none
            Assert.Equal(generated ? before + 2 : before, ScriptSystem.FieldsSetByGeneratedCode);
        }
    }
}
