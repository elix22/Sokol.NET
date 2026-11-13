// AnimationCurve.cs — a float curve of keyframes, as Unity's (docs: ANIMATION_SYSTEM_IMPLEMENTATION_PLAN.md §17).
//
// Keys sorted by time; between two keys a cubic Hermite on (value, out tangent) → (value, in tangent), the tangents
// slopes (value per second) scaled by the segment's length — the basis of CGltfBone's cubic spline. An infinite
// tangent (Constant) holds the key's value to the next key. Before the first key and after the last: the curve's
// PreWrap / PostWrap. Evaluating allocates nothing.

using System;

namespace GameEditor.Framework.Animation
{
    /// <summary>How a key's tangent on one side is set — Unity's five modes; ClampedAuto is a new key's.</summary>
    public enum TangentMode : byte
    {
        /// <summary>Smooth through the neighbours, flattened so the curve never goes past them (Unity's default).</summary>
        ClampedAuto,
        /// <summary>Smooth through the neighbours (Catmull-Rom); may overshoot.</summary>
        Auto,
        /// <summary>Points at the neighbour on that side.</summary>
        Linear,
        /// <summary>Holds the value until the next key (an infinite tangent).</summary>
        Constant,
        /// <summary>Set by hand; left as it is when keys move.</summary>
        Free,
    }

    /// <summary>What a curve does before its first key / after its last; what a clip does at its end (Unity's WrapMode).</summary>
    public enum WrapMode : byte { Once, Loop, PingPong, ClampForever }

    /// <summary>One key of a curve.</summary>
    public struct Keyframe
    {
        public float Time;
        public float Value;
        /// <summary>Slope arriving at the key (value per second); infinity for a Constant segment.</summary>
        public float InTangent;
        /// <summary>Slope leaving the key.</summary>
        public float OutTangent;
        public TangentMode ModeIn;
        public TangentMode ModeOut;
        /// <summary>The two sides edited apart (Unity's "Broken").</summary>
        public bool Broken;

        public Keyframe(float time, float value)
        {
            Time = time; Value = value; InTangent = 0f; OutTangent = 0f;
            ModeIn = ModeOut = TangentMode.ClampedAuto; Broken = false;
        }

        public Keyframe(float time, float value, float inTangent, float outTangent)
        {
            Time = time; Value = value; InTangent = inTangent; OutTangent = outTangent;
            ModeIn = ModeOut = TangentMode.Free; Broken = inTangent != outTangent;
        }
    }

    public sealed class AnimationCurve
    {
        private Keyframe[] _keys;
        private int _count;

        public WrapMode PreWrap  = WrapMode.ClampForever;
        public WrapMode PostWrap = WrapMode.ClampForever;

        public AnimationCurve() { _keys = Array.Empty<Keyframe>(); }

        /// <summary>A curve of the keys given (sorted here; the auto tangents set).</summary>
        public AnimationCurve(params Keyframe[] keys)
        {
            _keys = (Keyframe[])keys.Clone();
            _count = keys.Length;
            Array.Sort(_keys, 0, _count, KeyTimeOrder.Instance);
            UpdateAllTangents();
        }

        public int Length => _count;

        /// <summary>The key at <paramref name="i"/>; a change to it is the caller's to follow with <see cref="UpdateTangents"/>.</summary>
        public ref Keyframe this[int i]
        {
            get
            {
                if ((uint)i >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(i));
                return ref _keys[i];
            }
        }

        public ReadOnlySpan<Keyframe> Keys => new(_keys, 0, _count);

        // ── Evaluating ─────────────────────────────────────────────────────────────────

        public float Evaluate(float time)
        {
            int hint = -1;
            return Evaluate(time, ref hint);
        }

        /// <summary>
        /// The curve's value at <paramref name="time"/>. <paramref name="hint"/> is the segment last used (−1 at first):
        /// playback moves forward, so the next segment is found without a search.
        /// </summary>
        public float Evaluate(float time, ref int hint)
        {
            if (_count == 0) return 0f;
            if (_count == 1) return _keys[0].Value;
            float first = _keys[0].Time, last = _keys[_count - 1].Time;
            if (time < first) time = Wrap(time, first, last, PreWrap);
            else if (time > last) time = Wrap(time, first, last, PostWrap);
            if (time <= first) return _keys[0].Value;
            if (time >= last) return _keys[_count - 1].Value;

            int i = Segment(time, hint);
            hint = i;
            ref Keyframe a = ref _keys[i];
            ref Keyframe b = ref _keys[i + 1];
            if (float.IsInfinity(a.OutTangent) || float.IsInfinity(b.InTangent)) return a.Value;
            float dt = b.Time - a.Time;
            float s = (time - a.Time) / dt;
            float s2 = s * s, s3 = s2 * s;
            float h00 = 2f * s3 - 3f * s2 + 1f;
            float h10 = s3 - 2f * s2 + s;
            float h01 = -2f * s3 + 3f * s2;
            float h11 = s3 - s2;
            return h00 * a.Value + h10 * a.OutTangent * dt + h01 * b.Value + h11 * b.InTangent * dt;
        }

