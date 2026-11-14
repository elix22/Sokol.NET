using System.Numerics;
using System.Text.Json;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The particles' Collision module (G8.8 Q1, examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §6.1): the physics world
    /// and planes, the bounce and the dampening, the life lost, the kill speeds, the quality's share without tunnelling, the hits
    /// told, the block.</summary>
    public sealed class ParticleCollisionTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const float Step = 1f / 60f;

        public ParticleCollisionTests()
        {
            World.Clear();
            WorldParticles.Clear();
            SceneManager.SimulatorFactory = null;
            SceneManager.NewScene("Collision");
        }

        public void Dispose()
        {
            SceneManager.Stop();
            ScriptSystem.ClearFactories();
            WorldParticles.Clear();
            World.Clear();
        }

        static Entity Box(string name, Vector3 at, Vector3 half)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity(name);
            var tr = World.GetComponent<Transform>(e);
            tr.Position = at;
            World.AddComponent(e, tr);
            var shape = ShapeEntry.Default();
            shape.HalfExtent = half;
            World.AddComponent(e, new RigidbodyComponent { MotionType = RigidbodyMotionType.Static, Mass = 0f, Friction = 0.5f, Shapes = new() { shape } });
            return e;
        }

        static Entity Emitter(ParticlesComponent p, Vector3 at, Quaternion? turn = null)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity("Emitter");
            var t = World.GetComponent<Transform>(e);
            t.Position = at;
            if (turn is { } q) t.Rotation = q;
            World.AddComponent(e, t);
            World.AddComponent(e, p);
            return e;
        }

        // A box of still particles falling under gravity onto what is below, colliding with the world.
        static ParticlesComponent Falling(int count = 60, float speed = 0f)
        {
            var p = ParticlesComponent.Base;
            p.Rate = 0f; p.Life = new Vector2(20f, 20f); p.Speed = new Vector2(speed, speed); p.StartSize = new Vector2(0.2f, 0.2f);
            p.Gravity = 9.81f; p.World = true; p.Max = 500; p.AutoRandomSeed = false; p.RandomSeed = 7;
            p.Shape = ParticleShape.Box; p.ShapeScale = new Vector3(4f, 0.5f, 4f); p.Direction = -Vector3.UnitY;
            p.Bursts.Add(ParticleBurst.At(0f, count));
            p.CollisionOn = true; p.CollisionType = ParticleCollisionType.World; p.Bounce = 0.5f;
            return p;
        }

        static Vector3[] Positions(Entity e)
        {
            var at = new Vector3[4096];
            return at[..WorldParticles.CopyPositions(e, at)];
        }

        static void Run(float seconds)
        {
            for (int i = 0; i < (int)MathF.Round(seconds / Step); i++) SceneManager.UpdatePhysics(Step);
        }

        [Fact]
        public void Particles_falling_on_a_box_stop_on_its_top_at_their_radius_and_bounce()
        {
            Entity ground = Box("Ground", new Vector3(0, -0.5f, 0), new Vector3(20, 0.5f, 20));    // its top at y 0
            Entity e = Emitter(Falling(), new Vector3(0, 4, 0));
            SceneManager.Play();
            var hits = new ParticleCollisionEvent[64];
            bool up = false; Entity met = default;
            for (int i = 0; i < 180; i++)
            {
                SceneManager.UpdatePhysics(Step);
                int n = WorldParticles.CopyCollisions(e, hits);
                for (int k = 0; k < n; k++) { if (hits[k].Velocity.Y > 0.5f) up = true; met = hits[k].Other; }
            }
            Assert.Equal(60, WorldParticles.AliveOf(e));
            Assert.All(Positions(e), p => Assert.True(p.Y >= 0.1f - 1e-3f, $"below the top at its radius: {p.Y}"));
            Assert.True(up, "a hit turns the speed up (Bounce 0.5)");
            Assert.Equal(ground, met);
        }

        [Fact]
        public void Without_the_module_or_with_a_bounce_of_0_and_full_dampen_they_fall_through_or_stop_dead()
        {
            Box("Ground", new Vector3(0, -0.5f, 0), new Vector3(20, 0.5f, 20));
            var off = Falling(); off.CollisionOn = false;
            var dead = Falling(); dead.Bounce = 0f; dead.CollisionDampen = 1f;
            Entity a = Emitter(off, new Vector3(0, 4, 0)), b = Emitter(dead, new Vector3(10, 4, 0));
            SceneManager.Play();
            Run(2f);
            Assert.All(Positions(a), p => Assert.True(p.Y < -5f, $"no collision: through the ground {p.Y}"));
            Run(0.5f);
            Assert.All(Positions(b), p => Assert.InRange(p.Y, 0.099f, 0.12f));
        }

        [Fact]
        public void Lifetime_loss_kills_on_the_first_hit_and_a_minimum_kill_speed_kills_the_fast()
        {
            Box("Ground", new Vector3(0, -0.5f, 0), new Vector3(20, 0.5f, 20));
            var loss = Falling(); loss.LifetimeLoss = 1f;
            var kill = Falling(); kill.MinKillSpeed = 100f;
            var half = Falling(); half.LifetimeLoss = 0.25f; half.Bounce = 0f;
            Entity a = Emitter(loss, new Vector3(0, 4, 0)), b = Emitter(kill, new Vector3(10, 4, 0)), c = Emitter(half, new Vector3(-10, 4, 0));
            SceneManager.Play();
            Run(1.5f);
            Assert.Equal(0, WorldParticles.AliveOf(a));
            Assert.Equal(0, WorldParticles.AliveOf(b));
            // A quarter lost a hit: a particle resting on the ground is pushed into it each frame, a hit each frame - four
            // frames after landing it is gone (as the reference editor's), long before its 20 s.
            Assert.Equal(0, WorldParticles.AliveOf(c));
        }

        [Theory]
        [InlineData(ParticleCollisionQuality.High)]
        [InlineData(ParticleCollisionQuality.Medium)]
        [InlineData(ParticleCollisionQuality.Low)]
        public void Fast_particles_do_not_pass_a_thin_slab_at_any_quality(ParticleCollisionQuality quality)
        {
            Box("Slab", new Vector3(0, 0, 0), new Vector3(20, 0.05f, 20));            // 10 cm thick
            var p = Falling(80, 40f); p.CollisionQuality = quality; p.Bounce = 0.2f;     // 40 m/s down: 67 cm a frame
            Entity e = Emitter(p, new Vector3(0, 8, 0));
            SceneManager.Play();
            Run(2f);
            Assert.Equal(80, WorldParticles.AliveOf(e));
            Assert.All(Positions(e), q => Assert.True(q.Y > 0.05f, $"{quality}: through the slab at {q.Y}"));
        }

        [Fact]
        public void A_tilted_plane_holds_them_on_its_side_and_the_hit_names_it()
        {
            Entity plane = SceneManager.ActiveScene!.CreateEntity("Plane");
            var t = World.GetComponent<Transform>(plane);
            t.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 6f);
            World.AddComponent(plane, t);
            Vector3 up = Vector3.Transform(Vector3.UnitY, t.Rotation);
            var p = Falling(); p.CollisionType = ParticleCollisionType.Planes; p.Planes.Add(plane);
            Entity e = Emitter(p, new Vector3(0, 4, 0));
            var hits = new ParticleCollisionEvent[64];
            Entity met = default;
            for (int i = 0; i < 180; i++)
            {
                WorldParticles.Step(World, Step);                              // planes need no physics world: the editor's preview too
                if (WorldParticles.CopyCollisions(e, hits) > 0) met = hits[0].Other;
            }
            Assert.Equal(plane, met);
            Assert.All(Positions(e), q => Assert.True(Vector3.Dot(q, up) >= 0.1f - 1e-3f, $"behind the plane: {Vector3.Dot(q, up)}"));
        }

        [Fact]
        public void Local_particles_collide_too()
        {
            Box("Ground", new Vector3(0, -0.5f, 0), new Vector3(20, 0.5f, 20));
            var p = Falling(); p.World = false;
            Entity e = Emitter(p, new Vector3(0, 4, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.4f));
            SceneManager.Play();
            Run(3f);
            Assert.Equal(60, WorldParticles.AliveOf(e));
            Assert.All(Positions(e), q => Assert.True(q.Y >= 0.1f - 2e-3f, $"{q.Y}"));
        }

        [Fact]
        public void The_collision_fields_and_planes_are_written_and_read_back()
        {
            Entity plane = SceneManager.ActiveScene!.CreateEntity("Plane");
            var p = Falling(); p.CollisionType = ParticleCollisionType.Planes; p.Planes.Add(plane); p.CollisionDampen = 0.3f; p.LifetimeLoss = 0.2f;
            p.MinKillSpeed = 0.5f; p.MaxKillSpeed = 50f; p.RadiusScale = 2f; p.CollisionQuality = ParticleCollisionQuality.Low; p.SendCollisionMessages = true;
            Emitter(p, Vector3.Zero);
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            ParticlesComponent read = default; Entity readPlane = default;
            foreach (Entity e in World.Entities)
            {
                if (World.TryGetComponent<ParticlesComponent>(e, out var got)) read = got;
                if (World.GetComponent<NameTag>(e).Name == "Plane") readPlane = e;
            }
            Assert.Equal(1, read.Planes.Count);
            Assert.Equal(readPlane, read.Planes.Items[0]);
            p.Planes = read.Planes;
            Assert.Equal(p, read);
        }
    }
}
