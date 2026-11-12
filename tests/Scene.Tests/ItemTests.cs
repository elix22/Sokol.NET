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
    /// The race's items (H5 step K5), on the real Jolt: the draw by place, a box that gives an item and comes back, the boost,
    /// the puddle, the bolt (a hop and no drive — and no turn), the guard, the protection after a hit.
    /// </summary>
    public sealed class ItemTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const string Key = "Tracks/items.track";
        const string Loop = "{\"points\": [[0,0,0],[0,0,60],[30,0,90],[60,0,60],[60,0,0],[30,0,-30]], \"width\": 12, " +
                            "\"wall\": {\"height\": 1, \"thickness\": 0.5}, \"checkpointEvery\": 25}";

        public ItemTests()
        {
            World.Clear();
            SceneManager.SimulatorFactory = null;
            SceneManager.NewScene("Items");
            TrackLibrary.Clear();
            TrackLibrary.Reader = key => key == Key ? Loop : null;
            Race.Reset(); DriverSystem.Reset(); ItemSystem.Reset();
        }

        public void Dispose()
        {
            SceneManager.Stop();
            ScriptSystem.ClearFactories();
            World.Clear();
            TrackLibrary.Reader = null;
            TrackLibrary.Clear();
            Race.Reset(); DriverSystem.Reset(); ItemSystem.Reset();
        }

        static TrackData T => TrackLibrary.Track(Key)!;

        static void Scene()
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
            r.Laps = 3; r.Countdown = 0f;
            World.AddComponent(race, r);
        }

        static Entity Kart(string name, float along, float across)
        {
            TrackFrame f = T.FrameAt(along);
            Entity kart = SceneManager.ActiveScene!.CreateEntity(name);
            var tr = World.GetComponent<Transform>(kart);
            tr.Position = f.Position + f.Right * across + Vector3.UnitY * 0.8f;
            tr.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(f.Forward.X, f.Forward.Z));
            World.AddComponent(kart, tr);
            var shape = ShapeEntry.Default();
            shape.HalfExtent = new Vector3(0.5f, 0.2f, 0.8f);
            World.AddComponent(kart, new RigidbodyComponent { MotionType = RigidbodyMotionType.Dynamic, Mass = 150f, UseGravity = true, Friction = 0.5f, Shapes = new() { shape } });
            World.AddComponent(kart, KartComponent.Default);
            World.AddComponent(kart, new RacerComponent { Name = name });
            return kart;
        }

        // Each kart aims at the centre line (plus its offset) 8 m ahead with the throttle it is given.
        static void Drive(float seconds, params (Entity kart, float power, float across)[] karts)
        {
            for (int f = 0; f < (int)(seconds * 60f); f++)
            {
                foreach (var (kart, power, across) in karts)
                {
                    Transform now = World.GetComponent<Transform>(kart);
                    float along = T.Nearest(now.Position, out _);
                    TrackFrame aim = T.FrameAt(along + 8f);
                    Vector3 to = aim.Position + aim.Right * across - now.Position;
                    Vector3 fwd = Vector3.Transform(Vector3.UnitZ, now.Rotation);
                    float side = Vector3.Cross(fwd, Vector3.Normalize(new Vector3(to.X, 0, to.Z))).Y;
                    ref KartComponent k = ref World.GetComponent<KartComponent>(kart);
                    k.Steer = Math.Clamp(-side * 3f, -1f, 1f);
                    k.Throttle = power;
                }
                SceneManager.UpdatePhysics(1f / 60f);
            }
            SceneManager.FlushDestroyed();
        }

        static float Heading(Entity kart)
        {
            Vector3 f = Vector3.Transform(Vector3.UnitZ, World.GetComponent<Transform>(kart).Rotation);
            return MathF.Atan2(f.X, f.Z) * 180f / MathF.PI;
        }

        [Fact]
        public void The_draw_follows_the_table_of_each_place_bucket()
        {
            var counts = new Dictionary<(int, ItemKind), int>();
            for (int bucket = 0; bucket < 3; bucket++)
                for (int i = 0; i < 10000; i++)
                {
                    var k = ItemSystem.Draw(bucket, (i + 0.5f) / 10000f);
                    counts[(bucket, k)] = counts.GetValueOrDefault((bucket, k)) + 1;
                }
            Assert.Equal(0, counts.GetValueOrDefault((0, ItemKind.Bolt)));           // the leader gets no bolt
            Assert.InRange(counts[(0, ItemKind.Puddle)], 3900, 4100);
            Assert.InRange(counts[(2, ItemKind.Boost)], 4900, 5100);
            Assert.InRange(counts[(1, ItemKind.Bolt)], 2900, 3100);
            Assert.Equal((0, 1, 2, 1), (ItemSystem.Bucket(1, 4), ItemSystem.Bucket(2, 4), ItemSystem.Bucket(4, 4), ItemSystem.Bucket(3, 4)));
        }

        [Fact]
        public void A_box_gives_an_item_to_a_racer_with_an_empty_slot_and_comes_back()
        {
            Scene();
            TrackFrame at = T.FrameAt(30f);
            Entity box = SceneManager.ActiveScene!.CreateEntity("Box");
            var tr = World.GetComponent<Transform>(box);
            tr.Position = at.Position + Vector3.UnitY * 0.6f;
            World.AddComponent(box, tr);
            World.AddComponent(box, new MeshRenderer { MeshPath = "prim:box", Visible = true });
            World.AddComponent(box, ItemBoxComponent.Default);
            Entity kart = Kart("A", 0f, 0f);
            SceneManager.Play();
            Drive(3.5f, (kart, 1f, 0f));
            Assert.NotEqual(ItemKind.None, Race.Of(kart)!.Item);
            Assert.False(World.GetComponent<MeshRenderer>(box).Visible, "the box is hidden after it gave its item");
            Drive(3f, (kart, 1f, 0f));
            Assert.True(World.GetComponent<MeshRenderer>(box).Visible, "and back after its Respawn");
        }

        [Fact]
        public void A_boost_takes_a_kart_past_its_top_speed_for_a_while()
        {
            Scene();
            Entity kart = Kart("A", 0f, 0f);
            SceneManager.Play();
            Drive(3f, (kart, 1f, 0f));                                  // up to its top speed on the first straight
            float top = World.GetComponent<KartComponent>(kart).Speed;
            Assert.True(Race.GiveItem(kart, ItemKind.Boost) && Race.UseItem(kart));
            float best = 0f;
            for (int i = 0; i < 12; i++) { Drive(0.1f, (kart, 1f, 0f)); best = MathF.Max(best, World.GetComponent<KartComponent>(kart).Speed); }
            Assert.True(best > top * 1.25f, $"boosted to {best}, top {top}");
            Assert.Equal(ItemKind.None, Race.Of(kart)!.Item);
            Drive(4f, (kart, 1f, 0f));
            Assert.True(World.GetComponent<KartComponent>(kart).Speed < top * 1.05f, "back to its top speed after");
        }

        [Fact]
        public void A_puddle_slows_the_kart_that_drives_into_it_and_does_not_turn_it()
        {
            Scene();
            Entity front = Kart("Front", 6f, 0f), back = Kart("Back", 0f, 0f);
            SceneManager.Play();
            Drive(0.3f, (front, 0f, 0f), (back, 0f, 0f));     // the race begins on the first frame of play
            Assert.True(Race.GiveItem(front, ItemKind.Puddle) && Race.UseItem(front));
            Drive(0.2f, (front, 0.6f, 0f), (back, 0f, 0f));
            int hits = 0; float slowest = float.MaxValue;
            for (int i = 0; i < 40 && hits == 0; i++) { Drive(0.1f, (front, 0.6f, 0f), (back, 1f, 0f)); hits = Race.Of(back)!.Hits; }
            Assert.Equal(1, hits);
            Assert.True(Race.Of(back)!.Slowed > 0f);
            Assert.True(World.GetComponent<KartComponent>(back).DriveScale < 0.5f && World.GetComponent<KartComponent>(back).GripScale < 0.6f);
        }

        [Fact]
        public void A_bolt_makes_the_kart_ahead_hop_and_lose_drive_and_does_not_turn_it()
        {
            Scene();
            Entity shooter = Kart("Shooter", 0f, 0f), target = Kart("Target", 14f, 0f);
            SceneManager.Play();
            Drive(0.3f, (shooter, 0f, 0f), (target, 0f, 0f));
            float before = Heading(target);
            Assert.True(Race.GiveItem(shooter, ItemKind.Bolt) && Race.UseItem(shooter));
            float up = 0f;
            for (int i = 0; i < 30 && Race.Of(target)!.Hits == 0; i++) Drive(0.05f, (shooter, 0f, 0f), (target, 0f, 0f));
            Assert.Equal(1, Race.Of(target)!.Hits);
            Assert.True(World.GetComponent<KartComponent>(target).Stunned);
            // Hands off the hit kart (no steer, no throttle): the largest turn while it hops and rolls on.
            float turned = 0f;
            for (int i = 0; i < 30; i++)
            {
                Drive(1f / 60f, (shooter, 0f, 0f));
                ref KartComponent tk = ref World.GetComponent<KartComponent>(target);
                tk.Steer = 0f; tk.Throttle = 0f;
                SceneManager.TryGetLinearVelocity(target, out Vector3 v);
                up = MathF.Max(up, v.Y);
                turned = MathF.Max(turned, MathF.Abs(((Heading(target) - before) + 540f) % 360f - 180f));
            }
            Assert.True(up > 1f, $"it hopped: {up} m/s up");
            Assert.True(turned < 5f, $"turned {turned} degrees");
            Assert.Equal(0, Race.Of(shooter)!.Hits);
        }

        [Fact]
        public void A_guard_takes_one_hit_and_protection_ignores_a_second()
        {
            Scene();
            Entity shooter = Kart("Shooter", 0f, 0f), target = Kart("Target", 14f, 0f);
            SceneManager.Play();
            Drive(0.3f, (shooter, 0f, 0f), (target, 0f, 0f));
            Race.GiveItem(target, ItemKind.Guard); Race.UseItem(target);
            Race.GiveItem(shooter, ItemKind.Bolt); Race.UseItem(shooter);
            Drive(1f, (shooter, 0f, 0f), (target, 0f, 0f));
            Assert.Equal(0, Race.Of(target)!.Hits);
            Assert.Equal(0f, Race.Of(target)!.Guarded);
            Assert.True(Race.Of(target)!.Protected > 0f, "protected after the guard's block");
            Race.GiveItem(shooter, ItemKind.Bolt); Race.UseItem(shooter);         // within the protection
            Drive(0.3f, (shooter, 0f, 0f), (target, 0f, 0f));
            Assert.Equal(0, Race.Of(target)!.Hits);
            Drive(1.5f, (shooter, 0f, 0f), (target, 0f, 0f));
            Race.GiveItem(shooter, ItemKind.Bolt); Race.UseItem(shooter);         // after it: a hit
            Drive(1f, (shooter, 0f, 0f), (target, 0f, 0f));
            Assert.Equal(1, Race.Of(target)!.Hits);
        }

        [Fact]
        public void The_item_box_block_is_written_and_read_back()
        {
            Entity box = SceneManager.ActiveScene!.CreateEntity("Box");
            World.AddComponent(box, new ItemBoxComponent { Respawn = 4.5f });
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            Assert.Contains("\"ItemBox\":{\"Respawn\":4.5}", json);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            float back = 0f;
            foreach (Entity e in World.Entities) if (World.TryGetComponent<ItemBoxComponent>(e, out var b)) back = b.Respawn;
            Assert.Equal(4.5f, back);
        }
    }
}
