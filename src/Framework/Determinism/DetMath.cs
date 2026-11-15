using System;

namespace GameEditor.Framework.Determinism
{
    /// <summary>
    /// Trigonometry with the same bits on every device (G10, docs DETERMINISM_DESIGN.md §3): only <c>+ − × ÷</c>,
    /// <see cref="MathF.Floor"/> and <see cref="MathF.Sqrt"/> - IEEE-754 operations every target rounds alike - never the
    /// platform's libm, whose <c>sin</c> differs in its last bit from device to device. Within 2e-6 of the true value for
    /// angles up to a few thousand radians.
    /// </summary>
    public static class DetMath
    {
        public const float PI = 3.14159265358979f;
        // π/2 in two parts (Cody–Waite): the reduction keeps its precision for large angles
        private const float PiO2Hi = 1.5707963705062866f, PiO2Lo = -4.37113900018624e-8f, TwoOverPi = 0.636619772367581f;

        public static float Sin(float x) { SinCos(x, out float s, out _); return s; }
        public static float Cos(float x) { SinCos(x, out _, out float c); return c; }

        public static void SinCos(float x, out float sin, out float cos)
        {
            float k = MathF.Floor(x * TwoOverPi + 0.5f);
            float r = (x - k * PiO2Hi) - k * PiO2Lo;           // r in [-π/4, π/4]
            float z = r * r;
            float s = r + r * z * (-1.6666654611e-1f + z * (8.3321608736e-3f + z * -1.9515295891e-4f));
            float c = 1f - 0.5f * z + z * z * (4.166664568298827e-2f + z * (-1.388731625493765e-3f + z * 2.443315711809948e-5f));
            int q = (int)(k - 4f * MathF.Floor(k * 0.25f));      // k mod 4, k's sign whatever
            switch (q)
            {
                case 0: sin = s; cos = c; break;
                case 1: sin = c; cos = -s; break;
                case 2: sin = -s; cos = -c; break;
                default: sin = -c; cos = s; break;
            }
        }

        public static float Tan(float x) { SinCos(x, out float s, out float c); return s / c; }

        /// <summary>atan, in (−π/2, π/2).</summary>
        public static float Atan(float x)
        {
            float sign = x < 0f ? -1f : 1f, a = x * sign, y0 = 0f;
            if (a > 2.414213562373095f) { y0 = PI * 0.5f; a = -1f / a; }
            else if (a > 0.4142135623730950f) { y0 = PI * 0.25f; a = (a - 1f) / (a + 1f); }
            float z = a * a;
            float y = ((((8.05374449538e-2f * z - 1.38776856032e-1f) * z + 1.99777106478e-1f) * z - 3.33329491539e-1f) * z * a) + a;
            return sign * (y0 + y);
        }

        /// <summary>atan2(y, x), in (−π, π] (0 for (0, 0)).</summary>
        public static float Atan2(float y, float x)
        {
            if (x == 0f) return y > 0f ? PI * 0.5f : y < 0f ? -PI * 0.5f : 0f;
            float a = Atan(y / x);
            if (x > 0f) return a;
            return y >= 0f ? a + PI : a - PI;
        }

        /// <summary>The IEEE square root (correctly rounded on every target).</summary>
        public static float Sqrt(float x) => MathF.Sqrt(x);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
