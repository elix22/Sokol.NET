using System;

namespace Sokol.GUI;

public enum SliderOrientation { Horizontal, Vertical }

/// <summary>
/// Range slider with draggable thumb.
/// </summary>
public class Slider : Widget
{
    private float _value = 0f;
    private bool  _dragging;

    public float Min { get; set; } = 0f;
    public float Max { get; set; } = 1f;
    public float Step { get; set; } = 0f;  // 0 = continuous
    public SliderOrientation Orientation { get; set; } = SliderOrientation.Horizontal;

    public float Value
    {
        get => _value;
        set
        {
            float clamped = MathF.Min(MathF.Max(value, Min), Max);
            if (clamped == _value) return;
            _value = clamped;
            ValueChanged?.Invoke(_value);
        }
    }

    public event Action<float>? ValueChanged;

    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        var theme = ThemeManager.Current;
        return Orientation == SliderOrientation.Horizontal
            ? new Vector2(200, theme.SliderThickness)
            : new Vector2(theme.SliderThickness, 200);
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        float w    = Bounds.Width, h = Bounds.Height;
        float track = theme.SliderTrackThickness;
        float thumb = theme.SliderThumbSize;
        float t     = (Max > Min) ? (_value - Min) / (Max - Min) : 0f;

        if (Orientation == SliderOrientation.Horizontal)
        {
            bool rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
            float tDraw = rtl ? (1f - t) : t;  // mirror progress fraction visually

            float cy = h * 0.5f;
            var trackR = new Rect(thumb, cy - track * 0.5f, w - thumb * 2f, track);

            // Track: NanoGUI-style BoxGradient (sunken groove)
            var trackInset = renderer.BoxGradient(
                new Rect(trackR.X + 1, trackR.Y + 1, trackR.Width - 2, trackR.Height - 2),
                track * 0.5f, 3f,
                UIColor.Black.WithAlpha(0.32f),    // dark center
                UIColor.Black.WithAlpha(0.05f));   // bright edges
            renderer.FillRoundedRectWithPaint(trackR, track * 0.5f, trackInset);
            renderer.StrokeRoundedRect(trackR, track * 0.5f, 1f, UIColor.Black.WithAlpha(0.25f));

            // Fill: accent gradient (always drawn from the "start" side)
            float fillW = trackR.Width * tDraw;
            if (fillW > 0)
            {
                float fillX = rtl ? (trackR.Right - fillW) : trackR.X;
                var fillR = new Rect(fillX, trackR.Y, fillW, track);
                var fillGrad = renderer.LinearGradient(
                    new Vector2(fillR.X, fillR.Y), new Vector2(fillR.X, fillR.Bottom),
                    theme.AccentColor.Lighten(0.15f), theme.AccentColor.Darken(0.12f));
                renderer.FillRoundedRectWithPaint(fillR, track * 0.5f, fillGrad);
            }

            // Thumb position
            float tx = rtl ? (thumb + trackR.Width * (1f - t)) : (thumb + trackR.Width * t);
            var thumbCol = IsPressed ? theme.AccentColor : IsHovered ? theme.SliderThumbHoverColor : theme.SliderThumbColor;
            var thumbGrad = renderer.LinearGradient(
                new Vector2(tx, cy - thumb * 0.5f), new Vector2(tx, cy + thumb * 0.5f),
                thumbCol.Lighten(0.30f), thumbCol.Darken(0.20f));
            // Drop shadow under thumb
            renderer.DrawDropShadow(
                new Rect(tx - thumb * 0.5f, cy - thumb * 0.5f, thumb, thumb),
                thumb * 0.5f, new Vector2(0, 1), 3f, new UIColor(0f, 0f, 0f, 0.4f));
            renderer.FillCircleWithPaint(tx, cy, thumb * 0.5f, thumbGrad);
            // Dark outer border
            renderer.StrokeCircle(tx, cy, thumb * 0.5f, 1f, theme.BorderDark);
            // Light highlight at top
            renderer.StrokeCircle(tx, cy - 0.5f, thumb * 0.5f - 1f, 0.5f, theme.BorderLight.WithAlpha(0.5f));
        }
        else
        {
            float cx = w * 0.5f;
            var trackR = new Rect(cx - track * 0.5f, thumb, track, h - thumb * 2f);

            // Track: NanoGUI-style BoxGradient (sunken groove)
            var trackInsetV = renderer.BoxGradient(
                new Rect(trackR.X + 1, trackR.Y + 1, trackR.Width - 2, trackR.Height - 2),
                track * 0.5f, 3f,
                UIColor.Black.WithAlpha(0.32f),
                UIColor.Black.WithAlpha(0.05f));
            renderer.FillRoundedRectWithPaint(trackR, track * 0.5f, trackInsetV);
            renderer.StrokeRoundedRect(trackR, track * 0.5f, 1f, UIColor.Black.WithAlpha(0.25f));

            // Fill: accent gradient
            float fillH = trackR.Height * (1f - t);
            if (fillH < trackR.Height)
            {
                var fillR = new Rect(trackR.X, trackR.Y + fillH, track, trackR.Height - fillH);
                var fillGrad = renderer.LinearGradient(
                    new Vector2(fillR.X, fillR.Y), new Vector2(fillR.Right, fillR.Y),
                    theme.AccentColor.Lighten(0.15f), theme.AccentColor.Darken(0.12f));
                renderer.FillRoundedRectWithPaint(fillR, track * 0.5f, fillGrad);
            }

            // Thumb: NanoGUI-style gradient sphere + shadow + bevel
            float ty = thumb + trackR.Height * (1f - t);
            var thumbCol = IsPressed ? theme.AccentColor : IsHovered ? theme.SliderThumbHoverColor : theme.SliderThumbColor;
            var thumbGrad = renderer.LinearGradient(
                new Vector2(cx, ty - thumb * 0.5f), new Vector2(cx, ty + thumb * 0.5f),
                thumbCol.Lighten(0.30f), thumbCol.Darken(0.20f));
            renderer.DrawDropShadow(
                new Rect(cx - thumb * 0.5f, ty - thumb * 0.5f, thumb, thumb),
                thumb * 0.5f, new Vector2(0, 1), 3f, new UIColor(0f, 0f, 0f, 0.4f));
            renderer.FillCircleWithPaint(cx, ty, thumb * 0.5f, thumbGrad);
            renderer.StrokeCircle(cx, ty, thumb * 0.5f, 1f, theme.BorderDark);
            renderer.StrokeCircle(cx, ty - 0.5f, thumb * 0.5f - 1f, 0.5f, theme.BorderLight.WithAlpha(0.5f));
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; return true; }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button == MouseButton.Left && Enabled) { _dragging = true; IsPressed = true; SetFromMouse(e.Position); return true; }
        return false;
    }
    public override bool OnMouseMove(MouseEvent e)
    {
        if (_dragging) { SetFromMouse(e.Position); return true; }
        return false;
    }
    public override bool OnMouseUp(MouseEvent e)
    {
        if (_dragging) { _dragging = false; IsPressed = false; return true; }
        return false;
    }

    private void SetFromMouse(Vector2 screenPos)
    {
        var local = ToLocal(screenPos);
        var theme = ThemeManager.Current;
        float thumb = theme.SliderThumbSize;
        float t;
        if (Orientation == SliderOrientation.Horizontal)
        {
            float range = Bounds.Width - thumb * 2f;
            t = range > 0 ? (local.X - thumb) / range : 0f;
            if (ResolvedFlowDirection == FlowDirection.RightToLeft)
                t = 1f - t;
        }
        else
        {
            float range = Bounds.Height - thumb * 2f;
            t = range > 0 ? 1f - (local.Y - thumb) / range : 0f;
        }
        float raw = Min + MathF.Max(0f, MathF.Min(1f, t)) * (Max - Min);
        Value = Step > 0 ? MathF.Round(raw / Step) * Step : raw;
    }
}
