using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The particles' Sub Emitters and Inherit Velocity (G8.8 Q2, examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §6.2):
    /// a sub-emitter does not play on its own; Death, Birth and Collision make its particles at the parent particle's place,
    /// taking its colour, size, rotation and life as asked; Stop does not count as a death; Inherit Velocity; the block.</summary>
    public sealed class ParticleSubEmitterTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const float Step = 1f / 60f;

        public ParticleSubEmitterTests()
        {
            World.Clear();
            WorldParticles.Clear();
            SceneManager.NewScene("Subs");
        }

        public void Dispose()
        {
            WorldParticles.Clear();
            World.Clear();
        }

        static Entity Emitter(string name, ParticlesComponent p, Vector3 at = default, Entity? parent = null)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity(name);
            var t = World.GetComponent<Transform>(e);
            t.Position = at;
            t.Parent = parent;
            World.AddComponent(e, t);
            World.AddComponent(e, p);
            return e;
        }

        static void Run(float seconds)
        {
            for (int i = 0; i < (int)MathF.Round(seconds / Step); i++) WorldParticles.Step(World, Step);
        }

        static Vector3[] Positions(Entity e)
        {
            var at = new Vector3[4096];
            return at[..WorldParticles.CopyPositions(e, at)];
        }

        static Vector4[] Colors(Entity e)
        {
            var at = new Vector4[4096];
            return at[..WorldParticles.CopyColors(e, at)];
        }

        // Rockets: n particles up at 5 m/s, red or yellow, living 1 s, once.
        static ParticlesComponent Rockets(int n = 3)
        {
            var p = ParticlesComponent.Base;
            p.Rate = 0f; p.Looping = false; p.Bursts.Add(ParticleBurst.At(0f, n)); p.Life = new Vector2(1f, 1f); p.Speed = new Vector2(5f, 5f);
            p.Spread = 40f; p.Radius = 0f; p.StartColor = new Vector4(1, 0, 0, 1); p.StartColor2 = new Vector4(1, 1, 0, 1); p.World = true;
            p.AutoRandomSeed = false; p.RandomSeed = 3; p.StartSize = new Vector2(0.4f, 0.4f);
            return p;
        }

        // A burst of 20 white sparks from a small sphere, living 3 s; it plays on awake (so its own burst would show if it played).
        static ParticlesComponent Sparks()
        {
            var p = ParticlesComponent.Base;
            p.Rate = 0f; p.Bursts.Add(ParticleBurst.At(0f, 20)); p.Life = new Vector2(3f, 3f); p.Speed = new Vector2(1f, 1f); p.Shape = ParticleShape.Sphere;
            p.Radius = 0.1f; p.StartColor = p.StartColor2 = Vector4.One; p.World = true; p.Max = 500; p.AutoRandomSeed = false; p.RandomSeed = 4;
            p.StartSize = new Vector2(0.5f, 0.5f);
            return p;
        }

        static void AddSub(Entity parent, Entity sub, ParticleSubType type, ParticleInherit inherit = ParticleInherit.None, float probability = 1f)
        {
            var p = World.GetComponent<ParticlesComponent>(parent);
            p.SubOn = true;
            p.SubEmitters.Add(new ParticleSubEmitter { Emitter = sub, Type = type, Inherit = inherit, Probability = probability });
            World.AddComponent(parent, p);
        }

        [Fact]
        public void A_sub_emitter_waits_for_its_parent_and_makes_its_burst_where_each_parent_particle_dies_in_its_colour()
        {
            Entity rockets = Emitter("Rockets", Rockets(), new Vector3(0, 0, 0));
            Entity sparks = Emitter("Sparks", Sparks(), default, rockets);
            AddSub(rockets, sparks, ParticleSubType.Death, ParticleInherit.Color);
            Run(0.5f);
            Assert.Equal(0, WorldParticles.AliveOf(sparks));                   // it plays on awake, but only its parent makes it
            var last = new Vector3[3]; var colour = new Vector4[3];
            for (int i = 0; i < 29; i++) WorldParticles.Step(World, Step);
            WorldParticles.CopyPositions(rockets, last);
            WorldParticles.CopyColors(rockets, colour);
            Run(0.1f);
            Assert.Equal(0, WorldParticles.AliveOf(rockets));
            Assert.Equal(60, WorldParticles.AliveOf(sparks));
            Assert.All(Colors(sparks), c => Assert.Contains(colour, k => Vector4.Distance(k, c) < 1e-3f));
            Assert.All(Positions(sparks), q => Assert.Contains(last, k => Vector3.Distance(k, q) < 0.5f));
            Assert.Contains(Colors(sparks), c => c.Y < 0.5f);               // the rockets were red or yellow: not all yellow
        }

        [Fact]
        public void Stop_and_Clear_are_not_deaths_and_a_probability_of_0_makes_none()
        {
            Entity rockets = Emitter("Rockets", Rockets(), default);
            Entity sparks = Emitter("Sparks", Sparks(), default, rockets);
            Entity never = Emitter("Never", Sparks(), default, rockets);
            AddSub(rockets, sparks, ParticleSubType.Death);
            AddSub(rockets, never, ParticleSubType.Death, probability: 0f);
            Run(0.3f);
            WorldParticles.Stop(rockets, clear: true);
            Run(0.2f);
            Assert.Equal(0, WorldParticles.AliveOf(sparks));
            WorldParticles.Restart(rockets);
            Run(1.2f);
            Assert.Equal(60, WorldParticles.AliveOf(sparks));
            Assert.Equal(0, WorldParticles.AliveOf(never));
        }

        [Fact]
        public void A_birth_sub_emitter_is_a_trail_its_rate_a_second_from_each_living_particle()
        {
            Entity rockets = Emitter("Rockets", Rockets(2), default);
            var smoke = Sparks(); smoke.Bursts = default; smoke.Rate = 30f; smoke.Speed = Vector2.Zero; smoke.ShapeOn = false; smoke.Life = new Vector2(5f, 5f);
            Entity trail = Emitter("Smoke", smoke, default, rockets);
            AddSub(rockets, trail, ParticleSubType.Birth, ParticleInherit.Size);
            Run(0.9f);
            Assert.InRange(WorldParticles.AliveOf(trail), 50, 56);              // 2 rockets x 30 a second x 0.9 s
            Assert.All(Positions(trail), q => Assert.True(q.Y >= -1e-3f && q.Y < 4.6f, $"{q}"));   // along the rockets' paths
        }

        [Fact]
        public void A_collision_sub_emitter_splashes_at_the_hit()
        {
            Entity plane = SceneManager.ActiveScene!.CreateEntity("Floor");
            var t = World.GetComponent<Transform>(plane); t.Position = new Vector3(0, -2, 0); World.AddComponent(plane, t);
            var drops = ParticlesComponent.Base;
            drops.Rate = 0f; drops.Looping = false; drops.Bursts.Add(ParticleBurst.At(0f, 4)); drops.Speed = Vector2.Zero; drops.Gravity = 9.81f;
            drops.Life = new Vector2(5f, 5f); drops.World = true; drops.ShapeOn = false; drops.StartSize = new Vector2(0.2f, 0.2f);
            drops.CollisionOn = true; drops.CollisionType = ParticleCollisionType.Planes; drops.Planes.Add(plane); drops.LifetimeLoss = 1f;
            Entity d = Emitter("Drops", drops, new Vector3(0, 3, 0));
            var splash = Sparks(); splash.Bursts = default; splash.Bursts.Add(ParticleBurst.At(0f, 5)); splash.Speed = Vector2.Zero; splash.ShapeOn = false;
            Entity s = Emitter("Splash", splash, default, d);
            AddSub(d, s, ParticleSubType.Collision);
            Run(1.5f);
            Assert.Equal(0, WorldParticles.AliveOf(d));
            Assert.Equal(20, WorldParticles.AliveOf(s));                        // 4 drops x 5, at the floor where they hit
            Assert.All(Positions(s), q => Assert.InRange(q.Y, -1.95f, -1.85f));
        }

        [Fact]
        public void Inherit_velocity_gives_the_moving_emitters_velocity_or_the_parent_particles()
        {
            var p = ParticlesComponent.Base;
            p.Rate = 0f; p.Bursts.Add(ParticleBurst.At(0.2f, 10)); p.Speed = Vector2.Zero; p.Life = new Vector2(5f, 5f); p.World = true; p.ShapeOn = false;
            p.InheritVelocityOn = true; p.InheritVelocityMode = ParticleInheritVelocity.Initial; p.InheritVelocity = 1f;
            Entity e = Emitter("Moving", p);
            for (int i = 0; i < 30; i++)
            {
                var t = World.GetComponent<Transform>(e);
                t.Position = new Vector3(i * 0.05f, 0, 0);                    // 3 m/s along +X
                World.AddComponent(e, t);
                WorldParticles.Step(World, Step);
            }
            Vector3[] a = Positions(e);
            Run(1f);
            Vector3[] b = Positions(e);
            Assert.Equal(10, a.Length);
            Assert.All(Enumerable.Range(0, 10), i => Assert.InRange(b[i].X - a[i].X, 2.9f, 3.1f));

            // A sub-emitter's particles take their parent particle's velocity.
            WorldParticles.Clear(); World.Clear(); SceneManager.NewScene("Subs");
            Entity rockets = Emitter("Rockets", Rockets(1), default);
            var sp = Sparks(); sp.Speed = Vector2.Zero; sp.ShapeOn = false; sp.InheritVelocityOn = true; sp.InheritVelocity = 1f;
            Entity sparks = Emitter("Sparks", sp, default, rockets);
            AddSub(rockets, sparks, ParticleSubType.Birth);
            var trail = World.GetComponent<ParticlesComponent>(sparks); trail.Bursts = default; trail.Rate = 20f; World.AddComponent(sparks, trail);
            Run(0.5f);
            Vector3[] s0 = Positions(sparks);
            Run(0.1f);
            Vector3[] s1 = Positions(sparks);
            Assert.True(s0.Length > 3);
            Assert.True(s1[0].Y - s0[0].Y > 0.3f, $"the first smoke moves up with its rocket's 5 m/s: {s1[0].Y - s0[0].Y}");
        }

        [Fact]
        public void The_sub_emitters_are_written_and_read_back()
        {
            Entity rockets = Emitter("Rockets", Rockets(), default);
            Entity sparks = Emitter("Sparks", Sparks(), default, rockets);
            AddSub(rockets, sparks, ParticleSubType.Collision, ParticleInherit.Color | ParticleInherit.Lifetime, 0.5f);
            var p = World.GetComponent<ParticlesComponent>(rockets);
            p.InheritVelocityOn = true; p.InheritVelocityMode = ParticleInheritVelocity.Current; p.InheritVelocity = 0.7f;
            World.AddComponent(rockets, p);
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            Entity r = default, s = default;
            foreach (Entity e in World.Entities)
            {
                string n = World.GetComponent<NameTag>(e).Name;
                if (n == "Rockets") r = e; else if (n == "Sparks") s = e;
            }
            var read = World.GetComponent<ParticlesComponent>(r);
            Assert.Equal(1, read.SubEmitters.Count);
            Assert.Equal(s, read.SubEmitters.Items[0].Emitter);
            p.SubEmitters.Items[0].Emitter = s;
            Assert.Equal(p, read);
        }
    }
}
