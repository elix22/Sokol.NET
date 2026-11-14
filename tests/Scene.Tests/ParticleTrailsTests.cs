using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The particles' Trails (G8.8 Q3, examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §6.3): the ratio, the points'
    /// spacing, cap and lifetime, a trail dying with its particle or lingering, Stop, the emitter's space, nothing allocated, the block.</summary>
    public sealed class ParticleTrailsTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const float Step = 1f / 60f;

        public ParticleTrailsTests()
        {
            World.Clear();
            WorldParticles.Clear();
            SceneManager.NewScene("Trails");
        }

        public void Dispose()
        {
            WorldParticles.Clear();
            World.Clear();
        }

        static Entity Emitter(ParticlesComponent p, Vector3 at = default)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity("Emitter");
            var t = World.GetComponent<Transform>(e);
            t.Position = at;
            World.AddComponent(e, t);
            World.AddComponent(e, p);
            return e;
        }

        static void Run(float seconds)
        {
            for (int i = 0; i < (int)MathF.Round(seconds / Step); i++) WorldParticles.Step(World, Step);
        }

        static Vector3[] Points(Entity e)
        {
            var at = new Vector3[8192];
            return at[..WorldParticles.CopyTrailPoints(e, at)];
        }

        // One particle at a time moving along +X at 2 m/s with a trail.
        static ParticlesComponent Mover(int count = 1, float life = 4f)
        {
            var p = ParticlesComponent.Base;
            p.Rate = 0f; p.Looping = false; p.Bursts.Add(ParticleBurst.At(0f, count)); p.Life = new Vector2(life, life); p.Speed = new Vector2(2f, 2f);
            p.Direction = Vector3.UnitX; p.Spread = 0f; p.Radius = 0f; p.World = true; p.Max = 1000; p.AutoRandomSeed = false; p.RandomSeed = 9;
            p.TrailsOn = true; p.TrailMinVertexDistance = 0.3f;
            return p;
        }

        [Fact]
        public void A_ratio_of_a_half_gives_about_half_the_particles_a_trail()
        {
            var p = Mover(400); p.TrailRatio = 0.5f; p.Spread = 360f;
            Entity e = Emitter(p);
            WorldParticles.Step(World, Step);
            Assert.Equal(400, WorldParticles.AliveOf(e));
            Assert.InRange(WorldParticles.TrailCountOf(e), 160, 240);
        }

        [Fact]
        public void Points_are_at_least_the_minimum_distance_apart_and_a_trail_keeps_its_cap_at_most()
        {
            Entity e = Emitter(Mover());
            Run(1f);
            Vector3[] pts = Points(e);
            Assert.InRange(pts.Length, 5, 8);                                      // 2 m at 0.3 m a point
            for (int i = 1; i < pts.Length; i++) Assert.True(Vector3.Distance(pts[i - 1], pts[i]) >= 0.3f - 1e-4f);
            var fine = Mover(); fine.TrailMinVertexDistance = 0.001f;
            Entity f = Emitter(fine, new Vector3(0, 5, 0));
            Run(1.5f);                                                              // a point a frame: more than the cap
            Assert.Equal(WorldParticles.TrailPoints, Points(f).Length);
        }

        [Fact]
        public void A_point_lasts_its_part_of_the_particle_s_life()
        {
            var p = Mover(); p.TrailLifetime = 0.25f; p.TrailMinVertexDistance = 0.05f;   // 1 s of a 4 s life
            Entity e = Emitter(p);
            var ages = new float[64];
            float oldest = 0f;
            for (int i = 0; i < 150; i++)
            {
                WorldParticles.Step(World, Step);
                int n = WorldParticles.CopyTrailAges(e, ages);
                for (int k = 0; k < n; k++) oldest = MathF.Max(oldest, ages[k]);
            }
            Assert.InRange(oldest, 0.9f, 1f + 1e-3f);
        }

        [Fact]
        public void A_trail_dies_with_its_particle_or_lingers_until_its_points_age_out()
        {
            var with = Mover(5, 1f); with.TrailLifetime = 1f; with.TrailMinVertexDistance = 0.05f;
            var without = with; without.TrailDieWithParticles = false;
            Entity a = Emitter(with), b = Emitter(without, new Vector3(0, 5, 0));
            Run(1.1f);
            Assert.Equal(0, WorldParticles.AliveOf(a));
            Assert.Equal(0, WorldParticles.TrailCountOf(a));
            Assert.Equal(0, WorldParticles.AliveOf(b));
            Assert.Equal(5, WorldParticles.TrailCountOf(b));
            Assert.True(WorldParticles.AnyToDraw);                                  // drawn though no particle lives
            Run(1.1f);
            Assert.Equal(0, WorldParticles.TrailCountOf(b));
        }

        [Fact]
        public void Stop_takes_the_trails_with_the_particles_and_a_restart_has_trails_again()
        {
            var p = Mover(20); p.Spread = 360f; p.TrailDieWithParticles = false;
            Entity e = Emitter(p);
            Run(0.5f);
            Assert.Equal(20, WorldParticles.TrailCountOf(e));
            WorldParticles.Stop(e, clear: true);
            Assert.Equal(0, WorldParticles.TrailCountOf(e));
            WorldParticles.Restart(e);
            Run(0.5f);
            Assert.Equal(20, WorldParticles.TrailCountOf(e));
        }

        [Fact]
        public void A_trail_not_in_world_space_moves_with_its_emitter()
        {
            var p = Mover(); p.TrailWorldSpace = false; p.World = false;
            Entity e = Emitter(p);
            Run(1f);
            Vector3[] before = Points(e);
            var t = World.GetComponent<Transform>(e);
            t.Position = new Vector3(0, 0, 3);
            World.AddComponent(e, t);
            WorldParticles.Step(World, 1e-4f);
            Vector3[] after = Points(e);
            Assert.Equal(before.Length, after.Length);
            Assert.All(Enumerable.Range(0, before.Length), i => Assert.Equal(3f, after[i].Z - before[i].Z, 3));
        }

        [Fact]
        public void Trails_allocate_nothing_once_running()
        {
            var p = Mover(); p.Rate = 30f; p.Looping = true; p.Bursts = default; p.Spread = 90f; p.TrailRatio = 0.7f; p.TrailDieWithParticles = false; p.Life = new Vector2(1f, 1f);
            Emitter(p);
            Run(2f);
            // Two windows, the lesser: a one-off (the runtime compiling a method again, faster) is not a frame's allocation;
            // one that every frame makes is in both.
            long least = long.MaxValue;
            for (int w = 0; w < 2; w++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                Run(1f);
                least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            }
            Assert.Equal(0, least);
        }

        [Fact]
        public void The_trail_fields_are_written_and_read_back()
        {
            var p = Mover();
            p.TrailRatio = 0.4f; p.TrailLifetime = 0.3f; p.TrailWorldSpace = false; p.TrailDieWithParticles = false; p.TrailTextureMode = ParticleTrailTexture.Tile;
            p.TrailWidth = ParticleCurve.Line(1f, 0f); p.TrailColorOverTrail = ParticleGradient.Two(new Vector4(1, 0, 0, 1), new Vector4(0, 0, 1, 0));
            p.TrailInheritColor = false; p.TrailTexture = "Textures/streak.png";
            Emitter(p);
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            ParticlesComponent read = default;
            foreach (Entity e in World.Entities)
                if (World.TryGetComponent<ParticlesComponent>(e, out var got)) read = got;
            Assert.Equal(p, read);
        }
    }
}
