using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer;
using GameEditor.Framework.Renderer.Server.Sprites2D;
using Xunit;
using static Sokol.SApp;

namespace Scene.Tests
{
    /// <summary>
    /// 2D, step G1.2: the editor camera's 2D view (what it shows, moving it and zooming about the pointer), and the
    /// geometry a sprite is picked and outlined by — the quad from the world matrix, the order sprites are drawn in,
    /// a ray against a quad.
    /// </summary>
    public sealed class SceneView2DTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        public SceneView2DTests() => World.Clear();
        public void Dispose() => World.Clear();

        static void Near(float want, float got, float tolerance = 1e-3f)
            => Assert.True(MathF.Abs(want - got) <= tolerance, $"want {want}, got {got}");

        static void Near(Vector2 want, Vector2 got, float tolerance = 1e-3f)
            => Assert.True((want - got).Length() <= tolerance, $"want {want}, got {got}");

        static void Near(Vector3 want, Vector3 got, float tolerance = 1e-3f)
            => Assert.True((want - got).Length() <= tolerance, $"want {want}, got {got}");

        // Where the camera's matrices put a world point, in view units from the view's top-left corner.
        static Vector2 Shown(ViewportCamera cam, Vector3 world, int width, int height)
        {
            Vector4 clip = Vector4.Transform(new Vector4(world, 1f), cam.ViewProj);
            return new Vector2((clip.X / clip.W * 0.5f + 0.5f) * width, (1f - (clip.Y / clip.W * 0.5f + 0.5f)) * height);
        }

        static ViewportCamera Camera2D(int width = 800, int height = 400)
        {
            var cam = new ViewportCamera { Is2D = true, Center2D = new Vector2(3f, -2f), OrthoSize = 5f };
            cam.Update(width, height);
            return cam;
        }

        // ── The camera ──────────────────────────────────────────────────────────────────

        [Fact]
        public void The_2D_view_shows_x_to_the_right_and_y_up_with_its_centre_in_the_middle()
        {
            var cam = Camera2D();
            Near(new Vector2(400f, 200f), Shown(cam, new Vector3(3f, -2f, 0f), 800, 400));
            Near(new Vector2(800f, 0f), Shown(cam, new Vector3(13f, 3f, 0f), 800, 400));      // 10 units to each side, 5 up and down
            Near(new Vector2(0f, 400f), Shown(cam, new Vector3(-7f, -7f, 0f), 800, 400));

            // No perspective: a point farther along z is at the same place, and both are inside the view's depth.
            Near(Shown(cam, new Vector3(5f, 1f, 0f), 800, 400), Shown(cam, new Vector3(5f, 1f, -300f), 800, 400));
            foreach (float z in new[] { -400f, 0f, 400f })
            {
                Vector4 clip = Vector4.Transform(new Vector4(5f, 1f, z, 1f), cam.ViewProj);
                Assert.InRange(clip.Z / clip.W, 0f, 1f);
            }
        }

        [Fact]
        public void A_view_point_and_its_world_point_agree_with_the_matrices()
        {
            var cam = Camera2D();
            Near(0.025f, cam.UnitsPerPixel, 1e-6f);                                       // 10 units over 400 view units
            foreach (var p in new[] { new Vector2(0f, 0f), new Vector2(123f, 77f), new Vector2(800f, 400f) })
            {
                Vector2 world = cam.ViewToWorld2D(p.X, p.Y);
                Near(p, cam.WorldToView2D(world));
                Near(p, Shown(cam, new Vector3(world, 0f), 800, 400));
            }
        }

        [Fact]
        public void Moving_the_view_keeps_the_world_point_under_the_pointer()
        {
            var cam = Camera2D();
            Vector2 under = cam.ViewToWorld2D(100f, 80f);
            cam.Pan2D(30f, -20f);
            cam.Update(800, 400);
            Near(new Vector2(130f, 60f), cam.WorldToView2D(under));
            Near(new Vector2(130f, 60f), Shown(cam, new Vector3(under, 0f), 800, 400));
        }

