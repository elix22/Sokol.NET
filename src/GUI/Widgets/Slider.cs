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

            renderer.FillRoundedRect(trackR, track * 0.5f, theme.SliderTrackColor);
            float fillW = trackR.Width * t;
            if (fillW > 0)
                renderer.FillRoundedRect(new Rect(trackR.X, trackR.Y, fillW, track), track * 0.5f, theme.AccentColor);

            float tx = thumb + trackR.Width * t;
            renderer.FillCircle(tx, cy, thumb * 0.5f,
                IsPressed ? theme.AccentColor : IsHovered ? theme.SliderThumbHoverColor : theme.SliderThumbColor);
            renderer.StrokeCircle(tx, cy, thumb * 0.5f, 1.5f, theme.AccentColor);
        }
        else
        {
            float cx = w * 0.5f;
            var trackR = new Rect(cx - track * 0.5f, thumb, track, h - thumb * 2f);

            renderer.FillRoundedRect(trackR, track * 0.5f, theme.SliderTrackColor);
            float fillH = trackR.Height * (1f - t);
            if (fillH < trackR.Height)
                renderer.FillRoundedRect(new Rect(trackR.X, trackR.Y + fillH, track, trackR.Height - fillH),
                    track * 0.5f, theme.AccentColor);

            float ty = thumb + trackR.Height * (1f - t);
            renderer.FillCircle(cx, ty, thumb * 0.5f,
                IsPressed ? theme.AccentColor : IsHovered ? theme.SliderThumbHoverColor : theme.SliderThumbColor);
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
