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
    public TextAlign Align      { get; set; } = TextAlign.Center;

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

        // ── Background gradient (NanoGUI-style: fill inset rect + two border strokes) ──
        UIColor gradTop, gradBot;
        if (!Enabled)
        {
            gradTop = theme.ButtonDisabledColor.Lighten(0.06f);
            gradBot = theme.ButtonDisabledColor.Darken(0.06f);
        }
        else if (IsPressed)
        {
            gradTop = theme.ButtonPressedTop;
            gradBot = theme.ButtonPressedBottom;
        }
        else if (IsHovered)
        {
            gradTop = theme.ButtonHoverTop;
            gradBot = theme.ButtonHoverBottom;
        }
        else
        {
            gradTop = BackColor ?? theme.ButtonGradientTop;
            gradBot = theme.ButtonGradientBottom;
        }

        // Fill: slightly inset (like NanoGUI: pos+1, size-2)
        var fillR = new Rect(1, 1, bounds.Width - 2, bounds.Height - 2);
        var bgGrad = renderer.LinearGradient(
            new Vector2(0, 0), new Vector2(0, bounds.Height), gradTop, gradBot);
        renderer.FillRoundedRectWithPaint(fillR, MathF.Max(0f, cr - 1f), bgGrad);

        // ── NanoGUI double-stroke bevel ───────────────────────────────────────
        if (Enabled)
        {
            // Inner highlight (border_light): bright edge, shifted down when not pushed
            float iy = IsPressed ? 0.5f : 1.5f;
            float ih = bounds.Height - 1f - (IsPressed ? 0f : 1f);
            renderer.StrokeRoundedRect(
                new Rect(0.5f, iy, bounds.Width - 1f, ih), cr,
                1f, theme.BorderLight);

            // Outer dark border (border_dark): overall outline
            renderer.StrokeRoundedRect(
                new Rect(0.5f, 0.5f, bounds.Width - 1f, bounds.Height - 2f), cr,
                1f, theme.BorderDark);
        }
        else
        {
            // Muted bevel for disabled state — still gives depth, just subdued
            renderer.StrokeRoundedRect(
                new Rect(0.5f, 1.5f, bounds.Width - 1f, bounds.Height - 2f), cr,
                1f, theme.BorderLight.WithAlpha(0.3f));
            renderer.StrokeRoundedRect(
                new Rect(0.5f, 0.5f, bounds.Width - 1f, bounds.Height - 2f), cr,
                1f, theme.BorderDark.WithAlpha(0.4f));
        }

        // ── Label ─────────────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(Text))
        {
            // In RTL, visually mirror Left/Right so leading edge follows flow direction.
            bool rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
            TextAlign effectiveAlign = rtl
                ? (Align == TextAlign.Left ? TextAlign.Right
                   : Align == TextAlign.Right ? TextAlign.Left
                   : Align)
                : Align;
            var hAlign = effectiveAlign switch
            {
                TextAlign.Left   => TextHAlign.Left,
                TextAlign.Right  => TextHAlign.Right,
                _                => TextHAlign.Center,
            };
            float tx = effectiveAlign switch
            {
                TextAlign.Left  => Padding.Left + 4f,
                TextAlign.Right => bounds.Width - Padding.Right - 4f,
                _               => bounds.Width * 0.5f,
            };
            float ty = bounds.Height * 0.5f - 1f + (IsPressed ? 1f : 0f);
            var   fg = ForeColor ?? (Enabled ? theme.ButtonTextColor : theme.TextDisabledColor);
            ApplyFont(renderer, theme);
            renderer.SetTextAlign(hAlign);
            // Text shadow (NanoGUI: draws text with m_text_color_shadow offset 1px)
            if (Enabled)
                renderer.DrawText(tx, ty + 1f, Text, theme.TextShadow);
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
