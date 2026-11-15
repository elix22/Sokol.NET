using System;

namespace GameEditor.Framework.Determinism
{
    /// <summary>
    /// A Q16.16 fixed-point number (G10, docs DETERMINISM_DESIGN.md §3): integer math only - the same bits everywhere,
    /// whatever the floating-point unit. Range ±32768, step 1/65536. Products and quotients go through <c>long</c>.
    /// </summary>
    public readonly struct Fixed : IEquatable<Fixed>, IComparable<Fixed>
    {
        public const int FractionBits = 16;
        public const int OneRaw = 1 << FractionBits;
        public readonly int Raw;

        private Fixed(int raw) { Raw = raw; }
        public static Fixed FromRaw(int raw) => new(raw);
        public static Fixed FromInt(int v) => new(v << FractionBits);
        /// <summary>The nearest fixed value (a float read once, e.g. from a setting: the conversion is exact IEEE math).</summary>
        public static Fixed FromFloat(float v) => new((int)MathF.Round(v * OneRaw));
        public float ToFloat() => Raw * (1f / OneRaw);
        public int ToInt() => Raw >> FractionBits;   // toward −∞

        public static readonly Fixed Zero = new(0), One = new(OneRaw), Half = new(OneRaw / 2);
        public static readonly Fixed Pi = new(205887), TwoPi = new(411775), HalfPi = new(102944);

        public static Fixed operator +(Fixed a, Fixed b) => new(a.Raw + b.Raw);
        public static Fixed operator -(Fixed a, Fixed b) => new(a.Raw - b.Raw);
        public static Fixed operator -(Fixed a) => new(-a.Raw);
        public static Fixed operator *(Fixed a, Fixed b) => new((int)(((long)a.Raw * b.Raw) >> FractionBits));
        public static Fixed operator /(Fixed a, Fixed b) => new((int)(((long)a.Raw << FractionBits) / b.Raw));
        public static bool operator ==(Fixed a, Fixed b) => a.Raw == b.Raw;
        public static bool operator !=(Fixed a, Fixed b) => a.Raw != b.Raw;
        public static bool operator <(Fixed a, Fixed b) => a.Raw < b.Raw;
        public static bool operator >(Fixed a, Fixed b) => a.Raw > b.Raw;
        public static bool operator <=(Fixed a, Fixed b) => a.Raw <= b.Raw;
        public static bool operator >=(Fixed a, Fixed b) => a.Raw >= b.Raw;

        public static Fixed Abs(Fixed a) => new(a.Raw < 0 ? -a.Raw : a.Raw);
        public static Fixed Min(Fixed a, Fixed b) => a.Raw < b.Raw ? a : b;
        public static Fixed Max(Fixed a, Fixed b) => a.Raw > b.Raw ? a : b;
        public static Fixed Floor(Fixed a) => new(a.Raw & ~(OneRaw - 1));

        /// <summary>√a (0 for a ≤ 0): the integer square root of Raw·2¹⁶ - exact to the last step.</summary>
        public static Fixed Sqrt(Fixed a)
        {
            if (a.Raw <= 0) return Zero;
            ulong n = (ulong)a.Raw << FractionBits, r = 0, bit = 1UL << 46;
            while (bit > n) bit >>= 2;
            while (bit != 0)
            {
                if (n >= r + bit) { n -= r + bit; r = (r >> 1) + bit; }
                else r >>= 1;
                bit >>= 2;
            }
            return new((int)r);
        }

        /// <summary>sin of an angle in radians: reduced to [−π/4, π/4] and a polynomial, in integers (within 1e-4).</summary>
        public static Fixed Sin(Fixed a) { SinCos(a, out var s, out _); return s; }
        public static Fixed Cos(Fixed a) { SinCos(a, out _, out var c); return c; }

        public static void SinCos(Fixed a, out Fixed sin, out Fixed cos)
        {
            long x = a.Raw % TwoPi.Raw;
            if (x < 0) x += TwoPi.Raw;
            int q = (int)((x + HalfPi.Raw / 2) / HalfPi.Raw);        // nearest quadrant 0..4
            long r = x - (long)q * HalfPi.Raw;                       // [−π/4, π/4]
            long z = (r * r) >> FractionBits;
            // sin r ≈ r − r³/6 + r⁵/120; cos r ≈ 1 − r²/2 + r⁴/24 − r⁶/720
            long z2 = (z * z) >> FractionBits;
            long s = r - ((r * z) >> FractionBits) / 6 + ((((r * z) >> FractionBits) * z) >> FractionBits) / 120;
            long c = OneRaw - z / 2 + z2 / 24 - ((z2 * z) >> FractionBits) / 720;
            switch (q & 3)
            {
                case 0: sin = new((int)s); cos = new((int)c); break;
                case 1: sin = new((int)c); cos = new((int)-s); break;
                case 2: sin = new((int)-s); cos = new((int)-c); break;
                default: sin = new((int)-c); cos = new((int)s); break;
            }
        }

        public bool Equals(Fixed other) => Raw == other.Raw;
        public override bool Equals(object? obj) => obj is Fixed f && f.Raw == Raw;
        public override int GetHashCode() => Raw;
        public int CompareTo(Fixed other) => Raw.CompareTo(other.Raw);
        public override string ToString() => ToFloat().ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);
    }
}
