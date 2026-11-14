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
    /// <summary>The particles' Triggers and External Forces modules and the Wind Zone (G8.8 Q7,
    /// examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §6.8): the conditions and their actions, a turned trigger, the particle's
    /// radius, the events told; the directional and spherical winds, the multiplier, a zone switched off; the blocks.</summary>
    public sealed class ParticleTriggersTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const float Step = 1f / 60f;

        public ParticleTriggersTests()
        {
            World.Clear();
            WorldParticles.Clear();
            SceneManager.SimulatorFactory = null;
            SceneManager.NewScene("Triggers");
        }

        public void Dispose()
        {
            SceneManager.Stop();
            ScriptSystem.ClearFactories();
            WorldParticles.Clear();
            World.Clear();
        }

        static Entity Body(string name, Vector3 at, ShapeEntry shape, Quaternion? turn = null)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity(name);
            var tr = World.GetComponent<Transform>(e);
            tr.Position = at;
            if (turn is { } q) tr.Rotation = q;
            World.AddComponent(e, tr);
            World.AddComponent(e, new RigidbodyComponent { MotionType = RigidbodyMotionType.Static, IsTrigger = true, Shapes = new() { shape } });
            return e;
        }

        static ShapeEntry BoxShape(Vector3 half) { var s = ShapeEntry.Default(); s.HalfExtent = half; return s; }

        static ShapeEntry Ball(float r) { var s = ShapeEntry.Default(); s.Shape = ColliderShape.Sphere; s.Radius = r; return s; }

        static Entity Emitter(ParticlesComponent p, Vector3 at)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity("Emitter");
            var t = World.GetComponent<Transform>(e);
            t.Position = at;
            World.AddComponent(e, t);
            World.AddComponent(e, p);
            return e;
        }

        static Entity Zone(WindZone z, Vector3 at, Quaternion? turn = null)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity("Wind");
            var t = World.GetComponent<Transform>(e);
            t.Position = at;
            if (turn is { } q) t.Rotation = q;
            World.AddComponent(e, t);
            World.AddComponent(e, z);
            return e;
        }

        // A burst of still particles over a square, falling.
        static ParticlesComponent Falling(int count, float side, Entity trigger)
        {
            var p = ParticlesComponent.Base;
            p.Rate = 0f; p.Life = new Vector2(20f, 20f); p.Speed = Vector2.Zero; p.StartSize = new Vector2(0.2f, 0.2f);
            p.Gravity = 9.81f; p.World = true; p.Max = 2000; p.AutoRandomSeed = false; p.RandomSeed = 11;
            p.Shape = ParticleShape.Box; p.ShapeScale = new Vector3(side, 0.1f, side);
            p.Bursts.Add(ParticleBurst.At(0f, count));
            p.TriggersOn = true; p.Triggers.Add(trigger); p.TriggerRadiusScale = 0f;
            return p;
        }

        // A burst of particles still in the air (no gravity), taking the wind.
        static ParticlesComponent Floating(int count = 30)
        {
            var p = ParticlesComponent.Base;
            p.Rate = 0f; p.Life = new Vector2(20f, 20f); p.Speed = Vector2.Zero; p.Gravity = 0f; p.World = true; p.Max = 200;
            p.AutoRandomSeed = false; p.RandomSeed = 12; p.Shape = ParticleShape.Sphere; p.Radius = 0.01f; p.RadiusThickness = 0f;
            p.Bursts.Add(ParticleBurst.At(0f, count));
            p.ExternalForcesOn = true;
            return p;
        }

        static WindZone Still(WindZoneMode mode, float main, float radius = 20f) =>
            new() { Mode = mode, Main = main, Radius = radius, Turbulence = 0f, PulseMagnitude = 0f, PulseFrequency = 0f };

        static Vector3[] Positions(Entity e) { var at = new Vector3[4096]; return at[..WorldParticles.CopyPositions(e, at)]; }

        static Vector3[] Velocities(Entity e) { var at = new Vector3[4096]; return at[..WorldParticles.CopyVelocities(e, at)]; }

        static void Run(float seconds)
        {
            for (int i = 0; i < (int)MathF.Round(seconds / Step); i++) SceneManager.UpdatePhysics(Step);
        }

        [Fact]
        public void Inside_Kill_takes_the_particles_in_a_turned_box_and_only_its_turned_footprint()
        {
            Quaternion turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4f);
            Entity gate = Body("Gate", Vector3.Zero, BoxShape(new Vector3(1.5f, 0.25f, 1.5f)), turn);
            var p = Falling(1500, 6f, gate); p.TriggerInside = ParticleTriggerAction.Kill;
            Entity e = Emitter(p, new Vector3(0, 3, 0));
            SceneManager.Play();
            Run(1.5f);
            Quaternion back = Quaternion.Conjugate(turn);
            int corners = 0;
            foreach (Vector3 at in Positions(e))
            {
                if (at.Y > -1f) continue;
                Vector3 q = Vector3.Transform(at, back);
                Assert.False(MathF.Abs(q.X) < 1.45f && MathF.Abs(q.Z) < 1.45f, $"under the turned box: {at}");
                if (MathF.Abs(at.X) < 1.45f && MathF.Abs(at.Z) < 1.45f) corners++;     // under the unturned box's corners
            }
            Assert.True(corners > 20, $"the unturned box's corners let them by: {corners}");
        }

        [Fact]
        public void Enter_and_Exit_Callbacks_are_told_once_a_particle_with_the_trigger_and_Ignore_tells_nothing()
        {
            Entity bell = Body("Bell", Vector3.Zero, Ball(1f));
            var p = Falling(20, 0.2f, bell); p.TriggerEnter = ParticleTriggerAction.Callback; p.TriggerExit = ParticleTriggerAction.Callback;
            var q = Falling(20, 0.2f, bell);         // Ignore all four
            Entity e = Emitter(p, new Vector3(0, 3, 0)), quiet = Emitter(q, new Vector3(0, 3, 0));
            SceneManager.Play();
            var ev = new ParticleTriggerEvent[64];
            int enter = 0, exit = 0, other = 0;
            for (int i = 0; i < 120; i++)
            {
                SceneManager.UpdatePhysics(Step);
                int n = WorldParticles.CopyTriggered(e, ev);
                for (int k = 0; k < n; k++)
                {
                    Assert.Equal(bell, ev[k].Trigger);
                    if (ev[k].Kind == ParticleTriggerKind.Enter) { enter++; Assert.True(ev[k].Position.Length() <= 1.01f, $"entered at {ev[k].Position}"); }
                    else if (ev[k].Kind == ParticleTriggerKind.Exit) exit++;
                    else other++;
                }
                Assert.Equal(0, WorldParticles.CopyTriggered(quiet, ev));
            }
            Assert.Equal(20, enter);
            Assert.Equal(20, exit);
            Assert.Equal(0, other);
            Assert.Equal(20, WorldParticles.AliveOf(quiet));
        }

        [Fact]
        public void Outside_Kill_keeps_them_in_a_sphere_and_the_radius_scale_lets_them_out_by_their_size()
        {
            Entity bubble = Body("Bubble", Vector3.Zero, Ball(2f));
            ParticlesComponent Spread(float scale)
            {
                var p = ParticlesComponent.Base;
                p.Rate = 40f; p.Life = new Vector2(5f, 5f); p.Speed = new Vector2(1f, 2f); p.Gravity = 0f; p.World = true; p.Max = 400;
                p.AutoRandomSeed = false; p.RandomSeed = 13; p.Shape = ParticleShape.Sphere; p.Radius = 0.1f; p.StartSize = new Vector2(0.6f, 0.6f);
                p.TriggersOn = true; p.Triggers.Add(bubble); p.TriggerOutside = ParticleTriggerAction.Kill; p.TriggerRadiusScale = scale;
                return p;
            }
            Entity bare = Emitter(Spread(0f), Vector3.Zero), grown = Emitter(Spread(1f), Vector3.Zero);
            SceneManager.Play();
            float far0 = 0f, far1 = 0f;
            for (int i = 0; i < 240; i++)
            {
                SceneManager.UpdatePhysics(Step);
                foreach (Vector3 at in Positions(bare)) far0 = MathF.Max(far0, at.Length());
                foreach (Vector3 at in Positions(grown)) far1 = MathF.Max(far1, at.Length());
            }
            Assert.InRange(far0, 1.8f, 2.0f);
            Assert.InRange(far1, 2.15f, 2.3f);      // touching the sphere by its radius of 0.3
        }

        [Fact]
        public void Without_the_module_or_without_a_trigger_nothing_dies()
        {
            Entity gate = Body("Gate", Vector3.Zero, BoxShape(new Vector3(3f, 0.25f, 3f)));
            var off = Falling(100, 2f, gate); off.TriggerInside = ParticleTriggerAction.Kill; off.TriggersOn = false;
            var none = Falling(100, 2f, gate); none.TriggerInside = ParticleTriggerAction.Kill; none.Triggers = default;
            Entity a = Emitter(off, new Vector3(0, 3, 0)), b = Emitter(none, new Vector3(0, 3, 0));
            SceneManager.Play();
            Run(1.5f);
            Assert.Equal(100, WorldParticles.AliveOf(a));
            Assert.Equal(100, WorldParticles.AliveOf(b));
        }

        [Fact]
        public void A_directional_zone_pushes_along_its_forward_times_the_multiplier_and_only_those_taking_external_forces()
        {
            Zone(Still(WindZoneMode.Directional, 2f), new Vector3(50, 0, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 2f));
            var twice = Floating(); twice.ExternalMultiplier = 2f;
            var deaf = Floating(); deaf.ExternalForcesOn = false;
            Entity a = Emitter(Floating(), Vector3.Zero), b = Emitter(twice, new Vector3(0, 5, 0)), c = Emitter(deaf, new Vector3(0, -5, 0));
            SceneManager.Play();
            Run(1f);
            Assert.All(Velocities(a), v => { Assert.InRange(v.X, -2.05f, -1.95f); Assert.True(MathF.Abs(v.Y) + MathF.Abs(v.Z) < 1e-3f, $"{v}"); });
            Assert.All(Velocities(b), v => Assert.InRange(v.X, -4.1f, -3.9f));
            Assert.All(Velocities(c), v => Assert.True(v.Length() < 1e-4f, $"no external forces: {v}"));
        }

        [Fact]
        public void A_spherical_zone_pushes_out_of_its_centre_fading_to_nothing_at_its_radius_and_a_zone_switched_off_does_nothing()
        {
            Zone(Still(WindZoneMode.Spherical, 4f, 4f), new Vector3(-1, 0, 0));
            Entity near = Emitter(Floating(), Vector3.Zero), outside = Emitter(Floating(), new Vector3(10, 0, 0));
            SceneManager.Play();
            SceneManager.UpdatePhysics(Step);
            SceneManager.UpdatePhysics(Step);
            Vector3[] before = Velocities(near);
            SceneManager.UpdatePhysics(Step);
            Vector3[] after = Velocities(near);
            // One metre from the centre: 4 x (1 - 1/4) = 3 m/s/s, along +X (a step's change: those born in a step take the wind from the next).
            Assert.Equal(before.Length, after.Length);
            for (int i = 0; i < after.Length; i++)
            {
                Vector3 a = (after[i] - before[i]) / Step;
                Assert.True(a.X > 0f, $"{a}");
                Assert.InRange(a.Length(), 2.9f, 3.05f);
            }
            Assert.All(Velocities(outside), v => Assert.True(v.Length() < 1e-5f, $"beyond the radius: {v}"));
            SceneManager.Stop();

            WorldParticles.Clear();
            World.Clear();
            SceneManager.NewScene("Off");
            Entity z = Zone(Still(WindZoneMode.Spherical, 4f, 4f), new Vector3(-1, 0, 0));
            World.AddComponent(z, new ActiveFlag { Active = false });
            Entity e = Emitter(Floating(), Vector3.Zero);
            SceneManager.Play();
            Run(0.5f);
            Assert.All(Velocities(e), v => Assert.True(v.Length() < 1e-5f, $"a zone switched off: {v}"));
        }

        [Fact]
        public void The_trigger_and_wind_fields_and_the_wind_zone_are_written_and_read_back()
        {
            Entity bell = Body("Bell", Vector3.Zero, Ball(1f));
            var p = Falling(5, 1f, bell); p.TriggerInside = ParticleTriggerAction.Kill; p.TriggerOutside = ParticleTriggerAction.Callback;
            p.TriggerEnter = ParticleTriggerAction.Callback; p.TriggerExit = ParticleTriggerAction.Kill; p.TriggerRadiusScale = 0.5f;
            p.ExternalForcesOn = true; p.ExternalMultiplier = 3f;
            Emitter(p, Vector3.Zero);
            var wz = new WindZone { Mode = WindZoneMode.Spherical, Radius = 7f, Main = 2.5f, Turbulence = 0.3f, PulseMagnitude = 0.2f, PulseFrequency = 0.4f };
            Zone(wz, Vector3.One);
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            ParticlesComponent read = default; Entity readBell = default; WindZone readZone = default;
            foreach (Entity e in World.Entities)
            {
                if (World.TryGetComponent<ParticlesComponent>(e, out var got)) read = got;
                if (World.TryGetComponent<WindZone>(e, out var z)) readZone = z;
                if (World.GetComponent<NameTag>(e).Name == "Bell") readBell = e;
            }
            Assert.Equal(1, read.Triggers.Count);
            Assert.Equal(readBell, read.Triggers.Items[0]);
            p.Triggers = read.Triggers;
            Assert.Equal(p, read);
            Assert.Equal(wz, readZone);
        }
    }
}
