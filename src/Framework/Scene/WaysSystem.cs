using System;
using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;
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
        private const float StruckFor = 10f;          // a hit car stays where it stopped at least this long ...
        private const float AwayFrom = 40f;           // ... and is put back on a road this far from the player
        // Appearing and leaving (DESIGN §7): one farther than Radius from the player, out of the camera's view, is placed again
        // out of view, between Nearest and Radius from it. People farther than AnimateTo from the camera stop animating,
        // farther than DrawTo are not drawn.
        private const float Radius = 80f, Nearest = 25f, AnimateTo = 40f, DrawTo = 90f;

        /// <summary>How many of the scene's traffic cars and walking people are out (-1: all); the others are kept away, hidden.
        /// A game sets them for its quality level (<see cref="Scripting.Ways.Keep"/>).</summary>
        public static int KeepCars = -1, KeepPeople = -1;
        /// <summary>How many are out now.</summary>
        public static int CarsOut { get; private set; }
        public static int PeopleOut { get; private set; }

        private sealed class Graph
        {
            public WayGraph Data;
            public List<int>[] Out = Array.Empty<List<int>>();
            public float[] Length = Array.Empty<float>();
            public int[] In = Array.Empty<int>();       // how many edges come into each point (2 or more: a junction)
        }

        private static readonly Graph _roads = new(), _paves = new();
        private static readonly List<(Entity E, Vector3 At, int Edge, float Along, float Speed, Vector3 Fwd, int J, float ToJ)> _cars = new();
        // People on a crossing now (from the last step): a car brakes for one in its lane.
        private static readonly List<Vector3> _crossers = new(), _crossersNext = new();
        private const float Patience = 4f;           // a person waits at most this long at a crossing, then crosses
        private static readonly Dictionary<Entity, List<Entity>> _skinned = new();
        // What a person steps aside from: a moving car of the traffic, the player's kart, a character running fast - where it
        // is and its velocity (a character's from how far it moved since the last step).
        private static readonly List<(Vector3 At, Vector3 Velocity)> _fast = new();
        private static readonly Dictionary<Entity, Vector3> _lastAt = new();
        // The player: its karts and its characters - where each is, how fast it goes, and whether it is a kart.
        private static readonly List<(Vector3 At, Vector3 Velocity, bool Kart)> _players = new();

        /// <summary>Forgets what it kept of the last scene (a game stopped or another scene).</summary>
        public static void Reset()
        {
            _skinned.Clear(); _parts.Clear(); _lastAt.Clear(); _crossers.Clear(); _crossersNext.Clear(); _roads.Data = default; _paves.Data = default;
            KeepCars = KeepPeople = -1; CarsOut = PeopleOut = 0;
        }

        private static readonly Dictionary<Entity, List<Entity>> _parts = new();
        private static Vector3 _eye, _look;
        private static bool _hasEye;

        public static void Step(float dt)
        {
            var world = ECSWorld.Instance;
            if (dt <= 0f || !world.HasAnyComponent<WayGraph>()) return;
            Load(world);
            Players(world, dt);
            View(world);
            _cars.Clear();
            if (world.HasAnyComponent<TrafficCar>())
                foreach (var row in world.Query<TrafficCar, Transform>().EnumerateWithEntities<TrafficCar, Transform>())
                    if (!row.Item1.Value.Away)          // one kept away is nowhere
                    {
                        ref readonly TrafficCar t = ref row.Item1.Value;
                        float toJ = Junction(_roads, t.Edge, t.Along, t.Next, out int j);
                        _cars.Add((row.Entity, row.Item2.Value.Position, t.Edge, t.Along, t.Speed, Vector3.Transform(Vector3.UnitZ, row.Item2.Value.Rotation), j, toJ));
                    }
            if (_roads.Data.Edges is { Length: > 0 } && world.HasAnyComponent<TrafficCar>())
            {
                int n = 0, out_ = 0;
                foreach (var row in world.Query<TrafficCar, Transform>().EnumerateWithEntities<TrafficCar, Transform>())
                {
                    ref TrafficCar c = ref row.Item1.Value;
                    bool wanted = KeepCars < 0 || n++ < KeepCars;
                    int here = c.Struck > 0f ? 1 : Population(world, row.Entity, ref c.Away, wanted, ref row.Item2.Value, _roads, true, ref c.Edge, ref c.Along, ref c.Random, c.Seed);
                    if (here == 0) continue;
                    if (here == 2) { c.Next = -1; c.Prev = -1; c.Speed = 0f; }
                    out_++;
                    Drive(world, row.Entity, ref c, ref row.Item2.Value, dt);
                }
                CarsOut = out_;
            }
            if (_paves.Data.Edges is { Length: > 0 } && world.HasAnyComponent<Walker>())
            {
                Threats(world);
                _crossers.Clear();
                _crossers.AddRange(_crossersNext);
                _crossersNext.Clear();
                int n = 0, out_ = 0;
                foreach (var row in world.Query<Walker, Transform>().EnumerateWithEntities<Walker, Transform>())
                {
                    ref Walker w = ref row.Item1.Value;
                    bool wanted = KeepPeople < 0 || n++ < KeepPeople;
                    int here = Population(world, row.Entity, ref w.Away, wanted, ref row.Item2.Value, _paves, false, ref w.Edge, ref w.Along, ref w.Random, w.Seed);
                    if (here == 0) continue;
                    if (here == 2) { w.Next = -1; w.Aside = 0f; }
                    out_++;
                    Walk(world, row.Entity, ref w, ref row.Item2.Value, dt);
                    Detail(world, row.Entity, row.Item2.Value.Position);
                }
                PeopleOut = out_;
            }
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
                into.In = new int[n];
                for (int e = 0; e < into.Length.Length; e++)
                {
                    var ed = g.Edges![e];
                    if ((uint)ed.From >= (uint)n || (uint)ed.To >= (uint)n) continue;
                    into.Out[ed.From].Add(e);
                    into.In[ed.To]++;
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

        private static void Drive(ECSWorld world, Entity me, ref TrafficCar c, ref Transform tr, float dt)
        {
            var g = _roads;
            if ((uint)c.Edge >= (uint)g.Length.Length) return;
            if (c.Struck > 0f)
            {
                c.Struck += dt;
                c.Speed = 0f;
                if (c.Struck >= StruckFor) PutBack(me, ref c, ref tr);
                return;
            }
            float len = g.Length[c.Edge];
            if (c.Next < 0 || (uint)c.Next >= (uint)g.Length.Length) c.Next = Pick(g, c.Edge, ref c.Random, c.Seed);
            // The junction ahead - at the end of its edge, or of the next (a lane comes to a junction by a short edge: seen only
            // from that edge, a car at speed could not stop outside it, and two cars were in it at once).
            float toJ = Junction(g, c.Edge, c.Along, c.Next, out int junction);

            // How far it may go: to its leader less the gap - a car ahead on its own edge, or just onto the edge it takes next
            // (never a car of another lane: two cars crossing a junction each saw the other "ahead" and waited for ever); and
            // to the junction's edge while another car is in it, unless it is in it already.
            float free = float.MaxValue;
            foreach (var (other, at, oEdge, oAlong, _, _, oJ, oToJ) in _cars)
            {
                if (other == me) continue;
                if (oEdge == c.Edge && oAlong > c.Along) free = MathF.Min(free, oAlong - c.Along - c.Gap);
                else if (oEdge == c.Next && c.Next >= 0) free = MathF.Min(free, len - c.Along + oAlong - c.Gap);
                if (junction >= 0 && toJ > PointRadius - 0.01f)
                {
                    bool inside = Vector3.Distance(at, g.Data.Points[junction]) < PointRadius;
                    // Another car coming to the same junction, nearer to it: it goes first (two at once both went in).
                    bool before = oJ == junction && (oToJ < toJ || (oToJ == toJ && other.GetHashCode() < me.GetHashCode()));
                    if (inside || before)
                        free = MathF.Min(free, toJ - PointRadius - 0.5f);        // the junction is taken: stop before it
                }
            }
            // The player in its lane ahead: it stops short of them (a car's length between their middles). The player's kart
            // close enough to touch it, and moving: hit - it becomes a physics body, pushed by the kart.
            float yaw = MathF.Atan2(2f * (tr.Rotation.W * tr.Rotation.Y + tr.Rotation.X * tr.Rotation.Z), 1f - 2f * (tr.Rotation.Y * tr.Rotation.Y + tr.Rotation.X * tr.Rotation.X));
            Vector3 fwd = new(MathF.Sin(yaw), 0f, MathF.Cos(yaw)), side = new(fwd.Z, 0f, -fwd.X);
            foreach (var (pat, pv, kart) in _players)
            {
                Vector3 d = pat - tr.Position;
                float ahead = Vector3.Dot(d, fwd), across = MathF.Abs(Vector3.Dot(d, side));
                if (MathF.Abs(d.Y) > 3f) continue;
                if (kart && pv.Length() > 3f && MathF.Abs(ahead) < 4.3f && across < 2.3f
                    && world.TryGetComponent<RigidbodyComponent>(me, out var rb) && rb.MotionType == RigidbodyMotionType.Kinematic)
                {
                    c.Struck = dt;
                    SceneManager.SetMotionType(me, RigidbodyMotionType.Dynamic, fwd * c.Speed + pv * 0.4f);
                    c.Speed = 0f;
                    return;
                }
                if (ahead > 0f && across < 1.8f) free = MathF.Min(free, ahead - 4f - c.Gap * 0.25f);
            }
            foreach (var p in _crossers)                  // a person on a crossing in its lane: it stops short of them
            {
                Vector3 d = p - tr.Position;
                float ahead = Vector3.Dot(d, fwd), across = MathF.Abs(Vector3.Dot(d, side));
                if (ahead > 0f && across < 1.8f) free = MathF.Min(free, ahead - 3.5f);
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
                c.Prev = c.Edge;
                c.Edge = c.Next;
                len = g.Length[c.Edge];
                c.Next = Pick(g, c.Edge, ref c.Random, c.Seed);
            }
            if (c.Next < 0) c.Along = MathF.Min(c.Along, len);
            PlaceCar(ref tr, g, ref c);
        }

        // The main camera: where it is and where it looks (what is "in view").
        private static void View(ECSWorld world)
        {
            _hasEye = false;
            if (!world.HasAnyComponent<CameraComponent>()) return;
            foreach (var row in world.Query<CameraComponent, Transform>().Enumerate<CameraComponent, Transform>())
            {
                if (!row.Item1.Value.IsMain) continue;
                _eye = row.Item2.Value.Position;
                _look = Vector3.Transform(Vector3.UnitZ, row.Item2.Value.Rotation);
                _hasEye = true;
                return;
            }
        }

        // In the camera's view: within 300 m and about 55 degrees of where it looks (wider than a phone's view either way).
        private static bool InView(Vector3 p)
        {
            if (!_hasEye) return false;
            Vector3 d = p - _eye;
            float dist = d.Length();
            return dist < 3f || (dist < 300f && Vector3.Dot(d / dist, _look) > 0.57f);
        }

        // Where the player is - or, with none, the camera.
        private static float FromPlayer(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var pl in _players) best = MathF.Min(best, Vector3.Distance(pl.At, p));
            if (best == float.MaxValue && _hasEye) best = Vector3.Distance(_eye, p);
            return best;
        }

        // One car or person of the pool, before it moves: 0 kept away (hidden, not moved), 1 out where it is, 2 placed anew
        // (just brought out, or come back near the player from far behind it) - out of view, at the start of an edge.
        private static int Population(ECSWorld world, Entity me, ref bool away, bool wanted, ref Transform tr, Graph g, bool car,
                                      ref int edge, ref float along, ref uint random, int seed)
        {
            if (!wanted)
            {
                if (!away)
                {
                    away = true;
                    Show(world, me, false);
                    tr.Position = new Vector3(tr.Position.X, -500f, tr.Position.Z);
                    if (car) SceneManager.TeleportBody(me, tr.Position, tr.Rotation);
                }
                return 0;
            }
            bool far = !away && (_players.Count > 0 || _hasEye) && FromPlayer(tr.Position) > Radius && !InView(tr.Position);
            if (!away && !far) return 1;
            int n = g.Length.Length;
            uint r = Next(ref random, seed);
            for (int k = 0; k < n; k++)
            {
                int e = (int)((r + (uint)k) % (uint)n);
                Vector3 start = g.Data.Points[g.Data.Edges[e].From];
                float d = FromPlayer(start);
                if (g.Length[e] < 4f || InView(start) || (d != float.MaxValue && (d < Nearest || d > Radius))) continue;
                bool clear = true;
                if (car) foreach (var o in _cars) clear &= o.E == me || Vector3.Distance(o.At, start) > 10f;
                if (!clear) continue;
                edge = e; along = 1f;
                Place(ref tr, g, e, along);
                if (car) SceneManager.TeleportBody(me, tr.Position, tr.Rotation);
                if (away) { away = false; Show(world, me, true); }
                return 2;
            }
            return away ? 0 : 1;          // nowhere out of view now: as it was
        }

        // A person far from the camera stops animating; farther still is not drawn.
        private static void Detail(ECSWorld world, Entity me, Vector3 at)
        {
            if (!_hasEye) return;
            float d = Vector3.Distance(_eye, at);
            foreach (var e in Parts(world, me))
            {
                if (!e.IsAlive || !world.HasComponent<SkinnedMeshRenderer>(e)) continue;
                ref SkinnedMeshRenderer s = ref world.GetComponent<SkinnedMeshRenderer>(e);
                s.Playing = d < AnimateTo;
                s.Visible = d < DrawTo;
            }
        }

        private static void Show(ECSWorld world, Entity me, bool shown)
        {
            foreach (var e in Parts(world, me))
            {
                if (!e.IsAlive) continue;
                if (world.HasComponent<MeshRenderer>(e)) { ref MeshRenderer m = ref world.GetComponent<MeshRenderer>(e); m.Visible = shown; }
                if (world.HasComponent<SkinnedMeshRenderer>(e)) { ref SkinnedMeshRenderer s = ref world.GetComponent<SkinnedMeshRenderer>(e); s.Visible = shown; }
            }
        }

        // What draws it: it and the entities under it with a mesh or a skinned mesh.
        private static List<Entity> Parts(ECSWorld world, Entity me)
        {
            if (_parts.TryGetValue(me, out var list)) return list;
            list = new List<Entity>();
            foreach (Entity e in world.Entities)
            {
                if (!world.HasComponent<MeshRenderer>(e) && !world.HasComponent<SkinnedMeshRenderer>(e)) continue;
                Entity p = e;
                for (int i = 0; i < 8 && !p.IsNull; i++)
                {
                    if (p == me) { list.Add(e); break; }
                    if (!world.TryGetComponent<Transform>(p, out var t) || t.Parent is not { } up) break;
                    p = up;
                }
            }
            _parts[me] = list;
            return list;
        }

        // Metres to the junction (a point two or more edges come into) at the end of this edge or of the next, and which it is;
        // -1 for none so near.
        private static float Junction(Graph g, int edge, float along, int next, out int junction)
        {
            junction = -1;
            if ((uint)edge >= (uint)g.Length.Length) return -1f;
            int to = g.Data.Edges[edge].To;
            float left = g.Length[edge] - along;
            if (g.In[to] >= 2) { junction = to; return left; }
            if ((uint)next >= (uint)g.Length.Length) return -1f;
            int to2 = g.Data.Edges[next].To;
            if (g.In[to2] >= 2) { junction = to2; return left + g.Length[next]; }
            return -1f;
        }

        // A hit car, after a while and far from the player: back on the road at the start of an edge far from the player and
        // from every other car, at rest, kinematic again (out of view - the player is far).
        private static void PutBack(Entity me, ref TrafficCar c, ref Transform tr)
        {
            var g = _roads;
            foreach (var p in _players)
                if (Vector3.Distance(p.At, tr.Position) < AwayFrom) return;
            int n = g.Length.Length;
            uint r = Next(ref c.Random, c.Seed);
            for (int k = 0; k < n; k++)
            {
                int e = (int)((r + (uint)k) % (uint)n);
                Vector3 start = g.Data.Points[g.Data.Edges[e].From];
                bool clear = g.Length[e] > 8f;
                foreach (var p in _players) clear &= Vector3.Distance(p.At, start) >= AwayFrom;
                foreach (var o in _cars) clear &= o.E == me || Vector3.Distance(o.At, start) > 10f;
                if (!clear) continue;
                c.Edge = e; c.Along = 2f; c.Prev = -1; c.Next = Pick(g, e, ref c.Random, c.Seed);
                c.Speed = 0f; c.Struck = 0f;
                Place(ref tr, g, e, c.Along);
                SceneManager.SetMotionType(me, RigidbodyMotionType.Kinematic);
                return;
            }
        }

        private static void Players(ECSWorld world, float dt)
        {
            _players.Clear();
            if (world.HasAnyComponent<KartComponent>())
                foreach (var row in world.Query<KartComponent, Transform>().Enumerate<KartComponent, Transform>())
                    if (row.Item1.Value.Player)
                        _players.Add((row.Item2.Value.Position, Vector3.Transform(Vector3.UnitZ, row.Item2.Value.Rotation) * row.Item1.Value.Speed, true));
            if (world.HasAnyComponent<CharacterComponent>())
                foreach (var row in world.Query<CharacterComponent, Transform>().EnumerateWithEntities<CharacterComponent, Transform>())
                {
                    Vector3 at = row.Item2.Value.Position, v = Vector3.Zero;
                    if (_lastAt.TryGetValue(row.Entity, out var was)) { v = (at - was) / dt; v.Y = 0f; }
                    _lastAt[row.Entity] = at;
                    if (at.Y < 500f) _players.Add((at, v, false));      // one held far off (in a vehicle) is not on the street
                }
        }

        private static void Threats(ECSWorld world)
        {
            _fast.Clear();
            if (world.HasAnyComponent<TrafficCar>())
                foreach (var row in world.Query<TrafficCar, Transform>().Enumerate<TrafficCar, Transform>())
                    if (row.Item1.Value.Speed > 1f)
                        _fast.Add((row.Item2.Value.Position, Vector3.Transform(Vector3.UnitZ, row.Item2.Value.Rotation) * row.Item1.Value.Speed));
            // The player's kart moving, or the player running fast (a sprint, not a walk).
            foreach (var (at, v, kart) in _players)
                if (v.Length() > (kart ? 2f : 4f)) _fast.Add((at, v));
        }

        private static void Walk(ECSWorld world, Entity me, ref Walker w, ref Transform tr, float dt)
        {
            var g = _paves;
            if ((uint)w.Edge >= (uint)g.Length.Length) return;
            float len = g.Length[w.Edge];
            if (w.Next < 0 || (uint)w.Next >= (uint)g.Length.Length) w.Next = Pick(g, w.Edge, ref w.Random, w.Seed);
            // Before a crossing: waits while a MOVING car near it comes toward it (cars standing at the junction, or going away,
            // let people cross; with traffic always about, waiting for no car near at all kept a person there for minutes) -
            // and never longer than its patience: then it crosses, and the cars brake for it.
            bool wait = false;
            if (w.Next >= 0 && g.Data.Edges[w.Next].Crossing && len - w.Along < 0.6f && w.Waited < Patience)
            {
                var ce = g.Data.Edges[w.Next];
                Vector3 mid = (g.Data.Points[ce.From] + g.Data.Points[ce.To]) * 0.5f;
                foreach (var car in _cars)
                {
                    Vector3 to = mid - car.At;
                    to.Y = 0f;
                    if (car.Speed > 0.5f && to.Length() < w.WaitRadius && Vector3.Dot(to, car.Fwd) > 0f) { wait = true; break; }
                }
            }
            w.Waited = wait ? w.Waited + dt : 0f;
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
            if (g.Data.Edges[w.Edge].Crossing || (w.Next >= 0 && g.Data.Edges[w.Next].Crossing && len - w.Along < 1f && !wait))
                _crossersNext.Add(tr.Position);
            // Out of the way: something fast coming at it within 4 m steps it aside (up to 1.2 m, away from it); back after.
            var ed = g.Data.Edges[w.Edge];
            Vector3 along = g.Data.Points[ed.To] - g.Data.Points[ed.From];
            along.Y = 0f;
            Vector3 right = along.LengthSquared() > 1e-6f ? Vector3.Normalize(new Vector3(-along.Z, 0f, along.X)) : Vector3.UnitX;
            float want = 0f;
            foreach (var (at, v) in _fast)
            {
                Vector3 to = tr.Position - at;
                to.Y = 0f;
                float dist = to.Length();
                if (dist > 4f || v.LengthSquared() < 1e-4f) continue;
                if (Vector3.Dot(Vector3.Normalize(v), to) < 0f && dist > 1.5f) continue;   // going away from it
                want = Vector3.Dot(to, right) >= 0f ? 1.2f : -1.2f;
            }
            w.Aside += Math.Clamp(want - w.Aside, -6f * dt, 6f * dt);                   // a quick step out, a calmer one back
            if (want == 0f) w.Aside += Math.Clamp(-w.Aside, -1.5f * dt, 1.5f * dt);
            tr.Position += right * w.Aside;
            Clip(world, me, wait && MathF.Abs(w.Aside) < 0.1f ? w.IdleClip : w.WalkClip);
        }

        // A car through a point: on a curve from where it enters the point's circle on one edge, by the point, to where it
        // leaves it on the next (a quadratic Bezier, facing along it); elsewhere on its edge. A short edge (a turn at a street's
        // end) has a circle of half its length.
        private static void PlaceCar(ref Transform tr, Graph g, ref TrafficCar c)
        {
            float len = g.Length[c.Edge];
            int from = -1, to = -1;
            float u = -1f;
            if (c.Next >= 0)
            {
                float r = MathF.Min(PointRadius, MathF.Min(len, g.Length[c.Next]) * 0.5f);
                if (r > 0.01f && len - c.Along < r) { from = c.Edge; to = c.Next; u = 0.5f * (1f - (len - c.Along) / r); }
            }
            if (u < 0f && (uint)c.Prev < (uint)g.Length.Length && g.Data.Edges[c.Prev].To == g.Data.Edges[c.Edge].From)
            {
                float r = MathF.Min(PointRadius, MathF.Min(len, g.Length[c.Prev]) * 0.5f);
                if (r > 0.01f && c.Along < r) { from = c.Prev; to = c.Edge; u = 0.5f + 0.5f * c.Along / r; }
            }
            if (u < 0f) { Place(ref tr, g, c.Edge, c.Along); return; }
            float rr = MathF.Min(PointRadius, MathF.Min(g.Length[from], g.Length[to]) * 0.5f);
            Vector3 pin = At(g, from, g.Length[from] - rr), mid = g.Data.Points[g.Data.Edges[from].To], pout = At(g, to, rr);
            float v = 1f - u;
            tr.Position = v * v * pin + 2f * v * u * mid + u * u * pout;
            Vector3 d = 2f * v * (mid - pin) + 2f * u * (pout - mid);
            if (d.LengthSquared() > 1e-6f) tr.Rotation = Quaternion.CreateFromYawPitchRoll(MathF.Atan2(d.X, d.Z), 0f, 0f);
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
