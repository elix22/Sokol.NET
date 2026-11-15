using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// A frame of play in the reference editor's order (SceneManager.PlayFrame, which every frame loop calls): physics,
    /// the scripts, then the particles - so an emitter a script moves emits from where it is now, at the speed it moved
    /// this frame. (They stepped before the scripts: a moving emitter's particles carried the last frame's speed.)
    /// </summary>
    public sealed class FrameOrderTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const float Step = 1f / 60f;

        sealed class NoPhysics : ISceneSimulator
        {
            public void Start(ECSWorld world) { }
            public void Step(float dt) { }
            public void Stop() { }
        }

        static int Frame;

        /// <summary>Moves its entity along X faster every frame: frame n at 2n m/s.</summary>
        public sealed class Speeder : GameBehaviour
        {
            public override void OnUpdate(float deltaTime)
            {
                Frame++;
                var t = World.GetComponent<Transform>(EntityId);
                t.Position = t.Position + new Vector3(2f * Frame * deltaTime, 0, 0);
                World.AddComponent(EntityId, t);
            }
        }

        public FrameOrderTests()
        {
            World.Clear();
            SceneManager.SimulatorFactory = _ => new NoPhysics();
            ScriptSystem.RegisterType<Speeder>();
            SceneManager.NewScene("Order");
            SceneManager.ActiveScene!.PhysicsBackend = "none";
            Frame = 0;
        }

        public void Dispose()
        {
            SceneManager.Stop();
            SceneManager.SimulatorFactory = null;
            ScriptSystem.ClearFactories();
            World.Clear();
        }

        [Fact]
        public void A_script_moving_an_emitter_its_newest_particle_carries_this_frame_s_speed()
        {
            var p = ParticlesComponent.Base;
            p.Rate = 120f; p.Life = new Vector2(5f, 5f); p.Speed = Vector2.Zero; p.World = true; p.ShapeOn = false;
            p.InheritVelocityOn = true; p.InheritVelocityMode = ParticleInheritVelocity.Initial; p.InheritVelocity = 1f;
            Entity e = SceneManager.ActiveScene!.CreateEntity("Emitter");
            World.AddComponent(e, p);
            World.AddComponent(e, new ScriptComponent { TypeName = nameof(Speeder) });
            SceneManager.Play();
            for (int i = 0; i < 20; i++) SceneManager.PlayFrame(Step);

            var ages = new float[1024];
            var vels = new Vector3[1024];
            int n = WorldParticles.CopyAges(e, ages);
            Assert.Equal(n, WorldParticles.CopyVelocities(e, vels));
            Assert.True(n > 0, "the emitter has particles");
            int newest = 0;
            for (int k = 1; k < n; k++) if (ages[k] < ages[newest]) newest = k;
            // Frame 20 moved it at 40 m/s; the frame before at 38.
            Assert.InRange(vels[newest].X, 39.5f, 40.5f);
        }
    }
}
