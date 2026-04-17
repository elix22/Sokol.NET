using System;

namespace Sokol.GUI;

/// <summary>
/// An HSV color picker widget with:
/// - Saturation/Value square
/// - Hue strip
/// - Alpha strip
/// - Color preview swatches (original / new)
/// </summary>
public class ColorPicker : Widget
{
    // ─── State ────────────────────────────────────────────────────────────────
    private float _h = 0f;  // Hue        [0..1]
    private float _s = 1f;  // Saturation [0..1]
    private float _v = 1f;  // Value      [0..1]
    private float _a = 1f;  // Alpha      [0..1]

    private UIColor _originalColor;

    // ─── Drag tracking ────────────────────────────────────────────────────────
    private enum DragZone { None, SV, Hue, Alpha }
    private DragZone _drag = DragZone.None;

    // ─── Layout constants ─────────────────────────────────────────────────────
    private const float PadH      = 8f;  // horizontal/vertical padding
    private const float HueH        = 14f;
    private const float AlphaH      = 14f;
    private const float PreviewH    = 24f;
    private const float HexH        = 20f;
    private const float SliderGap   = 6f;

    public Font?  Font     { get; set; }
    public float  FontSize { get; set; } = 0f;

    public bool ShowAlpha { get; set; } = true;

    // ─── Color access ─────────────────────────────────────────────────────────
    public UIColor Color
    {
        get => HsvToRgb(_h, _s, _v, _a);
        set
        {
            RgbToHsv(value.R, value.G, value.B, out _h, out _s, out _v);
            _a = value.A;
        }
    }

    public event Action<UIColor>? ColorChanged;

    public ColorPicker() { _originalColor = UIColor.Red; }
    public ColorPicker(UIColor initial) { Color = initial; _originalColor = initial; }

    // ─── PreferredSize ────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        // Use actual Bounds.Width when laid out (the SV square is width-dependent).
        float w        = Bounds.Width > 0 ? Bounds.Width : 220f;
        float svSize   = w - PadH * 2;
        float total    = PadH + svSize + SliderGap + HueH + SliderGap;
        if (ShowAlpha)  total += AlphaH + SliderGap;
        total += PreviewH + SliderGap + HexH + PadH;
        return new Vector2(w, total);
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var   theme  = ThemeManager.Current;
        float w      = Bounds.Width;
        float svSize = w - PadH * 2;
        float y      = PadH;

        // ── SV square ──────────────────────────────────────────────────────
        var svRect = new Rect(PadH, y, svSize, svSize);
        DrawSVSquare(renderer, svRect);
        y += svSize + SliderGap;

        // ── Hue strip ──────────────────────────────────────────────────────
        var hueRect = new Rect(PadH, y, svSize, HueH);
        DrawHueStrip(renderer, hueRect);
        y += HueH + SliderGap;

        // ── Alpha strip ────────────────────────────────────────────────────
        if (ShowAlpha)
        {
            var alphaRect = new Rect(PadH, y, svSize, AlphaH);
            DrawAlphaStrip(renderer, alphaRect);
            y += AlphaH + SliderGap;
        }

        // ── Preview swatches ───────────────────────────────────────────────
        float swatchW = svSize * 0.5f;
        var origR = new Rect(PadH,            y, swatchW, PreviewH);
        var newR  = new Rect(PadH + swatchW,  y, swatchW, PreviewH);
        renderer.FillRoundedRect(origR, new CornerRadius(4f, 0f, 0f, 4f), _originalColor);
        renderer.StrokeRoundedRect(origR, new CornerRadius(4f, 0f, 0f, 4f), 1f, theme.BorderColor);
        renderer.FillRoundedRect(newR,  new CornerRadius(0f, 4f, 4f, 0f), Color);
        renderer.StrokeRoundedRect(newR,  new CornerRadius(0f, 4f, 4f, 0f), 1f, theme.BorderColor);
        y += PreviewH + SliderGap;

