using System;
using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Animation;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// Moves the traffic and the walking people of the scene that plays, each fixed step (Phase H gap E4; the game's IP rules
    /// W1, W3): a <see cref="TrafficCar"/> along the road graph, a <see cref="Walker"/> along the pavement graph - edge by edge,
    /// the next edge picked at each point, never a route searched, never off the graph.
    /// </summary>
    public static class WaysSystem
    {
        private const float PointRadius = 4.5f;       // a car "in" a point (a junction) - the next one waits outside it

        private sealed class Graph
        {
            public WayGraph Data;
            public List<int>[] Out = Array.Empty<List<int>>();
            public float[] Length = Array.Empty<float>();
        }

        private static readonly Graph _roads = new(), _paves = new();
        private static readonly List<(Entity E, Vector3 At)> _cars = new();
        private static readonly Dictionary<Entity, List<Entity>> _skinned = new();

        /// <summary>Forgets what it kept of the last scene (a game stopped or another scene).</summary>
        public static void Reset() { _skinned.Clear(); _roads.Data = default; _paves.Data = default; }

        public static void Step(float dt)
        {
            var world = ECSWorld.Instance;
            if (dt <= 0f || !world.HasAnyComponent<WayGraph>()) return;
            Load(world);
            _cars.Clear();
            if (world.HasAnyComponent<TrafficCar>())
                foreach (var row in world.Query<TrafficCar, Transform>().EnumerateWithEntities<TrafficCar, Transform>())
                    _cars.Add((row.Entity, row.Item2.Value.Position));
            if (_roads.Data.Edges is { Length: > 0 } && world.HasAnyComponent<TrafficCar>())
                foreach (var row in world.Query<TrafficCar, Transform>().EnumerateWithEntities<TrafficCar, Transform>())
                    Drive(row.Entity, ref row.Item1.Value, ref row.Item2.Value, dt);
            if (_paves.Data.Edges is { Length: > 0 } && world.HasAnyComponent<Walker>())
                foreach (var row in world.Query<Walker, Transform>().EnumerateWithEntities<Walker, Transform>())
                    Walk(world, row.Entity, ref row.Item1.Value, ref row.Item2.Value, dt);
        }

        private static void Load(ECSWorld world)
        {
            foreach (var row in world.Query<WayGraph, Transform>().Enumerate<WayGraph, Transform>())
            {
                var g = row.Item1.Value;
                var into = g.Kind == WayKind.Road ? _roads : _paves;
                if (ReferenceEquals(into.Data.Edges, g.Edges) && ReferenceEquals(into.Data.Points, g.Points)) continue;
                into.Data = g;
                int n = g.Points?.Length ?? 0;
                into.Out = new List<int>[n];
                for (int i = 0; i < n; i++) into.Out[i] = new List<int>();
                into.Length = new float[g.Edges?.Length ?? 0];
                for (int e = 0; e < into.Length.Length; e++)
                {
                    var ed = g.Edges![e];
                    if ((uint)ed.From >= (uint)n || (uint)ed.To >= (uint)n) continue;
                    into.Out[ed.From].Add(e);
                    into.Length[e] = Vector3.Distance(g.Points![ed.From], g.Points[ed.To]);
                }
            }
        }

        private static uint Next(ref uint state, int seed)
        {
            if (state == 0) state = (uint)(seed * 2654435761u) | 1u;
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return state;
        }

        // The next edge at the end of `edge`: one going on from its end, at random, not straight back while another is there.
        private static int Pick(Graph g, int edge, ref uint random, int seed)
        {
            var e = g.Data.Edges[edge];
            var outs = g.Out[e.To];
            if (outs.Count == 0) return -1;
            int count = 0;
            foreach (int o in outs) if (g.Data.Edges[o].To != e.From) count++;
            uint r = Next(ref random, seed);
            if (count == 0) return outs[(int)(r % (uint)outs.Count)];
            int k = (int)(r % (uint)count);
            foreach (int o in outs)
                if (g.Data.Edges[o].To != e.From && k-- == 0) return o;
            return outs[0];
        }

        private static Vector3 At(Graph g, int edge, float along)
        {
            var e = g.Data.Edges[edge];
            float len = g.Length[edge];
            return len > 1e-4f ? Vector3.Lerp(g.Data.Points[e.From], g.Data.Points[e.To], Math.Clamp(along / len, 0f, 1f)) : g.Data.Points[e.From];
        }

        private static void Drive(Entity me, ref TrafficCar c, ref Transform tr, float dt)
        {
            var g = _roads;
            if ((uint)c.Edge >= (uint)g.Length.Length) return;
            float len = g.Length[c.Edge];
            if (c.Next < 0 || (uint)c.Next >= (uint)g.Length.Length) c.Next = Pick(g, c.Edge, ref c.Random, c.Seed);
            Vector3 end = g.Data.Points[g.Data.Edges[c.Edge].To];

            // How far it may go: to the car ahead less the gap; to the point's edge while another car is in the point.
            float free = float.MaxValue;
            foreach (var (other, at) in _cars)
            {
                if (other == me) continue;
                Vector3 pos = At(g, c.Edge, c.Along);
                Vector3 dir = g.Data.Points[g.Data.Edges[c.Edge].To] - g.Data.Points[g.Data.Edges[c.Edge].From];
                if (dir.LengthSquared() < 1e-6f) continue;
                dir = Vector3.Normalize(dir);
                Vector3 to = at - pos;
                float ahead = Vector3.Dot(to, dir), side = (to - dir * ahead).Length();
                if (ahead > 0f && side < 2.2f) free = MathF.Min(free, ahead - c.Gap);       // in its lane, ahead
                if (Vector3.Distance(at, end) < PointRadius && len - c.Along > PointRadius - 0.01f)
                    free = MathF.Min(free, len - c.Along - PointRadius - 0.5f);            // the point is taken: stop before it
            }
            float want = MathF.Min(c.MaxSpeed, g.Data.Edges[c.Edge].Speed > 0f ? g.Data.Edges[c.Edge].Speed : c.MaxSpeed);
            if (free < 0.5f) want = 0f;
            else want = MathF.Min(want, MathF.Sqrt(2f * MathF.Max(c.Acceleration, 0.1f) * free));   // slows to stop in the room it has
            float step = MathF.Max(c.Acceleration, 0.1f) * dt;
            c.Speed = c.Speed < want ? MathF.Min(want, c.Speed + step) : MathF.Max(want, c.Speed - step * 2f);
            c.Waiting = c.Speed < 0.05f && want == 0f;
            c.Along += c.Speed * dt;
            while (c.Along >= len && c.Next >= 0)
            {
                c.Along -= len;
                c.Edge = c.Next;
                len = g.Length[c.Edge];
                c.Next = Pick(g, c.Edge, ref c.Random, c.Seed);
            }
            if (c.Next < 0) c.Along = MathF.Min(c.Along, len);
            Place(ref tr, g, c.Edge, c.Along);
        }

        private static void Walk(ECSWorld world, Entity me, ref Walker w, ref Transform tr, float dt)
        {
            var g = _paves;
            if ((uint)w.Edge >= (uint)g.Length.Length) return;
            float len = g.Length[w.Edge];
            if (w.Next < 0 || (uint)w.Next >= (uint)g.Length.Length) w.Next = Pick(g, w.Edge, ref w.Random, w.Seed);
            // Before a crossing: waits while a car is near it.
            bool wait = false;
            if (w.Next >= 0 && g.Data.Edges[w.Next].Crossing && len - w.Along < 0.6f)
            {
                var ce = g.Data.Edges[w.Next];
                Vector3 mid = (g.Data.Points[ce.From] + g.Data.Points[ce.To]) * 0.5f;
                foreach (var (_, at) in _cars)
                    if (Vector3.Distance(at, mid) < w.WaitRadius) { wait = true; break; }
            }
            w.Waiting = wait;
            if (!wait)
            {
                w.Along += w.Speed * dt;
                while (w.Along >= len && w.Next >= 0)
                {
                    w.Along -= len;
                    w.Edge = w.Next;
                    len = g.Length[w.Edge];
                    w.Next = Pick(g, w.Edge, ref w.Random, w.Seed);
                }
                if (w.Next < 0) w.Along = MathF.Min(w.Along, len);
            }
            Place(ref tr, g, w.Edge, w.Along);
            Clip(world, me, wait ? w.IdleClip : w.WalkClip);
        }

        private static void Place(ref Transform tr, Graph g, int edge, float along)
        {
            var e = g.Data.Edges[edge];
            Vector3 dir = g.Data.Points[e.To] - g.Data.Points[e.From];
            tr.Position = At(g, edge, along);
            if (dir.LengthSquared() > 1e-6f) tr.Rotation = Quaternion.CreateFromYawPitchRoll(MathF.Atan2(dir.X, dir.Z), 0f, 0f);
        }

        // The person's model plays this clip (its skinned meshes at and under it).
        private static void Clip(ECSWorld world, Entity me, string? clip)
        {
            if (string.IsNullOrEmpty(clip)) return;
            if (!_skinned.TryGetValue(me, out var list))
            {
                list = new List<Entity>();
                foreach (Entity e in world.Entities)
                {
                    if (!world.HasComponent<SkinnedMeshRenderer>(e)) continue;
                    Entity p = e;
                    for (int i = 0; i < 8 && !p.IsNull; i++)
                    {
                        if (p == me) { list.Add(e); break; }
                        if (!world.TryGetComponent<Transform>(p, out var t) || t.Parent is not { } up) break;
                        p = up;
                    }
                }
                _skinned[me] = list;
            }
            foreach (var e in list)
            {
                if (!e.IsAlive || !world.HasComponent<SkinnedMeshRenderer>(e)) continue;
                ref SkinnedMeshRenderer s = ref world.GetComponent<SkinnedMeshRenderer>(e);
                if (s.Clip != clip) { s.Clip = clip; s.Fade = 0.2f; s.Playing = true; s.Loop = true; }
            }
        }
    }
}
