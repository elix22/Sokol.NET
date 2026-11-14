using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The particles' by-speed modules (G8.8 Q4, examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §6.4): Color, Size and
    /// Rotation by Speed at a particle's own speed in the range, Lifetime by the emitter's speed, the block.</summary>
    public sealed class ParticleSpeedTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const float Step = 1f / 60f;

        public ParticleSpeedTests()
        {
            World.Clear();
            WorldParticles.Clear();
            SceneManager.NewScene("Speed");
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

        static ParticlesComponent Moving(float speed)
        {
            var p = ParticlesComponent.Base;
            p.Rate = 0f; p.Looping = false; p.Bursts.Add(ParticleBurst.At(0f, 5)); p.Life = new Vector2(5f, 5f); p.Speed = new Vector2(speed, speed);
            p.Direction = Vector3.UnitX; p.Spread = 0f; p.Radius = 0f; p.World = true; p.StartSize = new Vector2(0.5f, 0.5f);
            p.ColorBySpeedOn = true; p.ColorBySpeed = ParticleGradient.Two(new Vector4(0, 0, 1, 1), new Vector4(1, 0, 0, 1)); p.ColorSpeedRange = new Vector2(0f, 8f);
            p.SizeBySpeedOn = true; p.SizeBySpeed = ParticleCurve.Line(0.4f, 1f); p.SizeSpeedRange = new Vector2(0f, 8f);
            p.RotationBySpeedOn = true; p.RotationBySpeed = new Vector2(0f, 360f); p.RotationSpeedRange = new Vector2(0f, 8f);
            return p;
        }

        [Fact]
        public void Color_size_and_rotation_follow_the_particle_s_speed_in_the_range()
        {
            Entity slow = Emitter(Moving(0f)), mid = Emitter(Moving(4f), new Vector3(0, 5, 0)), fast = Emitter(Moving(20f), new Vector3(0, 10, 0));
            Run(1f);
            var col = new Vector4[5]; var size = new float[5]; var rot = new float[5];
            WorldParticles.CopyColors(slow, col); WorldParticles.CopySizes(slow, size);
            Assert.All(col, c => Assert.Equal(new Vector4(0, 0, 1, 1), c));
            Assert.All(size, s => Assert.Equal(0.2f, s, 4));
            WorldParticles.CopyColors(mid, col); WorldParticles.CopySizes(mid, size); WorldParticles.CopyRotations(mid, rot);
            Assert.All(col, c => Assert.True(Vector4.Distance(c, new Vector4(0.5f, 0, 0.5f, 1)) < 1e-4f, c.ToString()));
            Assert.All(size, s => Assert.Equal(0.35f, s, 4));
            Assert.All(rot, r => Assert.InRange(r, 176f, 181f));                 // 180 degrees a second for 59 steps (made after the first)
            WorldParticles.CopyColors(fast, col); WorldParticles.CopySizes(fast, size);
            Assert.All(col, c => Assert.Equal(new Vector4(1, 0, 0, 1), c));        // past the range: its end
            Assert.All(size, s => Assert.Equal(0.5f, s, 4));
        }

        [Fact]
        public void A_moving_emitter_s_particles_live_by_its_speed()
        {
            var p = ParticlesComponent.Base;
            p.Rate = 20f; p.Life = new Vector2(2f, 2f); p.Speed = Vector2.Zero; p.World = true; p.ShapeOn = false;
            p.LifetimeBySpeedOn = true; p.LifetimeBySpeed = ParticleCurve.Line(1f, 0.5f); p.LifetimeSpeedRange = new Vector2(0f, 10f);
            Entity carried = Emitter(p), still = Emitter(p, new Vector3(0, 5, 0));
            var ages = new float[256];
            float oldCarried = 0f, oldStill = 0f;
            for (int i = 0; i < 240; i++)
            {
                var t = World.GetComponent<Transform>(carried);
                t.Position = new Vector3(i * 10f / 60f, 0, 0);
                World.AddComponent(carried, t);
                WorldParticles.Step(World, Step);
                int n = WorldParticles.CopyAges(carried, ages);
                for (int k = 0; k < n; k++) oldCarried = MathF.Max(oldCarried, ages[k]);
                n = WorldParticles.CopyAges(still, ages);
                for (int k = 0; k < n; k++) oldStill = MathF.Max(oldStill, ages[k]);
            }
            Assert.InRange(oldCarried, 0.95f, 1.02f);
            Assert.InRange(oldStill, 1.95f, 2.02f);
        }

        [Fact]
        public void The_by_speed_fields_are_written_and_read_back()
        {
            var p = Moving(3f);
            p.LifetimeBySpeedOn = true; p.LifetimeBySpeed = ParticleCurve.Line(1f, 0.25f); p.LifetimeSpeedRange = new Vector2(1f, 9f);
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
