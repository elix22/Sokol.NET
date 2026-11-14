using System.Numerics;
using System.Text.Json;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The particle system's modules (G8.7, examples/GameEditor/docs/PARTICLES_3D_DESIGN.md): the curve and the gradient,
    /// the shapes, the system's time (loops, delay, bursts, prewarm), rate over distance, the over-lifetime modules, noise,
    /// playing from scripts, the block, and a block of before G8.7 making the very same particles as the code of before.</summary>
    public sealed class ParticleModulesTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        public ParticleModulesTests()
        {
            World.Clear();
            WorldParticles.Clear();
            SceneManager.NewScene("Particles");
        }

        public void Dispose()
        {
            WorldParticles.Clear();
            World.Clear();
        }

        static Entity Emitter(ParticlesComponent p, Vector3 at = default, Entity? parent = null)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity("Emitter");
            var t = World.GetComponent<Transform>(e);
            t.Position = at;
            t.Parent = parent;
            World.AddComponent(e, t);
            World.AddComponent(e, p);
            return e;
        }

        static void Run(float seconds, float dt = 1f / 60f)
        {
            for (int i = 0; i < (int)MathF.Round(seconds / dt); i++) WorldParticles.Step(World, dt);
        }

        static Vector3[] Positions(Entity e)
        {
            var at = new Vector3[4096];
            int n = WorldParticles.CopyPositions(e, at);
            return at[..n];
        }

        // An emitter that makes a burst of still particles that live long: where its shape puts them.
        static ParticlesComponent Still(ParticleShape shape, int count = 400)
        {
            var p = ParticlesComponent.Base;
            p.Emitting = true; p.Rate = 0f; p.Speed = Vector2.Zero; p.Life = new Vector2(100f, 100f); p.Max = 4096;
            p.Shape = shape;
            p.Bursts.Add(ParticleBurst.At(0f, count));
            return p;
        }

        [Fact]
        public void A_curve_of_two_keys_is_a_straight_line_and_more_keys_pass_through_each_key()
        {
            var line = ParticleCurve.Line(0.2f, 1f);
            Assert.Equal(0.6f, line.Evaluate(0.5f), 5);
            Assert.Equal(0.2f, line.Evaluate(-1f), 5);
            var bell = ParticleCurve.Of(new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 0f));
            Assert.Equal(1f, bell.Evaluate(0.5f), 5);
            Assert.InRange(bell.Evaluate(0.25f), 0.5f, 1f);
            Assert.Equal(bell.Evaluate(0.25f), bell.Evaluate(0.75f), 4);
            bell.Multiplier = 2f;
            Assert.Equal(2f, bell.Evaluate(0.5f), 5);
        }

        [Fact]
        public void A_gradient_blends_its_colour_and_alpha_keys_apart_and_Fixed_holds_each_key()
        {
            var g = ParticleGradient.Two(new Vector4(1, 0, 0, 1), new Vector4(0, 0, 1, 0));
            Assert.Equal(new Vector4(0.5f, 0f, 0.5f, 0.5f), g.Evaluate(0.5f));
            g.AddAlpha(0.25f, 1f, out _);
            Assert.Equal(1f, g.Evaluate(0.25f).W, 5);
            Assert.Equal(0.5f, g.Evaluate(0.625f).W, 4);
            g.AddColor(0.5f, new Vector3(0, 1, 0), out _);
            g.Fixed = true;
            Assert.Equal(new Vector3(0, 1, 0), new Vector3(g.Evaluate(0.3f).X, g.Evaluate(0.3f).Y, g.Evaluate(0.3f).Z));
            var copy = g;
            copy.RemoveColor(1);
            Assert.NotEqual(g, copy);              // a copy is a copy: the keys are values
            Assert.Equal(3, g.ColorCount);
        }

        [Fact]
        public void Each_shape_puts_its_particles_where_it_is()
        {
            var s = Still(ParticleShape.Sphere); s.Radius = 2f;
            var shell = Still(ParticleShape.Sphere); shell.Radius = 2f; shell.RadiusThickness = 0f;
            var hemi = Still(ParticleShape.Hemisphere); hemi.Radius = 1f;
            var box = Still(ParticleShape.Box); box.ShapeScale = new Vector3(2f, 1f, 4f);
            var circle = Still(ParticleShape.Circle); circle.Radius = 1.5f;
            var edge = Still(ParticleShape.Edge); edge.Radius = 3f;
            var arc = Still(ParticleShape.Circle); arc.Radius = 1f; arc.Arc = 90f; arc.RadiusThickness = 0f;
            Vector3 o = new(10, 0, 0);
            Entity[] e = { Emitter(s, o), Emitter(shell, o), Emitter(hemi, o), Emitter(box, o), Emitter(circle, o), Emitter(edge, o), Emitter(arc, o) };
            WorldParticles.Step(World, 1f / 60f);
            foreach (Entity x in e) Assert.Equal(400, WorldParticles.AliveOf(x));
            Assert.All(Positions(e[0]), p => Assert.True(Vector3.Distance(p, o) <= 2.0001f));
            Assert.Contains(Positions(e[0]), p => Vector3.Distance(p, o) < 1f);            // inside, not only on it
            Assert.All(Positions(e[1]), p => Assert.Equal(2f, Vector3.Distance(p, o), 3));
            Assert.All(Positions(e[2]), p => Assert.True(p.Y >= -1e-4f && Vector3.Distance(p, o) <= 1.0001f));
            Assert.All(Positions(e[3]), p => Assert.True(MathF.Abs(p.X - 10f) <= 1.0001f && MathF.Abs(p.Y) <= 0.5001f && MathF.Abs(p.Z) <= 2.0001f));
            Assert.Contains(Positions(e[3]), p => MathF.Abs(p.Z) > 1.5f);
            Assert.All(Positions(e[4]), p => Assert.True(MathF.Abs(p.Y) < 1e-4f && Vector3.Distance(p, o) <= 1.5001f));
            Assert.All(Positions(e[5]), p => Assert.True(MathF.Abs(p.Y) < 1e-4f && MathF.Abs(p.Z) < 1e-4f && MathF.Abs(p.X - 10f) <= 3.0001f));
            // A quarter of a circle: the angle from the shape's first axis between 0 and 90 degrees, all on its edge.
            Assert.All(Positions(e[6]), p => Assert.Equal(1f, Vector3.Distance(p, o), 3));
            float most = Positions(e[6]).Select(p => MathF.Atan2(p.Z, p.X - 10f)).Max() - Positions(e[6]).Select(p => MathF.Atan2(p.Z, p.X - 10f)).Min();
            Assert.InRange(most * 180f / MathF.PI, 80f, 90.01f);
        }

        [Fact]
        public void A_cone_of_no_angle_makes_a_line_along_its_axis_and_a_sphere_sends_them_outward()
        {
            var cone = ParticlesComponent.Base;
            cone.Spread = 0f; cone.Radius = 0f; cone.Speed = new Vector2(2f, 4f); cone.Life = new Vector2(9f, 9f); cone.Rate = 0f;
            cone.Direction = new Vector3(1, 0, 0); cone.Bursts.Add(ParticleBurst.At(0f, 50));
            var sphere = Still(ParticleShape.Sphere, 50); sphere.Speed = new Vector2(1f, 1f); sphere.Radius = 0.5f;
            Entity c = Emitter(cone), s = Emitter(sphere, new Vector3(0, 5, 0));
            Run(0.5f);
            Assert.All(Positions(c), p => Assert.True(MathF.Abs(p.Y) < 1e-4f && MathF.Abs(p.Z) < 1e-4f && p.X > 0.5f));
            Assert.All(Positions(s), p => Assert.True(Vector3.Distance(p, new Vector3(0, 5, 0)) > 0.45f));
        }

        [Fact]
        public void Bursts_fire_at_their_times_and_cycles_and_again_each_loop()
        {
            var p = ParticlesComponent.Base;
            p.Rate = 0f; p.Life = new Vector2(100f, 100f); p.Max = 4096; p.Duration = 3f;
            p.Bursts.Add(ParticleBurst.At(0f, 30, cycles: 2, interval: 0.5f));
            p.Bursts.Add(ParticleBurst.At(1f, 30, cycles: 2, interval: 0.5f));
            Entity e = Emitter(p);
            Run(2.5f);
            Assert.Equal(120, WorldParticles.AliveOf(e));
            Run(1f);                                           // past 3 s: the second loop's first burst
            Assert.Equal(150, WorldParticles.AliveOf(e));
            var once = p; once.Looping = false;
            Entity f = Emitter(once);
            Run(7f);
            Assert.Equal(120, WorldParticles.AliveOf(f));
            Assert.False(WorldParticles.IsPlaying(f));
        }

        [Fact]
        public void A_system_that_does_not_loop_stops_making_at_its_duration_and_is_gone_when_its_particles_are()
        {
            var p = ParticlesComponent.Base;
            p.Looping = false; p.Duration = 1f; p.Rate = 20f; p.Life = new Vector2(0.5f, 0.5f);
            Entity e = Emitter(p);
            Run(1f);
            Assert.InRange(WorldParticles.AliveOf(e), 9, 11);
            Assert.True(WorldParticles.IsAlive(e));
            Run(0.6f);
            Assert.Equal(0, WorldParticles.AliveOf(e));
            Assert.False(WorldParticles.IsAlive(e));
        }

        [Fact]
        public void The_start_delay_holds_the_first_particle_and_prewarm_starts_full()
        {
            var p = ParticlesComponent.Base;
            p.StartDelay = 1f; p.Rate = 30f; p.Life = new Vector2(2f, 2f);
            Entity e = Emitter(p);
            Run(0.95f);
            Assert.Equal(0, WorldParticles.AliveOf(e));
            Run(0.5f);
            Assert.InRange(WorldParticles.AliveOf(e), 13, 16);
            var w = ParticlesComponent.Base;
            w.Prewarm = true; w.Rate = 30f; w.Life = new Vector2(2f, 2f);
            Entity f = Emitter(w);
            WorldParticles.Step(World, 1f / 60f);
            Assert.InRange(WorldParticles.AliveOf(f), 58, 62);  // as after a whole loop: Rate x Life
        }

        [Fact]
        public void Rate_over_distance_makes_particles_as_the_emitter_moves()
        {
            var p = ParticlesComponent.Base;
            p.Rate = 0f; p.RateOverDistance = 2f; p.Life = new Vector2(50f, 50f); p.World = true;
            Entity e = Emitter(p);
            Run(0.5f);
            Assert.Equal(0, WorldParticles.AliveOf(e));
            for (int i = 1; i <= 100; i++)
            {
                var t = World.GetComponent<Transform>(e);
                t.Position = new Vector3(i * 0.1f, 0, 0);
                World.AddComponent(e, t);
                WorldParticles.Step(World, 1f / 60f);
            }
            Assert.InRange(WorldParticles.AliveOf(e), 19, 20);
        }

        [Fact]
        public void Velocity_and_force_over_lifetime_move_them_and_limit_velocity_slows_them_to_its_limit()
        {
            var v = Still(ParticleShape.Sphere, 20); v.Radius = 0f; v.VelocityOn = true; v.Velocity = new Vector3(0, 0, 3f);
            var f = Still(ParticleShape.Sphere, 20); f.Radius = 0f; f.ForceOn = true; f.Force = new Vector3(2f, 0, 0);
            var l = Still(ParticleShape.Sphere, 20); l.Radius = 0f; l.Speed = new Vector2(10f, 10f); l.LimitOn = true; l.SpeedLimit = 1f; l.Dampen = 1f; l.World = true;
            Entity ev = Emitter(v), ef = Emitter(f), el = Emitter(l);
            Run(1f);
            Assert.All(Positions(ev), p => Assert.InRange(p.Z, 2.9f, 3.0f));      // made after the first step's move: 59 steps
            Assert.All(Positions(ef), p => Assert.InRange(p.X, 0.9f, 1.1f));       // ½ a t²
            Span<Vector3> a = stackalloc Vector3[20], b = stackalloc Vector3[20];
            WorldParticles.CopyPositions(el, a);
            WorldParticles.Step(World, 0.1f);
            WorldParticles.CopyPositions(el, b);
            Assert.Equal(0.1f, Vector3.Distance(a[0], b[0]), 2);                     // at the limit: 1 m/s
        }

        [Fact]
        public void Noise_pushes_them_the_same_way_each_run_and_nothing_is_allocated_with_every_module_on()
        {
            ParticlesComponent All()
            {
                var p = ParticlesComponent.Fire;
                p.VelocityOn = true; p.Velocity = new Vector3(0.1f, 0, 0); p.ForceOn = true; p.Force = Vector3.UnitZ;
                p.LimitOn = true; p.Dampen = 0.3f; p.Drag = 0.2f; p.SheetOn = true; p.SheetColumns = 2; p.SheetRows = 2;
                p.RateOverDistance = 1f; p.Bursts.Add(ParticleBurst.At(0.5f, 10, 0, 0.25f)); p.Sort = ParticleSort.ByDistance;
                p.Shape = ParticleShape.Box; p.RandomizeDirection = 0.3f; p.SpherizeDirection = 0.2f;
                p.AutoRandomSeed = false; p.RandomSeed = 1234u;
                return p;
            }
            Entity e = Emitter(All());
            Run(1f);
            Vector3[] first = Positions(e);
            WorldParticles.Clear(); World.Clear(); SceneManager.NewScene("Particles");
            Entity again = Emitter(All());
            Run(1f);
            Assert.Equal(first, Positions(again));
            var calm = All(); calm.NoiseOn = false;
            WorldParticles.Clear(); World.Clear(); SceneManager.NewScene("Particles");
            Entity c = Emitter(calm);
            Run(1f);
            Assert.NotEqual(first, Positions(c));
            long before = GC.GetAllocatedBytesForCurrentThread();
            Run(2f);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public void Local_particles_turn_with_their_emitter()
        {
            var p = Still(ParticleShape.Cone, 1); p.Radius = 0f; p.Spread = 0f; p.Speed = new Vector2(1f, 1f); p.Direction = Vector3.UnitX; p.World = false;
            Entity e = Emitter(p);
            Run(1f);
            Assert.InRange(Positions(e)[0].X, 0.98f, 1f);
            var t = World.GetComponent<Transform>(e);
            t.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);   // a quarter turn: +X goes to -Z
            World.AddComponent(e, t);
            WorldParticles.Step(World, 1e-4f);
            Vector3 q = Positions(e)[0];
            Assert.InRange(q.Z, -1f, -0.98f);
            Assert.Equal(0f, q.X, 2);
        }

        [Fact]
        public void Scripts_play_pause_stop_restart_and_emit()
        {
            var p = ParticlesComponent.Base;
            p.PlayOnAwake = false; p.Rate = 20f; p.Life = new Vector2(10f, 10f);
            Entity e = Emitter(p);
            Run(1f);
            Assert.Equal(0, WorldParticles.AliveOf(e));
            Assert.False(WorldParticles.IsPlaying(e));
            WorldParticles.Play(e);
            Run(1f);
            int n = WorldParticles.AliveOf(e);
            Assert.InRange(n, 19, 21);
            WorldParticles.Pause(e);
            Vector3[] held = Positions(e);
            Run(1f);
            Assert.Equal(held, Positions(e));
            WorldParticles.Play(e);
            Run(0.5f);
            Assert.True(WorldParticles.AliveOf(e) > n);
            WorldParticles.Stop(e);
            int stopped = WorldParticles.AliveOf(e);
            Run(1f);
            Assert.Equal(stopped, WorldParticles.AliveOf(e));      // no more made, the living live on
            WorldParticles.Emit(e, 7);
            WorldParticles.Step(World, 1f / 60f);
            Assert.Equal(stopped + 7, WorldParticles.AliveOf(e));
            WorldParticles.Restart(e);
            Assert.Equal(0, WorldParticles.AliveOf(e));
            Assert.True(WorldParticles.IsPlaying(e));
            Assert.Equal(0f, WorldParticles.TimeOf(e));
        }

        [Fact]
        public void The_editor_s_preview_runs_only_the_selected_emitter_and_those_under_it()
        {
            Entity a = Emitter(ParticlesComponent.Dust), b = Emitter(ParticlesComponent.Dust);
            Entity child = Emitter(ParticlesComponent.Sparks, default, a);
            for (int i = 0; i < 30; i++) WorldParticles.StepPreview(World, 1f / 60f, a);
            Assert.True(WorldParticles.AliveOf(a) > 0 && WorldParticles.AliveOf(child) > 0);
            Assert.Equal(0, WorldParticles.AliveOf(b));
        }

        [Fact]
        public void Every_module_is_written_and_read_back()
        {
            var p = ParticlesComponent.Fire;
            p.Shape = ParticleShape.Box; p.BoxFrom = ParticleBoxFrom.Edge; p.ShapeScale = new Vector3(1, 2, 3); p.Sort = ParticleSort.OldestInFront;
            p.RenderMode = ParticleRenderMode.Stretched; p.SpeedScale = 0.2f; p.SheetOn = true; p.SheetColumns = 4; p.SheetRows = 2;
            p.SheetTime = ParticleSheetTime.Fps; p.Bursts.Add(ParticleBurst.At(0.25f, 12, 3, 0.1f)); p.StartColor2 = new Vector4(0.5f, 0.5f, 1f, 1f);
            p.Prewarm = true; p.StartDelay = 0.5f; p.VelocityOn = true; p.Velocity = new Vector3(1, 2, 3); p.VelocityWorld = true;
            Emitter(p);
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            ParticlesComponent read = default;
            foreach (Entity e in World.Entities)
                if (World.TryGetComponent<ParticlesComponent>(e, out var got)) read = got;
            Assert.Equal(p, read);
        }

        // H5's karts' blocks (scripts/automation/h5_game.py) under a kart turned and scaled as there; the positions after
        // the steps, from the code of before G8.7 (Sokol.NET aa3f1f45, run 2026-11-14, with the seeds it took from those
        // entities): the same particles, to the last bit.
        static readonly Vector3[] OldDust =
        {
            new(0.753482f, 1.7721201f, 2.1920693f), new(0.6771023f, 2.035235f, 1.9747669f), new(0.7588828f, 1.8296708f, 2.0923352f),
            new(0.6957847f, 1.8683599f, 2.0030472f), new(0.67548317f, 1.7940166f, 1.9575005f), new(0.79728836f, 1.9102756f, 2.017533f),
            new(0.75651914f, 1.7687943f, 2.015499f), new(0.71106994f, 1.8314748f, 2.06062f), new(0.77792645f, 1.7872008f, 2.143064f),
            new(0.671922f, 1.7868652f, 2.0647087f), new(0.6295768f, 1.7990749f, 2.0687652f), new(0.75309217f, 1.733782f, 2.0530436f),
            new(0.7663887f, 1.7716992f, 2.162642f), new(0.7308952f, 1.7270818f, 2.0929792f), new(0.7435251f, 1.7662752f, 2.2070596f),
            new(0.7590784f, 1.7659408f, 2.210089f), new(0.7600351f, 1.7638297f, 2.2280607f),
        };

        static readonly Vector3[] OldFlame =
        {
            new(-0.0017885227f, 0.12025528f, 4.8662453f), new(0.0034499771f, 0.11327965f, 4.574689f), new(0.023330998f, 0.1005901f, 4.6214375f),
            new(-0.0013911583f, 0.13402182f, 4.7625203f), new(0.024846194f, 0.18386722f, 4.4026423f), new(0.004459954f, 0.1349203f, 4.6853514f),
            new(0.007561176f, 0.10762447f, 4.718994f), new(-0.028718395f, 0.15378772f, 4.48473f), new(0.026570186f, 0.09748781f, 4.5450063f),
            new(0.02317159f, 0.17575346f, 4.355195f), new(0f, 0.120000005f, 4.92f), new(0f, 0.120000005f, 4.92f),
        };

        [Fact]
        public void A_block_of_before_makes_the_very_same_particles_as_the_code_of_before()
        {
            Entity kart = SceneManager.ActiveScene!.CreateEntity("Kart");
            var kt = World.GetComponent<Transform>(kart);
            kt.Position = new Vector3(1, 2, 3); kt.Scale = new Vector3(1, 0.4f, 1.6f); kt.Rotation = Quaternion.CreateFromYawPitchRoll(0.3f, 0, 0);
            World.AddComponent(kart, kt);
            Entity dust = SceneManager.ActiveScene!.CreateEntity("Dust");
            var t = World.GetComponent<Transform>(dust);
            t.Position = new Vector3(0, -0.6f, -0.5f); t.Parent = kart;
            World.AddComponent(dust, t);
            Read(dust, """{"Particles":{"Rate":25,"Life":[0.5,1.0],"Speed":[0.3,1.0],"Direction":[0,1,-1],"Spread":60,"Gravity":-0.3,"Drag":1.5,"Size":[0.35,1.0],"ColorStart":[0.62,0.56,0.45,0.5],"ColorEnd":[0.62,0.56,0.45,0],"World":true,"Max":40,"Emitting":true}}""");
            Seed(dust, 2723777243u);
            ParticlesComponent got = World.GetComponent<ParticlesComponent>(dust);
            Assert.Equal(new Vector2(0.35f, 1f), got.Size);
            Assert.Equal(new Vector4(0.62f, 0.56f, 0.45f, 0f), got.ColorEnd);
            Assert.True(got.LimitOn && got.ColorOn && got.SizeOn && got.ShapeOn && got.Radius == 0f && got.Bursts.Count == 0);
            for (int i = 0; i < 45; i++) WorldParticles.Step(World, 1f / 60f);
            Assert.Equal(OldDust, Positions(dust));

            WorldParticles.Clear(); World.Clear(); SceneManager.NewScene("G");
            Entity k2 = SceneManager.ActiveScene!.CreateEntity("Kart");
            var k2t = World.GetComponent<Transform>(k2);
            k2t.Scale = new Vector3(1, 0.4f, 1.6f);
            World.AddComponent(k2, k2t);
            Entity flame = SceneManager.ActiveScene!.CreateEntity("Flame");
            var ft = World.GetComponent<Transform>(flame);
            ft.Position = new Vector3(0, 0.3f, -0.55f); ft.Parent = k2;
            World.AddComponent(flame, ft);
            Read(flame, """{"Particles":{"Rate":70,"Life":[0.12,0.25],"Speed":[3,5],"Direction":[0,0,-1],"Spread":14,"Gravity":-1,"Drag":2,"Size":[0.45,0.12],"ColorStart":[1,0.8,0.3,0.9],"ColorEnd":[1,0.2,0.05,0],"World":false,"Max":32,"Emitting":true}}""");
            Seed(flame, 3874135933u);
            for (int i = 0; i < 30; i++)
            {
                k2t.Position = new Vector3(0, 0, i * 0.2f);
                World.AddComponent(k2, k2t);
                WorldParticles.Step(World, 1f / 60f);
            }
            Assert.Equal(OldFlame, Positions(flame));

            // The burst it made at the start while emitting is a burst of its Bursts now.
            Entity b = SceneManager.ActiveScene!.CreateEntity("Burster");
            Read(b, """{"Particles":{"Emitting":true,"Burst":25,"Rate":0,"Life":[5,5]}}""");
            Assert.Equal(1, World.GetComponent<ParticlesComponent>(b).Bursts.Count);
            Assert.Equal(25, World.GetComponent<ParticlesComponent>(b).Burst);
            WorldParticles.Step(World, 1f / 60f);
            Assert.Equal(25, WorldParticles.AliveOf(b));

            // The seed the code of before took from that entity (its hash), given as the Random Seed: the entity's hash
            // depends on the tests run before.
            static void Seed(Entity e, uint seed)
            {
                var p = World.GetComponent<ParticlesComponent>(e);
                p.AutoRandomSeed = false; p.RandomSeed = seed;
                World.AddComponent(e, p);
            }

            static void Read(Entity e, string json)
            {
                using var doc = JsonDocument.Parse(json);
                SceneSerializer.ReadComponents(e, doc.RootElement);
            }
        }
    }
}
