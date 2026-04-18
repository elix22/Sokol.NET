using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Group of mutually exclusive radio options.
/// Each <see cref="RadioButton"/> registers itself with a <see cref="RadioGroup"/>.
/// </summary>
public sealed class RadioGroup
{
    private readonly List<RadioButton> _buttons = [];

    public RadioButton? Selected { get; private set; }
    public event Action<RadioButton?>? SelectionChanged;

    internal void Register(RadioButton btn)
    {
        if (!_buttons.Contains(btn)) _buttons.Add(btn);
    }

    internal void Select(RadioButton btn)
    {
        if (Selected == btn) return;
        if (Selected != null) Selected.SetCheckedDirect(false);
        Selected = btn;
        Selected.SetCheckedDirect(true);
        SelectionChanged?.Invoke(Selected);
    }
}

/// <summary>
/// Single radio-button belonging to a <see cref="RadioGroup"/>.
/// </summary>
public class RadioButton : Widget
{
    private bool        _checked;
    private RadioGroup? _group;

    public bool IsChecked
    {
        get => _checked;
        set
        {
            if (value && _group != null) _group.Select(this); // routes through group → deselects previous
            else _checked = value;
        }
    }

    // Called by RadioGroup.Select only — bypasses group routing to avoid recursion.
    internal void SetCheckedDirect(bool v) => _checked = v;

    public string   Label     { get; set; } = string.Empty;
    public UIColor? ForeColor { get; set; }
    public Font?    Font      { get; set; }
    public float    FontSize  { get; set; } = 0f;

    public RadioGroup? Group
    {
        get => _group;
        set { _group = value; _group?.Register(this); }
    }

    public RadioButton() { }
    public RadioButton(RadioGroup group, string label) { Label = label; Group = group; }

    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        var theme = ThemeManager.Current;
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
        float cy   = Bounds.Height * 0.5f;
        bool  rtl  = ResolvedFlowDirection == FlowDirection.RightToLeft;
        float cx   = rtl ? Bounds.Width - Padding.Right - size * 0.5f
                         : Padding.Left + size * 0.5f;

        // Circle gradient (top-light → bottom-dark = raised/sphere look)
        {
            UIColor gTop, gBot;
            if (IsChecked)
            {
                gTop = theme.AccentColor.Lighten(0.15f);
                gBot = theme.AccentColor.Darken(0.20f);
            }
            else
            {
                var baseCol = IsHovered ? theme.CheckBoxHoverColor : theme.CheckBoxColor;
                gTop = baseCol.Lighten(0.08f);
                gBot = baseCol.Darken(0.12f);
            }
            var grad = renderer.LinearGradient(
                new Vector2(cx, cy - size * 0.5f),
                new Vector2(cx, cy + size * 0.5f),
                gTop, gBot);
            renderer.FillCircleWithPaint(cx, cy, size * 0.5f, grad);
        }
        renderer.StrokeCircle(cx, cy, size * 0.5f, 1f, IsChecked ? theme.AccentColor.Darken(0.20f) : theme.BorderColor);

        if (IsChecked)
        {
            // Inner dot with gradient for a 3D sphere illusion
            var dotGrad = renderer.LinearGradient(
                new Vector2(cx, cy - size * 0.25f),
                new Vector2(cx, cy + size * 0.25f),
                theme.ButtonTextColor.WithAlpha(0.9f),
                theme.ButtonTextColor.WithAlpha(0.55f));
            renderer.FillCircleWithPaint(cx, cy, size * 0.25f, dotGrad);
        }

        if (!string.IsNullOrEmpty(Label))
        {
            ApplyFont(renderer, theme);
            if (rtl)
            {
                float lx = Bounds.Width - Padding.Right - size - theme.CheckBoxLabelSpacing;
                renderer.SetTextAlign(TextHAlign.Right);
                renderer.DrawText(lx, cy, Label, ForeColor ?? theme.TextColor);
            }
            else
            {
                float lx = Padding.Left + size + theme.CheckBoxLabelSpacing;
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
            if (_group != null) _group.Select(this);
            else { IsChecked = !IsChecked; RaiseClicked(); }
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
