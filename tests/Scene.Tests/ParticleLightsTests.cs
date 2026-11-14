using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The particles' Lights (G8.8 Q5, examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §6.6): a point light at each lit
    /// particle from the template light, the multipliers and the particle's colour, size and alpha, Maximum Lights, Ratio, the block.</summary>
    public sealed class ParticleLightsTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        public ParticleLightsTests()
        {
            World.Clear();
            WorldParticles.Clear();
            SceneManager.NewScene("Lights");
        }

        public void Dispose()
        {
            WorldParticles.Clear();
            World.Clear();
        }

        static Entity Lamp()
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity("Lamp");
            World.AddComponent(e, new LightComponent { Type = LightType.Point, Color = new Vector3(1f, 0.5f, 0.25f), Intensity = 4f, Range = 3f, Enabled = false });
            return e;
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

        static ParticlesComponent Lit(Entity lamp, int count)
        {
            var p = ParticlesComponent.Base;
            p.Rate = 0f; p.Bursts.Add(ParticleBurst.At(0f, count)); p.Speed = Vector2.Zero; p.ShapeOn = false; p.World = true; p.Life = new Vector2(10f, 10f);
            p.StartColor = p.StartColor2 = new Vector4(0.5f, 1f, 1f, 0.5f); p.StartSize = new Vector2(2f, 2f); p.Max = 500;
            p.LightsOn = true; p.LightTemplate = lamp; p.LightRatio = 1f; p.LightMax = 256;
            return p;
        }

        [Fact]
        public void Each_lit_particle_is_a_point_light_from_the_template_as_asked()
        {
            Entity lamp = Lamp();
            var p = Lit(lamp, 3); p.LightRangeMultiplier = 2f; p.LightIntensityMultiplier = 0.5f;
            p.LightUseParticleColor = true; p.LightAlphaAffectsIntensity = true; p.LightSizeAffectsRange = true;
            Entity e = Emitter(p, new Vector3(1, 2, 3));
            WorldParticles.Step(World, 1f / 60f);
            Assert.Equal(3, WorldParticles.GatherLights(World));
            Assert.Equal(3, WorldParticles.LightCountOf(e));
            var l = WorldParticles.Lights[0];
            Assert.Equal(new Vector3(1, 2, 3), l.Position);
            Assert.Equal(new Vector3(0.5f, 0.5f, 0.25f), l.Color);           // the template's times the particle's
            Assert.Equal(4f * 0.5f * 0.5f, l.Intensity, 4);                     // times the multiplier, times its alpha
            Assert.Equal(3f * 2f * 2f, l.Range, 4);                             // times the multiplier, times its size
        }

        [Fact]
        public void Maximum_lights_and_the_ratio_limit_them_and_no_template_gives_none()
        {
            Entity lamp = Lamp();
            var capped = Lit(lamp, 30); capped.LightMax = 5;
            var half = Lit(lamp, 200); half.LightRatio = 0.5f; half.AutoRandomSeed = false; half.RandomSeed = 5;
            var none = Lit(lamp, 10); none.LightTemplate = default;
            Entity a = Emitter(capped), b = Emitter(half, new Vector3(0, 10, 0)), c = Emitter(none, new Vector3(0, 20, 0));
            WorldParticles.Step(World, 1f / 60f);
            WorldParticles.GatherLights(World);
            Assert.Equal(5, WorldParticles.LightCountOf(a));
            Assert.InRange(WorldParticles.LightCountOf(b), 80, 120);
            Assert.Equal(0, WorldParticles.LightCountOf(c));
        }

        [Fact]
        public void The_lights_fields_are_written_and_read_back()
        {
            Entity lamp = Lamp();
            var p = Lit(lamp, 1); p.LightRatio = 0.3f; p.LightMax = 7; p.LightRangeMultiplier = 1.5f; p.LightSizeAffectsRange = true;
            Emitter(p);
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            ParticlesComponent read = default; Entity readLamp = default;
            foreach (Entity e in World.Entities)
            {
                if (World.TryGetComponent<ParticlesComponent>(e, out var got)) read = got;
                if (World.GetComponent<NameTag>(e).Name == "Lamp") readLamp = e;
            }
            Assert.Equal(readLamp, read.LightTemplate);
            p.LightTemplate = readLamp;
            Assert.Equal(p, read);
        }
    }
}
