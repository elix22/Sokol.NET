using System;

namespace Sokol.GUI;

/// <summary>
/// Clickable push button with hover/press visual states.
/// </summary>
public class Button : Widget
{
    public string   Text        { get; set; } = string.Empty;
    public UIColor? BackColor   { get; set; }
    public UIColor? HoverColor  { get; set; }
    public UIColor? PressColor  { get; set; }
    public UIColor? ForeColor   { get; set; }
    public UIColor? BorderColor { get; set; }
    public float    BorderWidth { get; set; } = 1f;
    public float    CornerRadius { get; set; } = 0f;  // 0 = theme default
    public Font?    Font        { get; set; }
    public float    FontSize    { get; set; } = 0f;

    public Button() { }
    public Button(string text) => Text = text;

    // ─── Sizing ──────────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;

        var theme = ThemeManager.Current;
        ApplyFont(renderer, theme);
        float tw  = renderer.MeasureText(Text);
        float pad = Padding.Horizontal > 0 ? Padding.Horizontal : theme.ButtonPaddingH * 2;
        float ph  = Padding.Vertical   > 0 ? Padding.Vertical   : theme.ButtonPaddingV * 2;
        return new Vector2(tw + pad, theme.ButtonHeight + ph);
    }

    // ─── Drawing ─────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        float cr   = CornerRadius > 0 ? CornerRadius : theme.ButtonCornerRadius;

        // ── Background gradient (top-light → bottom-dark = raised look) ──────
        UIColor gradTop, gradBot;
        if (!Enabled)
        {
            gradTop = gradBot = theme.ButtonDisabledColor;
        }
        else if (IsPressed)
        {
            // Pressed: inverted depth — darker top, lighter bottom
            var pressBase = PressColor ?? theme.ButtonPressedColor;
            gradTop = pressBase.Darken(0.08f);
            gradBot = pressBase.Lighten(0.05f);
        }
        else if (IsHovered)
        {
            var hoverBase = HoverColor ?? theme.ButtonHoverColor;
            gradTop = hoverBase;
            gradBot = hoverBase.Darken(0.15f);
        }
        else
        {
            var baseCol = BackColor ?? theme.ButtonColor;
            gradTop = baseCol;
            gradBot = baseCol.Darken(0.18f);
        }
        var bgGrad = renderer.LinearGradient(
            new Vector2(0, 0), new Vector2(0, bounds.Height), gradTop, gradBot);
        renderer.FillRoundedRectWithPaint(bounds, cr, bgGrad);

        // ── Border ────────────────────────────────────────────────────────────
        if (BorderWidth > 0)
        {
            var borderCol = BorderColor ?? (Enabled
                ? theme.BorderColor.WithAlpha(0.8f)
                : theme.BorderColor.WithAlpha(0.4f));
            renderer.StrokeRoundedRect(bounds, cr, BorderWidth, borderCol);
        }

        // ── Inner top highlight (glassy sheen when not pressed) ───────────────
        if (Enabled && !IsPressed)
        {
            var hlGrad = renderer.LinearGradient(
                new Vector2(0, 1f), new Vector2(0, bounds.Height * 0.5f),
                UIColor.White.WithAlpha(0.14f), UIColor.Transparent);
            var inner = bounds.Deflate(new Thickness(1));
            renderer.FillRoundedRectWithPaint(inner, MathF.Max(0f, cr - 1f), hlGrad);
        }

        // ── Label ─────────────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(Text))
        {
            float tx = bounds.Width * 0.5f;
            float ty = bounds.Height * 0.5f + (IsPressed ? 0.5f : 0f);
            var   fg = ForeColor ?? (Enabled ? theme.ButtonTextColor : theme.TextDisabledColor);
            ApplyFont(renderer, theme);
            renderer.SetTextAlign(TextHAlign.Center);
            // Subtle text shadow for legibility on gradient background
            if (Enabled)
                renderer.DrawText(tx, ty + 1f, Text, UIColor.Black.WithAlpha(0.28f));
            renderer.DrawText(tx, ty, Text, fg);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; IsPressed = false; return true; }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button == MouseButton.Left && Enabled)
        {
            IsPressed = true;
            return true;
        }
        return false;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        if (e.Button == MouseButton.Left && IsPressed)
        {
            IsPressed = false;
            if (IsHovered && Enabled) RaiseClicked();
            return true;
        }
        return false;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
