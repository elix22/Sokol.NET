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
    /// 2D, step G1.5: the Body2D block, the walls of a solid tile layer, and the Box2D world itself — bodies that
    /// fall and rest where the numbers say, a body moved by its Transform, triggers, the calls a script makes on
    /// its body, rays and overlaps. The world is the real one: the tests copy Box2D's library beside them.
    /// </summary>
    public sealed class Physics2DTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const float Step = Physics2DWorld.FixedStep;

        sealed class NoPhysics : ISceneSimulator
        {
            public void Start(ECSWorld world) { }
            public void Step(float dt) { }
            public void Stop() { }
        }

        // What the scripts were told, in order: "enter Ball>Ground", "trigger-enter Zone>Ball", …
        static readonly List<string> Told = new();
        static int Stays;

        public sealed class Listener : GameBehaviour
        {
            string Me => World.GetComponent<NameTag>(EntityId).Name;
            static string Name(Entity e) => ECSWorld.Instance.GetComponent<NameTag>(e).Name;
            public override void OnCollisionEnter(Entity other) => Told.Add($"enter {Me}>{Name(other)}");
            public override void OnCollisionExit(Entity other) => Told.Add($"exit {Me}>{Name(other)}");
            public override void OnCollisionStay(Entity other) => Stays++;
            public override void OnTriggerEnter(Entity other) => Told.Add($"trigger-enter {Me}>{Name(other)}");
            public override void OnTriggerExit(Entity other) => Told.Add($"trigger-exit {Me}>{Name(other)}");
        }

        readonly Dictionary<string, string> _maps = new();

        public Physics2DTests()
        {
            World.Clear();
            TilemapLibrary.Clear();
            TilemapLibrary.Reader = key => _maps.TryGetValue(key, out string? text) ? text : null;
            SceneManager.SimulatorFactory = _ => new NoPhysics();
            ScriptSystem.RegisterType<Listener>();
            Told.Clear();
            Stays = 0;
            SceneManager.NewScene("Flat");
            SceneManager.ActiveScene!.PhysicsBackend = "none";   // no 3D world: any name but "jolt" is the stand-in above
        }

        public void Dispose()
        {
            SceneManager.Stop();
            SceneManager.SimulatorFactory = null;
            ScriptSystem.ClearFactories();
            TilemapLibrary.Reader = null;
            TilemapLibrary.Clear();
            World.Clear();
        }

        static Entity Make(string name, Vector3 at, Body2D body, bool listens = false, Vector3? scale = null)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity(name);
            var tr = World.GetComponent<Transform>(e);
            tr.Position = at;
            if (scale is { } s) tr.Scale = s;
            World.AddComponent(e, tr);
            World.AddComponent(e, body);
            if (listens) World.AddComponent(e, new ScriptComponent { TypeName = nameof(Listener) });
            return e;
        }

        static Body2D Box(Body2DType type, float w, float h, bool trigger = false)
        {
            Body2D b = Body2D.Default;
            b.Type = type; b.IsTrigger = trigger;
            b.Shapes = new[] { new Shape2D { Type = Shape2DType.Box, Size = new Vector2(w, h) } };
            return b;
        }

        static Body2D Circle(float radius)
        {
            Body2D b = Body2D.Default;
            b.Shapes = new[] { new Shape2D { Type = Shape2DType.Circle, Radius = radius } };
            return b;
        }

        static void Run(float seconds)
        {
            int steps = (int)MathF.Round(seconds / Step);
            for (int i = 0; i < steps; i++) { SceneManager.UpdatePhysics(Step); ScriptSystem.UpdateAll(Step); }
        }

        static Vector3 At(Entity e) => World.GetComponent<Transform>(e).Position;

        // ── The block ───────────────────────────────────────────────────────────────────

        [Fact]
        public void The_Body2D_block_round_trips_with_each_kind_of_shape()
        {
            var scene = SceneManager.ActiveScene!;
            Entity e = scene.CreateEntity("Crate");
            World.AddComponent(e, new Body2D
            {
                Type = Body2DType.Kinematic, GravityScale = 0.5f, LinearDamping = 0.1f, AngularDamping = 0.2f, FixedRotation = true, Bullet = true,
                Density = 2f, Friction = 0.3f, Restitution = 0.4f, IsTrigger = true, Layer = 2, Mask = 5,
                Shapes = new[]
                {
                    new Shape2D { Type = Shape2DType.Box, Size = new Vector2(1f, 2f), Offset = new Vector2(0f, 0.5f), Angle = 15f },
                    new Shape2D { Type = Shape2DType.Circle, Radius = 0.5f, Offset = new Vector2(0f, 1f) },
                    new Shape2D { Type = Shape2DType.Capsule, Size = new Vector2(0.5f, 1.5f) },
                    new Shape2D { Type = Shape2DType.Polygon, Points = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) } },
                },
            });
            string json = SceneSerializer.Serialize(scene);
            Assert.Contains("\"Body2D\":{\"Type\":\"Kinematic\",\"GravityScale\":0.5,\"LinearDamping\":0.1,\"AngularDamping\":0.2,\"FixedRotation\":true,\"Bullet\":true," +
                            "\"Density\":2,\"Friction\":0.3,\"Restitution\":0.4,\"IsTrigger\":true,\"Layer\":2,\"Mask\":5,\"Shapes\":[" +
                            "{\"Type\":\"Box\",\"Size\":[1,2],\"Offset\":[0,0.5],\"Angle\":15}," +
                            "{\"Type\":\"Circle\",\"Radius\":0.5,\"Offset\":[0,1]}," +
                            "{\"Type\":\"Capsule\",\"Size\":[0.5,1.5],\"Offset\":[0,0],\"Angle\":0}," +
                            "{\"Type\":\"Polygon\",\"Points\":[[0,0],[1,0],[0,1]],\"Offset\":[0,0],\"Angle\":0}]}", json);
            var loaded = new GameEditor.Framework.Scene.Scene("x");
            SceneSerializer.Deserialize(json, loaded);
            Assert.Equal(json, SceneSerializer.Serialize(loaded));
        }

        [Fact]
        public void A_Body2D_block_with_no_members_is_a_dynamic_body_with_no_shape_of_its_own()
        {
            var loaded = new GameEditor.Framework.Scene.Scene("x");
            SceneSerializer.Deserialize("""{"name":"s","entities":[{"id":1,"name":"A","c":{"Body2D":{}}}]}""", loaded);
            foreach (Entity e in World.Entities)
            {
                Body2D b = World.GetComponent<Body2D>(e);
                Assert.Equal((Body2DType.Dynamic, 1f, 1f, 0.6f, 0f, false, 1, 65535), (b.Type, b.GravityScale, b.Density, b.Friction, b.Restitution, b.IsTrigger, b.Layer, b.Mask));
                Assert.Null(b.Shapes);
            }
        }

        [Fact]
        public void The_outlines_of_a_bodys_shapes_are_where_the_world_makes_them()
        {
            Body2D body = Body2D.Default;
            body.Shapes = new[]
            {
                new Shape2D { Type = Shape2DType.Box, Size = new Vector2(2f, 1f), Offset = new Vector2(1f, 0f) },
                new Shape2D { Type = Shape2DType.Circle, Radius = 0.5f },
                new Shape2D { Type = Shape2DType.Polygon, Points = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) } },
            };
            Entity e = Make("Shaped", new Vector3(10f, 5f, 0f), body, scale: new Vector3(2f, 2f, 1f));
            ref Transform tr = ref World.GetComponent<Transform>(e);
            tr.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);       // a quarter turn: x becomes y

            var points = new List<Vector2>();
            var loops = new List<int>();
            Physics2DWorld.ShapeOutlines(World, e, points, loops, segments: 8);
            Assert.Equal(new[] { 4, 8, 3 }, loops);
            // The box: 4 x 2 at scale 2, its middle 2 along the entity's x — which points up. Its first corner
            // (-half, -half) of the unturned box is at local (0, -1): in the world (10 + 1, 5 + 0).
            Assert.Equal(11f, points[0].X, 3); Assert.Equal(5f, points[0].Y, 3);
            Assert.Equal(11f, points[1].X, 3); Assert.Equal(9f, points[1].Y, 3);
            Assert.Equal(9f, points[2].X, 3);  Assert.Equal(9f, points[2].Y, 3);
            // The circle: radius 1 about the entity.
            for (int i = 4; i < 12; i++) Assert.Equal(1f, Vector2.Distance(points[i], new Vector2(10f, 5f)), 3);
            // The polygon's second corner, (1, 0) → 2 along the entity's x: straight up.
            Assert.Equal(10f, points[13].X, 3); Assert.Equal(7f, points[13].Y, 3);

            // No shape: one unit, scaled.
            Entity plain = Make("Plain", new Vector3(0f, 0f, 0f), Body2D.Default, scale: new Vector3(3f, 1f, 1f));
            points.Clear(); loops.Clear();
            Physics2DWorld.ShapeOutlines(World, plain, points, loops);
            Assert.Equal(new[] { 4 }, loops);
            Assert.Equal(new Vector2(-1.5f, -0.5f), points[0]);
            Assert.Equal(new Vector2(1.5f, 0.5f), points[2]);
        }

        // ── A solid layer's walls ───────────────────────────────────────────────────────

        [Fact]
        public void Runs_of_tiles_are_one_box_each_and_the_same_run_in_the_row_above_joins_it()
        {
            TilemapData map = TilemapData.Parse("m.tilemap", """
                { "tilemap": 1, "tiles": "t.sprites", "layers": [ { "name": "g", "solid": true, "origin": [-1, 2], "rows": [
                    [1, 1, 0, 0, 1],
                    [1, 1, 0, 1, 1],
                    [1, 1, 1, 1, 1] ] } ] }
                """, out _)!;
            var boxes = Physics2DWorld.WallBoxes(map.Layers[0]);
            // The bottom row whole; above it the two on the left (two rows high) and the two on the right, then the one.
            Assert.Equal(new[] { (-1, 2, 5, 1), (-1, 3, 2, 2), (2, 3, 2, 1), (3, 4, 1, 1) }, boxes);
            Assert.Empty(Physics2DWorld.WallBoxes(new TileLayer()));
        }

        // ── The world ───────────────────────────────────────────────────────────────────

        [Fact]
        public void A_scene_with_no_2D_body_has_no_2D_world()
        {
            SceneManager.ActiveScene!.CreateEntity("Empty");
            SceneManager.Play();
            Assert.False(Physics2DWorld.Active);
            Run(0.1f);
        }

        [Fact]
        public void A_box_and_a_circle_fall_and_rest_on_the_ground_at_the_height_the_numbers_give()
        {
            Make("Ground", new Vector3(0f, -1f, 0f), Box(Body2DType.Static, 20f, 2f));           // its top at y = 0
            Entity crate = Make("Crate", new Vector3(-2f, 3f, 0.25f), Box(Body2DType.Dynamic, 1f, 2f));
            Entity ball = Make("Ball", new Vector3(2f, 4f, 0f), Circle(0.5f));
            SceneManager.Play();
            Assert.True(Physics2DWorld.Active);
            Assert.Equal(3, Physics2DWorld.BodyCount);

            Run(0.25f);
            // A quarter of a second of free fall: 9.81 / 2 * 0.25² = 0.31 units, to within the step's rounding.
            Assert.InRange(At(crate).Y, 3f - 0.36f, 3f - 0.27f);
            Assert.Equal(-2f, At(crate).X, 3);
            Assert.Equal(0.25f, At(crate).Z);                // z is the entity's own

            Run(3f);
            Assert.Equal(1f, At(crate).Y, 1);                // half its height above the ground's top
            Assert.Equal(0.5f, At(ball).Y, 1);               // its radius above it
            Assert.Equal(-2f, At(crate).X, 2);
            Assert.True(Physics2DWorld.TryGetLinearVelocity(crate, out Vector2 v) && v.Length() < 0.05f);
        }

        [Fact]
        public void A_body_with_no_shape_is_one_unit_and_scale_makes_it_larger()
        {
            Make("Ground", new Vector3(0f, -1f, 0f), Box(Body2DType.Static, 20f, 2f));
            Entity plain = Make("Plain", new Vector3(-3f, 2f, 0f), Body2D.Default);
            Entity large = Make("Large", new Vector3(3f, 4f, 0f), Body2D.Default, scale: new Vector3(2f, 3f, 1f));
            SceneManager.Play();
            Run(4f);
            Assert.Equal(0.5f, At(plain).Y, 1);
            Assert.Equal(1.5f, At(large).Y, 1);
        }

        [Fact]
        public void A_body_as_large_as_its_sprite_is_made_when_the_sprites_image_has_been_read()
        {
            // A game that has just started: the image arrives some frames after the scene.
            var answer = SpriteLibrary.Answer.Loading;
            Physics2DWorld.Frames = (string? texture, out SpriteFrame frame) =>
            {
                frame = new SpriteFrame { Size = new Vector2(0.8f, 0.4f), Pivot = new Vector2(0.5f, 0.5f) };
                return answer;
            };
            try
            {
                Make("Ground", new Vector3(0f, -1f, 0f), Box(Body2DType.Static, 20f, 2f));
                Entity crate = Make("Crate", new Vector3(0f, 2f, 0f), Body2D.Default);
                World.AddComponent(crate, new SpriteRenderer { Texture = "Textures/crate.png", Color = Vector4.One, Visible = true });
                Entity gone = Make("Gone", new Vector3(3f, 2f, 0f), Body2D.Default);
                World.AddComponent(gone, new SpriteRenderer { Texture = "Textures/crate.png", Color = Vector4.One, Visible = true });
                SceneManager.Play();
                Run(0.5f);
                Assert.Equal(1, Physics2DWorld.BodyCount);                  // the ground; the two wait for their image
                Assert.Equal(2f, At(crate).Y);
                SceneManager.Destroy(gone);

                answer = SpriteLibrary.Answer.Ready;
                Run(2f);
                Assert.Equal(2, Physics2DWorld.BodyCount);
                Assert.Equal(0.2f, At(crate).Y, 2);                         // at rest on half its image's height, not on half a unit
            }
            finally { Physics2DWorld.Frames = null; }
        }

        [Fact]
        public void A_child_falls_in_the_world_and_keeps_its_parent()
        {
            Make("Ground", new Vector3(0f, -1f, 0f), Box(Body2DType.Static, 20f, 2f));
            Entity parent = SceneManager.ActiveScene!.CreateEntity("Parent");
            var ptr = World.GetComponent<Transform>(parent);
            ptr.Position = new Vector3(5f, 10f, 0f);
            World.AddComponent(parent, ptr);
            Entity child = Make("Child", new Vector3(1f, -6f, 0f), Box(Body2DType.Dynamic, 1f, 1f));        // in the world: (6, 4)
            var ctr = World.GetComponent<Transform>(child);
            ctr.Parent = parent;
            World.AddComponent(child, ctr);
            SceneManager.Play();
            Run(3f);
            // At rest at y = 0.5 in the world: 9.5 below its parent.
            Assert.Equal(-9.5f, At(child).Y, 1);
            Assert.Equal(1f, At(child).X, 2);
            Assert.Equal(new Vector3(5f, 10f, 0f), At(parent));
        }

        [Fact]
        public void A_solid_tile_layer_is_a_floor_and_follows_a_cell_that_is_set_while_the_game_runs()
        {
            _maps["Tilemaps/room.tilemap"] = """
                { "tilemap": 1, "tiles": "t.sprites", "cell": [1, 1], "layers": [
                  { "name": "deco", "rows": [ [9, 9, 9, 9] ], "origin": [0, 5] },
                  { "name": "ground", "solid": true, "rows": [ [1, 1, 1, 1] ] } ] }
                """;
            Entity room = SceneManager.ActiveScene!.CreateEntity("Room");
            var tr = World.GetComponent<Transform>(room);
            tr.Position = new Vector3(-2f, -1f, 0f);                        // the floor's top at y = 0, from x = -2 to 2
            World.AddComponent(room, tr);
            World.AddComponent(room, new TilemapRenderer { Map = "Tilemaps/room.tilemap", Color = Vector4.One, Visible = true });
            Entity onFloor = Make("OnFloor", new Vector3(0f, 2f, 0f), Circle(0.5f));
            Entity beside = Make("Beside", new Vector3(3f, 2f, 0f), Circle(0.5f));
            SceneManager.Play();
            Assert.Equal(3, Physics2DWorld.BodyCount);
            Run(1.5f);
            Assert.Equal(0.5f, At(onFloor).Y, 1);                           // on the solid layer; the one that is not solid is no ceiling
            Assert.True(At(beside).Y < -3f);                                // past the floor's end

            // A tile put under the falling one, far below: it lands there.
            float y = MathF.Floor(At(beside).Y) - 8f;
            TilemapLibrary.Map("Tilemaps/room.tilemap")!.Layer("ground")!.Fill(4, (int)y + 1, 3, 1, 1);
            TilemapLibrary.Changed();
            Run(4f);
            Assert.Equal(y + 1f + 0.5f, At(beside).Y, 1);
        }

        [Fact]
        public void A_kinematic_body_goes_where_its_Transform_goes_and_pushes_what_it_meets()
        {
            Make("Ground", new Vector3(0f, -1f, 0f), Box(Body2DType.Static, 40f, 2f));
            Entity pusher = Make("Pusher", new Vector3(-3f, 0.5f, 0f), Box(Body2DType.Kinematic, 1f, 1f));
            Entity crate = Make("Crate", new Vector3(0f, 0.5f, 0f), Box(Body2DType.Dynamic, 1f, 1f));
            SceneManager.Play();
            Run(0.5f);
            Assert.Equal(-3f, At(pusher).X, 3);                             // gravity does not move it
            for (int i = 0; i < 120; i++)
            {
                ref Transform tr = ref World.GetComponent<Transform>(pusher);
                tr.Position.X += 0.05f;                                     // three units a second, for two seconds
                SceneManager.UpdatePhysics(Step);
            }
            Assert.Equal(3f, At(pusher).X, 2);
            Assert.True(At(crate).X > 3.9f, $"the crate was pushed ahead of the pusher: {At(crate).X}");
        }

        [Fact]
        public void A_dynamic_body_moved_by_its_Transform_is_there_and_falls_from_there()
        {
            Make("Ground", new Vector3(0f, -1f, 0f), Box(Body2DType.Static, 40f, 2f));
            Entity crate = Make("Crate", new Vector3(0f, 0.5f, 0f), Box(Body2DType.Dynamic, 1f, 1f));
            SceneManager.Play();
            Run(0.5f);
            ref Transform tr = ref World.GetComponent<Transform>(crate);
            tr.Position = new Vector3(5f, 3f, 0f);
            Run(Step);
            Assert.Equal(5f, At(crate).X, 2);
            Assert.InRange(At(crate).Y, 2.9f, 3.01f);
            Run(2f);
            Assert.Equal(0.5f, At(crate).Y, 1);
            Assert.Equal(5f, At(crate).X, 1);
        }

        [Fact]
        public void Scripts_are_told_once_when_bodies_meet_and_part_and_while_they_touch()
        {
            Make("Ground", new Vector3(0f, -1f, 0f), Box(Body2DType.Static, 20f, 2f), listens: true);
            Entity ball = Make("Ball", new Vector3(0f, 2f, 0f), Circle(0.5f), listens: true);
            SceneManager.Play();
            Run(1.5f);
            Assert.Equal(new[] { "enter Ball>Ground", "enter Ground>Ball" }, Told.OrderBy(t => t).ToArray());
            Assert.True(Stays > 20, $"Stay is told each step while they touch: {Stays}");

            Told.Clear();
            Assert.True(SceneManager.SetLinearVelocity(ball, new Vector3(0f, 8f, 0f)));
            Run(0.3f);
            Assert.Equal(new[] { "exit Ball>Ground", "exit Ground>Ball" }, Told.OrderBy(t => t).ToArray());
        }

        [Fact]
        public void Exit_is_told_when_the_last_of_a_bodys_shapes_has_left_not_the_first()
        {
            Make("Ground", new Vector3(-3f, -1f, 0f), Box(Body2DType.Static, 10f, 2f));       // its top at y = 0, from x = -8 to 2
            Body2D two = Body2D.Default;
            two.FixedRotation = true;
            two.Shapes = new[] { new Shape2D { Type = Shape2DType.Circle, Radius = 0.5f, Offset = new Vector2(-1f, 0f) },
                                 new Shape2D { Type = Shape2DType.Circle, Radius = 0.5f, Offset = new Vector2(1f, 0f) } };
            Entity feet = Make("Feet", new Vector3(0f, 1f, 0f), two, listens: true);
            SceneManager.Play();
            Run(1.5f);
            Assert.Equal(new[] { "enter Feet>Ground" }, Told.ToArray());              // two shapes touch the ground: told once

            // Moved so that one foot is past the ground's end: the other still stands on it.
            var tr = World.GetComponent<Transform>(feet);
            tr.Position = new Vector3(2.2f, tr.Position.Y, 0f);
            World.AddComponent(feet, tr);
            Run(0.5f);
            Assert.Equal(0.5f, At(feet).Y, 1);
            Assert.Equal(new[] { "enter Feet>Ground" }, Told.ToArray());

            tr = World.GetComponent<Transform>(feet);
            tr.Position = new Vector3(2.2f, 30f, 0f);
            World.AddComponent(feet, tr);
            Run(0.1f);
            Assert.Equal(new[] { "enter Feet>Ground", "exit Feet>Ground" }, Told.ToArray());
        }

        [Fact]
        public void A_bodys_density_friction_bounce_and_mask_are_its_shapes()
        {
            Make("Ground", new Vector3(0f, -1f, 0f), Box(Body2DType.Static, 200f, 2f));

            // Density: the same push moves a body four times as heavy a quarter as fast.
            Body2D heavy = Box(Body2DType.Dynamic, 1f, 1f);
            heavy.Density = 4f; heavy.GravityScale = 0f;
            Entity lead = Make("Lead", new Vector3(-50f, 20f, 0f), heavy);
            // Friction: a box sent along the ground stops; one with none goes on.
            Entity crate = Make("Crate", new Vector3(-20f, 0.5f, 0f), Box(Body2DType.Dynamic, 1f, 1f));
            Body2D slippery = Box(Body2DType.Dynamic, 1f, 1f);
            slippery.Friction = 0f;
            Entity ice = Make("Ice", new Vector3(-20f, 0.5f, 0f), slippery);
            // A mask that leaves the ice's layer out: the two boxes start in one place and do not push each other.
            Body2D iceBody = World.GetComponent<Body2D>(ice);
            iceBody.Layer = 2; iceBody.Mask = 1;
            World.AddComponent(ice, iceBody);
            Body2D crateBody = World.GetComponent<Body2D>(crate);
            crateBody.Mask = 1;
            World.AddComponent(crate, crateBody);
            // Restitution: a ball that bounces comes up again; one that does not stays down.
            Body2D bouncy = Circle(0.5f);
            bouncy.Restitution = 0.8f;
            Entity rubber = Make("Rubber", new Vector3(20f, 3f, 0f), bouncy);
            Entity clay = Make("Clay", new Vector3(30f, 3f, 0f), Circle(0.5f));
            // A mask that leaves the ground's layer out: the ball falls through it.
            Body2D ghost = Circle(0.5f);
            ghost.Mask = 2;
            Entity through = Make("Through", new Vector3(40f, 3f, 0f), ghost);
            SceneManager.Play();

            Assert.True(SceneManager.AddImpulse(lead, new Vector3(2f, 0f, 0f)));
            Assert.True(SceneManager.TryGetLinearVelocity(lead, out Vector3 v));
            Assert.Equal(0.5f, v.X, 3);

            Run(0.2f);
            Assert.Equal(-20f, At(crate).X, 1);                              // not pushed apart
            Assert.Equal(-20f, At(ice).X, 1);
            SceneManager.SetLinearVelocity(crate, new Vector3(5f, 0f, 0f));
            SceneManager.SetLinearVelocity(ice, new Vector3(5f, 0f, 0f));
            float rubberTop = 0f, clayTop = 0f;
            bool rubberDown = false, clayDown = false;
            for (int i = 0; i < 120; i++)
            {
                Run(Step);
                if (At(rubber).Y < 0.6f) rubberDown = true; else if (rubberDown) rubberTop = MathF.Max(rubberTop, At(rubber).Y);
                if (At(clay).Y < 0.6f) clayDown = true; else if (clayDown) clayTop = MathF.Max(clayTop, At(clay).Y);
            }
            SceneManager.TryGetLinearVelocity(crate, out Vector3 crateSpeed);
            SceneManager.TryGetLinearVelocity(ice, out Vector3 iceSpeed);
            Assert.True(MathF.Abs(crateSpeed.X) < 0.05f, $"friction stops the crate: {crateSpeed.X}");
            Assert.Equal(5f, iceSpeed.X, 1);
            Assert.True(rubberDown && rubberTop > 1.2f, $"the rubber ball comes up again: {rubberTop}");
            Assert.True(clayDown && clayTop == 0f, $"the clay ball stays down: {clayTop}");
            Assert.True(At(through).Y < -5f, $"a ball whose mask leaves the ground out falls through it: {At(through).Y}");
        }

        [Fact]
        public void A_maps_walls_are_as_large_as_its_cells_and_a_capsule_is_found_by_its_round_ends()
        {
            _maps["Tilemaps/wide.tilemap"] = """
                { "tilemap": 1, "tiles": "t.sprites", "cell": [2, 0.5], "layers": [ { "name": "ground", "solid": true, "rows": [ [1, 1, 1, 1] ] } ] }
                """;
            Entity wide = SceneManager.ActiveScene!.CreateEntity("Wide");
            World.AddComponent(wide, new TilemapRenderer { Map = "Tilemaps/wide.tilemap", Color = Vector4.One, Visible = true });
            Entity on = Make("On", new Vector3(7f, 3f, 0f), Circle(0.5f));         // four cells of two units: the floor ends at x = 8
            Entity off = Make("Off", new Vector3(9f, 3f, 0f), Circle(0.5f));
            Body2D pill = Body2D.Default;
            pill.Type = Body2DType.Static;
            pill.Shapes = new[] { new Shape2D { Type = Shape2DType.Capsule, Size = new Vector2(0.6f, 1.6f) } };
            Entity capsule = Make("Pill", new Vector3(-10f, 0f, 0f), pill);
            SceneManager.Play();
            Run(2f);
            Assert.Equal(1f, At(on).Y, 1);                                   // half a unit of floor, half a unit of ball
            Assert.True(At(off).Y < -3f);

            var found = new List<Entity>();
            Assert.Equal(1, Physics2DWorld.OverlapPoint(new Vector2(-10f, 0.7f), found));       // inside the upper round end
            Assert.Equal(capsule, found[0]);
            Assert.Equal(0, Physics2DWorld.OverlapPoint(new Vector2(-9.71f, 0.79f), found));    // inside a box of that size, outside the round end
            Assert.Equal(0, Physics2DWorld.OverlapPoint(new Vector2(-10f, 0.85f), found));
        }

        [Fact]
        public void A_trigger_tells_of_what_enters_and_leaves_it_once_each_and_stops_nothing()
        {
            Make("Ground", new Vector3(0f, -1f, 0f), Box(Body2DType.Static, 20f, 2f));
            Make("Zone", new Vector3(0f, 3f, 0f), Box(Body2DType.Static, 4f, 1f, trigger: true), listens: true);
            Entity ball = Make("Ball", new Vector3(0f, 6f, 0f), Circle(0.25f), listens: true);
            SceneManager.Play();
            Run(3f);
            Assert.Equal(0.25f, At(ball).Y, 1);                             // it fell through the zone to the ground
            Assert.Equal(new[] { "trigger-enter Ball>Zone", "trigger-enter Zone>Ball", "trigger-exit Ball>Zone", "trigger-exit Zone>Ball" },
                         Told.Where(t => t.StartsWith("trigger")).OrderBy(t => t).ToArray());
        }

        [Fact]
        public void The_calls_a_script_makes_act_on_a_2D_body()
        {
            Body2D floating = Box(Body2DType.Dynamic, 1f, 1f);
            floating.GravityScale = 0f;
            Entity e = Make("Floating", new Vector3(0f, 0f, 0f), floating);
            SceneManager.Play();

            Assert.True(SceneManager.SetLinearVelocity(e, new Vector3(2f, 1f, 99f)));
            Run(1f);
            Assert.Equal(2f, At(e).X, 1);
            Assert.Equal(1f, At(e).Y, 1);
            Assert.True(SceneManager.TryGetLinearVelocity(e, out Vector3 v));
            Assert.Equal(new Vector3(2f, 1f, 0f), new Vector3(MathF.Round(v.X, 3), MathF.Round(v.Y, 3), v.Z));

            // A unit box of density 1 weighs 1: an impulse of 3 adds 3 units a second.
            Assert.True(SceneManager.SetLinearVelocity(e, Vector3.Zero));
            Assert.True(SceneManager.AddImpulse(e, new Vector3(0f, 3f, 0f)));
            Assert.True(SceneManager.TryGetLinearVelocity(e, out v));
            Assert.Equal(3f, v.Y, 3);

            Assert.True(SceneManager.SetLinearVelocity(e, Vector3.Zero));
            Assert.True(SceneManager.SetAngularVelocity(e, new Vector3(9f, 9f, MathF.PI)));       // half a turn a second, about z
            Run(0.5f);
            Quaternion q = World.GetComponent<Transform>(e).Rotation;
            Assert.Equal(MathF.PI / 2f, 2f * MathF.Atan2(q.Z, q.W), 1);
            Assert.Equal((0f, 0f), (MathF.Round(q.X, 4), MathF.Round(q.Y, 4)));

            Assert.True(SceneManager.TeleportBody(e, new Vector3(-7f, 7f, 0f), Quaternion.Identity));
            Run(Step * 2);
            Assert.Equal(-7f, At(e).X, 2);
            Assert.Equal(7f, At(e).Y, 2);

            Entity none = SceneManager.ActiveScene!.CreateEntity("None");
            Assert.False(SceneManager.SetLinearVelocity(none, Vector3.One));
        }

        [Fact]
        public void A_ray_meets_the_first_body_and_overlaps_find_what_a_point_a_circle_and_a_box_reach()
        {
            Entity ground = Make("Ground", new Vector3(0f, -1f, 0f), Box(Body2DType.Static, 20f, 2f));
            Entity wall = Make("Wall", new Vector3(5f, 2f, 0f), Box(Body2DType.Static, 1f, 4f));
            Entity ball = Make("Ball", new Vector3(-4f, 0.5f, 0f), Circle(0.5f));
            Make("Zone", new Vector3(2f, 2f, 0f), Box(Body2DType.Static, 1f, 1f, trigger: true));
            SceneManager.Play();
            Run(Step);

            Assert.True(Physics2DWorld.Raycast(new Vector2(0f, 2f), new Vector2(10f, 0f), 20f, out Entity hit, out RaycastHit2D info));
            Assert.Equal(wall, hit);                                        // through the trigger, to the wall's near face
            Assert.Equal(4.5f, info.Point.X, 2);
            Assert.Equal(4.5f, info.Distance, 2);
            Assert.Equal(-1f, info.Normal.X, 2);
            Assert.True(Physics2DWorld.Raycast(new Vector2(0f, 5f), new Vector2(0f, -1f), 20f, out hit, out info));
            Assert.Equal(ground, hit);
            Assert.Equal(5f, info.Distance, 2);
            Assert.False(Physics2DWorld.Raycast(new Vector2(0f, 2f), new Vector2(1f, 0f), 3f, out _, out _));     // too short
            Assert.False(Physics2DWorld.Raycast(new Vector2(0f, 9f), new Vector2(1f, 0f), 50f, out _, out _));

            var found = new List<Entity>();
            Assert.Equal(1, Physics2DWorld.OverlapPoint(new Vector2(-4f, 0.6f), found));
            Assert.Equal(ball, found[0]);
            Assert.Equal(0, Physics2DWorld.OverlapPoint(new Vector2(-4f, 1.2f), found));
            Assert.Equal(2, Physics2DWorld.OverlapCircle(new Vector2(-4f, 1.2f), 1.3f, found));       // the ball, and the ground below
            Assert.Contains(ball, found);
            Assert.Contains(ground, found);
            Assert.Equal(0, Physics2DWorld.OverlapCircle(new Vector2(-4f, 3f), 1.9f, found));
            Assert.Equal(1, Physics2DWorld.OverlapBox(new Vector2(4f, 3f), new Vector2(1.2f, 1f), 0f, found));
            Assert.Equal(wall, found[0]);
            Assert.Equal(0, Physics2DWorld.OverlapBox(new Vector2(3.6f, 3f), new Vector2(1f, 1f), 0f, found));
            // The same box turned an eighth of a turn reaches the wall with its corner.
            Assert.Equal(1, Physics2DWorld.OverlapBox(new Vector2(3.85f, 3f), new Vector2(1f, 1f), 45f, found));
        }

        [Fact]
        public void A_map_of_many_walls_is_asked_about_every_one_of_them()
        {
            // Eight rows of twenty cells, every other one a tile: eighty walls of one cell, none joined to another.
            var rows = new System.Text.StringBuilder();
            for (int y = 7; y >= 0; y--)
            {
                rows.Append(y == 7 ? "[" : ", [");
                for (int x = 0; x < 20; x++) rows.Append(x == 0 ? "" : ", ").Append((x + y) % 2 == 0 ? 1 : 0);
                rows.Append(']');
            }
            _maps["Tilemaps/dots.tilemap"] = "{ \"tilemap\": 1, \"tiles\": \"t.sprites\", \"cell\": [1, 1], \"layers\": [ { \"name\": \"dots\", \"solid\": true, \"rows\": [ " + rows + " ] } ] }";
            Entity dots = SceneManager.ActiveScene!.CreateEntity("Dots");
            World.AddComponent(dots, new TilemapRenderer { Map = "Tilemaps/dots.tilemap", Color = Vector4.One, Visible = true });
            Make("Zone", new Vector3(-3f, 0.5f, 0f), Box(Body2DType.Static, 1f, 1f, trigger: true));
            SceneManager.Play();
            Run(Step);
            Assert.Equal(80, Physics2DWorld.WallBoxes(TilemapLibrary.Map("Tilemaps/dots.tilemap")!.Layer("dots")!).Count);

            var found = new List<Entity>();
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 20; x++)
                    Assert.Equal((x + y) % 2 == 0 ? 1 : 0, Physics2DWorld.OverlapPoint(new Vector2(x + 0.5f, y + 0.5f), found));
            // A ray that starts beyond a trigger asks each wall in turn: the first and the last made are among them.
            Assert.True(Physics2DWorld.Raycast(new Vector2(-5f, 0.5f), new Vector2(1f, 0f), 30f, out Entity hit, out RaycastHit2D info));
            Assert.Equal(dots, hit);
            Assert.Equal(5f, info.Distance, 2);
            Assert.True(Physics2DWorld.Raycast(new Vector2(-5f, 0.5f), new Vector2(24f, 7f), 30f, out hit, out info));
            Assert.Equal(dots, hit);
        }

        [Fact]
        public void A_body_that_is_removed_is_gone_from_the_world_and_Stop_gives_the_world_back()
        {
            Make("Ground", new Vector3(0f, -1f, 0f), Box(Body2DType.Static, 20f, 2f));
            Entity crate = Make("Crate", new Vector3(0f, 0.5f, 0f), Box(Body2DType.Dynamic, 1f, 1f));
            SceneManager.Play();
            Run(0.2f);
            var found = new List<Entity>();
            Assert.Equal(1, Physics2DWorld.OverlapPoint(new Vector2(0f, 0.5f), found));
            SceneManager.Destroy(crate);
            Run(Step);
            Assert.Equal(1, Physics2DWorld.BodyCount);
            Assert.Equal(0, Physics2DWorld.OverlapPoint(new Vector2(0f, 0.5f), found));
            SceneManager.Stop();
            Assert.False(Physics2DWorld.Active);
            Assert.Equal(0, Physics2DWorld.BodyCount);
        }

        static void Config(string json) => ConfigManager.LoadFromMemory(System.Text.Encoding.UTF8.GetBytes(json));

        [Fact]
        public void A_project_whose_2D_engine_is_none_makes_no_world()
        {
            Config("""{ "physics": { "engine2D": "none" } }""");
            try
            {
                Entity crate = Make("Crate", new Vector3(0f, 5f, 0f), Box(Body2DType.Dynamic, 1f, 1f));
                SceneManager.Play();
                Run(0.5f);
                Assert.False(Physics2DWorld.Active);
                Assert.Equal(5f, At(crate).Y);
            }
            finally { Config("{}"); }
        }

        [Fact]
        public void The_projects_gravity_is_the_worlds_and_a_script_can_change_it()
        {
            Config("""{ "physics": { "engine2D": "box2d", "gravity2D": [3, 0] } }""");
            try
            {
                Assert.Contains("\"gravity2D\": [3, 0]", ConfigManager.Serialize(ConfigManager.Config!));
                Entity crate = Make("Crate", new Vector3(0f, 0f, 0f), Box(Body2DType.Dynamic, 1f, 1f));
                SceneManager.Play();
                Assert.Equal(new Vector2(3f, 0f), Physics2DWorld.Gravity);
                Run(1f);
                Assert.InRange(At(crate).X, 1.4f, 1.6f);            // 3 / 2 * 1² to the right
                Assert.Equal(0f, At(crate).Y, 3);

                Physics2DWorld.Gravity = new Vector2(0f, 4f);       // upwards
                Assert.True(SceneManager.SetLinearVelocity(crate, Vector3.Zero));
                Run(1f);
                Assert.InRange(At(crate).Y, 1.9f, 2.1f);
            }
            finally { Config("{}"); }
            Assert.Equal(new Vector2(0f, -9.81f), ConfigManager.Config!.Gravity2D);
        }
    }
}
