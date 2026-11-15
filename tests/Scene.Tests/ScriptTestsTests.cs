using System;
using System.Collections.Generic;
using GameEditor.Framework.Determinism;
using GameEditor.Framework.Diagnostics;
using GameEditor.Framework.ECS;
using GameEditor.Framework.Scene;
using Xunit;
using ScriptTests = GameEditor.Framework.Diagnostics.Tests;

namespace Scene.Tests
{
    /// <summary>Script tests (G11.6, examples/GameEditor/docs/QOL_DESIGN.md): the asserts' messages, Run's results (a
    /// failure, a crash, the golden tests with them, a chosen few), a name registered again replaced, none left over when
    /// a scene plays again.</summary>
    public sealed class ScriptTestsTests : IDisposable
    {
        public ScriptTestsTests() { ScriptTests.Clear(); GoldenTests.Clear(); }
        public void Dispose() { ScriptTests.Clear(); GoldenTests.Clear(); SceneManager.Stop(); SceneManager.SimulatorFactory = null; ECSWorld.Instance.Clear(); }

        static string Fails(Action a)
        {
            try { a(); }
            catch (TestFailure f) { return f.Message; }
            return "(passed)";
        }

        [Fact]
        public void The_asserts_say_what_was_expected()
        {
            Assert.Equal("two: expected 2, got 3", Fails(() => ScriptTests.Equal(2, 3, "two")));
            Assert.Equal("expected \"a\", got \"b\"", Fails(() => ScriptTests.Equal("a", "b")));
            Assert.Equal("(passed)", Fails(() => ScriptTests.Equal(4, 2 + 2)));
            Assert.Equal("jump: expected 2 ± 0.01, got 2.5", Fails(() => ScriptTests.Near(2f, 2.5f, 0.01f, "jump")));
            Assert.Equal("(passed)", Fails(() => ScriptTests.Near(1f, 1.0004f, 0.001f)));
            Assert.NotEqual("(passed)", Fails(() => ScriptTests.Near(1f, float.NaN, 1f)));   // NaN is never near
            Assert.Equal("the door opens", Fails(() => ScriptTests.True(false, "the door opens")));
        }

        [Fact]
        public void Run_gives_each_tests_outcome_the_golden_ones_too()
        {
            ScriptTests.Add("adds", () => ScriptTests.Equal(4, 2 + 2));
            ScriptTests.Add("fails", () => ScriptTests.Equal(2, 3));
            ScriptTests.Add("crashes", () => throw new InvalidOperationException("boom"));
            ScriptTests.Add("adds", () => ScriptTests.Equal(5, 2 + 3));   // again: replaced where it was
            GoldenTests.Add("tiny", 0, (ref StateHash h) => h.Add(42));
            Assert.Equal(new List<string> { "adds", "fails", "crashes", "golden: tiny" }, ScriptTests.Names());

            var all = ScriptTests.Run();
            Assert.Equal(4, all.Count);
            Assert.True(all[0].Passed);
            Assert.False(all[1].Passed); Assert.Equal("expected 2, got 3", all[1].Message);
            Assert.False(all[2].Passed); Assert.Equal("InvalidOperationException: boom", all[2].Message);
            Assert.True(all[3].Passed); Assert.Equal("golden: tiny", all[3].Name); Assert.Contains("not pinned", all[3].Message);
            Assert.All(all, r => Assert.True(r.Ms >= 0));

            var some = ScriptTests.Run(new HashSet<string> { "fails" });
            Assert.Single(some);
            Assert.Equal("fails", some[0].Name);
        }

        sealed class NoPhysics : ISceneSimulator
        {
            public void Start(ECSWorld world) { }
            public void Step(float dt) { }
            public void Stop() { }
        }

        [Fact]
        public void Playing_a_scene_starts_from_no_tests()
        {
            ScriptTests.Add("left over", () => { });
            GoldenTests.Add("left over", 0, (ref StateHash h) => { });
            SceneManager.SimulatorFactory = _ => new NoPhysics();
            SceneManager.NewScene("T");
            SceneManager.ActiveScene!.PhysicsBackend = "none";
            SceneManager.Play();
            Assert.Empty(ScriptTests.Names());   // the scripts register theirs again as they start
        }
    }
}
