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
            float cy = h * 0.5f;
            var trackR = new Rect(thumb, cy - track * 0.5f, w - thumb * 2f, track);

            // Track: inset gradient (darker top edge = sunken look)
            var trackGrad = renderer.LinearGradient(
                new Vector2(trackR.X, trackR.Y), new Vector2(trackR.X, trackR.Bottom),
                theme.SliderTrackColor.Darken(0.18f), theme.SliderTrackColor.Lighten(0.08f));
            renderer.FillRoundedRectWithPaint(trackR, track * 0.5f, trackGrad);

            // Fill: accent gradient
            float fillW = trackR.Width * t;
            if (fillW > 0)
            {
                var fillR = new Rect(trackR.X, trackR.Y, fillW, track);
                var fillGrad = renderer.LinearGradient(
                    new Vector2(fillR.X, fillR.Y), new Vector2(fillR.X, fillR.Bottom),
                    theme.AccentColor.Lighten(0.15f), theme.AccentColor.Darken(0.12f));
                renderer.FillRoundedRectWithPaint(fillR, track * 0.5f, fillGrad);
            }

            // Thumb: gradient sphere + accent ring
            float tx = thumb + trackR.Width * t;
            var thumbCol = IsPressed ? theme.AccentColor : IsHovered ? theme.SliderThumbHoverColor : theme.SliderThumbColor;
            var thumbGrad = renderer.LinearGradient(
                new Vector2(tx, cy - thumb * 0.5f), new Vector2(tx, cy + thumb * 0.5f),
                thumbCol.Lighten(0.22f), thumbCol.Darken(0.15f));
            renderer.FillCircleWithPaint(tx, cy, thumb * 0.5f, thumbGrad);
            renderer.StrokeCircle(tx, cy, thumb * 0.5f, 1.5f, theme.AccentColor);
        }
        else
        {
            float cx = w * 0.5f;
            var trackR = new Rect(cx - track * 0.5f, thumb, track, h - thumb * 2f);

            // Track: inset gradient (darker left edge = sunken look)
            var trackGrad = renderer.LinearGradient(
                new Vector2(trackR.X, trackR.Y), new Vector2(trackR.Right, trackR.Y),
                theme.SliderTrackColor.Darken(0.18f), theme.SliderTrackColor.Lighten(0.08f));
            renderer.FillRoundedRectWithPaint(trackR, track * 0.5f, trackGrad);

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

            // Thumb: gradient sphere + accent ring
            float ty = thumb + trackR.Height * (1f - t);
            var thumbCol = IsPressed ? theme.AccentColor : IsHovered ? theme.SliderThumbHoverColor : theme.SliderThumbColor;
            var thumbGrad = renderer.LinearGradient(
                new Vector2(cx, ty - thumb * 0.5f), new Vector2(cx, ty + thumb * 0.5f),
                thumbCol.Lighten(0.22f), thumbCol.Darken(0.15f));
            renderer.FillCircleWithPaint(cx, ty, thumb * 0.5f, thumbGrad);
            renderer.StrokeCircle(cx, ty, thumb * 0.5f, 1.5f, theme.AccentColor);
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
