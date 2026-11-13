// AnimationSystem.cs — plays the AnimationPlayers of the world (docs: ANIMATION_SYSTEM_IMPLEMENTATION_PLAN.md §18).
//
// Once a game frame, after the scripts (animation comes after their update), only while the game plays, in game time: each
// player's clip bound once to its entity's hierarchy, its time advanced by its speed, the pose of the clip at that time
// (wrapped by the clip's wrap mode) written, and the events passed this frame given to the entity's scripts.

using System;
using System.Collections.Generic;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Animation
{
    public static class AnimationSystem
    {
        private sealed class Binding
        {
            public string Key = "";
            public int Version;
            public BoundClip Bound = null!;
        }

        private static readonly Dictionary<Entity, Binding> _bound = new();

        /// <summary>Where events go: the entity's scripts (ScriptSystem sets it; a test may set its own).</summary>
        public static Action<Entity, AnimationEvent>? EventSink;

        /// <summary>Forgets every binding: a game stopped, or went to another scene (its entities are not these).</summary>
        public static void Reset() => _bound.Clear();

        public static void Update(ECSWorld world, float deltaTime)
        {
            if (!world.HasAnyComponent<AnimationPlayer>()) return;
            foreach (var row in world.Query<ActiveFlag, AnimationPlayer>().EnumerateWithEntities<ActiveFlag, AnimationPlayer>())
            {
                if (!row.Item1.Value.Active) continue;
                Entity e = row.Entity;
                ref AnimationPlayer p = ref row.Item2.Value;
                if (string.IsNullOrEmpty(p.Clip)) continue;
                AnimationClip? clip = ClipLibrary.Clip(p.Clip);
                if (clip == null) continue;                      // on its way, or cannot be read (warned once)

                if (!_bound.TryGetValue(e, out var b) || b.Key != p.Clip || b.Version != ClipLibrary.Version)
                {
                    b ??= new Binding();
                    _bound[e] = b;
                    if (b.Key != p.Clip || b.Bound == null || b.Bound.Clip != clip) b.Bound = PropertyBinder.Bind(clip, e, world);
                    b.Key = p.Clip;
                    b.Version = ClipLibrary.Version;
                }

                float length = clip.Length;
                // (A Once clip that reached its end stopped there: the end's pose is written below, then it stands.)
                if (p.Current != p.Clip) { p.Current = p.Clip; p.Time = 0f; Events(e, clip, 0f, 0f, includeStart: true); }   // its events at 0: as it starts
                else if (!p.Playing) continue;                    // stands still: a script may move the entity itself
                else
                {
                    // A clip that ran to its end once and is set playing again starts over.
                    bool ended = (clip.Wrap == WrapMode.Once) && (p.Speed >= 0f ? p.Time >= length : p.Time <= 0f);
                    float from = ended ? (p.Speed >= 0f ? 0f : length) : p.Time;
                    float to = from + deltaTime * p.Speed;
                    p.Time = to;
                    Advance(e, clip, ref p, from, to);
                }
                PropertyBinder.Apply(b.Bound, ClipTime(clip, p.Time), world);
            }
        }

        /// <summary>The time within the clip of a player's <paramref name="time"/>, by the clip's wrap mode.</summary>
        public static float ClipTime(AnimationClip clip, float time)
        {
            float length = clip.Length;
            if (length <= 0f) return 0f;
            switch (clip.Wrap)
            {
                case WrapMode.Loop:
                {
                    float r = time % length;
                    return r < 0f ? r + length : r;
                }
                case WrapMode.PingPong:
                {
                    float r = time % (2f * length);
                    if (r < 0f) r += 2f * length;
                    return r <= length ? r : 2f * length - r;
                }
                default:
                    return Math.Clamp(time, 0f, length);
            }
        }

        // The events between the player's time `from` and `to` (they may lie rounds apart), and the end of a Once clip.
        private static void Advance(Entity e, AnimationClip clip, ref AnimationPlayer p, float from, float to)
        {
            float length = clip.Length;
            if (clip.Wrap is WrapMode.Once or WrapMode.ClampForever || length <= 0f)
            {
                float a = Math.Clamp(from, 0f, length), z = Math.Clamp(to, 0f, length);
                bool reachesEnd = to >= from ? to >= length : to <= 0f;
                Events(e, clip, a, z, includeStart: false);
                if (reachesEnd && clip.Wrap == WrapMode.Once) p.Playing = false;   // Once: it stops; the last pose stays
                return;
            }
            // Loop and PingPong: the span walked leg by leg — a leg is one pass through the clip, forwards or (a PingPong's
            // odd legs) backwards.
            float dir = to >= from ? 1f : -1f;
            float t = from;
            for (int guard = 0; guard < 64 && (dir > 0 ? t < to : t > to); guard++)
            {
                float leg = dir > 0 ? MathF.Floor(t / length) : MathF.Ceiling(t / length) - 1f;
                float legStart = leg * length, legEnd = legStart + length;
                float end = dir > 0 ? MathF.Min(to, legEnd) : MathF.Max(to, legStart);
                bool backwards = clip.Wrap == WrapMode.PingPong && ((long)leg & 1) == 1;
                float c0 = backwards ? legEnd - t : t - legStart;
                float c1 = backwards ? legEnd - end : end - legStart;
                // A Loop's new round starts at 0 again: its events at 0 come with it (a PingPong turns at its ends: the
                // end was passed as the last leg ended).
                Events(e, clip, c0, c1, includeStart: guard > 0 && clip.Wrap == WrapMode.Loop);
                t = end;
            }
        }

        // The events passed going from clip time `a` (not included, unless includeStart) to `b` (included): an event fires
        // on the frame that reaches its time.
        private static void Events(Entity e, AnimationClip clip, float a, float b, bool includeStart)
        {
            var sink = EventSink;
            if (sink == null || clip.Events.Count == 0) return;
            foreach (var ev in clip.Events)
            {
                bool hit = b >= a
                    ? (ev.Time > a || (includeStart && ev.Time == a)) && ev.Time <= b
                    : (ev.Time < a || (includeStart && ev.Time == a)) && ev.Time >= b;
                if (hit) sink(e, ev);
            }
        }
    }
}
