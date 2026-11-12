using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// The engine's kart, on the real Jolt: it stands on its suspension, reaches its top speed and no more, stops and
    /// backs up, turns at its rate to the side it is steered to, and drives the same whatever the frame rate.
    /// </summary>
    public sealed class KartTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const float Step = 1f / 60f;

        public KartTests()
        {
            World.Clear();
            SceneManager.SimulatorFactory = null;
            SceneManager.NewScene("Track");
        }

        public void Dispose()
        {
            SceneManager.Stop();
            ScriptSystem.ClearFactories();
            World.Clear();
        }

        static Entity Body(string name, Vector3 at, RigidbodyMotionType type, float mass, Vector3 half)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity(name);
            var tr = World.GetComponent<Transform>(e);
            tr.Position = at;
            World.AddComponent(e, tr);
            var shape = ShapeEntry.Default();
            shape.HalfExtent = half;
            World.AddComponent(e, new RigidbodyComponent { MotionType = type, Mass = mass, UseGravity = true, Friction = 0.5f, Shapes = new() { shape } });
            return e;
        }

        static Entity Ground() => Body("Ground", new Vector3(0, -0.5f, 0), RigidbodyMotionType.Static, 0f, new Vector3(500, 0.5f, 500));

        static Entity Kart(Vector3 at)
        {
            Entity kart = Body("Kart", at, RigidbodyMotionType.Dynamic, 150f, new Vector3(0.5f, 0.2f, 0.8f));
            World.AddComponent(kart, KartComponent.Default);
            return kart;
        }

        static ref KartComponent K(Entity e) => ref World.GetComponent<KartComponent>(e);
        static Transform T(Entity e) => World.GetComponent<Transform>(e);

        static void Run(float seconds, float frame = Step)
        {
            int frames = (int)MathF.Round(seconds / frame);
            for (int i = 0; i < frames; i++) SceneManager.UpdatePhysics(frame);
        }

        [Fact]
        public void A_kart_stands_on_its_four_wheels_at_rest()
        {
            Ground();
            Entity kart = Kart(new Vector3(0, 1f, 0));
            SceneManager.Play();
            Run(3f);
            Transform t = T(kart);
            Assert.Equal(4, K(kart).WheelsDown);
            // The spring holds the weight where k * x = g: pressed 0.16 of its 0.5 m, the rays start 0.2 below the middle.
            float expected = 0.2f + 0.5f - 9.81f / 60f;
            Assert.InRange(t.Position.Y, expected - 0.03f, expected + 0.03f);
            Assert.True(Vector3.Transform(Vector3.UnitY, t.Rotation).Y > 0.999f, "upright");
            Assert.InRange(MathF.Abs(K(kart).Speed), 0f, 0.01f);
        }

        [Fact]
        public void Rays_that_start_inside_the_kart_pass_through_it_to_the_ground()
        {
            Ground();
            Entity kart = Kart(new Vector3(0, 1f, 0));
            var k = K(kart);
            k.WheelHeight = 0f;                                                  // from the kart's middle, inside its box
            k.Suspension = 0.7f;
            World.AddComponent(kart, k);
            SceneManager.Play();
            Run(3f);
            Assert.Equal(4, K(kart).WheelsDown);
            Assert.InRange(T(kart).Position.Y, 0.7f - 9.81f / 60f - 0.03f, 0.7f - 9.81f / 60f + 0.03f);
        }

        [Fact]
        public void The_throttle_reaches_the_top_speed_and_no_more()
        {
            Ground();
            Entity kart = Kart(new Vector3(0, 0.6f, 0));
            SceneManager.Play();
            Run(1f);
            K(kart).Throttle = 1f;
            float max = 0f, at15 = -1f;
            for (int i = 0; i < 240; i++)
            {
                Run(Step);
                max = MathF.Max(max, K(kart).Speed);
                if (at15 < 0f && K(kart).Speed >= 15f) at15 = (i + 1) * Step;
            }
            Assert.InRange(K(kart).Speed, 17.7f, 18.05f);
            Assert.True(max <= 18.1f, "never past the top speed: " + max);
            Assert.InRange(at15, 1.1f, 1.5f);                                    // 15 m/s at 12 m/s²: about 1.25 s
            Assert.True(T(kart).Position.Z > 40f, "it went forward (+Z): " + T(kart).Position);
            Assert.InRange(MathF.Abs(T(kart).Position.X), 0f, 0.05f);           // and straight
        }

        [Fact]
        public void The_brake_stops_it_in_its_distance_and_held_backs_it_up()
        {
            Ground();
            Entity kart = Kart(new Vector3(0, 0.6f, 0));
            SceneManager.Play();
            Run(1f);
            K(kart).Throttle = 1f;
            Run(3f);
            float from = T(kart).Position.Z, speed = K(kart).Speed;
            K(kart).Throttle = 0f;
            K(kart).Brake = 1f;
            int frames = 0;
            while (K(kart).Speed > 0.5f && frames++ < 600) Run(Step);
            float stopped = T(kart).Position.Z - from;
            float want = speed * speed / (2f * 24f);                             // v² / 2a
            Assert.InRange(stopped, want * 0.9f, want * 1.15f);
            Run(3f);
            Assert.InRange(K(kart).Speed, -6.05f, -5.7f);                       // backward at the reverse speed
        }

        [Fact]
        public void Steering_right_turns_it_right_at_its_rate()
        {
            Ground();
            Entity kart = Kart(new Vector3(0, 0.6f, 0));
            SceneManager.Play();
            Run(1f);
            K(kart).Throttle = 1f;
            Run(1f);
            K(kart).Throttle = 0.5f;
            K(kart).Steer = 1f;
            Vector3 before = Vector3.Transform(Vector3.UnitZ, T(kart).Rotation);
            Run(0.5f);
            Vector3 after = Vector3.Transform(Vector3.UnitZ, T(kart).Rotation);
            float turned = MathF.Acos(Math.Clamp(Vector3.Dot(before, after), -1f, 1f)) * 180f / MathF.PI;
            Assert.InRange(turned, 45f, 52f);                                   // 100 degrees a second, half a second
            Assert.True(after.X < 0f, "to the kart's right, which is -X for a kart that faces +Z: " + after);
            Assert.True(Vector3.Transform(Vector3.UnitY, T(kart).Rotation).Y > 0.98f, "and stays on its wheels");

            // Standing still it does not turn at all. (Rolled to a stop: the brake held on a kart that stands backs it up.)
            K(kart).Throttle = 0f;
            K(kart).Steer = 0f;
            Run(6f);
            Assert.InRange(K(kart).Speed, -0.05f, 0.05f);
            Quaternion standing = T(kart).Rotation;
            K(kart).Steer = 1f;
            Run(1f);
            Assert.True(MathF.Abs(Quaternion.Dot(standing, T(kart).Rotation)) > 0.9999f, "a kart standing still is not turned by its steering");
        }

        [Fact]
        public void It_drives_the_same_at_thirty_and_at_a_hundred_and_twenty_frames_a_second()
        {
            Vector3 Drive(float frame)
            {
                World.Clear();
                SceneManager.NewScene("Track");
                Ground();
                Entity kart = Kart(new Vector3(0, 0.6f, 0));
                SceneManager.Play();
                Run(1f, frame);
                K(kart).Throttle = 1f;
                K(kart).Steer = 0.4f;
                Run(2f, frame);
                Vector3 at = T(kart).Position;
                SceneManager.Stop();
                return at;
            }
            Vector3 slow = Drive(1f / 30f), fast = Drive(1f / 120f);
            Assert.True(Vector3.Distance(slow, fast) < 0.01f, $"{slow} and {fast}");
        }

        [Fact]
        public void The_Kart_block_round_trips_and_a_block_with_nothing_has_the_defaults()
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity("K");
            var k = KartComponent.Default;
            k.TopSpeed = 22f; k.Grip = 5.5f; k.WheelSpan = new Vector2(0.6f, 0.9f); k.Player = true; k.Throttle = 1f;
            World.AddComponent(e, k);
            string text = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            Assert.Contains("\"Kart\":{\"TopSpeed\":22,", text);
            Assert.DoesNotContain("Throttle", text);
            SceneSerializer.Deserialize(text, SceneManager.ActiveScene!);
            Entity back = System.Linq.Enumerable.First(World.Entities, x => World.GetComponent<NameTag>(x).Name == "K");
            var read = World.GetComponent<KartComponent>(back);
            Assert.Equal((22f, 5.5f, new Vector2(0.6f, 0.9f), true, 0f), (read.TopSpeed, read.Grip, read.WheelSpan, read.Player, read.Throttle));

            SceneSerializer.Deserialize(text.Replace(text.Substring(text.IndexOf("\"Kart\":{"), text.IndexOf('}', text.IndexOf("\"Kart\":{")) - text.IndexOf("\"Kart\":{") + 1), "\"Kart\":{}"), SceneManager.ActiveScene!);
            back = System.Linq.Enumerable.First(World.Entities, x => World.GetComponent<NameTag>(x).Name == "K");
            Assert.Equal(KartComponent.Default.TopSpeed, World.GetComponent<KartComponent>(back).TopSpeed);
            Assert.False(World.GetComponent<KartComponent>(back).Player);
        }
    }
}