        [Theory]
        [InlineData(sapp_mousebutton.SAPP_MOUSEBUTTON_RIGHT)]
        [InlineData(sapp_mousebutton.SAPP_MOUSEBUTTON_MIDDLE)]
        public void A_drag_with_the_right_or_the_middle_button_moves_the_view_and_the_left_one_does_not(sapp_mousebutton button)
        {
            var cam = Camera2D();
            cam.PointerScale = 0.5f;                       // a display of scale 2: pointer positions are twice the view's units
            Vector2 under = cam.ViewToWorld2D(100f, 80f);
            cam.HandleMouseDown(button, 200f, 160f);
            Assert.True(cam.IsCapturing);
            cam.HandleMouseMove(260f, 120f);
            cam.HandleMouseUp(button);
            Assert.False(cam.IsCapturing);
            Near(new Vector2(130f, 60f), cam.WorldToView2D(under));

            Vector2 before = cam.Center2D;
            cam.HandleMouseDown(sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT, 200f, 160f);
            cam.HandleMouseMove(300f, 300f);
            cam.HandleMouseUp(sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT);
            Assert.Equal(before, cam.Center2D);
            cam.HandleMouseMove(500f, 10f);                // no button held
            Assert.Equal(before, cam.Center2D);
        }

        [Fact]
        public void The_wheel_zooms_about_the_pointer()
        {
            var cam = Camera2D();
            Vector2 under = cam.ViewToWorld2D(650f, 90f);
            cam.Zoom2D(3f, 650f, 90f);
            Near(5f * 0.9f * 0.9f * 0.9f, cam.OrthoSize, 1e-5f);
            Near(under, cam.ViewToWorld2D(650f, 90f));
            cam.Update(800, 400);
            Near(new Vector2(650f, 90f), Shown(cam, new Vector3(under, 0f), 800, 400), 0.01f);

            cam.Zoom2D(-3f, 650f, 90f);                    // and back out: the view it started from
            Near(5f, cam.OrthoSize, 1e-4f);
            Near(new Vector2(3f, -2f), cam.Center2D);
        }

        [Fact]
        public void The_2D_view_does_not_touch_the_orbit_view()
        {
            var cam = new ViewportCamera();
            cam.Init(new Vector3(1f, 2f, 3f), 7f, yaw: 0.4f, pitch: 0.3f);
            cam.Update(800, 400);
            Matrix4x4 view = cam.View, proj = cam.Proj;

            cam.Is2D = true;
            cam.Update(800, 400);
            Assert.NotEqual(view, cam.View);
            cam.HandleMouseDown(sapp_mousebutton.SAPP_MOUSEBUTTON_MIDDLE, 10f, 10f);
            cam.HandleMouseMove(90f, 50f);
            cam.HandleMouseUp(sapp_mousebutton.SAPP_MOUSEBUTTON_MIDDLE);
            cam.HandleScroll(4f);

            cam.Is2D = false;
            cam.Update(800, 400);
            Assert.Equal(view, cam.View);
            Assert.Equal(proj, cam.Proj);
            Assert.Equal(new Vector3(1f, 2f, 3f), cam.Center);
            Assert.Equal(7f, cam.Distance);
        }

        [Fact]
        public void A_rectangle_is_fitted_whole_into_the_view()
        {
            var cam = Camera2D();                                                         // twice as wide as high
            var (center, size) = cam.Fit2D(new Vector2(-1f, 2f), new Vector2(3f, 4f));    // 4 wide, 2 high
            Near(new Vector2(1f, 3f), center);
            Near(1.25f, size);                                                            // its half height (and half of half its width), and a quarter more
            (_, size) = cam.Fit2D(new Vector2(0f, 0f), new Vector2(40f, 2f));             // much wider than high: the width decides
            Near(40f * 0.5f / 2f * 1.25f, size);
        }

        // ── A sprite's quad, its place in the order, a ray ──────────────────────────────

        [Fact]
        public void A_child_sprites_quad_is_placed_by_its_parents()
        {
            var scene = new GameEditor.Framework.Scene.Scene("t");
            Entity parent = scene.CreateEntity("Parent");
            var pt = World.GetComponent<Transform>(parent);
            pt.Position = new Vector3(10f, 5f, 2f);
            pt.Scale = new Vector3(2f, 3f, 1f);
            pt.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);      // a quarter turn, anticlockwise
            World.AddComponent(parent, pt);
            Entity child = scene.CreateEntity("Child");
            var ct = World.GetComponent<Transform>(child);
            ct.Position = new Vector3(1f, 0f, 0f);
            ct.Parent = parent;
            World.AddComponent(child, ct);

