using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Input;
using GameEditor.Framework.Physics;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using GameEditor.Framework.UI;
using Sokol;
using Sokol.GUI;
using Xunit;
using static Sokol.SApp;
using Vector2 = System.Numerics.Vector2;

namespace Scene.Tests
{
    /// <summary>
    /// The controls of a kart game (H5 step K6): UI buttons that hold an axis or a button while a finger is on them — two
    /// fingers on two buttons at once — the automatic throttle, the chase camera and its look-back, the blocks.
    /// </summary>
    public sealed unsafe class KartControlsTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        public KartControlsTests()
        {
            World.Clear();
            GameUI.Clear();
            SceneManager.SimulatorFactory = null;
            SceneManager.NewScene("Controls");
            InputManager.SetScreenSize(1280, 720);
            InputManager.BeginFrame();
        }

        public void Dispose()
        {
            SceneManager.Stop();
            ScriptSystem.ClearFactories();
            GameUI.Clear();
            World.Clear();
            InputManager.SetScreenSize(0, 0);
            Lift(1); Lift(2);
            InputManager.BeginFrame();
        }

        static void Finger(sapp_event_type type, int id, float x, float y)
        {
            var e = new sapp_event { type = type, num_touches = 1 };
            e.touches[0].identifier = (nuint)id;
            e.touches[0].pos_x = x;
            e.touches[0].pos_y = y;
            e.touches[0].changed = true;
            InputManager.ProcessEvent(&e);
        }

        static void Lift(int id) => Finger(sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED, id, 0, 0);

