using System.Collections.Generic;
using System.Linq;
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
    /// A race track (H5 step K2): its file, the centre line through its points, the road and the walls made from it,
    /// its checkpoints, the questions a race asks — and, on the real Jolt, a road a kart drives on and walls that stop it.
    /// </summary>
    public sealed class TrackTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const string Key = "Tracks/test.track";

        // Six points round a loop of about 300 m, 10 m wide, walls 1 m high.
        const string Loop = "{\"points\": [[0,0,0],[0,0,60],[30,0,90],[60,0,60],[60,0,0],[30,0,-30]], \"width\": 10, " +
                            "\"wall\": {\"height\": 1, \"thickness\": 0.5}, \"checkpointEvery\": 20}";

        public TrackTests()
        {
            World.Clear();
            SceneManager.SimulatorFactory = null;
            SceneManager.NewScene("Track");
            TrackLibrary.Clear();
            TrackLibrary.Reader = key => key == Key ? Loop : null;
        }

        public void Dispose()
        {
            SceneManager.Stop();
            ScriptSystem.ClearFactories();
            World.Clear();
            TrackLibrary.Reader = null;
            TrackLibrary.Clear();
        }

        static TrackData Track() => TrackLibrary.Track(Key) ?? throw new InvalidOperationException("the test track is not read");

        [Fact]
        public void A_track_file_is_read_and_written_back_the_same()
        {
            TrackData t = Track();
            Assert.Equal(6, t.Points.Count);
            Assert.All(t.Widths, w => Assert.Equal(10f, w));
            Assert.Equal(1f, t.WallHeight);
            Assert.Equal(0.5f, t.WallThickness);
            TrackData again = TrackData.Parse(Key, t.ToJson(), out string? error)!;
            Assert.Null(error);
            Assert.Equal(t.Length, again.Length, 3);
            Assert.Equal(t.ToJson(), again.ToJson());
        }

        [Fact]
        public void A_file_that_is_not_a_track_says_why()
        {
            Assert.Null(TrackData.Parse(Key, "{\"points\": [[0,0,0],[0,0,10],[10,0,10]]}", out string? few));
            Assert.Contains("4 points", few);
            Assert.Null(TrackData.Parse(Key, "{\"points\": [[0,0,0],[0,0,0],[0,0,10],[10,0,10]]}", out string? same));
            Assert.Contains("one place", same);
            Assert.Null(TrackData.Parse(Key, "not json", out string? bad));
            Assert.NotNull(bad);
        }

        [Fact]
        public void The_centre_line_goes_through_every_point_and_is_sampled_every_metre()
        {
            TrackData t = Track();
            Assert.InRange(t.Length, 280f, 320f);
            Assert.Equal((int)MathF.Round(t.Length), t.Frames.Count);
            foreach (Vector3 p in t.Points)
            {
                float d = t.Nearest(p, out float offset);
                Assert.True(Vector3.Distance(t.FrameAt(d).Position, p) < 0.05f, $"point {p}: the line passes {Vector3.Distance(t.FrameAt(d).Position, p)} m away");
            }
            for (int i = 0; i < t.Frames.Count; i++)
            {
                TrackFrame a = t.Frames[i], b = t.Frames[(i + 1) % t.Frames.Count];
                Assert.InRange(Vector3.Distance(a.Position, b.Position), 0.9f, 1.1f);
                Assert.True(MathF.Abs(a.Forward.Length() - 1f) < 1e-4f && MathF.Abs(Vector3.Dot(a.Forward, a.Right)) < 1e-4f);
            }
        }

        [Fact]
        public void The_right_of_a_track_going_plus_Z_is_minus_X_as_a_karts()
        {
            TrackData t = TrackData.Parse(Key, "{\"points\": [[0,0,0],[0,0,100],[100,0,100],[100,0,0]], \"width\": 8}", out _)!;
            TrackFrame f = t.FrameAt(t.Nearest(new Vector3(0, 0, 50), out _));
            Assert.True(f.Forward.Z > 0.99f, f.Forward.ToString());
            Assert.True(f.Right.X < -0.99f, f.Right.ToString());
            Assert.True(f.Up.Y > 0.99f, f.Up.ToString());
        }

        [Fact]
        public void Nearest_gives_the_distance_along_and_the_offset_across()
        {
            TrackData t = Track();
            foreach (float d in new[] { 0.5f, 77f, 123.4f, 250f })
                foreach (float across in new[] { -4f, 0f, 3f })
                {
                    TrackFrame f = t.FrameAt(d);
                    float along = t.Nearest(f.Position + f.Right * across, out float offset);
                    Assert.True(MathF.Abs(along - d) < 0.05f, $"at {d}, {across} across: {along}");
                    Assert.True(MathF.Abs(offset - across) < 0.05f, $"at {d}, {across} across: offset {offset}");
                }
        }

        [Fact]
        public void The_road_covers_its_length_times_its_width_facing_up()
        {
            TrackData t = Track();
            var p = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tg = new List<Vector4>(); var ix = new List<uint>();
            t.RoadMesh(p, n, uv, tg, ix);
            double area = 0;
            for (int i = 0; i < ix.Count; i += 3)
            {
                Vector3 a = p[(int)ix[i]], b = p[(int)ix[i + 1]], c = p[(int)ix[i + 2]];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                area += cross.Length() / 2;
                Assert.True(Vector3.Dot(cross, n[(int)ix[i]]) > 0f, "a road triangle faces away from its normal");
            }
            Assert.InRange(area / (t.Length * 10f), 0.99, 1.01);
        }

        [Fact]
        public void The_walls_stand_at_both_edges_and_face_their_normals()
        {
            TrackData t = Track();
            var p = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tg = new List<Vector4>(); var ix = new List<uint>();
            t.WallMesh(p, n, uv, tg, ix);
            Assert.True(ix.Count > 0);
            for (int i = 0; i < ix.Count; i += 3)
            {
                Vector3 a = p[(int)ix[i]], b = p[(int)ix[i + 1]], c = p[(int)ix[i + 2]];
                Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), n[(int)ix[i]]) > 0f, "a wall triangle faces away from its normal");
            }
            Assert.InRange(p.Max(v => v.Y), 0.99f, 1.01f);
            t.WallHeight = 0f;
            p.Clear(); ix.Clear();
            t.WallMesh(p, n, uv, tg, ix);
            Assert.Empty(ix);
        }

        [Fact]
        public void Checkpoints_go_round_the_track_evenly_from_the_start()
        {
            TrackData t = Track();
            Assert.Equal((int)MathF.Floor(t.Length / 20f), t.Checkpoints.Count);
            Assert.Equal(0f, t.Checkpoints[0].Distance, 3);
            float gap = t.Length / t.Checkpoints.Count;
            for (int i = 1; i < t.Checkpoints.Count; i++)
                Assert.Equal(gap, t.Checkpoints[i].Distance - t.Checkpoints[i - 1].Distance, 2);
        }

        [Fact]
        public void A_tracks_parts_are_named_by_its_key_and_hash()
        {
            Assert.Equal("Tracks/test.track#road", TrackData.MeshPath(Key, TrackData.RoadPart));
            Assert.True(TrackData.TryParseMeshPath("Tracks/test.track#walls", out string key, out string part));
            Assert.Equal((Key, "walls"), (key, part));
            Assert.False(TrackData.TryParseMeshPath("Tracks/test.track#roof", out _, out _));
            Assert.False(TrackData.TryParseMeshPath("Models/duck.obj#road", out _, out _));
            Assert.Equal(Key, TrackData.KeyOf("Tracks/test.track#road"));
            Assert.Equal(Key, TrackData.KeyOf(Key));
            Assert.Null(TrackData.KeyOf("prim:box"));
            Assert.Equal(new[] { Key }, TrackLibrary.KeysIn("{\"MeshRenderer\": {\"Path\": \"Tracks/test.track#road\"}, \"Track\": {\"Key\": \"Tracks/test.track\"}}"));
        }

        // ── On the real Jolt ────────────────────────────────────────────────────────────────

        static Entity Part(string part)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity(part);
            World.AddComponent(e, new MeshRenderer { MeshPath = TrackData.MeshPath(Key, part), Visible = true });
            World.AddComponent(e, new RigidbodyComponent { MotionType = RigidbodyMotionType.Static, Friction = 0.8f, Shapes = new() { ShapeEntry.Default(ColliderShape.Mesh) } });
            return e;
        }

        static void Run(float seconds)
        {
            for (int i = 0; i < (int)(seconds * 60f); i++) SceneManager.UpdatePhysics(1f / 60f);
        }

        [Fact]
        public void A_ray_down_on_the_road_meets_the_road_and_one_beside_it_misses()
        {
            Entity road = Part(TrackData.RoadPart);
            SceneManager.Play();
            TrackFrame f = Track().FrameAt(100f);
            Assert.True(SceneManager.Raycast(f.Position + f.Right * 3f + Vector3.UnitY * 5f, -Vector3.UnitY, 10f, out Entity met, out RaycastHit hit));
            Assert.Equal(road, met);
            Assert.True(hit.Normal.Y > 0.99f, hit.Normal.ToString());
            Assert.False(SceneManager.Raycast(f.Position + f.Right * 7f + Vector3.UnitY * 5f, -Vector3.UnitY, 10f, out _, out _));
        }

        [Fact]
        public void A_box_on_the_road_rests_on_it_and_the_wall_stops_one_thrown_at_it()
        {
            Part(TrackData.RoadPart);
            Part(TrackData.WallsPart);
            TrackFrame f = Track().FrameAt(150f);
            Entity resting = Box(f.Position + Vector3.UnitY * 1f);
            Entity thrown = Box(f.Position + Vector3.UnitY * 0.4f);
            SceneManager.Play();
            Assert.True(SceneManager.AddImpulse(thrown, f.Right * 20f * 2f));      // 20 m/s towards the right-hand wall
            Run(3f);
            Vector3 rest = World.GetComponent<Transform>(resting).Position;
            Assert.InRange(rest.Y - f.Position.Y, 0.2f, 0.35f);
            float across = Vector3.Dot(World.GetComponent<Transform>(thrown).Position - f.Position, f.Right);
            Assert.InRange(across, 0f, 5f);                                    // still inside the right-hand edge

            static Entity Box(Vector3 at)
            {
                Entity e = SceneManager.ActiveScene!.CreateEntity("Box");
                var tr = World.GetComponent<Transform>(e);
                tr.Position = at;
                World.AddComponent(e, tr);
                var shape = ShapeEntry.Default();
                shape.HalfExtent = new Vector3(0.25f, 0.25f, 0.25f);
                World.AddComponent(e, new RigidbodyComponent { MotionType = RigidbodyMotionType.Dynamic, Mass = 2f, UseGravity = true, Friction = 0.6f, Shapes = new() { shape } });
                return e;
            }
        }

        [Fact]
        public void A_kart_driven_by_the_tracks_own_frames_goes_round_and_passes_every_checkpoint_in_order()
        {
            Part(TrackData.RoadPart);
            Part(TrackData.WallsPart);
            TrackData t = Track();
            TrackFrame start = t.FrameAt(0f);
            Entity kart = SceneManager.ActiveScene!.CreateEntity("Kart");
            var tr = World.GetComponent<Transform>(kart);
            tr.Position = start.Position + Vector3.UnitY * 0.8f;
            tr.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(start.Forward.X, start.Forward.Z));
            World.AddComponent(kart, tr);
            var shape = ShapeEntry.Default();
            shape.HalfExtent = new Vector3(0.5f, 0.2f, 0.8f);
            World.AddComponent(kart, new RigidbodyComponent { MotionType = RigidbodyMotionType.Dynamic, Mass = 150f, UseGravity = true, Friction = 0.5f, Shapes = new() { shape } });
            World.AddComponent(kart, KartComponent.Default);
            SceneManager.Play();

            int next = 1, passed = 0;
            float lastAlong = 0f;
            for (int frame = 0; frame < 60 * 60 && passed < t.Checkpoints.Count; frame++)
            {
                Transform now = World.GetComponent<Transform>(kart);
                float along = t.Nearest(now.Position, out float offset);
                // Aim at the centre line 8 m ahead: steer by the angle to it, a little throttle off in a bend.
                Vector3 aim = t.FrameAt(along + 8f).Position - now.Position;
                Vector3 fwd = Vector3.Transform(Vector3.UnitZ, now.Rotation);
                float side = Vector3.Dot(Vector3.Cross(fwd, Vector3.Normalize(new Vector3(aim.X, 0, aim.Z))), Vector3.UnitY);
                ref KartComponent k = ref World.GetComponent<KartComponent>(kart);
                k.Steer = Math.Clamp(-side * 3f, -1f, 1f);
                k.Throttle = MathF.Abs(k.Steer) > 0.6f ? 0.6f : 1f;
                SceneManager.UpdatePhysics(1f / 60f);
                // A checkpoint is passed when the distance along goes over it (the start line, last, after a lap).
                float cp = next < t.Checkpoints.Count ? t.Checkpoints[next].Distance : t.Length;
                bool wrapped = along < lastAlong - t.Length / 2f;
                if ((lastAlong < cp && along >= cp) || (next == t.Checkpoints.Count && wrapped)) { passed++; next++; }
                lastAlong = along;
                Assert.True(MathF.Abs(offset) < 5f, $"the kart left the road at {along:0} m: {offset:0.0} m across");
            }
            Assert.Equal(t.Checkpoints.Count, passed);
        }
    }
}
