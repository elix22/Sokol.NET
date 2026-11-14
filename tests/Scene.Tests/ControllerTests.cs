using System;
using System.Collections.Generic;
using GameEditor.Framework.Animation;
using Xunit;

namespace Scene.Tests
{
    /// <summary>G6 A7 (§23): a controller's states, transitions (conditions, exit time, duration), triggers, Any State, blend trees, the file.</summary>
    public sealed class ControllerTests
    {
        static readonly Dictionary<string, float> Lengths = new() { ["Idle"] = 2f, ["Walk"] = 1f, ["Run"] = 0.5f };
        static float Len(string clip) => Lengths.TryGetValue(clip, out var l) ? l : 0f;

        static AnimatorController Make()
        {
            var c = new AnimatorController { Name = "Hero", DefaultState = "Idle" };
            c.Parameters.Add(new AnimatorParameter { Name = "Speed", Type = ParameterType.Float });
            c.Parameters.Add(new AnimatorParameter { Name = "Jump", Type = ParameterType.Trigger });
            c.Parameters.Add(new AnimatorParameter { Name = "Grounded", Type = ParameterType.Bool, Default = 1f });
            foreach (var n in new[] { "Idle", "Walk", "Run" }) c.States.Add(new AnimatorState { Name = n, Clip = n });
            var t1 = new AnimatorTransition { From = "Idle", To = "Walk", Duration = 0.2f };
            t1.Conditions.Add(new TransitionCondition { Parameter = "Speed", Mode = ConditionMode.Greater, Threshold = 0.5f });
            var t2 = new AnimatorTransition { From = "Walk", To = "Run", Duration = 0f, HasExitTime = true, ExitTime = 0.5f };
            t2.Conditions.Add(new TransitionCondition { Parameter = "Speed", Mode = ConditionMode.Greater, Threshold = 2f });
            var any = new AnimatorTransition { From = "", To = "Idle", Duration = 0.1f };
            any.Conditions.Add(new TransitionCondition { Parameter = "Jump", Mode = ConditionMode.If });
            c.Transitions.AddRange(new[] { t1, t2, any });
            return c;
        }

        static void Run(ControllerInstance i, float seconds, float dt = 1f / 60f) { for (float t = 0; t < seconds - 1e-5f; t += dt) i.Update(dt); }

        [Fact]
        public void Starts_InTheDefaultState_ConditionsMove_It_TheTransitionCrossfades()
        {
            var i = new ControllerInstance(Make(), Len);
            Assert.Equal("Idle", i.Current!.Name);
            Run(i, 0.5f);
            Assert.Equal("Idle", i.Current.Name);
            i.SetFloat("Speed", 1f);
            i.Update(1f / 60f);
            Assert.True(i.TransitionStarted);
            Assert.Equal("Walk", i.Next!.Name);
            Run(i, 0.1f);
            Assert.InRange(i.TransitionWeight, 0.4f, 0.65f);                 // half of the 0.2 s fade
            Run(i, 0.15f);
            Assert.Equal("Walk", i.Current.Name);
            Assert.Null(i.Transition);
        }

        [Fact]
        public void ExitTime_WaitsForItsPointInTheRound()
        {
            var i = new ControllerInstance(Make(), Len);
            i.Play("Walk");
            i.SetFloat("Speed", 3f);
            Run(i, 0.3f);                                                     // 0.3 of Walk's 1 s: before its exit time 0.5
            Assert.Equal("Walk", i.Current!.Name);
            Run(i, 0.25f);
            Assert.Equal("Run", i.Current.Name);                              // duration 0: at once
        }

        [Fact]
        public void Trigger_FromAnyState_UsedOnce()
        {
            var i = new ControllerInstance(Make(), Len);
            i.Play("Run");
            i.SetTrigger("Jump");
            i.Update(1f / 60f);
            Assert.Equal("Idle", i.Next!.Name);
            Assert.False(i.GetBool("Jump"));                                  // reset by the transition that took it
            Run(i, 0.2f);
            Assert.Equal("Idle", i.Current!.Name);
            Run(i, 0.5f);
            Assert.Equal("Idle", i.Current.Name);                             // not taken again: the trigger is gone
            Assert.True(i.GetBool("Grounded"));                               // a Bool's default
        }

        [Fact]
        public void BlendTree_Neighbours_ByTheParameter()
        {
            var c = Make();
            var tree = new AnimatorState { Name = "Move", BlendParameter = "Speed" };
            tree.Blend.AddRange(new[] { new BlendMotion { Clip = "Idle", Threshold = 0 }, new BlendMotion { Clip = "Walk", Threshold = 1 }, new BlendMotion { Clip = "Run", Threshold = 3 } });
            c.States.Add(tree);
            var i = new ControllerInstance(c, Len);
            i.SetFloat("Speed", 2f);
            Assert.Equal(("Walk", "Run", 0.5f), i.BlendOf(tree));
            i.SetFloat("Speed", -1f);
            Assert.Equal(("Idle", "", 0f), i.BlendOf(tree));
            i.SetFloat("Speed", 9f);
            Assert.Equal(("Run", "", 0f), i.BlendOf(tree));
        }

        [Fact]
        public void File_RoundTrip()
        {
            var c = Make();
            c.States[1].Position = new System.Numerics.Vector2(200, 40);
            string json = ControllerJson.Write(c);
            var back = ControllerJson.Read(json);
            Assert.Equal(json, ControllerJson.Write(back));
            Assert.Equal("Idle", back.DefaultState);
            Assert.Equal(3, back.Transitions.Count);
            Assert.True(back.Transitions[1].HasExitTime);
            Assert.Equal(0.5f, back.Transitions[1].ExitTime);
            Assert.Equal(ParameterType.Trigger, back.Parameter("Jump")!.Type);
            Assert.Equal(200f, back.State("Walk")!.Position.X);
            Assert.Throws<FormatException>(() => ControllerJson.Read("""{ "sokolnetController": 1, "parameters": [ { "name": "x", "type": "Vector" } ] }"""));
        }
    }
}
