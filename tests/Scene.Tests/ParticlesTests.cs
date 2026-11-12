using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>Particles in the world (H5 step K8): made at their rate and in bursts, held by the pool, aged with the game's
    /// time, moved by gravity, left behind or carried along, the block, and nothing allocated once running.</summary>
    public sealed class ParticlesTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        public ParticlesTests()
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

        static Entity Emitter(ParticlesComponent p, Vector3 at = default)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity("Emitter");
            var t = World.GetComponent<Transform>(e);
            t.Position = at;
            World.AddComponent(e, t);
            World.AddComponent(e, p);
            return e;
        }

        static void Run(float seconds, float dt = 1f / 60f)
        {
            for (int i = 0; i < (int)MathF.Round(seconds / dt); i++) WorldParticles.Step(World, dt);
        }

        [Fact]
        public void An_emitter_makes_its_rate_a_second_and_they_die_at_the_end_of_their_life()
        {
            var p = ParticlesComponent.Dust;
            p.Rate = 30f; p.Life = new Vector2(2f, 2f); p.Max = 200;
            Entity e = Emitter(p);
            Run(1f);
            Assert.InRange(WorldParticles.AliveOf(e), 29, 31);
            p.Emitting = false;
            World.AddComponent(e, p);
            Run(2.1f);
            Assert.Equal(0, WorldParticles.AliveOf(e));
        }

        [Fact]
        public void A_burst_makes_its_count_at_once_and_the_pool_holds_no_more_than_Max()
        {
            var p = ParticlesComponent.Sparks;
            p.Emitting = false; p.Burst = 20; p.Max = 32; p.Life = new Vector2(5f, 5f);
            Entity e = Emitter(p);
            WorldParticles.Step(World, 1f / 60f);
            Assert.Equal(0, WorldParticles.AliveOf(e));
            WorldParticles.Burst(e);
            WorldParticles.Step(World, 1f / 60f);
            Assert.Equal(20, WorldParticles.AliveOf(e));
            WorldParticles.Burst(e);
            WorldParticles.Step(World, 1f / 60f);
            Assert.Equal(32, WorldParticles.AliveOf(e));
        }

        [Fact]
        public void With_the_game_s_time_stood_still_nothing_moves_ages_or_is_made()
        {
            var p = ParticlesComponent.Dust;
            p.Life = new Vector2(0.5f, 0.5f);
            Entity e = Emitter(p);
            Run(0.3f);
            int n = WorldParticles.AliveOf(e);
            Span<Vector3> before = stackalloc Vector3[64], after = stackalloc Vector3[64];
            WorldParticles.CopyPositions(e, before);
            for (int i = 0; i < 120; i++) WorldParticles.Step(World, 0f);     // two seconds of frames with no game time
            WorldParticles.CopyPositions(e, after);
            Assert.Equal(n, WorldParticles.AliveOf(e));
            Assert.True(before.Slice(0, n).SequenceEqual(after.Slice(0, n)));
        }

        [Fact]
        public void Gravity_pulls_sparks_down_and_dust_with_a_negative_gravity_rises()
        {
            var s = ParticlesComponent.Sparks;
            s.Emitting = false; s.Burst = 30; s.Life = new Vector2(3f, 3f); s.Direction = Vector3.UnitX; s.Spread = 0f; s.Drag = 0f;
            Entity sparks = Emitter(s, new Vector3(0, 10, 0));
            var d = ParticlesComponent.Dust;
            d.Emitting = false; d.Burst = 30; d.Life = new Vector2(3f, 3f); d.Direction = Vector3.UnitX; d.Spread = 0f;
            Entity dust = Emitter(d, new Vector3(0, 10, 0));
            WorldParticles.Burst(sparks); WorldParticles.Burst(dust);
            Run(1f);
            Span<Vector3> at = stackalloc Vector3[30];
            WorldParticles.CopyPositions(sparks, at);
            Assert.InRange(at[0].Y, 10f - 4.9f - 0.3f, 10f - 4.9f + 0.3f);      // ½ g t², sideways only at first
            WorldParticles.CopyPositions(dust, at);
            Assert.True(at[0].Y > 10.1f, $"dust rises: {at[0].Y}");
        }

        [Fact]
        public void World_particles_stay_where_they_were_made_and_the_others_go_with_the_emitter()
        {
            var w = ParticlesComponent.Dust;
            w.Emitting = false; w.Burst = 10; w.Life = new Vector2(5f, 5f); w.Speed = Vector2.Zero; w.Gravity = 0f; w.World = true;
            var l = w; l.World = false;
            Entity left = Emitter(w, new Vector3(2, 0, 0)), carried = Emitter(l, new Vector3(2, 0, 0));   // made away from 0
            WorldParticles.Burst(left); WorldParticles.Burst(carried);
            WorldParticles.Step(World, 1f / 60f);
            foreach (Entity e in new[] { left, carried })
            {
                var t = World.GetComponent<Transform>(e);
                t.Position = new Vector3(5, 0, 0);
                World.AddComponent(e, t);
            }
            WorldParticles.Step(World, 1f / 60f);
            Span<Vector3> at = stackalloc Vector3[10];
            WorldParticles.CopyPositions(left, at);
            Assert.Equal(2f, at[0].X, 3);
            WorldParticles.CopyPositions(carried, at);
            Assert.Equal(5f, at[0].X, 3);
        }

        [Fact]
        public void Running_particles_allocate_nothing_and_an_emitter_taken_away_takes_its_particles()
        {
            Entity a = Emitter(ParticlesComponent.Dust), b = Emitter(ParticlesComponent.Sparks), c = Emitter(ParticlesComponent.Flame);
            Run(1f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            Run(2f);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.True(WorldParticles.AliveOf(c) > 0);
            World.RemoveComponent<ParticlesComponent>(c);
            WorldParticles.Step(World, 1f / 60f);
            Assert.Equal(0, WorldParticles.AliveOf(c));
            Assert.True(WorldParticles.AliveOf(a) > 0 && WorldParticles.AliveOf(b) > 0);
        }

        [Fact]
        public void The_Particles_block_is_written_and_read_back()
        {
            var p = ParticlesComponent.Flame;
            p.Texture = "Sheets/fx.sprites#flame"; p.Burst = 7; p.Direction = new Vector3(0, 0.5f, -1);
            Emitter(p);
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            Assert.Contains("\"Particles\":{\"Texture\":\"Sheets/fx.sprites#flame\",\"Emitting\":true,", json);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            ParticlesComponent read = default;
            foreach (Entity e in World.Entities)
                if (World.TryGetComponent<ParticlesComponent>(e, out var got)) read = got;
            Assert.Equal((p.Texture, p.Burst, p.Direction, p.World, p.Max, p.ColorEnd), (read.Texture, read.Burst, read.Direction, read.World, read.Max, read.ColorEnd));
        }
    }
}
