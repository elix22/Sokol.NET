using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// The computer's drivers (H5 step K4), on the real Jolt: they follow the racing line at the speed it allows times
    /// their skill, finish in the order of their skill without leaving the road, drive the same race the same twice,
    /// run wide only when a mistake is drawn, and catch up only by the gap to the player.
    /// </summary>
    public sealed class DriverTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const string Key = "Tracks/drive.track";
        static string Loop(string line = "") =>
            "{\"points\": [[0,0,0],[0,0,60],[30,0,90],[60,0,60],[60,0,0],[30,0,-30]], \"width\": 12, " + line +
            "\"wall\": {\"height\": 1, \"thickness\": 0.5}, \"checkpointEvery\": 25}";
        static string _json = Loop();

        public DriverTests()
        {
            World.Clear();
            SceneManager.SimulatorFactory = null;
            SceneManager.NewScene("Drive");
            _json = Loop();
            TrackLibrary.Clear();
            TrackLibrary.Reader = key => key == Key ? _json : null;
            Race.Reset();
            DriverSystem.Reset();
        }

        public void Dispose()
        {
            SceneManager.Stop();
            ScriptSystem.ClearFactories();
            World.Clear();
            TrackLibrary.Reader = null;
            TrackLibrary.Clear();
            Race.Reset();
            DriverSystem.Reset();
        }

        static TrackData T => TrackLibrary.Track(Key)!;

        static void Scene(int laps, float countdown = 0f)
        {
            Entity track = SceneManager.ActiveScene!.CreateEntity("Track");
            World.AddComponent(track, new TrackComponent { Key = Key });
            foreach (string part in new[] { TrackData.RoadPart, TrackData.WallsPart })
            {
                Entity e = SceneManager.ActiveScene!.CreateEntity(part);
                World.AddComponent(e, new MeshRenderer { MeshPath = TrackData.MeshPath(Key, part), Visible = true });
                World.AddComponent(e, new RigidbodyComponent { MotionType = RigidbodyMotionType.Static, Friction = 0.8f, Shapes = new() { ShapeEntry.Default(ColliderShape.Mesh) } });
            }
            Entity race = SceneManager.ActiveScene!.CreateEntity("Race");
            var r = RaceComponent.Default;
            r.Laps = laps; r.Countdown = countdown;
            World.AddComponent(race, r);
        }

        // A kart on the grid: `row` rows behind the line, `across` metres to the right; with a Driver unless driver is null.
        static Entity Kart(string name, float across, int row, DriverComponent? driver)
        {
            TrackFrame start = T.FrameAt(-4f - row * 5f);
            Entity kart = SceneManager.ActiveScene!.CreateEntity(name);
            var tr = World.GetComponent<Transform>(kart);
            tr.Position = start.Position + start.Right * across + Vector3.UnitY * 0.8f;
            tr.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(start.Forward.X, start.Forward.Z));
            World.AddComponent(kart, tr);
            var shape = ShapeEntry.Default();
            shape.HalfExtent = new Vector3(0.5f, 0.2f, 0.8f);
            World.AddComponent(kart, new RigidbodyComponent { MotionType = RigidbodyMotionType.Dynamic, Mass = 150f, UseGravity = true, Friction = 0.5f, Shapes = new() { shape } });
            World.AddComponent(kart, KartComponent.Default);
            World.AddComponent(kart, new RacerComponent { Name = name });
            if (driver is { } d) World.AddComponent(kart, d);
            return kart;
        }

        static DriverComponent D(float skill, float mistakes = 0f, bool catchUp = false)
        {
            var d = DriverComponent.Default;
            d.Skill = skill; d.Mistakes = mistakes; d.CatchUp = catchUp;
            return d;
        }

        // Runs until the race is over (or `seconds`), and says how far off the line and the road each kart went.
        static Dictionary<Entity, (float road, float line)> Run(float seconds, params Entity[] karts)
        {
            var worst = new Dictionary<Entity, (float road, float line)>();
            foreach (Entity k in karts) worst[k] = (-100f, 0f);
            for (int f = 0; f < (int)(seconds * 60f) && Race.State != RaceState.Finished; f++)
            {
                SceneManager.UpdatePhysics(1f / 60f);
                if (Race.State != RaceState.Racing || Race.Clock < 3f) continue;      // the grid is off the line; from 3 s on
                foreach (Entity k in karts)
                {
                    if (Race.Of(k) is { Finished: true }) continue;
                    Vector3 at = World.GetComponent<Transform>(k).Position;
                    TrackFrame here = T.FrameAt(T.Nearest(at, out float offset));
                    var w = worst[k];
                    worst[k] = (MathF.Max(w.road, MathF.Abs(offset) - here.HalfWidth), MathF.Max(w.line, MathF.Abs(offset - here.Line)));
                }
            }
            return worst;
        }

        [Fact]
        public void Three_drivers_finish_two_laps_in_the_order_of_their_skill_and_never_leave_the_road()
        {
            Scene(2);
            Entity slow = Kart("Slow", -3f, 0, D(0.75f)), mid = Kart("Mid", 3f, 0, D(0.85f)), fast = Kart("Fast", 0f, 1, D(0.95f));
            SceneManager.Play();
            var worst = Run(150f, slow, mid, fast);
            Assert.Equal(RaceState.Finished, Race.State);
            Assert.Equal((1, 2, 3), (Race.Of(fast)!.Place, Race.Of(mid)!.Place, Race.Of(slow)!.Place));
            foreach (var (k, w) in worst) Assert.True(w.road < 0f, $"{World.GetComponent<NameTag>(k).Name} went {w.road:0.00} m past the road's edge");
            Assert.True(Race.Of(fast)!.Time < Race.Of(mid)!.Time && Race.Of(mid)!.Time < Race.Of(slow)!.Time);
            Assert.Equal(0, Race.Of(fast)!.Respawns + Race.Of(mid)!.Respawns + Race.Of(slow)!.Respawns);
        }

        [Fact]
        public void The_same_race_twice_gives_the_same_times()
        {
            float[] Times()
            {
                World.Clear();
                SceneManager.NewScene("Drive");
                Race.Reset(); DriverSystem.Reset();
                Scene(1);
                Entity a = Kart("A", -3f, 0, D(0.9f, mistakes: 1f)), b = Kart("B", 3f, 0, D(0.8f, mistakes: 1f));
                SceneManager.Play();
                Run(90f, a, b);
                float[] t = { Race.Of(a)!.Time, Race.Of(b)!.Time };
                SceneManager.Stop();
                return t;
            }
            float[] first = Times(), second = Times();
            Assert.True(first[0] > 10f && first[1] > 10f);
            Assert.Equal(first[0], second[0], 2);
            Assert.Equal(first[1], second[1], 2);
        }

        [Fact]
        public void A_driver_follows_the_racing_line()
        {
            _json = Loop("\"line\": [3, 3, 3, 3, 3, 3], ");
            TrackLibrary.Clear();
            Scene(1);
            Entity a = Kart("A", 3f, 0, D(0.8f));
            SceneManager.Play();
            var offsets = new List<float>();
            for (int f = 0; f < 60 * 12; f++)
            {
                SceneManager.UpdatePhysics(1f / 60f);
                if (f > 120) { T.Nearest(World.GetComponent<Transform>(a).Position, out float offset); offsets.Add(offset); }
            }
            offsets.Sort();
            float median = offsets[offsets.Count / 2];
            Assert.InRange(median, 2f, 4f);
        }

        [Fact]
        public void The_line_allows_less_speed_in_a_bend_than_on_a_straight()
        {
            Scene(1);
            Kart("A", 0f, 0, D(0.8f));
            SceneManager.Play();
            SceneManager.UpdatePhysics(1f / 60f);
            float straight = DriverSystem.SpeedAt(30f);                         // the middle of the first straight
            float bend = DriverSystem.SpeedAt(T.Nearest(new Vector3(30, 0, 90), out _));
            Assert.True(bend < straight * 0.85f, $"bend {bend}, straight {straight}");
            Assert.True(bend > 4f, $"a bend's speed {bend}");
        }

        [Fact]
        public void A_driver_runs_wide_only_when_a_mistake_is_drawn()
        {
            (float road, float line) Alone(float mistakes)
            {
                World.Clear();
                SceneManager.NewScene("Drive");
                Race.Reset(); DriverSystem.Reset();
                Scene(2);
                Entity k = Kart("K", 0f, 0, D(0.75f, mistakes: mistakes));
                SceneManager.Play();
                var w = Run(150f, k)[k];
                SceneManager.Stop();
                return w;
            }
            var careful = Alone(0f);
            var sloppy = Alone(1f);
            Assert.True(sloppy.line > careful.line + 1f, $"sloppy {sloppy.line:0.0} m off the line, careful {careful.line:0.0} m");
            Assert.True(sloppy.road < 0f, "a mistake does not take it off the road");
        }

        [Fact]
        public void Catch_up_slows_a_driver_far_ahead_of_the_player()
        {
            float Lap(bool catchUp)
            {
                World.Clear();
                SceneManager.NewScene("Drive");
                Race.Reset(); DriverSystem.Reset();
                Scene(1);
                Entity player = Kart("You", 3f, 0, null);
                var k = World.GetComponent<KartComponent>(player);
                k.Player = true;
                World.AddComponent(player, k);
                Entity rival = Kart("Rival", -3f, 0, D(0.9f, catchUp: catchUp));
                SceneManager.Play();
                Run(90f, rival);
                for (int f = 0; f < 60 * 60 && !Race.Of(rival)!.Finished; f++) SceneManager.UpdatePhysics(1f / 60f);
                Assert.True(Race.Of(rival)!.Finished, "the rival finished its lap");
                float t = Race.Of(rival)!.Time;
                SceneManager.Stop();
                return t;
            }
            float plain = Lap(false), slowed = Lap(true);
            Assert.True(slowed > plain + 0.3f, $"with catch-up {slowed} s, without {plain} s");
        }

        [Fact]
        public void The_driver_block_is_written_and_read_back()
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity("K");
            var d = D(0.7f, 0.4f, true);
            d.LookAhead = 11f; d.WideBy = 1.5f;
            World.AddComponent(e, d);
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            Assert.Contains("\"Driver\":{\"Skill\":0.7,\"LookAhead\":11,\"Mistakes\":0.4,\"WideBy\":1.5,\"CatchUp\":true}", json);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            DriverComponent back = default;
            foreach (Entity x in World.Entities) if (World.TryGetComponent<DriverComponent>(x, out var dd)) back = dd;
            Assert.Equal((0.7f, 11f, 0.4f, 1.5f, true), (back.Skill, back.LookAhead, back.Mistakes, back.WideBy, back.CatchUp));
        }

        [Fact]
        public void A_track_file_keeps_its_racing_line()
        {
            _json = Loop("\"line\": [1, -2, 0, 2.5, 0, -1], ");
            TrackLibrary.Clear();
            TrackData t = T;
            Assert.Equal(new[] { 1f, -2f, 0f, 2.5f, 0f, -1f }, t.Line.ToArray());
            Assert.Equal(1f, t.FrameAt(0f).Line, 2);
            TrackData again = TrackData.Parse(Key, t.ToJson(), out _)!;
            Assert.Equal(t.Line, again.Line);
        }
    }
}
