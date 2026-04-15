using System;

namespace Sokol.GUI;

/// <summary>
/// Two-state toggle with a checkmark glyph.
/// </summary>
public class CheckBox : Widget
{
    private bool _checked;

    public bool IsChecked
    {
        get => _checked;
        set { if (_checked != value) { _checked = value; CheckedChanged?.Invoke(_checked); } }
    }

    public string   Label       { get; set; } = string.Empty;
    public UIColor? ForeColor   { get; set; }
    public Font?    Font        { get; set; }
    public float    FontSize    { get; set; } = 0f;

    public event Action<bool>? CheckedChanged;

    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        var theme  = ThemeManager.Current;
        float size = theme.CheckBoxSize;
        ApplyFont(renderer, theme);
        float tw = renderer.MeasureText(Label);
        return new Vector2(size + theme.CheckBoxLabelSpacing + tw + Padding.Horizontal,
                           MathF.Max(size, theme.FontSize) + Padding.Vertical);
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        float size = theme.CheckBoxSize;
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        float cy   = bounds.Height * 0.5f;

        // Box background
        var boxR = new Rect(Padding.Left, cy - size * 0.5f, size, size);
        var bg   = IsChecked ? theme.AccentColor
                 : IsHovered ? theme.CheckBoxHoverColor
                 : theme.CheckBoxColor;
        renderer.FillRoundedRect(boxR, theme.CheckBoxCornerRadius, bg);
        renderer.StrokeRoundedRect(boxR, theme.CheckBoxCornerRadius, 1f,
            IsChecked ? theme.AccentColor : theme.BorderColor);

        // Checkmark
        if (IsChecked)
        {
            float m  = size * 0.22f;
            float cx = boxR.X + size * 0.5f, bcy = boxR.Y + size * 0.5f;
            renderer.DrawLine(boxR.X + m,        bcy,
                              cx - m * 0.4f,     boxR.Bottom - m,
                              2f, theme.ButtonTextColor);
            renderer.DrawLine(cx - m * 0.4f,     boxR.Bottom - m,
                              boxR.Right - m,    boxR.Y + m + m * 0.3f,
                              2f, theme.ButtonTextColor);
        }

        // Label
        if (!string.IsNullOrEmpty(Label))
        {
            float lx = boxR.Right + theme.CheckBoxLabelSpacing;
            ApplyFont(renderer, theme);
            renderer.SetTextAlign(TextHAlign.Left);
            renderer.DrawText(lx, cy, Label, ForeColor ?? theme.TextColor);
        }
    }

    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; return true; }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button == MouseButton.Left && Enabled)
        {
            IsChecked = !IsChecked;
            RaiseClicked();
            return true;
        }
        return false;
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
