using System;
using System.Globalization;
using static Sokol.NanoVG;

namespace Sokol.GUI;

/// <summary>A normalized RGBA color value used throughout the Sokol.GUI framework.</summary>
public readonly record struct UIColor(float R, float G, float B, float A)
{
    // Named colors
    public static readonly UIColor Transparent = new(0f, 0f, 0f, 0f);
    public static readonly UIColor Black       = new(0f, 0f, 0f, 1f);
    public static readonly UIColor White       = new(1f, 1f, 1f, 1f);
    public static readonly UIColor Red         = new(1f, 0f, 0f, 1f);
    public static readonly UIColor Green       = new(0f, 1f, 0f, 1f);
    public static readonly UIColor Blue        = new(0f, 0f, 1f, 1f);
    public static readonly UIColor Gray        = new(0.5f, 0.5f, 0.5f, 1f);
    public static readonly UIColor LightGray   = new(0.75f, 0.75f, 0.75f, 1f);
    public static readonly UIColor DarkGray    = new(0.25f, 0.25f, 0.25f, 1f);
    public static readonly UIColor Yellow      = new(1f, 1f, 0f, 1f);
    public static readonly UIColor Cyan        = new(0f, 1f, 1f, 1f);
    public static readonly UIColor Magenta     = new(1f, 0f, 1f, 1f);

    /// <summary>Convenience constructor with alpha defaulting to 1.</summary>
    public UIColor(float r, float g, float b) : this(r, g, b, 1f) { }

    /// <summary>Convert to NanoVG NVGcolor for rendering calls.</summary>
    public NVGcolor ToNVGcolor() => nvgRGBAf(R, G, B, A);

    /// <summary>Parse a hex string: #RGB, #RGBA, #RRGGBB, #RRGGBBAA.</summary>
    public static UIColor FromHex(string hex)
    {
        ReadOnlySpan<char> s = hex.AsSpan().TrimStart('#');
        float r, g, b, a = 1f;
        switch (s.Length)
        {
            case 3:
                r = ParseNibble(s[0]) / 15f;
                g = ParseNibble(s[1]) / 15f;
                b = ParseNibble(s[2]) / 15f;
                break;
            case 4:
                r = ParseNibble(s[0]) / 15f;
                g = ParseNibble(s[1]) / 15f;
                b = ParseNibble(s[2]) / 15f;
                a = ParseNibble(s[3]) / 15f;
                break;
            case 6:
                r = ParseByte(s[..2]) / 255f;
                g = ParseByte(s[2..4]) / 255f;
                b = ParseByte(s[4..6]) / 255f;
                break;
            case 8:
                r = ParseByte(s[..2]) / 255f;
                g = ParseByte(s[2..4]) / 255f;
                b = ParseByte(s[4..6]) / 255f;
                a = ParseByte(s[6..8]) / 255f;
                break;
            default:
                throw new FormatException($"Invalid hex color: {hex}");
        }
        return new UIColor(r, g, b, a);

        static float ParseNibble(char c) => byte.Parse(stackalloc char[1] { c }, NumberStyles.HexNumber);
        static float ParseByte(ReadOnlySpan<char> span) => byte.Parse(span, NumberStyles.HexNumber);
    }

    /// <summary>Linearly interpolate between two colors.</summary>
    public static UIColor Lerp(UIColor a, UIColor b, float t) => new(
        a.R + (b.R - a.R) * t,
        a.G + (b.G - a.G) * t,
        a.B + (b.B - a.B) * t,
        a.A + (b.A - a.A) * t);

    /// <summary>Return a copy with modified alpha.</summary>
    public UIColor WithAlpha(float alpha) => this with { A = alpha };

    /// <summary>Multiply alpha by a factor (darken transparency).</summary>
    public UIColor MultiplyAlpha(float factor) => this with { A = MathF.Max(0f, MathF.Min(1f, A * factor)) };

    /// <summary>Blend toward white by <paramref name="amount"/> (0=unchanged, 1=white).</summary>
    public UIColor Lighten(float amount) => Lerp(this, White with { A = A }, amount);

    /// <summary>Blend toward black by <paramref name="amount"/> (0=unchanged, 1=black).</summary>
    public UIColor Darken(float amount)  => Lerp(this, Black with { A = A }, amount);

    public override string ToString() => $"#{(int)(R*255):X2}{(int)(G*255):X2}{(int)(B*255):X2}{(int)(A*255):X2}";
}
