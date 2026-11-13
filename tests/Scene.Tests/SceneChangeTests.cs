using System.Numerics;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics2D;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// A game that goes to another scene (a script's LoadScene): the change is made at the end of the frame, the
    /// scene that is left is left whole — its behaviours told, its bodies gone — the new one starts, static fields
    /// live on, a tile map is as its file has it, and Stop gives back the scene that was open before Play.
    /// </summary>
    public sealed class SceneChangeTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const string A = "Scenes/A.scene.json", B = "Scenes/B.scene.json", Map = "Tilemaps/level.tilemap";
        const string MapText = """
            { "tilemap": 1, "tiles": "t.sprites", "cell": [1, 1], "layers": [ { "name": "ground", "solid": true, "rows": [ [1, 1, 0, 1] ] } ] }
            """;

        readonly Dictionary<string, string> _scenes = new();
        readonly List<string> _errors = new(), _warnings = new();

        // The scene's other world (3D, or a robot's): what it was told, in order.
        static readonly List<string> World3D = new();

        sealed class NoPhysics : ISceneSimulator
        {
            public void Start(ECSWorld world) => World3D.Add("start");
            public void Step(float dt) { }
            public void Stop() => World3D.Add("stop");
        }

        static readonly List<string> Did = new();
        static int Visits;
        static string? GoTo;
        static bool Again;
        static Action? Once;

        public sealed class Visitor : GameBehaviour
        {
            public string Tag = "";
            public override void OnStart() { Visits++; Did.Add($"start {Tag} in {CurrentScene}"); }
            public override void OnDestroy() => Did.Add($"destroy {Tag}");
            public override void OnUpdate(float deltaTime)
            {
                Did.Add($"update {Tag}");
                Once?.Invoke(); Once = null;
                if (GoTo != null) { LoadScene(GoTo); GoTo = null; }
                if (Again) { ReloadScene(); Again = false; }
            }
        }

        public SceneChangeTests()
        {
            World.Clear();
            TilemapLibrary.Clear();
            TilemapLibrary.Reader = key => key == Map ? MapText : null;
            SceneManager.SimulatorFactory = _ => new NoPhysics();
            SceneManager.SceneReader = key => _scenes.TryGetValue(key, out string? text) ? text : null;
            ScriptSystem.RegisterType<Visitor>();
            Did.Clear(); World3D.Clear(); Visits = 0; GoTo = null; Again = false; Once = null;
            Logger.OnLog += OnLog;

            // B: a visitor "b" and one ball. A: a visitor "a", two balls and the tile map — and A is the scene that is open.
            _scenes[B] = SceneText("B", scene => { Add(scene, "VisitorB", "b"); Ball(scene, "Ball"); });
            _scenes[A] = SceneText("A", scene =>
            {
                Add(scene, "VisitorA", "a");
                Ball(scene, "Ball1"); Ball(scene, "Ball2");
                Entity room = scene.CreateEntity("Room");
                World.AddComponent(room, new TilemapRenderer { Map = Map, Color = Vector4.One, Visible = true });
            });
            SceneManager.ActiveScene!.FilePath = "/project/Assets/" + A;
        }

        public void Dispose()
        {
            Logger.OnLog -= OnLog;
            SceneManager.Stop();
            SceneManager.SimulatorFactory = null;
            SceneManager.SceneReader = null;
            ScriptSystem.ClearFactories();
            TilemapLibrary.Reader = null;
            TilemapLibrary.Clear();
            World.Clear();
        }

        void OnLog(LogLevel level, string text)
        {
            if (level == LogLevel.Error) _errors.Add(text);
            if (level == LogLevel.Warning) _warnings.Add(text);
        }

        static void Add(GameEditor.Framework.Scene.Scene scene, string name, string tag)
            => World.AddComponent(scene.CreateEntity(name), new ScriptComponent { TypeName = nameof(Visitor), Properties = new() { ["Tag"] = tag } });

        static void Ball(GameEditor.Framework.Scene.Scene scene, string name)
        {
            Body2D body = Body2D.Default;
            body.Shapes = new[] { new Shape2D { Type = Shape2DType.Circle, Radius = 0.5f } };
            body.GravityScale = 0f;
            World.AddComponent(scene.CreateEntity(name), body);
        }

        // Makes a scene, writes it as text, and leaves it as the scene that is open.
        static string SceneText(string name, Action<GameEditor.Framework.Scene.Scene> fill)
        {
            SceneManager.NewScene(name);
            SceneManager.ActiveScene!.PhysicsBackend = "none";
            fill(SceneManager.ActiveScene);
            return SceneSerializer.Serialize(SceneManager.ActiveScene);
        }

        static void Frame() { SceneManager.UpdatePhysics(Physics2DWorld.FixedStep); ScriptSystem.UpdateAll(Physics2DWorld.FixedStep); }

        static string[] Names() => World.Entities.Select(e => World.GetComponent<NameTag>(e).Name).OrderBy(n => n).ToArray();

        [Fact]
        public void The_scene_changes_at_the_end_of_the_frame_and_the_one_that_is_left_is_left_whole()
        {
            SceneManager.Play();
            Assert.Equal(A, SceneManager.CurrentSceneKey);
            Assert.Equal(0, SceneManager.SceneChanges);
            Assert.Equal(3, Physics2DWorld.BodyCount);                          // two balls and the map's walls
            Frame();
            GoTo = B;
            Frame();
            Assert.Equal(new[] { $"start a in {A}", "update a", "update a", "destroy a", $"start b in {B}" }, Did.ToArray());
            Assert.Equal(B, SceneManager.CurrentSceneKey);
            Assert.Equal(new[] { "Ball", "VisitorB" }, Names());
            Assert.Equal(1, Physics2DWorld.BodyCount);                          // A's bodies are gone, B's ball is there
            Assert.Equal(new[] { "start", "stop", "start" }, World3D.ToArray());   // and the other world was given back and made anew
            Assert.Equal(2, Visits);                                            // a static field lived on
            Assert.Equal(1, SceneManager.SceneChanges);
            Assert.Equal(PlayModeState.Playing, SceneManager.PlayMode);
            Frame();
            Assert.Equal("update b", Did[^1]);

            // The open scene's file is not written with another scene's entities.
            Assert.True(SceneManager.GameChangedScene);
            SceneManager.SaveScene("/project/Assets/" + A);
            Assert.Contains(_warnings, w => w.Contains("Nothing was saved"));

            // Stop gives back the scene that was open when Play was pressed.
            SceneManager.Stop();
            Assert.False(SceneManager.GameChangedScene);
            Assert.Equal(new[] { "Ball1", "Ball2", "Room", "VisitorA" }, Names());
            Assert.Null(SceneManager.CurrentSceneKey);
            Assert.Equal("destroy b", Did[^1]);

            SceneManager.Play();
            Assert.Equal(0, SceneManager.SceneChanges);                         // a new game: its count starts again
        }

        [Fact]
        public void A_key_that_names_no_scene_is_said_and_the_game_stays_where_it_is()
        {
            SceneManager.Play();
            GoTo = "Scenes/Nowhere.scene.json";
            Frame();
            Assert.Contains(_errors, e => e.Contains("Scenes/Nowhere.scene.json"));
            Assert.Equal(A, SceneManager.CurrentSceneKey);
            Assert.Equal(3, Physics2DWorld.BodyCount);
            Frame();
            Assert.Equal(new[] { $"start a in {A}", "update a", "update a" }, Did.ToArray());
        }

        [Fact]
        public void Of_two_scenes_asked_for_in_one_frame_the_last_is_gone_to()
        {
            SceneManager.Play();
            Once = () => { SceneManager.RequestScene("Scenes/Nowhere.scene.json"); SceneManager.RequestScene(B); };
            Frame();
            Assert.Equal(B, SceneManager.CurrentSceneKey);
            Assert.Empty(_errors);
        }

        [Fact]
        public void A_scene_started_again_has_its_tile_map_as_the_file_has_it()
        {
            SceneManager.Play();
            TileLayer ground = TilemapLibrary.Map(Map)!.Layer("ground")!;
            ground.Set(2, 0, 7);                                                // the game fills the gap
            TilemapLibrary.Changed();
            Frame();
            Assert.Equal(7, TilemapLibrary.Map(Map)!.Layer("ground")!.Get(2, 0));

            Again = true;
            Frame();
            Assert.Equal(new[] { $"start a in {A}", "update a", "update a", "destroy a", $"start a in {A}" }, Did.ToArray());
            Assert.Equal(0, TilemapLibrary.Map(Map)!.Layer("ground")!.Get(2, 0));
            Assert.Equal(3, Physics2DWorld.BodyCount);
            Assert.Equal(new[] { "Ball1", "Ball2", "Room", "VisitorA" }, Names());
        }

        [Fact]
        public void A_scene_asked_for_while_nothing_plays_is_not_gone_to()
        {
            SceneManager.RequestScene(B);
            Assert.Contains(_warnings, w => w.Contains("no game is playing"));
            SceneManager.Play();
            Frame();
            Assert.Equal(A, SceneManager.CurrentSceneKey);
        }
        // ── G4.5: a scene added to the one that plays, and taken out ────────────────────

        [Fact]
        public void A_scene_added_comes_in_whole_with_ids_of_its_own_and_goes_when_taken_out()
        {
            SceneManager.Play();
            Frame();
            Once = () => SceneManager.RequestSceneAdditive(B);
            Frame();
            Assert.True(SceneManager.IsSceneLoaded(B));
            Assert.Equal(A, SceneManager.CurrentSceneKey);                      // the scene that plays is still A
            Assert.Equal(new[] { "Ball", "Ball1", "Ball2", "Room", "VisitorA", "VisitorB" }, Names());
            Assert.Equal(4, Physics2DWorld.BodyCount);                          // B's ball has its body
            var ids = World.Entities.Select(e => World.GetSceneId(e)).ToArray();
            Assert.Equal(ids.Length, ids.Distinct().Count());                   // B's saved ids were A's too: each its own now
            Frame();
            Assert.Contains($"start b in {A}", Did);                            // B's script started, in the scene that plays
            Assert.Equal("update b", Did[^1]);

            Once = () => SceneManager.RequestSceneAdditive(B);                  // in already
            Frame();
            Assert.Contains(_warnings, w => w.Contains("is in already"));
            Assert.Equal(6, World.Entities.Count());

            Entity made = SceneManager.ActiveScene!.CreateEntity("Made");       // made while it plays: not B's
            Once = () => SceneManager.RequestUnloadScene(B);
            Frame();
            Assert.False(SceneManager.IsSceneLoaded(B));
            Assert.Equal(new[] { "Ball1", "Ball2", "Made", "Room", "VisitorA" }, Names());
            Assert.Contains("destroy b", Did);
            Assert.Equal(3, Physics2DWorld.BodyCount);
            Assert.True(made.IsAlive);

            Once = () => SceneManager.RequestUnloadScene(B);                    // not in
            Frame();
            Assert.Contains(_warnings, w => w.Contains("is not in"));
        }

        [Fact]
        public void The_scene_that_plays_is_not_added_a_missing_one_is_said_and_a_change_of_scene_leaves_the_added_ones()
        {
            _scenes["Scenes/C.scene.json"] = SceneText("C", scene => Ball(scene, "BallC"));
            SceneManager.NewScene("A");                                         // A open again (making C opened C)
            SceneSerializer.Deserialize(_scenes[A], SceneManager.ActiveScene!);
            SceneManager.ActiveScene!.FilePath = "/project/Assets/" + A;
            SceneManager.Play();
            Once = () => { SceneManager.RequestSceneAdditive(A); SceneManager.RequestSceneAdditive("Scenes/Nowhere.scene.json"); SceneManager.RequestSceneAdditive("Scenes/C.scene.json"); };
            Frame();
            Assert.Contains(_warnings, w => w.Contains("is the scene that plays"));
            Assert.Contains(_errors, e => e.Contains("Scenes/Nowhere.scene.json"));
            Assert.True(SceneManager.IsSceneLoaded("Scenes/C.scene.json"));
            GoTo = B;
            Frame();
            Frame();
            Assert.Equal(B, SceneManager.CurrentSceneKey);
            Assert.False(SceneManager.IsSceneLoaded("Scenes/C.scene.json"));    // left with the scene it was added to
            Assert.Equal(new[] { "Ball", "VisitorB" }, Names());
        }
    }
}
