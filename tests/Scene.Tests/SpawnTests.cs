using System.Numerics;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// Entities that come and go while a scene plays: SceneManager.Instantiate / Destroy, the scripts of what was
    /// placed (ScriptSystem.AddEntities / RemoveEntity), a script's Prefab field, and the keys a scene's text names
    /// (Prefabs.KeysIn). Played with a stand-in physics backend, so no native library is needed; the bodies of what
    /// is placed are checked where the physics library is, in a running game.
    /// </summary>
    public sealed class SpawnTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const string BulletKey = "Prefabs/Bullet.prefab";
        const string ClusterKey = "Prefabs/Cluster.prefab";

        readonly Dictionary<string, string> _files = new();
        readonly List<string> _warnings = new();

        sealed class NoPhysics : ISceneSimulator
        {
            public void Start(ECSWorld world) { }
            public void Step(float dt) { }
            public void Stop() { }
        }

        // What the scripts did, in order: "start bullet", "update bullet", "destroy bullet".
        static readonly List<string> Did = new();

        public sealed class Recorder : GameBehaviour
        {
            public string Tag = "";
            public override void OnStart() => Did.Add($"start {Tag}");
            public override void OnUpdate(float deltaTime) => Did.Add($"update {Tag}");
            public override void OnDestroy() => Did.Add($"destroy {Tag}");
        }

        public sealed class Spawner : GameBehaviour
        {
            public static Spawner? Instance;
            public static Action<Spawner>? EachUpdate;
            public Prefab? What;

            public override void OnStart() => Instance = this;
            public override void OnUpdate(float deltaTime) => EachUpdate?.Invoke(this);
            public Entity Spawn(Vector3 at, Entity? parent = null) => Instantiate(What, at, Quaternion.Identity, parent);
            public void Remove(Entity e) => Destroy(e);
        }

        // A script whose OnStart places a prefab, and whose OnDestroy removes an entity.
        public sealed class Chain : GameBehaviour
        {
            public static Entity AlsoRemove;
            public Prefab? Next;
            public override void OnStart() { Did.Add("start chain"); if (Next != null) Instantiate(Next, Vector3.Zero, Quaternion.Identity); }
            public override void OnDestroy() { Did.Add("destroy chain"); if (!AlsoRemove.IsNull) Destroy(AlsoRemove); }
        }

        public SpawnTests()
        {
            World.Clear();
            PrefabLibrary.Clear();
            PrefabLibrary.Reader = key => _files.TryGetValue(key, out string? text) ? text : null;
            SceneManager.SimulatorFactory = _ => new NoPhysics();
            ScriptSystem.RegisterType<Recorder>();
            ScriptSystem.RegisterType<Spawner>();
            ScriptSystem.RegisterType<Chain>();
            Did.Clear();
            Spawner.Instance = null;
            Spawner.EachUpdate = null;
            Chain.AlsoRemove = default;
            Logger.OnLog += OnLog;

            // Bullet (a Recorder "bullet") with a child Glow (a Recorder "glow").
            _files[BulletKey] = PrefabText("Bullet", root =>
            {
                World.AddComponent(root, new ScriptComponent { TypeName = nameof(Recorder), Properties = new() { ["Tag"] = "bullet" } });
                Entity glow = Child("Glow", root);
                World.AddComponent(glow, new ScriptComponent { TypeName = nameof(Recorder), Properties = new() { ["Tag"] = "glow" } });
            });
            // Cluster: its script places a Bullet when it starts.
            _files[ClusterKey] = PrefabText("Cluster", root =>
                World.AddComponent(root, new ScriptComponent { TypeName = nameof(Chain), Properties = new() { ["Next"] = BulletKey } }));
        }

        public void Dispose()
        {
            Logger.OnLog -= OnLog;
            SceneManager.Stop();
            SceneManager.SimulatorFactory = null;
            ScriptSystem.ClearFactories();
            PrefabLibrary.Reader = null;
            PrefabLibrary.Clear();
            World.Clear();
        }

        void OnLog(LogLevel level, string text) { if (level == LogLevel.Warning) _warnings.Add(text); }

        // ── Helpers ─────────────────────────────────────────────────────────────────────

        static Entity Child(string name, Entity parent)
        {
            Entity e = World.CreateEntity(name);
            var tr = World.GetComponent<Transform>(e);
            tr.Parent = parent;
            World.AddComponent(e, tr);
            return e;
        }

        /// <summary>A prefab file's text: a root named <paramref name="name"/>, filled by <paramref name="fill"/>. Leaves an empty world.</summary>
        static string PrefabText(string name, Action<Entity> fill)
        {
            World.Clear();
            var s = new GameEditor.Framework.Scene.Scene(name);
            fill(s.CreateEntity(name));
            s.Blocks["prefab"] = "1";
            string text = SceneSerializer.Serialize(s);
            World.Clear();
            return text;
        }

        /// <summary>A scene with a Spawner (its prefab field: the bullet) and a Crate with a child Lid, playing.</summary>
        (Entity spawner, Entity crate, Entity lid) Play()
        {
            SceneManager.NewScene("Spawn");
            var scene = SceneManager.ActiveScene!;
            scene.PhysicsBackend = "none";   // any name but "jolt": the stand-in above
            Entity spawner = scene.CreateEntity("Spawner");
            World.AddComponent(spawner, new ScriptComponent { TypeName = nameof(Spawner), Properties = new() { ["What"] = BulletKey } });
            Entity crate = scene.CreateEntity("Crate");
            World.AddComponent(crate, new ScriptComponent { TypeName = nameof(Recorder), Properties = new() { ["Tag"] = "crate" } });
            Entity lid = Child("Lid", crate);
            SceneManager.Play();
            Assert.Equal(PlayModeState.Playing, SceneManager.PlayMode);
            Did.Clear();   // "start crate"
            return (spawner, crate, lid);
        }

        static void Frame() => ScriptSystem.UpdateAll(1f / 60f);

        static string Name(Entity e) => World.GetComponent<NameTag>(e).Name;

        // ── Instantiate ─────────────────────────────────────────────────────────────────

        [Fact]
        public void AScriptsPrefabField_IsSetFromThePrefabsKey()
        {
            Play();
            Assert.NotNull(Spawner.Instance!.What);
            Assert.Equal(BulletKey, Spawner.Instance.What!.Key);
        }

        [Fact]
        public void Instantiate_PlacesThePrefabsEntities_WhereAsked()
        {
            Play();
            int before = World.Entities.Count;
            Entity bullet = Spawner.Instance!.Spawn(new Vector3(1, 2, 3));

            Assert.True(bullet.IsAlive);
            Assert.Equal(before + 2, World.Entities.Count);
            Assert.Equal("Bullet", Name(bullet));
            var tr = World.GetComponent<Transform>(bullet);
            Assert.Equal(new Vector3(1, 2, 3), tr.Position);
            Assert.Null(tr.Parent);
            Entity glow = Assert.Single(World.GetChildren(bullet));
            Assert.Equal("Glow", Name(glow));
            Assert.True(World.GetComponent<PrefabLink>(bullet).IsRoot);
            Assert.Equal(bullet, World.GetComponent<PrefabLink>(glow).Root);
        }

        [Fact]
        public void Instantiate_UnderAParent_IsItsChild()
        {
            var (_, crate, _) = Play();
            Entity bullet = Spawner.Instance!.Spawn(new Vector3(0, 1, 0), crate);

            Assert.Equal(crate, World.GetComponent<Transform>(bullet).Parent);
            Assert.Contains(bullet, World.GetChildren(crate));
        }

        [Fact]
        public void Instantiate_WithNoPrefab_ReturnsNull_AndSaysSo()
        {
            Play();
            Spawner.Instance!.What = null;
            int before = World.Entities.Count;

            Assert.True(Spawner.Instance.Spawn(Vector3.Zero).IsNull);
            Assert.Equal(before, World.Entities.Count);
            Assert.Contains(_warnings, w => w.Contains("Instantiate: no prefab"));
        }

        // ── Scripts of what was placed ──────────────────────────────────────────────────

        [Fact]
        public void PlacedScripts_StartInTheNextPass_BeforeTheirFirstUpdate()
        {
            Play();
            bool spawned = false;
            Spawner.EachUpdate = s => { if (!spawned) { spawned = true; s.Spawn(Vector3.Zero); } };

            Frame();   // the spawner places the bullet during this pass
            Assert.Equal(["update crate"], Did);

            Frame();
            Assert.Equal(["update crate", "start bullet", "start glow", "update crate", "update bullet", "update glow"], Did);

            Frame();
            Assert.Equal(1, Did.Count(l => l == "start bullet"));
            Assert.Equal(2, Did.Count(l => l == "update bullet"));
        }

        [Fact]
        public void AScriptThatPlacesAPrefabWhenItStarts_BothStartInThatPass()
        {
            Play();
            Spawner.Instance!.What = PrefabLibrary.Get(ClusterKey);
            Spawner.Instance.Spawn(Vector3.Zero);

            Frame();
            Assert.Equal(["start chain", "start bullet", "start glow"], Did.Where(l => l.StartsWith("start")).ToList());
            Assert.Contains("update bullet", Did);
        }

        // ── Destroy ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Destroy_WaitsForTheEndOfTheFrame_ThenTakesWhatIsUnderIt()
        {
            Play();
            Entity bullet = Spawner.Instance!.Spawn(Vector3.Zero);
            Entity glow = World.GetChildren(bullet)[0];
            Frame();
            int withBullet = World.Entities.Count;
            Did.Clear();

            bool aliveInTheSameUpdate = false;
            Spawner.EachUpdate = s => { s.Remove(bullet); s.Remove(bullet); aliveInTheSameUpdate = bullet.IsAlive; };
            Frame();
            Spawner.EachUpdate = null;

            Assert.True(aliveInTheSameUpdate);
            Assert.False(bullet.IsAlive);
            Assert.False(glow.IsAlive);
            Assert.Equal(withBullet - 2, World.Entities.Count);
            // It was updated in its last frame, then each script ended once.
            Assert.Equal(["update crate", "update bullet", "update glow", "destroy bullet", "destroy glow"], Did);

            Did.Clear();
            Frame();
            Assert.Equal(["update crate"], Did);
        }

        [Fact]
        public void Destroy_OfAnEntityOfTheScene_AndStopBringsItBack()
        {
            var (_, crate, lid) = Play();
            int crateId = World.GetSceneId(crate);
            int count = World.Entities.Count;

            SceneManager.Destroy(crate);
            Frame();
            Assert.False(crate.IsAlive);
            Assert.False(lid.IsAlive);
            Assert.Equal(count - 2, World.Entities.Count);
            Assert.Contains("destroy crate", Did);

            Spawner.Instance!.Spawn(Vector3.Zero);
            SceneManager.Stop();
            Assert.Equal(count, World.Entities.Count);   // what was placed is gone, what was removed is back
            Assert.True(World.TryGetEntityBySceneId(crateId, out Entity back));
            Assert.Equal("Crate", Name(back));
        }

        [Fact]
        public void PlacedAndDestroyedInOneFrame_NeverStarts()
        {
            Play();
            Entity bullet = default;
            Spawner.EachUpdate = s => { if (bullet.IsNull) { bullet = s.Spawn(Vector3.Zero); s.Remove(bullet); } };
            Frame();
            Frame();

            Assert.False(bullet.IsAlive);
            Assert.DoesNotContain("start bullet", Did);
            Assert.DoesNotContain("destroy bullet", Did);
        }

        [Fact]
        public void AnOnDestroyThatDestroysAnotherEntity_IsDoneInTheSameFrame()
        {
            var (_, crate, _) = Play();
            Spawner.Instance!.What = PrefabLibrary.Get(ClusterKey);
            Entity cluster = Spawner.Instance.Spawn(Vector3.Zero);
            Frame();
            Chain.AlsoRemove = crate;

            SceneManager.Destroy(cluster);
            Frame();

            Assert.False(cluster.IsAlive);
            Assert.False(crate.IsAlive);
            Assert.Contains("destroy chain", Did);
            Assert.Contains("destroy crate", Did);
        }

        // ── What a scene's text names ───────────────────────────────────────────────────

        [Fact]
        public void KeysIn_AreTheInstancesAndTheScriptFields_OfASceneText()
        {
            SceneManager.NewScene("Keys");
            var scene = SceneManager.ActiveScene!;
            Entity spawner = scene.CreateEntity("Spawner");
            World.AddComponent(spawner, new ScriptComponent { TypeName = nameof(Spawner), Properties = new() { ["What"] = BulletKey } });
            World.AddComponent(spawner, new MeshRenderer { MeshPath = "Models/crate.obj", MaterialPath = "Materials/red.pbrmat", Visible = true });
            SceneSerializer.InstantiatePrefab(ClusterKey, out _);
            string text = SceneSerializer.Serialize(scene);

            Assert.Equal([BulletKey, ClusterKey], Prefabs.KeysIn(text).Order().ToList());
            Assert.Equal([BulletKey], Prefabs.KeysIn(_files[ClusterKey]));   // a prefab's own text names what it places
            Assert.Empty(Prefabs.KeysIn("not json"));
        }

        [Fact]
        public void Prefabs_Load_OfAFileThatCanBeRead_AnswersAtOnce()
        {
            Prefab? got = null;
            bool called = false;
            Prefabs.Load(BulletKey, p => { got = p; called = true; });

            Assert.True(called);
            Assert.Equal(BulletKey, got!.Key);
            Assert.NotNull(Prefabs.Get(BulletKey));
            Assert.Null(Prefabs.Get("Prefabs/Nothing.prefab"));
            Assert.DoesNotContain(_warnings, w => w.Contains("Nothing.prefab"));   // asking is not a complaint
        }
    }
}
