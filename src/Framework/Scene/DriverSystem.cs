using System;
using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// Drives every kart with a <see cref="DriverComponent"/> (docs/H5_DESIGN.md §8), before each fixed physics step and
    /// ahead of the kart controller: it steers at the racing line <see cref="DriverComponent.LookAhead"/> metres ahead,
    /// and takes the throttle or the brake to go at the speed the line allows there times its skill. The speed a line
    /// allows comes from its own curvature, lowered backwards from each bend by a kart's braking. A mistake — running wide
    /// in a bend — is drawn once a lap from a random number seeded by the kart and the lap: a race is the same everywhere.
    /// </summary>
    public static class DriverSystem
    {
        /// <summary>Sideways grip a line's speed is worked out with (metres a second each second).</summary>
        public const float LateralGrip = 13f;
        /// <summary>The braking the speeds are lowered backwards with: less than a kart's, so a driver brakes early.</summary>
        public const float PlannedBraking = 14f;

        private static TrackData? _track;
        private static int _trackVersion = -1;
        private static float[] _speeds = Array.Empty<float>();      // the line's speed at each frame of the track
        private static readonly Dictionary<Entity, (int lap, float from, float side)> _wide = new();
        private static readonly List<(Entity e, float along, float offset, float speed)> _karts = new();   // every kart this step

        /// <summary>The speed the racing line allows at a distance along the track (before a driver's skill).</summary>
        public static float SpeedAt(float distance)
        {
            if (_track == null || _speeds.Length == 0) return 0f;
            int n = _speeds.Length;
            float step = _track.Length / n;
            float d = distance % _track.Length;
            if (d < 0f) d += _track.Length;
            int i = Math.Min((int)(d / step), n - 1);
            float f = (d - i * step) / step;
            return _speeds[i] + (_speeds[(i + 1) % n] - _speeds[i]) * f;
        }

        /// <summary>The track drivers follow now (the scene's), with its speeds made again when it changed.</summary>
        public static TrackData? Track(ECSWorld world)
        {
            if (_track != null && _trackVersion == TrackLibrary.Version) return _track;
            _track = null;
            foreach (var row in world.Query<TrackComponent, Transform>().EnumerateWithEntities<TrackComponent, Transform>())
                if (TrackLibrary.Track(row.Item1.Value.Key) is { } t) { _track = t; break; }
            _trackVersion = TrackLibrary.Version;
            _speeds = _track != null ? Speeds(_track) : Array.Empty<float>();
            return _track;
        }

        /// <summary>Forgets what was worked out: when play stops.</summary>
        public static void Reset()
        {
            _track = null;
            _trackVersion = -1;
            _speeds = Array.Empty<float>();
            _wide.Clear();
        }

        // The line's speed at each frame: sqrt(grip × radius) from the curvature over ±4 frames, then lowered backwards.
        private static float[] Speeds(TrackData t)
        {
            int n = t.Frames.Count;
            var v = new float[n];
            Vector3 P(int i) { TrackFrame f = t.Frames[((i % n) + n) % n]; return f.Position + f.Right * f.Line; }
            for (int i = 0; i < n; i++)
            {
                Vector3 a = P(i - 4), b = P(i), c = P(i + 4);
                Vector3 ab = b - a, bc = c - b;
                float turn = MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(ab), Vector3.Normalize(bc)), -1f, 1f));
                float length = (ab.Length() + bc.Length()) * 0.5f;
                float curvature = turn / MathF.Max(length, 0.01f);
                v[i] = curvature < 1e-4f ? 1000f : MathF.Sqrt(LateralGrip / curvature);
            }
            float step = t.Length / n;
            for (int pass = 0; pass < 2; pass++)
                for (int i = n - 1; i >= 0; i--)
                {
                    float next = v[(i + 1) % n];
                    v[i] = MathF.Min(v[i], MathF.Sqrt(next * next + 2f * PlannedBraking * step));
                }
            return v;
        }

        /// <summary>One fixed step of every driver (called before the kart controller's).</summary>
        public static void Step(float dt)
        {
            var world = ECSWorld.Instance;
            if (!world.HasAnyComponent<DriverComponent>() || Track(world) is not { } t) return;
            IPhysicsWorld? physics = SceneManager.Physics3D;
            if (physics == null) return;
            float playerAt = PlayerProgress(world, t);
            _karts.Clear();
            foreach (var row in world.Query<KartComponent, Transform>().EnumerateWithEntities<KartComponent, Transform>())
                if (SceneManager.TryGetBody(row.Entity, out PhysicsBodyHandle kb))
                {
                    float a = t.Nearest(physics.GetPosition(kb), out float o);
                    _karts.Add((row.Entity, a, o, row.Item1.Value.Speed));
                }
            foreach (var row in world.Query<DriverComponent, KartComponent>().EnumerateWithEntities<DriverComponent, KartComponent>())
            {
                if (!SceneManager.TryGetBody(row.Entity, out PhysicsBodyHandle body)) continue;
                ref readonly DriverComponent d = ref row.Item1.Value;
                ref KartComponent k = ref row.Item2.Value;
                RacerState? racer = Race.Of(row.Entity);
                if (racer is { Finished: true }) { k.Throttle = 0f; k.Steer = 0f; k.Brake = 0.3f; continue; }

                Vector3 at = physics.GetPosition(body);
                Quaternion turn = physics.GetRotation(body);
                float along = t.Nearest(at, out _);
                float wide = Wide(row.Entity, d, racer, along, t);
                TrackFrame aim = t.FrameAt(along + MathF.Max(2f, d.LookAhead));
                float across = aim.Line + wide + Traffic(row.Entity, along, aim.Line + wide, aim.HalfWidth, t, out float behind);
                Vector3 target = aim.Position + aim.Right * Math.Clamp(across, -aim.HalfWidth + 1f, aim.HalfWidth - 1f);
                Vector3 to = target - at;
                Vector3 fwd = Vector3.Transform(Vector3.UnitZ, turn);
                to.Y = 0f;
                float side = to.LengthSquared() > 1e-6f ? Vector3.Cross(fwd, Vector3.Normalize(to)).Y : 0f;
                k.Steer = Math.Clamp(-side * 3f, -1f, 1f);

                float allowed = MathF.Min(k.TopSpeed, SpeedAt(along + 2f)) * Math.Clamp(d.Skill, 0.3f, 1f);
                if (!float.IsNaN(behind)) allowed = MathF.Min(allowed, behind + 0.5f);   // right behind another kart: no faster than it, until there is room
                if (d.CatchUp && racer != null && !float.IsNaN(playerAt))
                {
                    float gap = playerAt - Progress(racer, along, t);       // > 0: the player is ahead
                    allowed *= 1f + Math.Clamp((MathF.Abs(gap) - 40f) / 200f, 0f, 0.06f) * MathF.Sign(gap);
                }
                float speed = k.Speed;
                if (speed > allowed + 1f) { k.Throttle = 0f; k.Brake = Math.Clamp((speed - allowed) / 4f, 0.2f, 1f); }
                else { k.Brake = 0f; k.Throttle = Math.Clamp((allowed - speed) / 2f + 0.3f, 0f, 1f); }
            }
        }

        // A kart close ahead on the line (within 10 m along, 2.2 m across): aim 2.6 m to the side that has more room. One
        // right in front (within 6 m, 1.6 m across of where this kart is): `behind` is its speed, else NaN.
        private static float Traffic(Entity me, float along, float line, float halfWidth, TrackData t, out float behind)
        {
            behind = float.NaN;
            float mine = 0f;
            foreach (var (e, a, o, _) in _karts) if (e == me) { mine = o; break; }
            float shift = 0f;
            foreach (var (e, a, o, s) in _karts)
            {
                if (e == me) continue;
                float ahead = a - along;
                if (ahead < -t.Length / 2f) ahead += t.Length;
                else if (ahead > t.Length / 2f) ahead -= t.Length;
                if (ahead <= 0.5f || ahead > 10f) continue;
                if (ahead < 6f && MathF.Abs(o - mine) < 1.6f) behind = float.IsNaN(behind) ? s : MathF.Min(behind, s);
                if (shift == 0f && MathF.Abs(o - line) <= 2.2f)
                {
                    float roomRight = halfWidth - o, roomLeft = o + halfWidth;
                    shift = roomRight > roomLeft ? 2.6f : -2.6f;
                }
            }
            return shift;
        }

        // How far round the race a racer is: laps done × length + the distance along.
        private static float Progress(RacerState r, float along, TrackData t)
            => (r.Lap - 1) * t.Length + (r.Next == 1 && along > t.Length * 0.75f ? along - t.Length : along);

        private static float PlayerProgress(ECSWorld world, TrackData t)
        {
            foreach (RacerState r in Race.Racers)
                if (world.TryGetComponent<KartComponent>(r.Entity, out var k) && k.Player && world.TryGetComponent<Transform>(r.Entity, out var tr))
                    return Progress(r, t.Nearest(tr.Position, out _), t);
            return float.NaN;
        }

        // The offset of a mistake now: once a lap, with its chance, 30 m of one stretch run WideBy outwards.
        private static float Wide(Entity e, in DriverComponent d, RacerState? racer, float along, TrackData t)
        {
            int lap = racer?.Lap ?? 1;
            if (!_wide.TryGetValue(e, out var w) || w.lap != lap)
            {
                uint seed = (uint)(ECSWorld.Instance.GetSceneId(e) * 73856093) ^ (uint)(lap * 19349663) ^ 0x9E3779B9u;
                float chance = Next(ref seed), where = Next(ref seed);
                w = chance < d.Mistakes ? (lap, where * t.Length, Next(ref seed) < 0.5f ? -1f : 1f) : (lap, -1f, 0f);
                _wide[e] = w;
            }
            if (w.from < 0f) return 0f;
            float into = along - w.from;
            if (into < 0f) into += t.Length;
            return into < 30f ? w.side * d.WideBy : 0f;

            static float Next(ref uint s) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / (float)0x1000000; }
        }
    }
}
