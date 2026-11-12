using System.Numerics;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Input;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// A game's own time (a pause screen, slow motion): what stands still with it and what goes on; and the Back
    /// key of a phone as an Escape press.
    /// </summary>
    public sealed class TimeScaleTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const float Step = 1f / 60f;

        sealed class NoPhysics : ISceneSimulator
        {
            public void Start(ECSWorld world) { }
            public void Step(float dt) { }
            public void Stop() { }
        }

        static int Updates;
        static float Sum, RealSum, Seen;

        public sealed class Clock : GameBehaviour
        {
            public override void OnUpdate(float deltaTime)
            {
                Updates++;
                Sum += deltaTime;
                RealSum += UnscaledDeltaTime;
                Seen = TimeScale;
            }
        }

        public TimeScaleTests()
        {
            World.Clear();
            SceneManager.SimulatorFactory = _ => new NoPhysics();
            ScriptSystem.RegisterType<Clock>();
            SceneManager.NewScene("Flat");
            SceneManager.ActiveScene!.PhysicsBackend = "none";
            Updates = 0; Sum = RealSum = Seen = 0f;
        }

        public void Dispose()
        {
            SceneManager.Stop();
            SceneManager.TimeScale = 1f;
            SceneManager.SimulatorFactory = null;
            ScriptSystem.ClearFactories();
            World.Clear();
        }

        static void Run(int frames)
        {
            for (int i = 0; i < frames; i++) { SceneManager.UpdatePhysics(Step); ScriptSystem.UpdateAll(Step); }
        }

        [Fact]
        public void With_the_time_stopped_nothing_falls_scripts_still_run_and_the_real_time_is_told()
        {
            Entity ball = SceneManager.ActiveScene!.CreateEntity("Ball");
            Body2D body = Body2D.Default;
            body.Shapes = new[] { new Shape2D { Type = Shape2DType.Circle, Radius = 0.5f } };
            World.AddComponent(ball, body);
            World.AddComponent(ball, new ScriptComponent { TypeName = nameof(Clock) });
            float Y() => World.GetComponent<Transform>(ball).Position.Y;

            SceneManager.Play();
            Run(30);                                             // half a second of falling
            float fell = -Y();
            Assert.InRange(fell, 0.9f, 1.4f);                    // about g/8
            Assert.Equal(30, Updates);
            Assert.Equal(0.5f, Sum, 3);

            SceneManager.TimeScale = 0f;                         // a pause screen
            float at = Y();
            Run(30);
            Assert.Equal(at, Y());                               // the ball hangs where it was
            Assert.Equal(60, Updates);                           // the scripts ran
            Assert.Equal(0.5f, Sum, 3);                          // with no time
            Assert.Equal(1f, RealSum, 3);                        // and know the real one
            Assert.Equal(0f, Seen);

            SceneManager.TimeScale = 0.5f;                       // half as fast
            Run(60);
            Assert.Equal(1f, Sum, 3);
            Assert.True(Y() < at - 0.5f);                        // it falls again, from the speed it had

            SceneManager.TimeScale = float.NaN;                  // nonsense is the clock's own time
            Assert.Equal(1f, SceneManager.TimeScale);
            SceneManager.TimeScale = -3f;
            Assert.Equal(0f, SceneManager.TimeScale);
        }

        [Fact]
        public void A_game_starts_with_the_clocks_time_whatever_the_last_one_left()
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity("Clock");
            World.AddComponent(e, new ScriptComponent { TypeName = nameof(Clock) });
            SceneManager.Play();
            SceneManager.TimeScale = 0f;
            Run(3);
            SceneManager.Stop();
            SceneManager.Play();
            Assert.Equal(1f, SceneManager.TimeScale);
            Sum = 0f;
            Run(6);
            Assert.Equal(0.1f, Sum, 3);
        }

        [Fact]
        public void A_sprite_animation_stands_with_the_games_time()
        {
            const string sheet = "Sprites/hero.sprites";
            SpriteLibrary.Clear();
            SpriteLibrary.Reader = key => key != sheet ? null : """
                { "sprites": 1, "image": "Textures/hero.png", "ppu": 32, "grid": { "w": 32, "h": 32 },
                  "clips": { "walk": { "frames": ["hero_0", "hero_1", "hero_2", "hero_3"], "fps": 10, "loop": true } } }
                """;
            try
            {
                Entity e = SceneManager.ActiveScene!.CreateEntity("Walker");
                var sprite = SpriteRenderer.Default; sprite.Texture = sheet + "#hero_0";
                World.AddComponent(e, sprite);
                World.AddComponent(e, new SpriteAnimator { Clip = "walk", Speed = 1f, Playing = true });
                World.AddComponent(e, new ScriptComponent { TypeName = nameof(Clock) });
                int Frame() => World.GetComponent<SpriteAnimator>(e).Frame;

                SceneManager.Play();
                Run(7);                                          // ten frames a second: the second one after six steps
                Assert.Equal(1, Frame());
                SceneManager.TimeScale = 0f;
                Run(30);
                Assert.Equal(1, Frame());                        // half a second later: the same frame
                SceneManager.TimeScale = 1f;
                Run(6);
                Assert.Equal(2, Frame());
            }
            finally
            {
                SpriteLibrary.Reader = null;
                SpriteLibrary.Clear();
            }
        }

        [Fact]
        public void The_Back_key_made_an_Escape_press_lasts_one_frame()
        {
            InputManager.BeginFrame();
            InputManager.PressBack();                            // between two frames, as the key's event comes
            Assert.False(InputManager.GetKeyDown(Key.Escape));

            InputManager.BeginFrame();
            Assert.True(InputManager.GetKeyDown(Key.Escape));
            Assert.True(InputManager.GetButtonDown("Cancel"));

            InputManager.BeginFrame();
            Assert.False(InputManager.GetKeyDown(Key.Escape));
            InputManager.BeginFrame();
            Assert.False(InputManager.GetKey(Key.Escape));
            Assert.False(InputManager.GetKeyDown(Key.Escape));
        }
    }
}
