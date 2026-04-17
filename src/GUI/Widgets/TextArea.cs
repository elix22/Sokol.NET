using System;

namespace Sokol.GUI;

/// <summary>
/// Read-only scrollable multi-line text area.
/// </summary>
public class TextArea : Widget
{
    public string   Text      { get; set; } = string.Empty;
    public UIColor? ForeColor { get; set; }
    public UIColor? BackColor { get; set; }
    public Font?    Font      { get; set; }
    public float    FontSize  { get; set; } = 0f;

    private float _scrollY;
    private bool  _sbDragging;
    private float _sbDragStartY;
    private float _sbDragStartScroll;

    private void ApplyFont(Renderer renderer)
    {
        var theme = ThemeManager.Current;
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var   theme = ThemeManager.Current;
        float w     = Bounds.Width, h = Bounds.Height;
        float cr    = theme.InputCornerRadius;
        var   bgCol = BackColor ?? theme.InputBackColor;

        // NanoGUI-style sunken container
        renderer.FillRoundedRect(new Rect(0, 0, w, h), cr, bgCol);
        var insetPaint = renderer.BoxGradient(
            new Rect(1, 2, w - 2, h - 2), cr, 4f,
            new UIColor(1f, 1f, 1f, 0.06f),
            new UIColor(0f, 0f, 0f, 0.15f));
        renderer.FillRoundedRectWithPaint(new Rect(0, 0, w, h), cr, insetPaint);
        renderer.StrokeRoundedRect(
            new Rect(0.5f, 0.5f, w - 1f, h - 1f),
            MathF.Max(cr - 0.5f, 0f), 1f,
            IsFocused ? theme.AccentColor : UIColor.Black.WithAlpha(0.188f));

        ApplyFont(renderer);
        renderer.SetTextAlign(TextHAlign.Left, TextVAlign.Top);

        float sbW    = theme.ScrollBarWidth;
        var   inner  = new Rect(0, 0, w, h).Deflate(Padding);
        if (inner.Width <= 0 || inner.Height <= 0) return;

        // Measure at text-column width (reserve scrollbar width so the measure
        // accounts for the narrower usable column when the bar is shown).
        float textW   = MathF.Max(10f, inner.Width - sbW);
        var (_, measH) = renderer.MeasureTextBounds(inner.X, inner.Y, textW, Text);

        bool  showSb    = measH > inner.Height;
        float maxScroll = MathF.Max(0f, measH - inner.Height);
        _scrollY        = Math.Clamp(_scrollY, 0f, maxScroll);

        // Clip content to inner rect and draw with vertical scroll
        renderer.Save();
        renderer.IntersectClip(new Rect(0, 0, w, h));
        renderer.DrawTextBox(inner.X, inner.Y - _scrollY, textW, Text,
            ForeColor ?? theme.TextColor);
        renderer.Restore();

        // Vertical scrollbar
        if (showSb)
        {
            float sbX    = inner.X + textW;
            float thumbH = MathF.Max(16f, inner.Height * inner.Height / measH);
            float thumbY = inner.Y +
                           (maxScroll > 0 ? _scrollY / maxScroll : 0f) * (inner.Height - thumbH);
            renderer.FillRect(new Rect(sbX, inner.Y, sbW, inner.Height),
                theme.ScrollBarTrackColor);
            renderer.FillRoundedRect(
                new Rect(sbX + 2, thumbY + 2, sbW - 4, thumbH - 4),
                (sbW - 4) * 0.5f, theme.ScrollBarThumbColor);
        }
    }

    public override bool OnMouseScroll(MouseEvent e)
    {
        float spd = ThemeManager.Current.ScrollSpeed;
        _scrollY  = MathF.Max(0f, _scrollY - e.Scroll.Y * spd);
        return true;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        var   theme = ThemeManager.Current;
        float sbW   = theme.ScrollBarWidth;
        var   inner = new Rect(0, 0, Bounds.Width, Bounds.Height).Deflate(Padding);
        float textW = MathF.Max(10f, inner.Width - sbW);
        float sbX   = inner.X + textW;
        if (e.LocalPosition.X >= sbX)
        {
            _sbDragging        = true;
            _sbDragStartY      = e.Position.Y;
            _sbDragStartScroll = _scrollY;
            return true;
        }
        return false;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        if (!_sbDragging) return false;
        var renderer = Screen.Instance?.Renderer;
        if (renderer == null) return true;
        ApplyFont(renderer);
        var   inner    = new Rect(0, 0, Bounds.Width, Bounds.Height).Deflate(Padding);
        float textW    = MathF.Max(10f, inner.Width - ThemeManager.Current.ScrollBarWidth);
        var (_, measH) = renderer.MeasureTextBounds(inner.X, inner.Y, textW, Text);
        float maxScroll  = MathF.Max(0f, measH - inner.Height);
        float thumbH     = MathF.Max(16f, inner.Height * inner.Height / measH);
        float trackRange = inner.Height - thumbH;
        if (trackRange > 0)
        {
            float delta = e.Position.Y - _sbDragStartY;
            _scrollY = Math.Clamp(_sbDragStartScroll + delta * maxScroll / trackRange, 0f, maxScroll);
        }
        return true;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        if (_sbDragging) { _sbDragging = false; return true; }
        return false;
    }
}
