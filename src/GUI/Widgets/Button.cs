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

        // Background
        UIColor bg = IsPressed ? (PressColor ?? theme.ButtonPressedColor)
                   : IsHovered ? (HoverColor  ?? theme.ButtonHoverColor)
                   : (BackColor ?? (Enabled ? theme.ButtonColor : theme.ButtonDisabledColor));

        renderer.FillRoundedRect(bounds, cr, bg);

        // Border
        if (BorderWidth > 0)
            renderer.StrokeRoundedRect(bounds, cr, BorderWidth, BorderColor ?? theme.BorderColor);

        // Label
        if (!string.IsNullOrEmpty(Text))
        {
            var fg = ForeColor ?? (Enabled ? theme.ButtonTextColor : theme.TextDisabledColor);
            ApplyFont(renderer, theme);
            renderer.SetTextAlign(TextHAlign.Center);
            renderer.DrawText(bounds.X + bounds.Width * 0.5f, bounds.Y + bounds.Height * 0.5f, Text, fg);
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