            Matrix4x4 model = Transform.GetWorldMatrix(World, World.GetComponent<Transform>(child));
            SpriteGeometry.Quad(in model, new Vector2(0.64f, 0.32f), new Vector2(0.5f, 0.5f), out var origin, out var axisX, out var axisY);

            // The child's own x is the parent's: scaled by 2, then turned to point up. Its middle is at (10, 7, 2).
            Near(new Vector3(10f, 7f, 2f), origin + (axisX + axisY) * 0.5f);
            Near(new Vector3(0f, 1.28f, 0f), axisX);
            Near(new Vector3(-0.96f, 0f, 0f), axisY);

            // A pivot at the bottom-left puts that corner on the entity.
            SpriteGeometry.Quad(in model, new Vector2(0.64f, 0.32f), Vector2.Zero, out origin, out _, out _);
            Near(new Vector3(10f, 7f, 2f), origin);
        }

        [Fact]
        public void The_draw_key_orders_by_layer_then_order_then_distance()
        {
            var cam = Camera2D();
            Matrix4x4 vp = cam.ViewProj;
            ulong Key(int layer, int order, float z) => SpriteGeometry.DrawKey(layer, order, new Vector3(0f, 0f, z), in vp);

            Assert.True(Key(1, 0, -5f) > Key(0, 9, 5f));          // a layer is above every order of the layer below
            Assert.True(Key(0, 2, -5f) > Key(0, 1, 5f));          // an order above the nearer sprite of a lower order
            Assert.True(Key(0, 0, 1f) > Key(0, 0, -1f));          // of equals, the nearer (the camera is at +z)
            Assert.True(Key(-3, 0, 0f) < Key(0, -3, 0f));
        }

        [Fact]
        public void A_ray_meets_a_quad_inside_its_edges_and_in_front_only()
        {
            Vector3 origin = new(1f, 1f, 0f), axisX = new(2f, 0f, 0f), axisY = new(0f, 1f, 0f);
            Vector3 eye = new(0f, 0f, 10f), down = new(0f, 0f, -1f);

            Assert.True(SpriteGeometry.RayHitsQuad(eye + new Vector3(2f, 1.5f, 0f), down, origin, axisX, axisY, out float t));
            Near(10f, t);
            Assert.True(SpriteGeometry.RayHitsQuad(eye + new Vector3(1.01f, 1.99f, 0f), down, origin, axisX, axisY, out _));
            Assert.False(SpriteGeometry.RayHitsQuad(eye + new Vector3(3.1f, 1.5f, 0f), down, origin, axisX, axisY, out _));   // beside it
            Assert.False(SpriteGeometry.RayHitsQuad(eye + new Vector3(2f, 2.1f, 0f), down, origin, axisX, axisY, out _));     // above it
            Assert.False(SpriteGeometry.RayHitsQuad(eye + new Vector3(2f, 1.5f, 0f), -down, origin, axisX, axisY, out _));    // behind the ray
            Assert.False(SpriteGeometry.RayHitsQuad(eye, new Vector3(1f, 0f, 0f), origin, axisX, axisY, out _));              // along the quad

            // A quad turned a quarter: what was beside it is now on it.
            Vector3 turnedX = new(0f, 2f, 0f), turnedY = new(-1f, 0f, 0f);
            Assert.True(SpriteGeometry.RayHitsQuad(eye + new Vector3(0.5f, 2.5f, 0f), down, origin, turnedX, turnedY, out _));
            Assert.False(SpriteGeometry.RayHitsQuad(eye + new Vector3(2f, 1.5f, 0f), down, origin, turnedX, turnedY, out _));

            // A ray that is not straight on, as a perspective camera's.
            Vector3 slanted = Vector3.Normalize(new Vector3(2f, 1.5f, -10f));
            Assert.True(SpriteGeometry.RayHitsQuad(eye, slanted, origin, axisX, axisY, out t));
            Near((new Vector3(2f, 1.5f, 0f) - eye).Length(), t);
        }
    }
}
