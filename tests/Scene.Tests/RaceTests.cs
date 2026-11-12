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
    /// The race (H5 step K3), on the real Jolt and a real track: the countdown holds the karts, gates in order make laps,
    /// a gate skipped does not count, places, the finish, a kart turned over or off the road put back, the HUD's texts.
    /// </summary>
    public sealed class RaceTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const string Key = "Tracks/race.track";
        const string Loop = "{\"points\": [[0,0,0],[0,0,60],[30,0,90],[60,0,60],[60,0,0],[30,0,-30]], \"width\": 12, " +
                            "\"wall\": {\"height\": 1, \"thickness\": 0.5}, \"checkpointEvery\": 25}";
        const float Frame = 1f / 60f;

        public RaceTests()
        {
            World.Clear();
            SceneManager.SimulatorFactory = null;
            SceneManager.NewScene("Race");
            TrackLibrary.Clear();
            TrackLibrary.Reader = key => key == Key ? Loop : null;
            Race.Reset();
        }

        public void Dispose()
        {
            SceneManager.Stop();
            ScriptSystem.ClearFactories();
            World.Clear();
            TrackLibrary.Reader = null;
            TrackLibrary.Clear();
            Race.Reset();
        }

        static TrackData T => TrackLibrary.Track(Key)!;

        static void TrackEntities()
        {
            Entity track = SceneManager.ActiveScene!.CreateEntity("Track");
            World.AddComponent(track, new TrackComponent { Key = Key });
            foreach (string part in new[] { TrackData.RoadPart, TrackData.WallsPart })
            {
                Entity e = SceneManager.ActiveScene!.CreateEntity(part);
                World.AddComponent(e, new MeshRenderer { MeshPath = TrackData.MeshPath(Key, part), Visible = true });
                World.AddComponent(e, new RigidbodyComponent { MotionType = RigidbodyMotionType.Static, Friction = 0.8f, Shapes = new() { ShapeEntry.Default(ColliderShape.Mesh) } });
            }
        }

        static Entity RaceEntity(int laps, float countdown)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity("Race");
            var r = RaceComponent.Default;
            r.Laps = laps; r.Countdown = countdown;
            World.AddComponent(e, r);
            return e;
        }

        // A kart on the start line, `across` metres to the track's right.
        static Entity Kart(string name, float across, float back = 0f)
        {
            TrackFrame start = T.FrameAt(-back);
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
            return kart;
        }

        // Each kart aims at the centre line 8 m ahead; `power` is its throttle on a straight.
        static void Drive(float seconds, params (Entity kart, float power)[] karts)
        {
            for (int f = 0; f < (int)(seconds * 60f); f++)
            {
                foreach (var (kart, power) in karts)
                {
                    Transform now = World.GetComponent<Transform>(kart);
                    float along = T.Nearest(now.Position, out _);
                    Vector3 aim = T.FrameAt(along + 8f).Position - now.Position;
                    Vector3 fwd = Vector3.Transform(Vector3.UnitZ, now.Rotation);
                    float side = Vector3.Cross(fwd, Vector3.Normalize(new Vector3(aim.X, 0, aim.Z))).Y;
                    ref KartComponent k = ref World.GetComponent<KartComponent>(kart);
                    k.Steer = Math.Clamp(-side * 3f, -1f, 1f);
                    k.Throttle = MathF.Abs(k.Steer) > 0.6f ? power * 0.6f : power;
                }
                SceneManager.UpdatePhysics(Frame);
            }
        }

        [Fact]
        public void The_countdown_holds_the_karts_then_lets_them_go()
        {
            TrackEntities();
            RaceEntity(1, 2f);
            Entity kart = Kart("A", 0f);
            SceneManager.Play();
            Vector3 start = World.GetComponent<Transform>(kart).Position;
            Drive(1.5f, (kart, 1f));
            Assert.Equal(RaceState.Countdown, Race.State);
            Assert.True(World.GetComponent<KartComponent>(kart).Held);
            Vector3 held = World.GetComponent<Transform>(kart).Position;
            Assert.True(Vector2.Distance(new Vector2(held.X, held.Z), new Vector2(start.X, start.Z)) < 0.3f, $"a held kart does not drive: {start} -> {held}");
            Drive(2.5f, (kart, 1f));
            Assert.Equal(RaceState.Racing, Race.State);
            Assert.False(World.GetComponent<KartComponent>(kart).Held);
            Assert.True(Vector3.Distance(World.GetComponent<Transform>(kart).Position, start) > 10f, "after GO it drives");
        }

        [Fact]
        public void Two_karts_race_two_laps_every_gate_in_order_and_finish_in_their_places()
        {
            TrackEntities();
            RaceEntity(2, 1f);
            Entity fast = Kart("Fast", -3f), slow = Kart("Slow", 3f);
            var laps = new List<(Entity, int, float)>();
            var finished = new List<(Entity, int, float)>();
            void OnLap(Entity e, int lap, float t) => laps.Add((e, lap, t));
            void OnFinish(Entity e, int place, float t) => finished.Add((e, place, t));
            Race.LapDone += OnLap;
            Race.Finished += OnFinish;
            try
            {
                SceneManager.Play();
                for (int i = 0; i < 90 && Race.State != RaceState.Finished; i++) Drive(1f, (fast, 1f), (slow, 0.75f));
            }
            finally { Race.LapDone -= OnLap; Race.Finished -= OnFinish; }
            Assert.Equal(RaceState.Finished, Race.State);
            Assert.Equal(4, laps.Count);
            Assert.Equal(new[] { (fast, 1), (slow, 2) }, finished.ConvertAll(f => (f.Item1, f.Item2)).ToArray());
            RacerState f0 = Race.Of(fast)!, s0 = Race.Of(slow)!;
            Assert.True(f0.Finished && s0.Finished && f0.Time < s0.Time, $"{f0.Time} vs {s0.Time}");
            Assert.Equal((1, 2), (f0.Place, s0.Place));
            Assert.True(f0.BestLap > 10f && f0.BestLap < f0.Time);
            Assert.Equal(0, f0.Respawns + s0.Respawns);
        }

        [Fact]
        public void A_gate_skipped_does_not_count()
        {
            TrackEntities();
            RaceEntity(1, 0f);
            Entity kart = Kart("A", 0f);
            SceneManager.Play();
            Drive(0.5f, (kart, 1f));
            // Put the kart past gates 1 and 2 (a shortcut): it drives on through gate 3 and 4, and none of them counts.
            TrackFrame ahead = T.Checkpoints[2];
            SceneManager.TeleportBody(kart, ahead.Position + ahead.Forward * 5f + Vector3.UnitY * 0.8f,
                                      Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(ahead.Forward.X, ahead.Forward.Z)));
            Drive(6f, (kart, 1f));
            float along = T.Nearest(World.GetComponent<Transform>(kart).Position, out _);
            Assert.True(along > T.Checkpoints[4].Distance, $"the kart went on past gate 4: at {along} m");
            Assert.Equal(1, Race.Of(kart)!.Next);
            Assert.Equal(1, Race.Of(kart)!.Lap);
        }

        [Fact]
        public void A_kart_turned_over_is_put_back_at_the_last_gate_at_rest_facing_forward()
        {
            TrackEntities();
            RaceEntity(1, 0f);
            Entity kart = Kart("A", 0f);
            int respawned = 0;
            void OnRespawn(Entity e) => respawned++;
            Race.Respawned += OnRespawn;
            try
            {
                SceneManager.Play();
                Drive(4f, (kart, 1f));
                RacerState r = Race.Of(kart)!;
                Assert.True(r.Next >= 2, $"it passed a gate: next {r.Next}");
                int gate = r.Next - 1;
                Transform tr = World.GetComponent<Transform>(kart);
                SceneManager.TeleportBody(kart, tr.Position + Vector3.UnitY * 0.5f, tr.Rotation * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI));
                Drive(3f, (kart, 0f));
                Assert.Equal(1, respawned);
                Assert.Equal(1, r.Respawns);
                Transform back = World.GetComponent<Transform>(kart);
                TrackFrame g = T.Checkpoints[gate];
                Assert.True(Vector3.Distance(new Vector3(back.Position.X, 0, back.Position.Z), new Vector3(g.Position.X, 0, g.Position.Z)) < 1.5f, $"at gate {gate}: {back.Position} vs {g.Position}");
                Assert.True(Vector3.Transform(Vector3.UnitY, back.Rotation).Y > 0.95f, "upright");
                Assert.True(Vector3.Dot(Vector3.Transform(Vector3.UnitZ, back.Rotation), g.Forward) > 0.95f, "facing the track's forward");
                Assert.True(SceneManager.TryGetLinearVelocity(kart, out Vector3 v) && v.Length() < 1f, $"at rest: {v}");
            }
            finally { Race.Respawned -= OnRespawn; }
        }

        [Fact]
        public void A_kart_off_the_road_is_put_back_too()
        {
            TrackEntities();
            RaceEntity(1, 0f);
            Entity kart = Kart("A", 0f);
            SceneManager.Play();
            Drive(2f, (kart, 1f));
            Transform tr = World.GetComponent<Transform>(kart);
            SceneManager.TeleportBody(kart, tr.Position + T.FrameAt(T.Nearest(tr.Position, out _)).Right * 30f, tr.Rotation);
            Drive(3f, (kart, 0f));
            Assert.Equal(1, Race.Of(kart)!.Respawns);
            T.Nearest(World.GetComponent<Transform>(kart).Position, out float offset);
            Assert.True(MathF.Abs(offset) < 2f, $"back on the road: {offset} m across");
        }

        [Fact]
        public void The_HUD_texts_show_the_lap_the_place_the_time_and_the_countdown()
        {
            TrackEntities();
            RaceEntity(2, 2f);
            Entity kart = Kart("A", 0f);
            var k = World.GetComponent<KartComponent>(kart);
            k.Player = true;
            World.AddComponent(kart, k);
            Kart("B", 3f);
            var huds = new Dictionary<RaceHudShows, Entity>();
            foreach (RaceHudShows s in Enum.GetValues<RaceHudShows>())
            {
                Entity e = SceneManager.ActiveScene!.CreateEntity(s.ToString());
                World.AddComponent(e, UIText.Default);
                World.AddComponent(e, new RaceHudComponent { Shows = s });
                huds[s] = e;
            }
            string Text(RaceHudShows s) => World.GetComponent<UIText>(huds[s]).Text;
            SceneManager.Play();
            Drive(0.6f);
            Assert.Equal("2", Text(RaceHudShows.Countdown));
            Assert.Equal("Lap 1/2", Text(RaceHudShows.Lap));
            Assert.Equal("0:00.0", Text(RaceHudShows.Time));
            Drive(1.6f);
            Assert.Equal("GO!", Text(RaceHudShows.Countdown));
            Drive(2f);
            Assert.Equal("", Text(RaceHudShows.Countdown));
            Assert.Matches(@"^0:0[12]\.\d$", Text(RaceHudShows.Time));
            Assert.Matches(@"^(1st|2nd)$", Text(RaceHudShows.Place));
            Assert.Equal("", Text(RaceHudShows.Results));
            Assert.Equal(Race.FormatTime(62.45f), "1:02.4");
        }

        [Fact]
        public void A_kart_a_lap_ahead_is_ahead_of_one_further_round_its_first_lap()
        {
            TrackEntities();
            RaceEntity(3, 0f);
            Entity lapper = Kart("Lapper", -3f), stopper = Kart("Stopper", 3f);
            SceneManager.Play();
            Drive(0.1f, (lapper, 0.8f), (stopper, 1f));     // the race begins on the first frame of play
            // The stopper drives most of a lap, then stops for good; the lapper goes on round into its second lap.
            for (int i = 0; i < 400 && Race.Of(stopper)!.Next < T.Checkpoints.Count - 6; i++) Drive(0.1f, (lapper, 0.8f), (stopper, 1f));
            int stopGate = Race.Of(stopper)!.Next;
            Assert.True(stopGate >= T.Checkpoints.Count - 6, $"the stopper got round to gate {stopGate}");
            // It stops at the road's right edge, out of the lapper's way (a move across the road crosses no gate).
            Transform st = World.GetComponent<Transform>(stopper);
            TrackFrame side = T.FrameAt(T.Nearest(st.Position, out _));
            SceneManager.TeleportBody(stopper, side.Position + side.Right * 4.5f + Vector3.UnitY * 0.6f, st.Rotation);
            SceneManager.SetLinearVelocity(stopper, Vector3.Zero);
            for (int i = 0; i < 600 && !(Race.Of(lapper)!.Lap == 2 && Race.Of(lapper)!.Next >= 3); i++)
            {
                Drive(0.1f, (lapper, 1f));
                ref KartComponent sk = ref World.GetComponent<KartComponent>(stopper);
                sk.Throttle = 0f; sk.Steer = 0f; sk.Brake = 0f;
            }
            RacerState a = Race.Of(lapper)!, b = Race.Of(stopper)!;
            Assert.True(a.Lap == 2 && a.Next >= 3 && b.Lap == 1 && b.Next == stopGate, $"lapper {a.Lap}/{a.Next}, stopper {b.Lap}/{b.Next} (stopped at gate {stopGate})");
            Assert.Equal((1, 2), (a.Place, b.Place));
        }

        [Fact]
        public void Stop_forgets_the_race()
        {
            TrackEntities();
            RaceEntity(1, 2f);
            Kart("A", 0f);
            SceneManager.Play();
            Drive(0.5f);
            Assert.Equal(RaceState.Countdown, Race.State);
            SceneManager.Stop();
            Assert.Equal(RaceState.None, Race.State);
            Assert.Empty(Race.Racers);
        }

        [Fact]
        public void The_race_blocks_are_written_and_read_back()
        {
            Entity race = RaceEntity(5, 4f);
            var r = World.GetComponent<RaceComponent>(race);
            r.RespawnAfter = 3f; r.OffTrack = 2.5f;
            World.AddComponent(race, r);
            Entity kart = Kart("Zed", 0f);
            Entity hud = SceneManager.ActiveScene!.CreateEntity("Hud");
            World.AddComponent(hud, new RaceHudComponent { Shows = RaceHudShows.Results });
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            Assert.Contains("\"Race\":{\"Laps\":5,\"Countdown\":4,\"RespawnAfter\":3,\"OffTrack\":2.5}", json);
            Assert.Contains("\"Racer\":{\"Name\":\"Zed\"}", json);
            Assert.Contains("\"RaceHud\":{\"Shows\":\"Results\"}", json);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            var back = new List<string>();
            foreach (Entity e in World.Entities)
            {
                if (World.TryGetComponent<RaceComponent>(e, out var rr)) back.Add($"race {rr.Laps} {rr.Countdown} {rr.RespawnAfter} {rr.OffTrack}");
                if (World.TryGetComponent<RacerComponent>(e, out var rc)) back.Add("racer " + rc.Name);
                if (World.TryGetComponent<RaceHudComponent>(e, out var h)) back.Add("hud " + h.Shows);
            }
            back.Sort();
            Assert.Equal(new[] { "hud Results", "race 5 4 3 2.5", "racer Zed" }, back.ToArray());
        }
    }
}