        // The segment [i, i+1] holding time (first < time < last).
        private int Segment(float time, int hint)
        {
            if ((uint)hint < (uint)(_count - 1))
            {
                if (time >= _keys[hint].Time && time < _keys[hint + 1].Time) return hint;
                if (hint + 2 < _count && time >= _keys[hint + 1].Time && time < _keys[hint + 2].Time) return hint + 1;
            }
            int lo = 0, hi = _count - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (_keys[mid].Time <= time) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        private static float Wrap(float time, float first, float last, WrapMode mode)
        {
            float length = last - first;
            if (length <= 0f) return first;
            switch (mode)
            {
                case WrapMode.Loop:
                {
                    float r = (time - first) % length;
                    return first + (r < 0f ? r + length : r);
                }
                case WrapMode.PingPong:
                {
                    float r = (time - first) % (2f * length);
                    if (r < 0f) r += 2f * length;
                    return first + (r <= length ? r : 2f * length - r);
                }
                default:
                    return time < first ? first : last;     // Once, ClampForever: the end key's value
            }
        }

        // ── Editing ────────────────────────────────────────────────────────────────────

        /// <summary>Adds a key with Unity's default tangents (ClampedAuto); −1 when a key is at that time already.</summary>
        public int AddKey(float time, float value) => AddKey(new Keyframe(time, value));

        /// <summary>Adds the key; its index, or −1 when a key is at that time already (Unity's rule).</summary>
        public int AddKey(in Keyframe key)
        {
            int at = Find(key.Time, out bool exists);
            if (exists) return -1;
            Insert(at, key);
            UpdateTangents(at);
            return at;
        }

        /// <summary>
        /// Puts <paramref name="key"/> in the place of the key at <paramref name="index"/>; the keys are sorted again, so
        /// the key's index may change — the new one is returned. On another key's time the old time is kept (Unity's rule).
        /// </summary>
        public int MoveKey(int index, in Keyframe key)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            Keyframe k = key;
            float old = _keys[index].Time;
            RemoveAt(index);
            int at = Find(k.Time, out bool exists);
            if (exists) { k.Time = old; at = Find(old, out _); }
            Insert(at, k);
            UpdateAllTangents();          // its old neighbours and its new ones (an edit, not a frame: the cost is no matter)
            return at;
        }

        public void RemoveKey(int index)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            RemoveAt(index);
            if (_count > 0) UpdateTangents(Math.Min(index, _count - 1));
        }

        /// <summary>The tangents set by mode (all but Free) of the key at <paramref name="index"/> and its neighbours.</summary>
        public void UpdateTangents(int index)
        {
            for (int i = Math.Max(0, index - 1); i <= Math.Min(_count - 1, index + 1); i++) SetTangents(i);
        }

        public void UpdateAllTangents()
        {
            for (int i = 0; i < _count; i++) SetTangents(i);
        }

        private void SetTangents(int i)
        {
            ref Keyframe k = ref _keys[i];
            float left = i > 0 ? Slope(i - 1, i) : float.NaN;              // the straight slopes to each neighbour
            float right = i < _count - 1 ? Slope(i, i + 1) : float.NaN;
            k.InTangent = Tangent(k.ModeIn, k.InTangent, i, left, right, isIn: true);
            k.OutTangent = Tangent(k.ModeOut, k.OutTangent, i, left, right, isIn: false);
        }

        private float Slope(int a, int b) => (_keys[b].Value - _keys[a].Value) / (_keys[b].Time - _keys[a].Time);

        private float Tangent(TangentMode mode, float current, int i, float left, float right, bool isIn)
        {
            switch (mode)
            {
                case TangentMode.Free: return current;
                case TangentMode.Constant: return float.PositiveInfinity;
                case TangentMode.Linear:
                {
                    float s = isIn ? left : right;
                    return float.IsNaN(s) ? 0f : s;
                }
                default:
                {
                    if (float.IsNaN(left) || float.IsNaN(right)) return 0f;   // an end key: flat
                    float auto = (_keys[i + 1].Value - _keys[i - 1].Value) / (_keys[i + 1].Time - _keys[i - 1].Time);
                    if (mode == TangentMode.Auto) return auto;
                    // ClampedAuto: flat at a peak or a trough; else no steeper than 3× either side's slope, which keeps
                    // each Hermite segment between its two keys (Fritsch–Carlson).
                    if (left * right <= 0f) return 0f;
                    float limit = 3f * MathF.Min(MathF.Abs(left), MathF.Abs(right));
                    return MathF.Abs(auto) > limit ? MathF.CopySign(limit, auto) : auto;
                }
            }
        }

        // The index of the key at time (exists) or where it would go.
        private int Find(float time, out bool exists)
        {
            int lo = 0, hi = _count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_keys[mid].Time < time) lo = mid + 1; else hi = mid;
            }
            exists = lo < _count && _keys[lo].Time == time;
            return lo;
        }

        private void Insert(int at, in Keyframe k)
        {
            if (_count == _keys.Length) Array.Resize(ref _keys, Math.Max(4, _count * 2));
            Array.Copy(_keys, at, _keys, at + 1, _count - at);
            _keys[at] = k;
            _count++;
        }

        private void RemoveAt(int at)
        {
            Array.Copy(_keys, at + 1, _keys, at, _count - at - 1);
            _count--;
        }

        private sealed class KeyTimeOrder : System.Collections.Generic.IComparer<Keyframe>
        {
            public static readonly KeyTimeOrder Instance = new();
            public int Compare(Keyframe a, Keyframe b) => a.Time.CompareTo(b.Time);
        }
    }
}
