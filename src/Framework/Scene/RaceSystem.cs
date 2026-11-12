using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    /// <summary>Where a race is.</summary>
    public enum RaceState { None, Countdown, Racing, Finished }

    /// <summary>What the race found for one racer.</summary>
    public sealed class RacerState
    {
        public Entity Entity { get; internal set; }
        public string Name { get; internal set; } = "";
        /// <summary>The lap it is on, from 1 (Laps + 1 once finished).</summary>
        public int Lap { get; internal set; } = 1;
        /// <summary>The gate (checkpoint) it must pass next; 0 is the start and finish line.</summary>
        public int Next { get; internal set; } = 1;
        /// <summary>1 = first.</summary>
        public int Place { get; internal set; } = 1;
        public bool Finished { get; internal set; }
        /// <summary>The race's time when it finished (seconds), or the time so far.</summary>
        public float Time { get; internal set; }
        /// <summary>Its best lap (seconds); 0 before its first lap is done.</summary>
        public float BestLap { get; internal set; }
        /// <summary>How many times it was put back on the road.</summary>
        public int Respawns { get; internal set; }
        /// <summary>The item in its slot (None: empty).</summary>
        public ItemKind Item { get; internal set; }
        /// <summary>Seconds left of a boost, a puddle's slowdown, a bolt's stun, a guard, the protection after a hit.</summary>
        public float Boosting { get; internal set; }
        public float Slowed { get; internal set; }
        public float Stunned { get; internal set; }
        public float Guarded { get; internal set; }
        public float Protected { get; internal set; }
        /// <summary>How many times an item hit it (a guard's block not counted).</summary>
        public int Hits { get; internal set; }

        internal Vector3 Last;
        internal float LapStart, Bad;
        internal bool Player;
    }

    /// <summary>
    /// The race of the scene that plays (docs/H5_DESIGN.md §7): a <see cref="RaceComponent"/> and the karts with a
    /// <see cref="RacerComponent"/>, on the track the scene's <see cref="TrackComponent"/> names. A countdown holds the
    /// karts; then a gate (checkpoint) is passed when a kart's path in a frame crosses it — the line from the road's left
    /// edge to its right edge — in order, and a lap counts when every gate of it was passed; places by laps, gates and the
    /// distance to the next gate; a kart off the road, turned over or stuck is put back at the last gate it passed, at rest
    /// and facing forward — nothing else helps it (R1, R2). Run after the physics, each frame of play.
    /// </summary>
    public static class Race
    {
        private static readonly List<RacerState> _racers = new();
        private static TrackData? _track;
        private static RaceComponent _race;
        private static float _sinceGo;

        public static RaceState State { get; private set; }
        /// <summary>Seconds of countdown left (0 once racing).</summary>
        public static float CountdownLeft { get; private set; }
        /// <summary>Seconds since GO.</summary>
        public static float Clock { get; private set; }
        public static int Laps => _race.Laps;
        public static IReadOnlyList<RacerState> Racers => _racers;
        public static TrackData? Track => _track;

        /// <summary>A racer's state, or null.</summary>
        public static RacerState? Of(Entity e)
        {
            foreach (var r in _racers) if (r.Entity == e) return r;
            return null;
        }

        /// <summary>Uses the item in a racer's slot now (a boost, a puddle behind, a bolt ahead, a guard); false when its slot is empty.</summary>
        public static bool UseItem(Entity kart) => Of(kart) is { } r && State == RaceState.Racing && ItemSystem.Use(r);

        /// <summary>Puts an item in a racer's slot (a script's own pick-up, a test); false when it is not a racer.</summary>
        public static bool GiveItem(Entity kart, ItemKind item)
        {
            if (Of(kart) is not { } r) return false;
            r.Item = item;
            return true;
        }

        /// <summary>A racer finished a lap: (kart, the lap done, its time in seconds).</summary>
        public static event Action<Entity, int, float>? LapDone;
        /// <summary>A racer finished the race: (kart, its place, the race's time).</summary>
        public static event Action<Entity, int, float>? Finished;
        /// <summary>A racer was put back on the road.</summary>
        public static event Action<Entity>? Respawned;

        /// <summary>Forgets the race: when play stops or the scene changes.</summary>
        public static void Reset()
        {
            _racers.Clear();
            _track = null;
            State = RaceState.None;
            CountdownLeft = Clock = _sinceGo = 0f;
            ForgetHud();
        }

        // The race begins on the first frame of play that has a Race, a track and racers.
        private static bool Begin(ECSWorld world)
        {
            bool found = false;
            foreach (Entity e in world.Entities)
            {
                if (!found && world.TryGetComponent<RaceComponent>(e, out var race)) { _race = race; found = true; }
                if (_track == null && world.TryGetComponent<TrackComponent>(e, out var tc) && TrackLibrary.Track(tc.Key) is { } t) _track = t;
            }
            if (!found) return false;
            if (_track == null || _track.Checkpoints.Count < 2) return false;
            foreach (var row in world.Query<RacerComponent, Transform>().EnumerateWithEntities<RacerComponent, Transform>())
            {
                string name = row.Item1.Value.Name;
                if (string.IsNullOrEmpty(name) && world.TryGetComponent<NameTag>(row.Entity, out var nt)) name = nt.Name;
                bool player = world.TryGetComponent<KartComponent>(row.Entity, out var k) && k.Player;
                _racers.Add(new RacerState { Entity = row.Entity, Name = name ?? "", Last = Transform.GetWorldMatrix(world, row.Item2.Value).Translation, Player = player });
            }
            if (_racers.Count == 0) return false;
            _race.Laps = Math.Max(1, _race.Laps);
            CountdownLeft = MathF.Max(0f, _race.Countdown);
            State = CountdownLeft > 0f ? RaceState.Countdown : RaceState.Racing;
            return true;
        }

        // Where a racer's kart is now: its body's place in the 3D world (a fixed step reads it before the Transform is synced).
        internal static bool Where(ECSWorld world, Entity e, out Vector3 at, out Quaternion turn)
        {
            if (SceneManager.Physics3D is { } physics && SceneManager.TryGetBody(e, out var body))
            {
                at = physics.GetPosition(body);
                turn = physics.GetRotation(body);
                return true;
            }
            at = default; turn = Quaternion.Identity;
            if (!world.TryGetComponent<Transform>(e, out var tr)) return false;
            Matrix4x4 m = Transform.GetWorldMatrix(world, tr);
            at = m.Translation;
            turn = Quaternion.CreateFromRotationMatrix(m);
            return true;
        }

        /// <summary>
        /// One fixed step of the race (before the drivers and the karts): the countdown, the gates, the respawns, the places
        /// and the clock all move in whole physics steps, so a race comes out the same on every machine.
        /// </summary>
        internal static void Step(float dt)
        {
            var world = ECSWorld.Instance;
            if (State == RaceState.None && (!world.HasAnyComponent<RaceComponent>() || !Begin(world))) return;

            if (State == RaceState.Countdown)
            {
                CountdownLeft -= dt;
                Hold(world, true);
                if (CountdownLeft <= 0f)
                {
                    CountdownLeft = 0f;
                    State = RaceState.Racing;
                    Hold(world, false);
                }
                else return;
            }

            if (State == RaceState.Racing)
            {
                Clock += dt;
                _sinceGo += dt;
                foreach (var r in _racers)
                {
                    if (!Where(world, r.Entity, out Vector3 now, out Quaternion turn)) continue;
                    if (!r.Finished)
                    {
                        Gates(r, r.Last, now);
                        if (Watch(world, r, turn, now, dt)) continue;      // put back: Last is where it was put
                        r.Time = Clock;
                    }
                    r.Last = now;
                }
                Places();
                bool any = false, allDone = true;
                foreach (var r in _racers) if (r.Player) { any = true; allDone &= r.Finished; }
                if (!any) { allDone = true; foreach (var r in _racers) allDone &= r.Finished; }
                if (allDone) State = RaceState.Finished;
            }
            else if (State == RaceState.Finished) _sinceGo += dt;
        }

        /// <summary>Each frame of play: the HUD's texts (the race itself moves in <see cref="Step"/>).</summary>
        internal static void Update(float dt)
        {
            if (State != RaceState.None) WriteHud(ECSWorld.Instance);
        }

        // A gate crossed in order: the kart's path this frame meets the gate's line, going forward.
        private static void Gates(RacerState r, Vector3 from, Vector3 to)
        {
            var t = _track!;
            // A frame's move of more than 10 m is a jump (a kart put somewhere), not driving: it crosses no gate.
            if (Vector3.DistanceSquared(from, to) > 100f) return;
            int count = t.Checkpoints.Count;
            TrackFrame g = t.Checkpoints[r.Next % count];
            Vector3 left = g.Position - g.Right * g.HalfWidth, right = g.Position + g.Right * g.HalfWidth;
            if (Vector3.Dot(to - from, g.Forward) <= 0f || !Crosses(from, to, left, right)) return;
            r.Next++;
            if (r.Next <= count) return;
            // The finish line after every gate of the lap: a lap done.
            float lapTime = Clock - r.LapStart;
            r.LapStart = Clock;
            r.BestLap = r.BestLap <= 0f ? lapTime : MathF.Min(r.BestLap, lapTime);
            LapDone?.Invoke(r.Entity, r.Lap, lapTime);
            r.Lap++;
            r.Next = 1;
            if (r.Lap > _race.Laps)
            {
                r.Finished = true;
                r.Time = Clock;
                int place = 1;
                foreach (var o in _racers) if (o != r && o.Finished) place++;
                r.Place = place;
                Finished?.Invoke(r.Entity, place, Clock);
            }
        }

        // Do two segments cross, seen from above (x and z)?
        private static bool Crosses(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            static float Cross(float ax, float az, float bx, float bz) => ax * bz - az * bx;
            float d1 = Cross(d.X - c.X, d.Z - c.Z, a.X - c.X, a.Z - c.Z);
            float d2 = Cross(d.X - c.X, d.Z - c.Z, b.X - c.X, b.Z - c.Z);
            float d3 = Cross(b.X - a.X, b.Z - a.Z, c.X - a.X, c.Z - a.Z);
            float d4 = Cross(b.X - a.X, b.Z - a.Z, d.X - a.X, d.Z - a.Z);
            return (d1 > 0f) != (d2 > 0f) && (d3 > 0f) != (d4 > 0f);
        }

        // Off the road, turned over, or trying to drive and not moving: put back after RespawnAfter seconds.
        private static bool Watch(ECSWorld world, RacerState r, Quaternion turn, Vector3 now, float dt)
        {
            var t = _track!;
            t.Nearest(now, out float offset);
            TrackFrame here = t.FrameAt(t.Nearest(now, out _));
            Vector3 up = Vector3.Transform(Vector3.UnitY, turn);
            bool off = MathF.Abs(offset) > here.HalfWidth + _race.OffTrack;
            bool over = up.Y < 0.5f;
            bool stuck = false;
            if (world.TryGetComponent<KartComponent>(r.Entity, out var k) && k.Throttle > 0.1f && MathF.Abs(k.Speed) < 0.5f) stuck = true;
            r.Bad = off || over || stuck ? r.Bad + dt : 0f;
            if (r.Bad < MathF.Max(0.1f, _race.RespawnAfter)) return false;
            r.Bad = 0f;
            int count = t.Checkpoints.Count;
            TrackFrame back = t.Checkpoints[(r.Next - 1 + count) % count];
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(back.Forward.X, back.Forward.Z));
            Vector3 at = back.Position + back.Up * 1f;
            SceneManager.TeleportBody(r.Entity, at, rotation);
            SceneManager.SetLinearVelocity(r.Entity, Vector3.Zero);
            SceneManager.SetAngularVelocity(r.Entity, Vector3.Zero);
            r.Last = at;
            r.Respawns++;
            Respawned?.Invoke(r.Entity);
            return true;
        }

        // Finished racers by their time; the others by laps, gates passed, and the distance to their next gate.
        private static void Places()
        {
            var t = _track!;
            int count = t.Checkpoints.Count;
            foreach (var r in _racers)
            {
                if (r.Finished) continue;
                int ahead = 0;
                float mine = Score(r);
                foreach (var o in _racers) if (o != r && (o.Finished || Score(o) > mine)) ahead++;
                r.Place = ahead + 1;
            }

            float Score(RacerState r)
            {
                TrackFrame g = t.Checkpoints[r.Next % count];
                float toGate = MathF.Min(Vector3.Distance(r.Last, g.Position), t.Length);
                return ((r.Lap - 1) * count + r.Next) * 100000f - toGate;
            }
        }

        private static void Hold(ECSWorld world, bool held)
        {
            foreach (var r in _racers)
                if (world.TryGetComponent<KartComponent>(r.Entity, out var k) && k.Held != held)
                {
                    k.Held = held;
                    world.AddComponent(r.Entity, k);
                }
        }

        // ── The HUD ────────────────────────────────────────────────────────────────────

        private static readonly string[] Ordinals = { "1st", "2nd", "3rd" };
        private static string Ordinal(int n) => n >= 1 && n <= 3 ? Ordinals[n - 1] : n.ToString(CultureInfo.InvariantCulture) + "th";

        /// <summary>A time as m:ss.t.</summary>
        public static string FormatTime(float seconds)
        {
            int tenths = (int)MathF.Floor(MathF.Max(0f, seconds) * 10f);
            return (tenths / 600).ToString(CultureInfo.InvariantCulture) + ":" + (tenths / 10 % 60).ToString("00", CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(CultureInfo.InvariantCulture);
        }

        private static RacerState? Shown()
        {
            foreach (var r in _racers) if (r.Player) return r;
            return _racers.Count > 0 ? _racers[0] : null;
        }

        // The HUD's texts, made again only when what they show changes (a frame makes no new string otherwise).
        private static int _cLap = -1, _cLaps = -1, _cPlace = -1, _cTenths = -1, _cCount = -1;
        private static string _sLap = "", _sPlace = "", _sTime = "", _sCount = "", _sResults = "";
        private static bool _resultsMade;

        private static void ForgetHud()
        {
            _cLap = _cLaps = _cPlace = _cTenths = _cCount = -1;
            _sLap = _sPlace = _sTime = _sCount = _sResults = "";
            _resultsMade = false;
        }

        /// <summary>The text a HUD element shows now.</summary>
        public static string Text(RaceHudShows shows)
        {
            RacerState? me = Shown();
            switch (shows)
            {
                case RaceHudShows.Lap:
                    if (me == null) return "";
                    int lap = Math.Min(me.Lap, _race.Laps);
                    if (lap != _cLap || _race.Laps != _cLaps)
                    {
                        _cLap = lap; _cLaps = _race.Laps;
                        _sLap = "Lap " + lap.ToString(CultureInfo.InvariantCulture) + "/" + _race.Laps.ToString(CultureInfo.InvariantCulture);
                    }
                    return _sLap;
                case RaceHudShows.Place:
                    if (me == null) return "";
                    if (me.Place != _cPlace) { _cPlace = me.Place; _sPlace = Ordinal(me.Place); }
                    return _sPlace;
                case RaceHudShows.Time:
                    float seconds = me?.Finished == true ? me.Time : Clock;
                    int tenths = (int)MathF.Floor(MathF.Max(0f, seconds) * 10f);
                    if (tenths != _cTenths) { _cTenths = tenths; _sTime = FormatTime(seconds); }
                    return _sTime;
                case RaceHudShows.Countdown:
                    int count = State == RaceState.Countdown ? (int)MathF.Ceiling(CountdownLeft) : State == RaceState.Racing && _sinceGo < 1f ? 0 : -2;
                    if (count != _cCount)
                    {
                        _cCount = count;
                        _sCount = count > 0 ? count.ToString(CultureInfo.InvariantCulture) : count == 0 ? "GO!" : "";
                    }
                    return _sCount;
                default:
                    if (State != RaceState.Finished) return "";
                    if (!_resultsMade)
                    {
                        _resultsMade = true;
                        var order = new List<RacerState>(_racers);
                        order.Sort((x, y) => x.Place.CompareTo(y.Place));
                        var lines = new System.Text.StringBuilder();
                        foreach (var r in order)
                            lines.Append(r.Place).Append(". ").Append(r.Name).Append("  ").Append(r.Finished ? FormatTime(r.Time) : "-").Append('\n');
                        _sResults = lines.ToString().TrimEnd();
                    }
                    return _sResults;
            }
        }

        private static void WriteHud(ECSWorld world)
        {
            if (!world.HasAnyComponent<RaceHudComponent>()) return;
            foreach (var row in world.Query<RaceHudComponent, UIText>().EnumerateWithEntities<RaceHudComponent, UIText>())
            {
                string text = Text(row.Item1.Value.Shows);
                ref UIText ui = ref row.Item2.Value;
                if (ui.Text != text) ui.Text = text;
            }
        }
    }
}
