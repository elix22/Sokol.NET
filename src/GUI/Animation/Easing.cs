using System;

namespace Sokol.GUI;

/// <summary>
/// Easing function library.
/// </summary>
public static class Easing
{
    public static float Linear(float t) => t;

    public static float EaseInQuad(float t)  => t * t;
    public static float EaseOutQuad(float t) => t * (2f - t);
    public static float EaseInOutQuad(float t) =>
        t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

    public static float EaseInCubic(float t)  => t * t * t;
    public static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
    public static float EaseInOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;

    public static float EaseInSine(float t)  => 1f - MathF.Cos(t * MathF.PI * 0.5f);
    public static float EaseOutSine(float t) => MathF.Sin(t * MathF.PI * 0.5f);
    public static float EaseInOutSine(float t) => -(MathF.Cos(MathF.PI * t) - 1f) / 2f;

    public static float EaseOutElastic(float t)
    {
        const float c4 = 2f * MathF.PI / 3f;
        if (t == 0f || t == 1f) return t;
        return MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    public static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f, d1 = 2.75f;
        if (t < 1f / d1)       return n1 * t * t;
        if (t < 2f / d1)     { t -= 1.5f / d1;   return n1 * t * t + 0.75f; }
        if (t < 2.5f / d1)   { t -= 2.25f / d1;  return n1 * t * t + 0.9375f; }
        t -= 2.625f / d1;    return n1 * t * t + 0.984375f;
    }
}
