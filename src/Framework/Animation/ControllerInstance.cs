// ControllerInstance.cs — a controller running on one character (docs: ANIMATION_SYSTEM_IMPLEMENTATION_PLAN.md §23):
// its parameters' values, the state it is in and how far, a transition on its way. It says what to pose; the animator
// poses it (AnimatorSystem). Allocates nothing per frame.

using System;
using System.Collections.Generic;

namespace GameEditor.Framework.Animation
{
    public sealed class ControllerInstance
    {
        public AnimatorController Controller { get; }
        private readonly Func<string, float> _clipLength;      // a clip's length in seconds, by name (0 when the character has none)
        private readonly Dictionary<string, float> _values = new(StringComparer.Ordinal);

        public AnimatorState? Current { get; private set; }
        /// <summary>Seconds of the current state played (at its speed).</summary>
        public float Time { get; private set; }
        /// <summary>The state a transition goes to, null when none runs.</summary>
        public AnimatorState? Next { get; private set; }
        public float NextTime { get; private set; }
        public AnimatorTransition? Transition { get; private set; }
        public float TransitionTime { get; private set; }
        /// <summary>A transition began this frame: the pose as it is now is what fades out.</summary>
        public bool TransitionStarted { get; private set; }

        public ControllerInstance(AnimatorController controller, Func<string, float> clipLength)
        {
            Controller = controller;
            _clipLength = clipLength;
            foreach (var p in controller.Parameters) _values[p.Name] = p.Type == ParameterType.Trigger ? 0f : p.Default;
            Current = controller.State(controller.DefaultState) ?? (controller.States.Count > 0 ? controller.States[0] : null);
        }

        // ── Parameters ─────────────────────────────────────────────────────────────────

        public void SetFloat(string name, float v) { if (_values.ContainsKey(name)) _values[name] = v; }
        public void SetInt(string name, int v) { if (_values.ContainsKey(name)) _values[name] = v; }
        public void SetBool(string name, bool v) { if (_values.ContainsKey(name)) _values[name] = v ? 1f : 0f; }
        public void SetTrigger(string name) { if (_values.ContainsKey(name)) _values[name] = 1f; }
        public void ResetTrigger(string name) { if (_values.ContainsKey(name)) _values[name] = 0f; }
        public float Get(string name) => _values.TryGetValue(name, out var v) ? v : 0f;
        public bool GetBool(string name) => Get(name) != 0f;

        /// <summary>Goes to <paramref name="state"/> at once (a script's Play).</summary>
        public void Play(string state)
        {
            if (Controller.State(state) is not { } s) return;
            Current = s; Time = 0f; Next = null; Transition = null;
        }

        // ── Time ───────────────────────────────────────────────────────────────────────

        /// <summary>The state's length in seconds (a blend tree's: its first clip's; time in step is normalized).</summary>
        public float Length(AnimatorState s) => _clipLength(s.IsBlendTree ? s.Blend[0].Clip : s.Clip);

        /// <summary>How far into the state, 0..1 a round (round and round: 1.5 = half way through the second round).</summary>
        public float Normalized(AnimatorState s, float time)
        {
            float len = Length(s);
            return len > 0f ? time / len : 0f;
        }

        public float NormalizedTime => Current == null ? 0f : Normalized(Current, Time);

        /// <summary>The weight of the transition's target, 0 → 1 over its duration; 0 when none runs.</summary>
        public float TransitionWeight => Transition == null || Transition.Duration <= 0f ? 0f : Math.Clamp(TransitionTime / Transition.Duration, 0f, 1f);

        public void Update(float dt)
        {
            TransitionStarted = false;
            if (Current == null) return;
            Time += dt * Current.Speed;
            if (Transition != null)
            {
                NextTime += dt * Next!.Speed;
                TransitionTime += dt;
                if (TransitionTime >= Transition.Duration) { Current = Next; Time = NextTime; Next = null; Transition = null; }
                return;                                       // a transition runs to its end (the reference editor's default: no interruption)
            }
            if (Choose() is { } t) Begin(t);
        }

        // The transition to take now: Any State's first, then the current state's; null when none.
        private AnimatorTransition? Choose()
        {
            AnimatorTransition? found = null;
            foreach (var t in Controller.Transitions)
                if (t.From.Length == 0 && t.To != Current!.Name && Holds(t)) { found = t; break; }
            if (found == null)
                foreach (var t in Controller.Transitions)
                    if (t.From == Current!.Name && Holds(t)) { found = t; break; }
            return found;
        }

        private bool Holds(AnimatorTransition t)
        {
            if (t.Conditions.Count == 0 && !t.HasExitTime) return false;    // a transition with nothing to wait for is never taken
            if (t.HasExitTime)
            {
                // Below 1: within each round, once past that point of it (a looping clip leaves at the same point of a
                // round); 1 or more: once that much has played.
                float now = NormalizedTime;
                bool past = t.ExitTime < 1f ? now - MathF.Floor(now) >= t.ExitTime : now >= t.ExitTime;
                if (!past) return false;
            }
            foreach (var k in t.Conditions)
            {
                float v = Get(k.Parameter);
                bool ok = k.Mode switch
                {
                    ConditionMode.Greater => v > k.Threshold,
                    ConditionMode.Less => v < k.Threshold,
                    ConditionMode.Equals => MathF.Abs(v - k.Threshold) < 1e-6f,
                    ConditionMode.NotEqual => MathF.Abs(v - k.Threshold) >= 1e-6f,
                    ConditionMode.If => v != 0f,
                    _ => v == 0f,
                };
                if (!ok) return false;
            }
            return true;
        }

        private void Begin(AnimatorTransition t)
        {
            foreach (var k in t.Conditions)                    // a trigger is used up by the transition that takes it
                if (Controller.Parameter(k.Parameter) is { Type: ParameterType.Trigger }) _values[k.Parameter] = 0f;
            var to = Controller.State(t.To);
            if (to == null) return;
            if (t.Duration <= 0f) { Current = to; Time = 0f; return; }
            Transition = t; Next = to; NextTime = 0f; TransitionTime = 0f; TransitionStarted = true;
        }

        /// <summary>A blend tree's two clips at the parameter's value and the second's weight (one clip: weight 0).</summary>
        public (string A, string B, float WeightB) BlendOf(AnimatorState s)
        {
            if (!s.IsBlendTree) return (s.Clip, "", 0f);
            var m = s.Blend;
            float v = Get(s.BlendParameter);
            if (v <= m[0].Threshold) return (m[0].Clip, "", 0f);
            if (v >= m[^1].Threshold) return (m[^1].Clip, "", 0f);
            for (int i = 0; i < m.Count - 1; i++)
                if (v >= m[i].Threshold && v <= m[i + 1].Threshold)
                {
                    float span = m[i + 1].Threshold - m[i].Threshold;
                    return (m[i].Clip, m[i + 1].Clip, span > 0f ? (v - m[i].Threshold) / span : 0f);
                }
            return (m[^1].Clip, "", 0f);
        }
    }
}