        // A held button: 160 x 160 design units in a corner of the 1280 x 720 screen.
        static Entity Button(string name, UIAnchor anchor, string holds)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity(name);
            World.AddComponent(e, new UIElement { Anchor = anchor, Offset = new Vector2(20, 20), Size = new Vector2(160, 160), Visible = true });
            var b = UIButton.Default;
            b.Holds = holds;
            World.AddComponent(e, b);
            return e;
        }

        static void Frame()
        {
            InputManager.BeginFrame();
            GameUI.Sync(World, 1280, 720, Thickness.Zero);
        }

        [Fact]
        public void Two_fingers_hold_two_buttons_an_axis_and_a_button_at_once()
        {
            Button("Left", UIAnchor.BottomLeft, "Horizontal:-1");
            Button("Fire", UIAnchor.BottomRight, "Fire1");
            Frame();
            Assert.Equal(0f, InputManager.GetAxis("Horizontal"));
            Finger(sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN, 1, 100, 620);      // on Left (x 20..180, y 540..700)
            Finger(sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN, 2, 1180, 620);     // on Fire
            Frame();
            Assert.Equal(-1f, InputManager.GetAxis("Horizontal"));
            Assert.True(InputManager.GetButton("Fire1"));
            Assert.True(InputManager.GetButtonDown("Fire1"), "pressed in the first frame it is held");
            Frame();
            Assert.Equal(-1f, InputManager.GetAxis("Horizontal"));
            Assert.True(InputManager.GetButton("Fire1"));
            Assert.False(InputManager.GetButtonDown("Fire1"), "not pressed again while held");
            Lift(2);
            Frame();
            Assert.False(InputManager.GetButton("Fire1"));
            Assert.Equal(-1f, InputManager.GetAxis("Horizontal"));
            Lift(1);
            Frame();
            Assert.Equal(0f, InputManager.GetAxis("Horizontal"));
        }

        [Fact]
        public void Two_buttons_of_one_axis_held_together_cancel_and_a_finger_beside_holds_nothing()
        {
            Button("Left", UIAnchor.BottomLeft, "Horizontal:-1");
            Button("Right", UIAnchor.BottomRight, "Horizontal:1");
            Finger(sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN, 1, 100, 620);
            Finger(sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN, 2, 1180, 620);
            Frame();
            Assert.Equal(0f, InputManager.GetAxis("Horizontal"));
            Lift(1); Lift(2);
            Finger(sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN, 1, 640, 360);      // the middle of the screen
            Frame();
            Assert.Equal(0f, InputManager.GetAxis("Horizontal"));
        }

        static Entity Kart(bool auto)
        {
            Entity ground = SceneManager.ActiveScene!.CreateEntity("Ground");
            var gt = World.GetComponent<Transform>(ground);
            gt.Position = new Vector3(0, -0.5f, 0);
            World.AddComponent(ground, gt);
            var gs = ShapeEntry.Default();
            gs.HalfExtent = new Vector3(500, 0.5f, 500);
            World.AddComponent(ground, new RigidbodyComponent { MotionType = RigidbodyMotionType.Static, Shapes = new() { gs } });
            Entity kart = SceneManager.ActiveScene!.CreateEntity("Kart");
            var tr = World.GetComponent<Transform>(kart);
            tr.Position = new Vector3(0, 0.8f, 0);
            World.AddComponent(kart, tr);
            var shape = ShapeEntry.Default();
            shape.HalfExtent = new Vector3(0.5f, 0.2f, 0.8f);
            World.AddComponent(kart, new RigidbodyComponent { MotionType = RigidbodyMotionType.Dynamic, Mass = 150f, UseGravity = true, Friction = 0.5f, Shapes = new() { shape } });
            var k = KartComponent.Default;
            k.Player = true; k.AutoThrottle = auto;
            World.AddComponent(kart, k);
            return kart;
        }

        static void Run(float seconds)
        {
            for (int i = 0; i < (int)(seconds * 60f); i++) { InputManager.BeginFrame(); SceneManager.UpdatePhysics(1f / 60f); }
        }

        [Fact]
        public void The_automatic_throttle_drives_the_players_kart_with_no_key_and_the_brake_still_stops_it()
        {
            Entity kart = Kart(auto: true);
            SceneManager.Play();
            Run(3f);
            Assert.True(World.GetComponent<KartComponent>(kart).Speed > 15f, $"it drove off by itself: {World.GetComponent<KartComponent>(kart).Speed} m/s");
            for (int i = 0; i < 120; i++) { InputManager.BeginFrame(); InputManager.SetVirtualAxis("Vertical", -1f); SceneManager.UpdatePhysics(1f / 60f); }
            Assert.True(World.GetComponent<KartComponent>(kart).Speed < 1f, "the brake stops it");
        }

        [Fact]
        public void Without_it_the_players_kart_stands_until_a_key()
        {
            Entity kart = Kart(auto: false);
            SceneManager.Play();
            Run(2f);
            Assert.True(MathF.Abs(World.GetComponent<KartComponent>(kart).Speed) < 0.3f);
        }

        [Fact]
        public void The_chase_camera_follows_the_kart_from_behind_and_looks_back_while_its_button_is_held()
        {
            Entity kart = Kart(auto: true);
            Entity cam = SceneManager.ActiveScene!.CreateEntity("Camera");
            World.AddComponent(cam, new CameraComponent { Fov = 60, NearZ = 0.1f, FarZ = 500, IsMain = true });
            World.AddComponent(cam, ChaseCameraComponent.Default);
            SceneManager.Play();
            Run(3f);
            Vector3 k = World.GetComponent<Transform>(kart).Position, c = World.GetComponent<Transform>(cam).Position;
            Vector3 fwd = Vector3.Transform(Vector3.UnitZ, World.GetComponent<Transform>(kart).Rotation);
            Assert.InRange(Vector3.Dot(k - c, fwd), 4f, 9f);                           // about Distance behind it
            Assert.InRange(c.Y - k.Y, 1.5f, 3.5f);                                     // and Height above
            Vector3 look = Vector3.Transform(Vector3.UnitZ, World.GetComponent<Transform>(cam).Rotation);
            Assert.True(Vector3.Dot(look, fwd) > 0.8f, $"looking the kart's way: {look}");
            for (int i = 0; i < 3; i++) { InputManager.BeginFrame(); InputManager.SetVirtualButton("Fire2", true); SceneManager.UpdatePhysics(1f / 60f); }
            k = World.GetComponent<Transform>(kart).Position; c = World.GetComponent<Transform>(cam).Position;
            look = Vector3.Transform(Vector3.UnitZ, World.GetComponent<Transform>(cam).Rotation);
            Assert.True(Vector3.Dot(c - k, fwd) > 4f, "in front while looking back");
            Assert.True(Vector3.Dot(look, fwd) < -0.8f, "looking back");
        }

        [Fact]
        public void The_new_blocks_are_written_and_read_back()
        {
            Entity b = Button("Brake", UIAnchor.Bottom, "Vertical:-1");
            Entity map = SceneManager.ActiveScene!.CreateEntity("Map");
            World.AddComponent(map, new UIElement { Anchor = UIAnchor.TopRight, Size = new Vector2(200, 200), Visible = true });
            World.AddComponent(map, UIMiniMap.Default);
            Entity cam = SceneManager.ActiveScene!.CreateEntity("Camera");
            var cc = ChaseCameraComponent.Default;
            cc.Target = "You"; cc.Distance = 7.5f;
            World.AddComponent(cam, cc);
            Entity kart = SceneManager.ActiveScene!.CreateEntity("You");
            var k = KartComponent.Default; k.AutoThrottle = true;
            World.AddComponent(kart, k);
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            Assert.Contains("\"Holds\":\"Vertical:-1\"", json);
            Assert.Contains("\"UIMiniMap\":{", json);
            Assert.Contains("\"ChaseCamera\":{\"Target\":\"You\",\"Distance\":7.5", json);
            Assert.Contains("\"AutoThrottle\":true", json);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            string holds = "", target = ""; bool auto = false, hasMap = false;
            foreach (Entity e in World.Entities)
            {
                if (World.TryGetComponent<UIButton>(e, out var ub)) holds = ub.Holds;
                if (World.TryGetComponent<ChaseCameraComponent>(e, out var c)) target = c.Target;
                if (World.TryGetComponent<KartComponent>(e, out var kk)) auto = kk.AutoThrottle;
                hasMap |= World.HasComponent<UIMiniMap>(e);
            }
            Assert.Equal(("Vertical:-1", "You", true, true), (holds, target, auto, hasMap));
        }
    }
}
