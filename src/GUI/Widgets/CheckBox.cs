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
        bool  rtl  = ResolvedFlowDirection == FlowDirection.RightToLeft;

        // Box position: LTR = left edge, RTL = right edge
        float boxLeft = rtl ? (Bounds.Width - Padding.Right - size) : Padding.Left;
        var boxR = new Rect(boxLeft, cy - size * 0.5f, size, size);
        var boxInner = new Rect(boxR.X + 1.5f, boxR.Y + 1.5f, size - 2f, size - 2f);

        UIColor chkInner = IsPressed
            ? UIColor.Black.WithAlpha(0.392f)    // Color(0,100) — deeper when pushed
            : UIColor.Black.WithAlpha(0.125f);   // Color(0,32)
        UIColor chkOuter = new UIColor(0f, 0f, 0f, 0.706f); // Color(0,0,0,180)

        var chkPaint = renderer.BoxGradient(boxInner, 3f, 3f, chkInner, chkOuter);
        renderer.FillRoundedRectWithPaint(
            new Rect(boxR.X + 1f, boxR.Y + 1f, size - 2f, size - 2f),
            theme.CheckBoxCornerRadius, chkPaint);

        // Visible outer border — drawn for both checked and unchecked states.
        renderer.StrokeRoundedRect(boxR, theme.CheckBoxCornerRadius, 1f,
            IsChecked ? theme.AccentColor.Darken(0.25f) : theme.Border);

        // If checked, draw a filled accent background on top
        if (IsChecked)
        {
            var accentGrad = renderer.LinearGradient(
                new Vector2(boxR.X, boxR.Y), new Vector2(boxR.X, boxR.Bottom),
                theme.AccentColor.Lighten(0.15f), theme.AccentColor.Darken(0.20f));
            renderer.FillRoundedRectWithPaint(boxR, theme.CheckBoxCornerRadius, accentGrad);
            // Outer dark border on checked box
            renderer.StrokeRoundedRect(boxR, theme.CheckBoxCornerRadius, 1f, theme.AccentColor.Darken(0.35f));
        }

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

        // Label — LTR: to the right of the box; RTL: to the left of the box
        if (!string.IsNullOrEmpty(Label))
        {
            ApplyFont(renderer, theme);
            if (rtl)
            {
                float lx = boxLeft - theme.CheckBoxLabelSpacing;
                renderer.SetTextAlign(TextHAlign.Right);
                renderer.DrawText(lx, cy, Label, ForeColor ?? theme.TextColor);
            }
            else
            {
                float lx = boxR.Right + theme.CheckBoxLabelSpacing;
                renderer.SetTextAlign(TextHAlign.Left);
                renderer.DrawText(lx, cy, Label, ForeColor ?? theme.TextColor);
            }
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
