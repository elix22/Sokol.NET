using System.Numerics;
using System.Text.Json;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// 2D, step G1.1: the Sprite block of a scene and a prefab, an image that moved, and the one function that
    /// reads a camera (orthographic, perspective, a camera that is a child).
    /// </summary>
    public sealed class Sprite2DTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const string Hero = "Textures/hero.png", BadgeKey = "Prefabs/Badge.prefab";

        readonly Dictionary<string, string> _files = new();

        public Sprite2DTests()
        {
            World.Clear();
            AssetDatabase.Clear();
            PrefabLibrary.Clear();
            PrefabLibrary.Reader = key => _files.TryGetValue(key, out string? text) ? text : null;
        }

        public void Dispose()
        {
            PrefabLibrary.Reader = null;
            PrefabLibrary.Clear();
            AssetDatabase.Clear();
            World.Clear();
        }

        static GameEditor.Framework.Scene.Scene NewScene()
        {
            World.Clear();
            return new GameEditor.Framework.Scene.Scene("t");
        }

        static GameEditor.Framework.Scene.Scene Load(string json)
        {
            var s = new GameEditor.Framework.Scene.Scene("x");
            SceneSerializer.Deserialize(json, s);
            return s;
        }

        static Entity Find(string name)
        {
            foreach (Entity e in World.Entities)
                if (World.GetComponent<NameTag>(e).Name == name) return e;
            throw new Xunit.Sdk.XunitException($"entity '{name}' not found");
        }

        static SpriteRenderer SpriteOf(string name) => World.GetComponent<SpriteRenderer>(Find(name));

        static SpriteRenderer Odd => new()
        {
            Texture = Hero, Color = new Vector4(0.25f, 0.5f, 0.75f, 0.5f), FlipX = true, FlipY = false, Layer = -2, Order = 7, Visible = false,
        };

        // ── The block ───────────────────────────────────────────────────────────────────

        [Fact]
        public void A_sprite_comes_back_from_a_scene_as_it_was_saved()
        {
            var s = NewScene();
            World.AddComponent(s.CreateEntity("Hero"), Odd);
            string saved = SceneSerializer.Serialize(s);
            Assert.Contains("\"Sprite\":{\"Tex\":\"Textures/hero.png\",\"Color\":[0.25,0.5,0.75,0.5],\"FlipX\":true,\"FlipY\":false,\"Layer\":-2,\"Order\":7,\"Vis\":false}", saved);

            var loaded = Load(saved);
            Assert.Equal(Odd, SpriteOf("Hero"));
            Assert.Equal(saved, SceneSerializer.Serialize(loaded));
        }

        [Fact]
        public void A_block_with_only_an_image_gets_the_values_a_new_sprite_has()
        {
            var s = NewScene();
            World.AddComponent(s.CreateEntity("Hero"), Odd);
            string saved = SceneSerializer.Serialize(s);
            int from = saved.IndexOf("\"Sprite\":{", StringComparison.Ordinal), to = saved.IndexOf('}', from);
            Load(saved.Substring(0, from) + "\"Sprite\":{\"Tex\":\"Textures/hero.png\"" + saved.Substring(to));
            var expected = SpriteRenderer.Default;
            expected.Texture = Hero;
            Assert.Equal(expected, SpriteOf("Hero"));
            Assert.Equal(Vector4.One, expected.Color);
            Assert.True(expected.Visible);
        }

        [Fact]
        public void A_scene_with_no_sprite_does_not_mention_one()
        {
            var s = NewScene();
            World.AddComponent(s.CreateEntity("Box"), new MeshRenderer { MeshPath = "prim:box", MaterialPath = "", Visible = true });
            Assert.DoesNotContain("Sprite", SceneSerializer.Serialize(s));
        }

        [Fact]
        public void Taking_the_sprite_off_an_entity_takes_its_block_out_of_the_scene()
        {
            var s = NewScene();
            Entity hero = s.CreateEntity("Hero");
            World.AddComponent(hero, Odd);
            World.RemoveComponent<SpriteRenderer>(hero);
            Assert.DoesNotContain("Sprite", SceneSerializer.Serialize(s));
        }

        // ── In a prefab ─────────────────────────────────────────────────────────────────

        static string BadgePrefab(string image)
        {
            var s = NewScene();
            var sprite = SpriteRenderer.Default;
            sprite.Texture = image;
            sprite.Order = 3;
            World.AddComponent(s.CreateEntity("Badge"), sprite);
            s.Name = "Badge";
            s.Blocks["prefab"] = "1";
            string text = SceneSerializer.Serialize(s);
            World.Clear();
            return text;
        }

        [Fact]
        public void An_instance_that_changed_only_the_colour_stores_only_the_colour_and_follows_its_prefab_in_the_rest()
        {
            _files[BadgeKey] = BadgePrefab(Hero);
            var s = NewScene();
            Entity root = SceneSerializer.InstantiatePrefab(BadgeKey, out string? error);
            Assert.Null(error);
            var sprite = World.GetComponent<SpriteRenderer>(root);
            sprite.Color = new Vector4(1f, 0f, 0f, 1f);
            World.AddComponent(root, sprite);

            string overrides = JsonDocument.Parse(SceneSerializer.DescribePrefabLink(root)!).RootElement.GetProperty("overrides").GetRawText();
            Assert.Equal("{\"Sprite\":{\"Color\":[1,0,0,1]}}", overrides);
            string saved = SceneSerializer.Serialize(s);
            Assert.DoesNotContain(Hero, saved);                    // the image is the prefab's to name

            // The prefab's file gets another image and another order: the instance takes both and keeps its colour.
            _files[BadgeKey] = BadgePrefab("Textures/star.png").Replace("\"Order\":3", "\"Order\":9");
            PrefabLibrary.Clear();
            Load(saved);
            var after = SpriteOf("Badge");
            Assert.Equal("Textures/star.png", after.Texture);
            Assert.Equal(9, after.Order);
            Assert.Equal(new Vector4(1f, 0f, 0f, 1f), after.Color);
        }

        // ── A moved image ───────────────────────────────────────────────────────────────

        [Fact]
        public void A_sprite_finds_its_image_after_the_file_moved_and_the_frame_name_stays()
        {
            AssetDatabase.Set(1.ToString("x32"), "Assets/" + Hero);
            AssetDatabase.Set(2.ToString("x32"), "Assets/Sprites/tiles.sprites");
            var s = NewScene();
            var whole = SpriteRenderer.Default; whole.Texture = Hero;
            var frame = SpriteRenderer.Default; frame.Texture = "Sprites/tiles.sprites#grass_3";
            World.AddComponent(s.CreateEntity("Hero"), whole);
            World.AddComponent(s.CreateEntity("Tile"), frame);
            string saved = AssetRefs.Stamp(SceneSerializer.Serialize(s));
            Assert.Equal(2, AssetRefs.ReadTable(saved)!.Count);

            AssetDatabase.Rename("Assets/" + Hero, "Assets/Art/Characters/hero.png");
            AssetDatabase.Rename("Assets/Sprites/tiles.sprites", "Assets/Art/tiles.sprites");

            var loaded = Load(saved);
            Assert.Equal("Art/Characters/hero.png", SpriteOf("Hero").Texture);
            Assert.Equal("Art/tiles.sprites#grass_3", SpriteOf("Tile").Texture);
            string again = AssetRefs.Stamp(SceneSerializer.Serialize(loaded));
            Assert.DoesNotContain(Hero, again);
            Assert.Equal(again, AssetRefs.Stamp(SceneSerializer.Serialize(Load(again))));
        }

        // ── The camera ──────────────────────────────────────────────────────────────────

        static Vector3 Project(Vector3 world, Matrix4x4 view, Matrix4x4 proj)
        {
            Vector4 clip = Vector4.Transform(new Vector4(world, 1f), view * proj);
            return new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
        }

        static void Near(float expected, float actual) => Assert.InRange(actual, expected - 1e-4f, expected + 1e-4f);

        // A 2D scene's camera: at z = 10, turned half round about Y, so that it looks at the XY plane.
        static (Transform tr, CameraComponent cam) Camera2D(float size)
        {
            var tr = Transform.Default;
            tr.Position = new Vector3(0f, 0f, 10f);
            tr.Rotation = Quaternion.CreateFromYawPitchRoll(MathF.PI, 0f, 0f);
            return (tr, new CameraComponent { NearZ = 0.1f, FarZ = 100f, Fov = 60f, IsMain = true, IsOrthographic = true, OrthoSize = size, Enabled = true });
        }

        [Fact]
        public void A_pixel_of_the_picture_is_a_world_point_on_the_plane_asked_for()
        {
            // A 2D camera of size 5 at (3, 1, 10) in a picture of 800 x 400: 10 units to each side, 5 up and down.
            var s = NewScene();
            var (tr, cam) = Camera2D(5f);
            tr.Position = new Vector3(3f, 1f, 10f);
            Entity camera = s.CreateEntity("Cam");
            World.AddComponent(camera, tr);
            World.AddComponent(camera, cam);
            GameEditor.Framework.Input.InputManager.SetScreenSize(800f, 400f);
            try
            {
                Vector3 middle = SceneManager.ScreenToWorld(new Vector2(400f, 200f));
                Near(3f, middle.X); Near(1f, middle.Y); Near(0f, middle.Z);
                Vector3 topLeft = SceneManager.ScreenToWorld(new Vector2(0f, 0f));          // y down in the picture, up in the world
                Near(-7f, topLeft.X); Near(6f, topLeft.Y);
                Vector3 lowRight = SceneManager.ScreenToWorld(new Vector2(800f, 400f), z: -2f);
                Near(13f, lowRight.X); Near(-4f, lowRight.Y); Near(-2f, lowRight.Z);

                // A perspective camera: the point where the ray through the pixel meets the plane.
                tr.Position = new Vector3(0f, 0f, 10f);
                World.AddComponent(camera, tr);
                World.AddComponent(camera, new CameraComponent { NearZ = 0.1f, FarZ = 100f, Fov = 90f, IsMain = true, Enabled = true });
                Vector3 centre = SceneManager.ScreenToWorld(new Vector2(400f, 200f));
                Near(0f, centre.X); Near(0f, centre.Y);
                Vector3 top = SceneManager.ScreenToWorld(new Vector2(400f, 0f));              // 45 degrees up, 10 away: 10 high
                Assert.Equal(10f, MathF.Abs(top.Y), 2);
                Near(0f, top.Z);

                World.Clear();
                Assert.Equal(new Vector3(0f, 0f, 4f), SceneManager.ScreenToWorld(new Vector2(1f, 1f), 4f));       // no camera
            }
            finally { GameEditor.Framework.Input.InputManager.SetScreenSize(0f, 0f); }
        }

        [Fact]
        public void An_orthographic_camera_shows_its_size_above_and_below_and_x_to_the_right()
        {
            var (tr, cam) = Camera2D(5f);
            SceneManager.GetCameraMatrices(World, tr, cam, 800, 400, out var view, out var proj, out var eye);
            Assert.Equal(new Vector3(0f, 0f, 10f), eye);

            Vector3 topRight = Project(new Vector3(10f, 5f, 0f), view, proj);    // 800 × 400: 10 units to each side
            Near(1f, topRight.X); Near(1f, topRight.Y);
            Vector3 bottomLeft = Project(new Vector3(-10f, -5f, 0f), view, proj);
            Near(-1f, bottomLeft.X); Near(-1f, bottomLeft.Y);

            // No perspective: a point farther away is at the same place in the picture, and deeper.
            Vector3 near = Project(new Vector3(3f, 2f, 0f), view, proj), far = Project(new Vector3(3f, 2f, -40f), view, proj);
            Near(near.X, far.X); Near(near.Y, far.Y);
            Assert.True(far.Z > near.Z);
        }

        [Fact]
        public void A_perspective_camera_is_read_as_before()
        {
            var tr = Transform.Default;
            tr.Position = new Vector3(1f, 3f, -9f);
            tr.Rotation = Quaternion.CreateFromYawPitchRoll(0.3f, 0.2f, 0f);
            var cam = new CameraComponent { NearZ = 0.1f, FarZ = 500f, Fov = 55f, IsMain = true, Enabled = true };
            SceneManager.GetCameraMatrices(World, tr, cam, 1280, 720, out var view, out var proj, out _);

            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(tr.Rotation);
            Vector3 forward = new(rot.M31, rot.M32, rot.M33), up = new(rot.M21, rot.M22, rot.M23);
            Assert.Equal(Matrix4x4.CreateLookAt(tr.Position, tr.Position + forward, up), view);
            Assert.Equal(Matrix4x4.CreatePerspectiveFieldOfView(55f * MathF.PI / 180f, 1280f / 720f, 0.1f, 500f), proj);
        }

        [Fact]
        public void A_camera_that_is_a_child_follows_its_parent()
        {
            var s = NewScene();
            Entity player = s.CreateEntity("Player");
            var pt = World.GetComponent<Transform>(player);
            pt.Position = new Vector3(7f, -2f, 0f);
            World.AddComponent(player, pt);
            Entity camE = s.CreateEntity("Cam");
            var (tr, cam) = Camera2D(5f);
            tr.Parent = player;
            World.AddComponent(camE, tr);
            World.AddComponent(camE, cam);

            Assert.True(SceneManager.GetMainCamera(800, 400, out var view, out var proj, out var found, out var eye));
            Assert.True(found.IsOrthographic);
            Assert.Equal(new Vector3(7f, -2f, 10f), eye);
            Vector3 centre = Project(new Vector3(7f, -2f, 0f), view, proj);       // the player is in the middle of the picture
            Near(0f, centre.X); Near(0f, centre.Y);
        }

        [Fact]
        public void The_main_camera_is_the_first_active_one_marked_main()
        {
            var s = NewScene();
            Entity off = s.CreateEntity("Off");
            var (tr, cam) = Camera2D(5f);
            World.AddComponent(off, tr);
            World.AddComponent(off, cam);
            World.AddComponent(off, new ActiveFlag { Active = false });
            Entity on = s.CreateEntity("On");
            tr.Position = new Vector3(0f, 0f, 20f);
            cam.OrthoSize = 8f;
            World.AddComponent(on, tr);
            World.AddComponent(on, cam);

            Assert.True(SceneManager.GetMainCamera(100, 100, out _, out _, out var found, out var eye));
            Assert.Equal(8f, found.OrthoSize);
            Assert.Equal(20f, eye.Z);
            Assert.False(SceneManager.GetMainCamera(0, 100, out _, out _, out _, out _));
        }
    }
}
