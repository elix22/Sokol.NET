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
        float cx   = Padding.Left + size * 0.5f;

        var bg = IsChecked ? theme.AccentColor
               : IsHovered ? theme.CheckBoxHoverColor
               : theme.CheckBoxColor;

        renderer.FillCircle(cx, cy, size * 0.5f, bg);
        renderer.StrokeCircle(cx, cy, size * 0.5f, 1f, IsChecked ? theme.AccentColor : theme.BorderColor);

        if (IsChecked)
            renderer.FillCircle(cx, cy, size * 0.25f, theme.ButtonTextColor);

        if (!string.IsNullOrEmpty(Label))
        {
            float lx = Padding.Left + size + theme.CheckBoxLabelSpacing;
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
