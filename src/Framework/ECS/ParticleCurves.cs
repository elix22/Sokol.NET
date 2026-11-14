using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace GameEditor.Framework.ECS.Components
{
    // The value types of the particle modules (examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §3.0): a curve, a gradient
    // and a list of bursts, each a struct of inline arrays, so that a copy of a ParticlesComponent is a whole copy (an undo
    // step) and two compare by value. Evaluating allocates nothing.

    /// <summary>
    /// A curve over a particle's life (0..1): up to <see cref="Capacity"/> keys, smooth between them (automatic tangents:
    /// two keys are a straight line), held flat before the first key and after the last, times <see cref="Multiplier"/>.
    /// No keys: <see cref="Multiplier"/> everywhere.
    /// </summary>
    public struct ParticleCurve : IEquatable<ParticleCurve>
    {
        public const int Capacity = 8;

        [InlineArray(Capacity)]
        public struct KeyArray { private Vector2 _e; }

        /// <summary>The keys, (time, value), in time order: the first <see cref="Count"/>.</summary>
        public KeyArray Keys;
        public int Count;
        public float Multiplier;

        /// <summary>A straight line from <paramref name="from"/> at 0 to <paramref name="to"/> at 1.</summary>
        public static ParticleCurve Line(float from, float to)
        {
            var c = new ParticleCurve { Multiplier = 1f, Count = 2 };
            c.Keys[0] = new Vector2(0f, from);
            c.Keys[1] = new Vector2(1f, to);
            return c;
        }

        /// <summary>The curve of the given (time, value) keys (sorted by time; at most <see cref="Capacity"/>).</summary>
        public static ParticleCurve Of(params Vector2[] keys)
        {
            var c = new ParticleCurve { Multiplier = 1f };
            foreach (Vector2 k in keys) c.Add(k.X, k.Y);
            return c;
        }

        /// <summary>Adds a key in its place by time; false when the curve is full. Returns its index in <paramref name="index"/>.</summary>
        public bool Add(float time, float value, out int index)
        {
            index = -1;
            if (Count >= Capacity) return false;
            time = Math.Clamp(time, 0f, 1f);
            int i = Count;
            while (i > 0 && Keys[i - 1].X > time) { Keys[i] = Keys[i - 1]; i--; }
            Keys[i] = new Vector2(time, value);
            Count++;
            index = i;
            return true;
        }

        public bool Add(float time, float value) => Add(time, value, out _);

        /// <summary>Takes key <paramref name="index"/> away (a curve keeps at least one).</summary>
        public void Remove(int index)
        {
            if (index < 0 || index >= Count || Count <= 1) return;
            for (int i = index; i < Count - 1; i++) Keys[i] = Keys[i + 1];
            Count--;
        }

        /// <summary>Moves key <paramref name="index"/> to (time, value), kept between its neighbours' times; returns its index.</summary>
        public int Move(int index, float time, float value)
        {
            if (index < 0 || index >= Count) return index;
            float lo = index > 0 ? Keys[index - 1].X : 0f, hi = index < Count - 1 ? Keys[index + 1].X : 1f;
            Keys[index] = new Vector2(Math.Clamp(time, lo, hi), value);
            return index;
        }

        public readonly float Evaluate(float t)
        {
            int n = Count;
            if (n <= 0) return Multiplier;
            if (n == 1 || t <= Keys[0].X) return Keys[0].Y * Multiplier;
            if (t >= Keys[n - 1].X) return Keys[n - 1].Y * Multiplier;
            int i = 1;
            while (i < n - 1 && t > Keys[i].X) i++;
            Vector2 a = Keys[i - 1], b = Keys[i];
            float span = b.X - a.X;
            if (span <= 1e-6f) return b.Y * Multiplier;
            float s = (t - a.X) / span;
            // Hermite with the automatic tangents (slope across the neighbours; at an end, the slope to the one neighbour).
            float ma = Slope(i - 1), mb = Slope(i);
            float s2 = s * s, s3 = s2 * s;
            float v = (2f * s3 - 3f * s2 + 1f) * a.Y + (s3 - 2f * s2 + s) * span * ma + (-2f * s3 + 3f * s2) * b.Y + (s3 - s2) * span * mb;
            return v * Multiplier;
        }

        private readonly float Slope(int i)
        {
            int lo = Math.Max(0, i - 1), hi = Math.Min(Count - 1, i + 1);
            float dt = Keys[hi].X - Keys[lo].X;
            return dt > 1e-6f ? (Keys[hi].Y - Keys[lo].Y) / dt : 0f;
        }

        public readonly bool Equals(ParticleCurve o)
        {
            if (Count != o.Count || Multiplier != o.Multiplier) return false;
            for (int i = 0; i < Count; i++) if (Keys[i] != o.Keys[i]) return false;
            return true;
        }

        public override readonly bool Equals(object? obj) => obj is ParticleCurve o && Equals(o);
        public override readonly int GetHashCode() => HashCode.Combine(Count, Multiplier, Count > 0 ? Keys[0] : default);
    }

    /// <summary>
    /// A colour gradient over a particle's life (0..1): up to <see cref="Capacity"/> colour keys and as many alpha keys, each
    /// list blended between its keys (or, <see cref="Fixed"/>, holding a key's value until the next), as the reference
    /// editor's Gradient. No keys: white, opaque.
    /// </summary>
    public struct ParticleGradient : IEquatable<ParticleGradient>
    {
        public const int Capacity = 8;

        [InlineArray(Capacity)]
        public struct ColorArray { private Vector4 _e; }     // xyz colour, w time
        [InlineArray(Capacity)]
        public struct AlphaArray { private Vector2 _e; }     // x alpha, y time

        public ColorArray Colors;
        public AlphaArray Alphas;
        public int ColorCount, AlphaCount;
        public bool Fixed;

        /// <summary>From <paramref name="a"/> at 0 to <paramref name="b"/> at 1 (colour and alpha): a straight blend.</summary>
        public static ParticleGradient Two(Vector4 a, Vector4 b)
        {
            var g = new ParticleGradient { ColorCount = 2, AlphaCount = 2 };
            g.Colors[0] = new Vector4(a.X, a.Y, a.Z, 0f); g.Colors[1] = new Vector4(b.X, b.Y, b.Z, 1f);
            g.Alphas[0] = new Vector2(a.W, 0f); g.Alphas[1] = new Vector2(b.W, 1f);
            return g;
        }

        public static ParticleGradient White => Two(Vector4.One, Vector4.One);

        public readonly Vector4 Evaluate(float t)
        {
            Vector3 rgb = Vector3.One;
            float alpha = 1f;
            int n = ColorCount;
            if (n > 0)
            {
                int i = 0;
                while (i < n && Colors[i].W < t) i++;
                if (i == 0) rgb = Xyz(Colors[0]);
                else if (i == n) rgb = Xyz(Colors[n - 1]);
                else if (Fixed) rgb = Xyz(Colors[i]);
                else
                {
                    Vector4 a = Colors[i - 1], b = Colors[i];
                    float f = b.W - a.W > 1e-6f ? (t - a.W) / (b.W - a.W) : 1f;
                    rgb = Vector3.Lerp(Xyz(a), Xyz(b), f);
                }
            }
            n = AlphaCount;
            if (n > 0)
            {
                int i = 0;
                while (i < n && Alphas[i].Y < t) i++;
                if (i == 0) alpha = Alphas[0].X;
                else if (i == n) alpha = Alphas[n - 1].X;
                else if (Fixed) alpha = Alphas[i].X;
                else
                {
                    Vector2 a = Alphas[i - 1], b = Alphas[i];
                    float f = b.Y - a.Y > 1e-6f ? (t - a.Y) / (b.Y - a.Y) : 1f;
                    alpha = a.X + (b.X - a.X) * f;
                }
            }
            return new Vector4(rgb, alpha);

            static Vector3 Xyz(Vector4 v) => new(v.X, v.Y, v.Z);
        }

        /// <summary>Adds a colour key in its place by time; false when full. Its index in <paramref name="index"/>.</summary>
        public bool AddColor(float time, Vector3 rgb, out int index)
        {
            index = -1;
            if (ColorCount >= Capacity) return false;
            time = Math.Clamp(time, 0f, 1f);
            int i = ColorCount;
            while (i > 0 && Colors[i - 1].W > time) { Colors[i] = Colors[i - 1]; i--; }
            Colors[i] = new Vector4(rgb, time);
            ColorCount++;
            index = i;
            return true;
        }

        /// <summary>Adds an alpha key in its place by time; false when full.</summary>
        public bool AddAlpha(float time, float alpha, out int index)
        {
            index = -1;
            if (AlphaCount >= Capacity) return false;
            time = Math.Clamp(time, 0f, 1f);
            int i = AlphaCount;
            while (i > 0 && Alphas[i - 1].Y > time) { Alphas[i] = Alphas[i - 1]; i--; }
            Alphas[i] = new Vector2(alpha, time);
            AlphaCount++;
            index = i;
            return true;
        }

        /// <summary>Takes a colour key away (one is kept).</summary>
        public void RemoveColor(int index)
        {
            if (index < 0 || index >= ColorCount || ColorCount <= 1) return;
            for (int i = index; i < ColorCount - 1; i++) Colors[i] = Colors[i + 1];
            ColorCount--;
        }

        /// <summary>Takes an alpha key away (one is kept).</summary>
        public void RemoveAlpha(int index)
        {
            if (index < 0 || index >= AlphaCount || AlphaCount <= 1) return;
            for (int i = index; i < AlphaCount - 1; i++) Alphas[i] = Alphas[i + 1];
            AlphaCount--;
        }

        /// <summary>Moves colour key <paramref name="index"/> to <paramref name="time"/>, re-sorted; returns its new index.</summary>
        public int MoveColor(int index, float time)
        {
            if (index < 0 || index >= ColorCount) return index;
            Vector4 k = Colors[index];
            k.W = Math.Clamp(time, 0f, 1f);
            for (int i = index; i < ColorCount - 1; i++) Colors[i] = Colors[i + 1];
            ColorCount--;
            AddColor(k.W, new Vector3(k.X, k.Y, k.Z), out int at);
            return at;
        }

        /// <summary>Moves alpha key <paramref name="index"/> to <paramref name="time"/>, re-sorted; returns its new index.</summary>
        public int MoveAlpha(int index, float time)
        {
            if (index < 0 || index >= AlphaCount) return index;
            Vector2 k = Alphas[index];
            for (int i = index; i < AlphaCount - 1; i++) Alphas[i] = Alphas[i + 1];
            AlphaCount--;
            AddAlpha(time, k.X, out int at);
            return at;
        }

        public readonly bool Equals(ParticleGradient o)
        {
            if (ColorCount != o.ColorCount || AlphaCount != o.AlphaCount || Fixed != o.Fixed) return false;
            for (int i = 0; i < ColorCount; i++) if (Colors[i] != o.Colors[i]) return false;
            for (int i = 0; i < AlphaCount; i++) if (Alphas[i] != o.Alphas[i]) return false;
            return true;
        }

        public override readonly bool Equals(object? obj) => obj is ParticleGradient o && Equals(o);
        public override readonly int GetHashCode() => HashCode.Combine(ColorCount, AlphaCount, Fixed);
    }

    /// <summary>One burst of the Emission module: <see cref="Count"/> particles at <see cref="Time"/> seconds into each loop
    /// of the system, <see cref="Cycles"/> times (0: as long as the loop lasts) every <see cref="Interval"/> seconds, each
    /// time with the chance <see cref="Probability"/>.</summary>
    public struct ParticleBurst
    {
        public float Time;
        public int Count;
        public int Cycles;
        public float Interval;
        public float Probability;

        public static ParticleBurst At(float time, int count, int cycles = 1, float interval = 0.01f) =>
            new() { Time = time, Count = count, Cycles = cycles, Interval = interval, Probability = 1f };
    }

    /// <summary>The Emission module's bursts: up to <see cref="Capacity"/>, the first <see cref="Count"/>.</summary>
    public struct ParticleBursts : IEquatable<ParticleBursts>
    {
        public const int Capacity = 8;

        [InlineArray(Capacity)]
        public struct BurstArray { private ParticleBurst _e; }

        public BurstArray Items;
        public int Count;

        public bool Add(in ParticleBurst b)
        {
            if (Count >= Capacity) return false;
            Items[Count++] = b;
            return true;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Count) return;
            for (int i = index; i < Count - 1; i++) Items[i] = Items[i + 1];
            Count--;
        }

        public readonly bool Equals(ParticleBursts o)
        {
            if (Count != o.Count) return false;
            for (int i = 0; i < Count; i++)
            {
                ParticleBurst a = Items[i], b = o.Items[i];
                if (a.Time != b.Time || a.Count != b.Count || a.Cycles != b.Cycles || a.Interval != b.Interval || a.Probability != b.Probability) return false;
            }
            return true;
        }

        public override readonly bool Equals(object? obj) => obj is ParticleBursts o && Equals(o);
        public override readonly int GetHashCode() => Count;
    }

    /// <summary>The Collision module's planes (G8.8 Q1): up to <see cref="Capacity"/> entities, each a plane through its
    /// position facing its up; the first <see cref="Count"/>.</summary>
    public struct ParticlePlanes : IEquatable<ParticlePlanes>
    {
        public const int Capacity = 6;

        [InlineArray(Capacity)]
        public struct EntityArray { private Frent.Entity _e; }

        public EntityArray Items;
        public int Count;

        public bool Add(Frent.Entity e)
        {
            if (Count >= Capacity) return false;
            Items[Count++] = e;
            return true;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Count) return;
            for (int i = index; i < Count - 1; i++) Items[i] = Items[i + 1];
            Items[--Count] = default;
        }

        public readonly bool Equals(ParticlePlanes o)
        {
            if (Count != o.Count) return false;
            for (int i = 0; i < Count; i++) if (Items[i] != o.Items[i]) return false;
            return true;
        }

        public override readonly bool Equals(object? obj) => obj is ParticlePlanes o && Equals(o);
        public override readonly int GetHashCode() => Count;
    }

    /// <summary>When a sub-emitter makes particles: as long as a parent particle lives, when it hits something, when it dies.</summary>
    public enum ParticleSubType : byte { Birth, Collision, Death }

    /// <summary>What a sub-emitter's particles take from their parent particle.</summary>
    [Flags]
    public enum ParticleInherit : byte { None = 0, Color = 1, Size = 2, Rotation = 4, Lifetime = 8 }

    /// <summary>One entry of the Sub Emitters module (G8.8 Q2): an emitter entity, when it makes particles, what they take, the chance.</summary>
    public struct ParticleSubEmitter
    {
        public Frent.Entity Emitter;
        public ParticleSubType Type;
        public ParticleInherit Inherit;
        public float Probability;
    }

    /// <summary>The Sub Emitters module's list: up to <see cref="Capacity"/> entries, the first <see cref="Count"/>.</summary>
    public struct ParticleSubEmitters : IEquatable<ParticleSubEmitters>
    {
        public const int Capacity = 4;

        [InlineArray(Capacity)]
        public struct EntryArray { private ParticleSubEmitter _e; }

        public EntryArray Items;
        public int Count;

        public bool Add(in ParticleSubEmitter s)
        {
            if (Count >= Capacity) return false;
            Items[Count++] = s;
            return true;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Count) return;
            for (int i = index; i < Count - 1; i++) Items[i] = Items[i + 1];
            Items[--Count] = default;
        }

        public readonly bool Equals(ParticleSubEmitters o)
        {
            if (Count != o.Count) return false;
            for (int i = 0; i < Count; i++)
            {
                ParticleSubEmitter a = Items[i], b = o.Items[i];
                if (a.Emitter != b.Emitter || a.Type != b.Type || a.Inherit != b.Inherit || a.Probability != b.Probability) return false;
            }
            return true;
        }

        public override readonly bool Equals(object? obj) => obj is ParticleSubEmitters o && Equals(o);
        public override readonly int GetHashCode() => Count;
    }
}