        // ── Hex label ──────────────────────────────────────────────────────
        ApplyFont(renderer, theme);
        renderer.SetTextAlign(TextHAlign.Center);
        string hex = Color.ToString();
        renderer.DrawText(w * 0.5f, y + HexH * 0.5f, hex, theme.TextMutedColor);
    }

    // ─── Draw helpers ─────────────────────────────────────────────────────────

    private void DrawSVSquare(Renderer renderer, Rect r)
    {
        var theme = ThemeManager.Current;

        // 1. Base fill: pure hue color
        var hueColor = HsvToRgb(_h, 1f, 1f, 1f);
        renderer.FillRect(r, hueColor);

        // 2. Saturation gradient: white (left) → transparent (right)
        var satPaint = renderer.LinearGradient(
            new Vector2(r.X, r.Y), new Vector2(r.X + r.Width, r.Y),
            UIColor.White, UIColor.Transparent);
        renderer.FillRoundedRectWithPaint(r, 0f, satPaint);

        // 3. Value gradient: transparent (top) → black (bottom)
        var valPaint = renderer.LinearGradient(
            new Vector2(r.X, r.Y), new Vector2(r.X, r.Y + r.Height),
            UIColor.Transparent, UIColor.Black);
        renderer.FillRoundedRectWithPaint(r, 0f, valPaint);

        // Border
        renderer.StrokeRoundedRect(r, 3f, 1f, theme.BorderColor);

        // Indicator circle at (S × w, (1-V) × h)
        float ix = r.X + _s * r.Width;
        float iy = r.Y + (1f - _v) * r.Height;
        renderer.StrokeCircle(ix, iy, 5f, 1.5f, UIColor.White);
        renderer.StrokeCircle(ix, iy, 5f, 1f, UIColor.Black.WithAlpha(0.5f));
    }

    private void DrawHueStrip(Renderer renderer, Rect r)
    {
        var theme = ThemeManager.Current;

        // Draw 6 gradient segments for the rainbow
        float segW = r.Width / 6f;
        (UIColor from, UIColor to)[] segments =
        [
            (new UIColor(1f,0f,0f), new UIColor(1f,1f,0f)),   // red→yellow
            (new UIColor(1f,1f,0f), new UIColor(0f,1f,0f)),   // yellow→green
            (new UIColor(0f,1f,0f), new UIColor(0f,1f,1f)),   // green→cyan
            (new UIColor(0f,1f,1f), new UIColor(0f,0f,1f)),   // cyan→blue
            (new UIColor(0f,0f,1f), new UIColor(1f,0f,1f)),   // blue→magenta
            (new UIColor(1f,0f,1f), new UIColor(1f,0f,0f)),   // magenta→red
        ];

        renderer.Save();
        renderer.IntersectClip(r);
        for (int i = 0; i < 6; i++)
        {
            float x = r.X + i * segW;
            var   sr  = new Rect(x, r.Y, segW + 1f, r.Height);  // +1 to avoid seam
            var   paint = renderer.LinearGradient(
                new Vector2(x, r.Y), new Vector2(x + segW, r.Y),
                segments[i].from, segments[i].to);
            renderer.FillRoundedRectWithPaint(sr, 0f, paint);
        }
        renderer.Restore();

        renderer.StrokeRoundedRect(r, 3f, 1f, theme.BorderColor);

        // Hue indicator
        float hx = r.X + _h * r.Width;
        DrawStripIndicator(renderer, hx, r.Y, r.Height, HsvToRgb(_h, 1f, 1f, 1f));
    }

    private void DrawAlphaStrip(Renderer renderer, Rect r)
    {
        var theme = ThemeManager.Current;
        var solidColor = HsvToRgb(_h, _s, _v, 1f);

        // Checkerboard-ish: grey bg then gradient
        renderer.FillRect(r, new UIColor(0.7f, 0.7f, 0.7f));
        var paint = renderer.LinearGradient(
            new Vector2(r.X, r.Y), new Vector2(r.X + r.Width, r.Y),
            solidColor.WithAlpha(0f), solidColor);
        renderer.FillRoundedRectWithPaint(r, 0f, paint);
        renderer.StrokeRoundedRect(r, 3f, 1f, theme.BorderColor);

        float ax = r.X + _a * r.Width;
        DrawStripIndicator(renderer, ax, r.Y, r.Height, Color);
    }

    private static void DrawStripIndicator(Renderer renderer, float cx, float stripY, float stripH, UIColor innerColor)
    {
        float ir = stripH * 0.5f;
        renderer.FillCircle(cx, stripY + stripH * 0.5f, ir - 1f, innerColor);
        renderer.StrokeCircle(cx, stripY + stripH * 0.5f, ir - 1f, 1.5f, UIColor.White);
        renderer.StrokeCircle(cx, stripY + stripH * 0.5f, ir - 1f, 1f, UIColor.Black.WithAlpha(0.5f));
    }

    // ─── Input ───────────────────────────────────────────────────────────────

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        _drag = HitZone(e.LocalPosition);
        if (_drag != DragZone.None) UpdateFromPos(e.LocalPosition);
        return _drag != DragZone.None;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        if (_drag == DragZone.None) return false;
        UpdateFromPos(e.LocalPosition);
        return true;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        bool wasDragging = _drag != DragZone.None;
        _drag = DragZone.None;
        return wasDragging;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private DragZone HitZone(Vector2 pos)
    {
        float svSize = Bounds.Width - PadH * 2;
        float y      = PadH;
        var svRect = new Rect(PadH, y, svSize, svSize);
        y += svSize + SliderGap;
        var hueRect = new Rect(PadH, y, svSize, HueH);
        y += HueH + SliderGap;
        var alphaRect = ShowAlpha ? new Rect(PadH, y, svSize, AlphaH) : default;

        if (svRect.Contains(pos))    return DragZone.SV;
        if (hueRect.Contains(pos))   return DragZone.Hue;
        if (ShowAlpha && alphaRect.Contains(pos)) return DragZone.Alpha;
        return DragZone.None;
    }

    private void UpdateFromPos(Vector2 pos)
    {
        float svSize = Bounds.Width - PadH * 2;
        float y      = PadH;
        var svRect = new Rect(PadH, y, svSize, svSize);
        y += svSize + SliderGap;
        var hueRect = new Rect(PadH, y, svSize, HueH);
        y += HueH + SliderGap;
        var alphaRect = ShowAlpha ? new Rect(PadH, y, svSize, AlphaH) : default;

        switch (_drag)
        {
            case DragZone.SV:
                _s = Math.Clamp((pos.X - svRect.X) / svRect.Width,  0f, 1f);
                _v = Math.Clamp(1f - (pos.Y - svRect.Y) / svRect.Height, 0f, 1f);
                break;
            case DragZone.Hue:
                _h = Math.Clamp((pos.X - hueRect.X) / hueRect.Width, 0f, 1f);
                break;
            case DragZone.Alpha:
                _a = Math.Clamp((pos.X - alphaRect.X) / alphaRect.Width, 0f, 1f);
                break;
        }
        ColorChanged?.Invoke(Color);
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0f ? FontSize : theme.SmallFontSize);
    }

    // ─── HSV ↔ RGB ────────────────────────────────────────────────────────────

    public static UIColor HsvToRgb(float h, float s, float v, float a)
    {
        if (s <= 0f) return new UIColor(v, v, v, a);
        h = h * 6f;
        int   i  = (int)h % 6;
        float f  = h - MathF.Floor(h);
        float p  = v * (1f - s);
        float q  = v * (1f - s * f);
        float t  = v * (1f - s * (1f - f));
        return i switch
        {
            0 => new UIColor(v, t, p, a),
            1 => new UIColor(q, v, p, a),
            2 => new UIColor(p, v, t, a),
            3 => new UIColor(p, q, v, a),
            4 => new UIColor(t, p, v, a),
            _ => new UIColor(v, p, q, a),
        };
    }

    public static void RgbToHsv(float r, float g, float b, out float h, out float s, out float v)
    {
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;
        v = max;
        s = max < 1e-6f ? 0f : delta / max;
        if (delta < 1e-6f) { h = 0f; return; }
        if (max == r) h = (g - b) / delta + (g < b ? 6f : 0f);
        else if (max == g) h = (b - r) / delta + 2f;
        else h = (r - g) / delta + 4f;
        h /= 6f;
    }
}
