// AnimatorSystem.cs — runs the Animators of the world (docs: ANIMATION_SYSTEM_IMPLEMENTATION_PLAN.md §23): each one's
// controller instance stepped in game time after the scripts, and the skinned character at and below its entity posed —
// the state's clip, or its blend tree's two neighbours; a transition fading out the pose as it was when it began. A
// character with no Animator plays as before (its SkinnedMeshRenderer's clip).

using System.Collections.Generic;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Animation;

namespace GameEditor.Framework.Animation
{
    public static class AnimatorSystem
    {
        private sealed class Run
        {
            public string Key = "";
            public int Version;
            public ControllerInstance Instance = null!;
            public SkinnedCharacterRegistry.Entry Character = null!;
        }

        private static readonly Dictionary<Entity, Run> _runs = new();
        private static readonly List<SkinnedCharacterRegistry.Entry> _driven = new(), _drivenNow = new();

        /// <summary>Forgets every running controller: a game stopped or went to another scene.</summary>
        public static void Reset()
        {
            _runs.Clear();
            foreach (var e in _driven) e.Driven = false;
            _driven.Clear();
        }

        public static void Update(ECSWorld world, float dt)
        {
            _drivenNow.Clear();
            if (world.HasAnyComponent<Animator>())
                foreach (var row in world.Query<ActiveFlag, Animator>().EnumerateWithEntities<ActiveFlag, Animator>())
                {
                    if (!row.Item1.Value.Active) continue;
                    var run = RunOf(world, row.Entity, row.Item2.Value.Controller);
                    if (run == null) continue;
                    run.Instance.Update(dt);
                    Pose(run);
                    run.Character.Driven = true;
                    _drivenNow.Add(run.Character);
                }
            foreach (var e in _driven) if (!_drivenNow.Contains(e)) e.Driven = false;   // an Animator gone: the character plays as before
            _driven.Clear();
            _driven.AddRange(_drivenNow);
        }

        // The entity's run: its controller read, its character found (the first skinned mesh at or below it).
        private static Run? RunOf(ECSWorld world, Entity e, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_runs.TryGetValue(e, out var run) && run.Key == key && run.Version == ControllerLibrary.Version) return run;
            var controller = ControllerLibrary.Controller(key);
            if (controller == null) return null;
            var character = CharacterOf(world, e);
            if (character?.Animator == null) return null;
            if (run != null && run.Key == key && ReferenceEquals(run.Instance.Controller, controller)) { run.Version = ControllerLibrary.Version; return run; }
            run = new Run { Key = key, Version = ControllerLibrary.Version, Character = character,
                            Instance = new ControllerInstance(controller, character.ClipSeconds) };
            _runs[e] = run;
            return run;
        }

        /// <summary>The skinned character at or below <paramref name="root"/>: its first skinned mesh's.</summary>
        public static SkinnedCharacterRegistry.Entry? CharacterOf(ECSWorld world, Entity root)
        {
            var below = new List<Entity> { root };
            for (int i = 0; i < below.Count; i++)
            {
                if (world.TryGetComponent<SkinnedMeshRenderer>(below[i], out var s) && SkinnedCharacterRegistry.TryGet(s.CharacterKey ?? "", out var entry)) return entry;
                foreach (var x in world.Entities)
                    if (world.TryGetComponent<Transform>(x, out var t) && t.Parent == below[i]) below.Add(x);
            }
            return null;
        }

        private static void Pose(Run run)
        {
            var i = run.Instance;
            var character = run.Character;
            var anim = character.Animator!;
            // A transition: the target state posed, faded in from the pose kept as it began; else the current state.
            var state = i.Transition != null ? i.Next! : i.Current;
            if (state == null) return;
            float seconds = i.Transition != null ? i.NextTime : i.Time;
            var (a, b, wb) = i.BlendOf(state);
            var clipA = character.FindClip(a);
            if (clipA == null) return;
            var clipB = b.Length > 0 ? character.FindClip(b) : null;
            // A blend tree's clips in step: the same part of each (normalized time).
            float lenA = character.ClipSeconds(a), lenB = clipB != null ? character.ClipSeconds(b) : 0f;
            float tB = clipB != null && lenA > 0f ? seconds / lenA * lenB : 0f;
            anim.Pose(clipA, seconds, clipB, tB, wb, i.Transition != null ? i.TransitionWeight : -1f, takeSnapshot: i.TransitionStarted);
        }

        // ── A script's word to its character's controller ─────────────────────────────

        private static ControllerInstance? Of(Entity e)
        {
            var world = ECSWorld.Instance;
            if (_runs.TryGetValue(e, out var run)) return run.Instance;
            return world.TryGetComponent<Animator>(e, out var a) ? RunOf(world, e, a.Controller)?.Instance : null;
        }

        public static void SetFloat(Entity e, string name, float v) => Of(e)?.SetFloat(name, v);
        public static void SetInt(Entity e, string name, int v) => Of(e)?.SetInt(name, v);
        public static void SetBool(Entity e, string name, bool v) => Of(e)?.SetBool(name, v);
        public static void SetTrigger(Entity e, string name) => Of(e)?.SetTrigger(name);
        public static void ResetTrigger(Entity e, string name) => Of(e)?.ResetTrigger(name);
        public static float GetFloat(Entity e, string name) => Of(e)?.Get(name) ?? 0f;
        /// <summary>Goes to the state at once.</summary>
        public static void Play(Entity e, string state) => Of(e)?.Play(state);
        /// <summary>The state it is in ("" when none), and the one a transition goes to (null when none runs).</summary>
        public static (string Current, string? Next, float NormalizedTime) State(Entity e)
            => Of(e) is { } i ? (i.Current?.Name ?? "", i.Next?.Name, i.NormalizedTime) : ("", null, 0f);
        /// <summary>The running instance (the editor's Animator panel shows its parameters and state).</summary>
        public static ControllerInstance? Instance(Entity e) => _runs.TryGetValue(e, out var r) ? r.Instance : null;
    }
}
