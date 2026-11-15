using System;
using System.Numerics;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.Determinism;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The determinism kit (G10, examples/GameEditor/docs/DETERMINISM_DESIGN.md): DetRandom's sequence pinned and
    /// spread, DetMath within 2e-6 and its bits pinned, Fixed exact, StateHash pinned, the golden runner, and the fixed tick
    /// (50 a second whatever the frames, 25 at Time Scale 0.5, at most 8 a frame, none paused).</summary>
    public sealed class DeterminismTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        sealed class NoPhysics : ISceneSimulator
        {
            public void Start(ECSWorld world) { }
            public void Step(float dt) { }
            public void Stop() { }
        }

        public sealed class Ticker : GameBehaviour
        {
            public static int Ticks, Updates, TicksAtFirstUpdate = -1;
            public override void OnFixedUpdate() => Ticks++;
            public override void OnUpdate(float deltaTime) { if (Updates++ == 0) TicksAtFirstUpdate = Ticks; }
        }

        public DeterminismTests()
        {
            World.Clear();
            SceneManager.SimulatorFactory = _ => new NoPhysics();
            ScriptSystem.RegisterType<Ticker>();
            Ticker.Ticks = 0; Ticker.Updates = 0; Ticker.TicksAtFirstUpdate = -1;
            Time.FixedDeltaTime = 0.02f;
            GoldenTests.Clear();
        }

        public void Dispose()
        {
            SceneManager.Stop();
            SceneManager.TimeScale = 1f;
            SceneManager.SimulatorFactory = null;
            ScriptSystem.ClearFactories();
            GoldenTests.Clear();
            Time.FixedDeltaTime = 0.02f;
            World.Clear();
        }

        // ── DetRandom ────────────────────────────────────────────────────────────────────

        [Fact]
        public void DetRandom_sequence_is_pinned()
        {
            // xorshift32 (13, 17, 5) from 12345, first step mixed in, each value the state less 1 (worked out apart, in Python).
            var r = new DetRandom(12345);
            uint[] want = { 0x652a09ae, 0xa7e08f9f, 0x748e41e9, 0x2ad8a9d2, 0xc3b81261 };
            foreach (uint w in want) Assert.Equal(w, r.NextUInt());

            // State saved and put back: the same values again.
            var a = new DetRandom(7);
            a.NextUInt();
            uint saved = a.State;
            uint x1 = a.NextUInt(), x2 = a.NextUInt();
            a.State = saved;
            Assert.Equal(x1, a.NextUInt());
            Assert.Equal(x2, a.NextUInt());

            // Seed 0 is not stuck at 0.
            var z = new DetRandom(0);
            Assert.NotEqual(z.NextUInt(), z.NextUInt());
        }

        [Fact]
        public void DetRandom_spreads_evenly_and_keeps_its_ranges()
        {
            var r = new DetRandom(99);
            const int N = 100_000, Bins = 10;
            var counts = new int[Bins];
            for (int i = 0; i < N; i++)
            {
                float f = r.NextFloat();
                Assert.InRange(f, 0f, 0.99999994f);
                counts[(int)(f * Bins)]++;
            }
            double expect = N / (double)Bins, chi2 = 0;
            foreach (int c in counts) chi2 += (c - expect) * (c - expect) / expect;
            Assert.True(chi2 < 27.9, $"chi2 {chi2:0.0} (9 degrees of freedom, p = 0.001 at 27.9)");

            bool sawMin = false, sawMax = false;
            for (int i = 0; i < 10_000; i++)
            {
                int v = r.NextInt(-3, 4);
                Assert.InRange(v, -3, 3);
                sawMin |= v == -3; sawMax |= v == 3;
                Assert.InRange(r.NextFloat(2f, 5f), 2f, 5f);
                Assert.True(r.NextUInt(6) < 6);
            }
            Assert.True(sawMin && sawMax);
            Assert.Equal(5, r.NextInt(5, 5));
        }

        // ── DetMath ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void DetMath_is_within_2e6_of_the_true_value()
        {
            float worstSin = 0, worstCos = 0, worstAtan2 = 0;
            for (int i = -20000; i <= 20000; i++)
            {
                float x = i * 0.0017f;   // about ±34 radians
                worstSin = MathF.Max(worstSin, MathF.Abs(DetMath.Sin(x) - (float)Math.Sin(x)));
                worstCos = MathF.Max(worstCos, MathF.Abs(DetMath.Cos(x) - (float)Math.Cos(x)));
            }
            var r = new DetRandom(3);
            for (int i = 0; i < 20000; i++)
            {
                float y = r.NextFloat(-10f, 10f), x = r.NextFloat(-10f, 10f);
                worstAtan2 = MathF.Max(worstAtan2, MathF.Abs(DetMath.Atan2(y, x) - (float)Math.Atan2(y, x)));
            }
            Assert.True(worstSin <= 2e-6f, $"sin off by {worstSin}");
            Assert.True(worstCos <= 2e-6f, $"cos off by {worstCos}");
            Assert.True(worstAtan2 <= 2e-6f, $"atan2 off by {worstAtan2}");
            Assert.Equal(0f, DetMath.Atan2(0f, 1f));
            Assert.Equal(DetMath.PI, DetMath.Atan2(0f, -1f), 6);
            Assert.Equal(DetMath.PI / 2f, DetMath.Atan2(1f, 0f), 6);
        }

        [Fact]
        public void DetMath_bits_are_pinned()
        {
            // The bits every device must give (this machine's; a change of the polynomials changes them on purpose).
            var h = new StateHash();
            foreach (float x in new[] { 0f, 0.5f, 1f, 2f, 3f, -1.25f, 10f, 100f, 1000f })
            {
                DetMath.SinCos(x, out float s, out float c);
                h.Add(s); h.Add(c); h.Add(DetMath.Sin(x)); h.Add(DetMath.Cos(x)); h.Add(DetMath.Tan(x * 0.1f));
                h.Add(DetMath.Atan2(s, c + 0.5f)); h.Add(DetMath.Atan(x));
            }
            Assert.Equal(PinnedMathHash, h.Hex);
        }

        const string PinnedMathHash = "f1c926fd793df376";

        // ── Fixed ────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Fixed_arithmetic_is_exact()
        {
            Fixed three = Fixed.FromInt(3), half = Fixed.Half;
            Assert.Equal(Fixed.FromRaw(3 * 65536 + 32768), three + half);
            Assert.Equal(Fixed.FromRaw(98304), three * half);              // 1.5
            Assert.Equal(Fixed.FromInt(6), three / half);
            Assert.Equal(Fixed.FromInt(-3), -three);
            Assert.Equal(Fixed.FromInt(2), Fixed.Floor(Fixed.FromFloat(2.75f)));
            Assert.Equal(Fixed.FromInt(-3), Fixed.Floor(Fixed.FromFloat(-2.25f)));
            Assert.Equal(1.5f, (three * half).ToFloat());
            for (int i = 0; i <= 180; i++) Assert.Equal(Fixed.FromInt(i), Fixed.Sqrt(Fixed.FromInt(i * i)));
            Assert.Equal(Fixed.Zero, Fixed.Sqrt(Fixed.Zero));
            Assert.Equal(1.41421f, Fixed.Sqrt(Fixed.FromInt(2)).ToFloat(), 4);

            float worst = 0;
            for (int i = -4000; i <= 4000; i++)
            {
                Fixed a = Fixed.FromRaw(i * 100);   // about ±6.1 radians
                Fixed.SinCos(a, out var s, out var c);
                worst = MathF.Max(worst, MathF.Abs(s.ToFloat() - (float)Math.Sin(a.ToFloat())));
                worst = MathF.Max(worst, MathF.Abs(c.ToFloat() - (float)Math.Cos(a.ToFloat())));
            }
            Assert.True(worst <= 1e-4f, $"Fixed sin/cos off by {worst}");
        }

        // ── StateHash and the golden runner ──────────────────────────────────────────────

        [Fact]
        public void StateHash_is_pinned()
        {
            var h = new StateHash();
            Assert.Equal(14695981039346656037UL, h.Value);   // FNV-1a's offset: nothing added
            h.Add(1); h.Add(1f); h.Add("det");
            Assert.Equal(0x2f4c902c9588e612UL, h.Value);     // worked out apart, in Python
            var a = new StateHash(); a.Add(new Vector3(1, 2, 3));
            var b = new StateHash(); b.Add(1f); b.Add(2f); b.Add(3f);
            Assert.Equal(b.Value, a.Value);
            var c = new StateHash(); c.Add(-0f);
            var d = new StateHash(); d.Add(0f);
            Assert.NotEqual(d.Value, c.Value);                // the bits, not the value
        }

        [Fact]
        public void GoldenTests_pass_fail_and_unpinned()
        {
            static void Sim(ref StateHash h)
            {
                var r = new DetRandom(1);
                Vector2 p = Vector2.Zero;
                for (int i = 0; i < 100; i++) { DetMath.SinCos(r.NextFloat(0f, 6.28f), out float s, out float c); p += new Vector2(c, s); }
                h.Add(p);
            }
            var once = new StateHash(); Sim(ref once);
            GoldenTests.Add("ok", once.Value, Sim);
            GoldenTests.Add("wrong", once.Value ^ 1, Sim);
            GoldenTests.Add("new", 0, Sim);
            GoldenTests.Add("ok", once.Value, Sim);   // the same name again replaces it
            Assert.Equal(3, GoldenTests.Count);
            var res = GoldenTests.RunAll();
            Assert.True(res[0].Passed); Assert.False(res[1].Passed); Assert.True(res[2].Passed);
            Assert.All(res, x => Assert.Equal(once.Value, x.Hash));
        }

        // ── The fixed tick ───────────────────────────────────────────────────────────────

        void Play()
        {
            SceneManager.NewScene("Tick");
            var scene = SceneManager.ActiveScene!;
            scene.PhysicsBackend = "none";
            Entity e = scene.CreateEntity("Ticker");
            World.AddComponent(e, new ScriptComponent { TypeName = nameof(Ticker) });
            SceneManager.Play();
            Assert.Equal(PlayModeState.Playing, SceneManager.PlayMode);
        }

        static void Frames(int n, float dt)
        {
            for (int i = 0; i < n; i++) { SceneManager.UpdatePhysics(dt); ScriptSystem.UpdateAll(dt); }
        }

        [Theory]
        [InlineData(60)]
        [InlineData(30)]
        [InlineData(144)]
        public void A_second_of_frames_gives_50_ticks(int fps)
        {
            Play();
            Frames(fps, 1f / fps);
            Assert.Equal(50, Ticker.Ticks);
            Assert.Equal(50 * (double)0.02f, Time.FixedTime, 9);
        }

        [Fact]
        public void Ticks_come_before_the_frames_update()
        {
            Play();
            Frames(1, 0.05f);   // two ticks, then OnUpdate
            Assert.Equal(2, Ticker.TicksAtFirstUpdate);
        }

        [Fact]
        public void Time_scale_slows_the_tick_and_zero_stops_it()
        {
            Play();
            SceneManager.TimeScale = 0.5f;
            Frames(60, 1f / 60f);
            Assert.Equal(25, Ticker.Ticks);
            SceneManager.TimeScale = 0f;
            Frames(60, 1f / 60f);
            Assert.Equal(25, Ticker.Ticks);
        }

        [Fact]
        public void A_hitch_gives_at_most_8_and_no_backlog()
        {
            Play();
            Frames(1, 1f);
            Assert.Equal(8, Ticker.Ticks);
            Frames(1, 1f / 60f);                // the lost time is not made up
            Assert.Equal(8, Ticker.Ticks);
        }

        [Fact]
        public void No_ticks_while_paused_and_a_new_play_starts_from_zero()
        {
            Play();
            Frames(6, 1f / 60f);
            int t = Ticker.Ticks;
            SceneManager.Pause();
            Frames(60, 1f / 60f);
            Assert.Equal(t, Ticker.Ticks);
            SceneManager.Stop();
            Play();
            Assert.Equal(0.0, Time.FixedTime);
        }

        [Fact]
        public void The_fixed_timestep_is_the_settings()
        {
            Time.FixedDeltaTime = 0.01f;
            Play();
            Frames(60, 1f / 60f);
            Assert.InRange(Ticker.Ticks, 99, 100);
            Time.FixedDeltaTime = 0f;
            Assert.True(Time.FixedDeltaTime > 0f);   // never 0: the loop would not end
        }
    }
}
