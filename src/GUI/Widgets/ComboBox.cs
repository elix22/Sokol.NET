using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Drop-down combo box.
/// </summary>
public class ComboBox : Widget
{
    private int                 _selectedIndex = -1;
    private bool                _open;
    private int                 _hoveredIndex  = -1;
    private readonly List<string> _items = [];

    public IReadOnlyList<string> Items => _items;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int clamped = (value < 0 || _items.Count == 0) ? -1
                        : Math.Clamp(value, 0, _items.Count - 1);
            if (clamped == _selectedIndex) return;
            _selectedIndex = clamped;
            SelectionChanged?.Invoke(_selectedIndex, SelectedItem);
        }
    }

    public string? SelectedItem => (_selectedIndex >= 0 && _selectedIndex < _items.Count)
        ? _items[_selectedIndex] : null;

    public void AddItem(string item) { _items.Add(item); }
    public void SetItems(IEnumerable<string> items) { _items.Clear(); _items.AddRange(items); _selectedIndex = -1; }

    public event Action<int, string?>? SelectionChanged;

    public Font?   Font     { get; set; }
    public float   FontSize { get; set; } = 0f;

    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        return new Vector2(180, ThemeManager.Current.InputHeight);
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme   = ThemeManager.Current;
        var bounds  = new Rect(0, 0, Bounds.Width, Bounds.Height);
        float cr    = theme.InputCornerRadius;
        float arrowW = bounds.Height;

        // Box — NanoGUI bevel button (ComboBox main control is button-like)
        var fillR = new Rect(1, 1, bounds.Width - 2, bounds.Height - 2);
        UIColor gradTop, gradBot;
        if (IsHovered) { gradTop = theme.ButtonHoverTop;   gradBot = theme.ButtonHoverBottom; }
        else           { gradTop = theme.ButtonGradientTop; gradBot = theme.ButtonGradientBottom; }
        var bgGrad = renderer.LinearGradient(
            new Vector2(0, 0), new Vector2(0, bounds.Height),
            gradTop, gradBot);
        renderer.FillRoundedRectWithPaint(fillR, MathF.Max(0f, cr - 1f), bgGrad);

        // Inner highlight
        renderer.StrokeRoundedRect(
            new Rect(0.5f, 1.5f, bounds.Width - 1f, bounds.Height - 2f), cr,
            1f, theme.BorderLight);
        // Outer dark border
        renderer.StrokeRoundedRect(
            new Rect(0.5f, 0.5f, bounds.Width - 1f, bounds.Height - 2f), cr,
            1f, theme.BorderDark);

        // Selected text
        ApplyFont(renderer, theme);
        bool rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
        var text = SelectedItem ?? string.Empty;
        renderer.Save();
        if (rtl)
        {
            renderer.SetTextAlign(TextHAlign.Right);
            renderer.IntersectClip(new Rect(arrowW + 4, 0, bounds.Width - arrowW - 8, bounds.Height));
            renderer.DrawText(bounds.Width - 4, bounds.Height * 0.5f, text, theme.TextColor);
        }
        else
        {
            renderer.SetTextAlign(TextHAlign.Left);
            renderer.IntersectClip(new Rect(4, 0, bounds.Width - arrowW - 8, bounds.Height));
            renderer.DrawText(4, bounds.Height * 0.5f, text, theme.TextColor);
        }
        renderer.Restore();

        // Arrow: RTL = left side, LTR = right side
        float ax = rtl ? arrowW * 0.5f : bounds.Width - arrowW * 0.5f;
        float ay = bounds.Height * 0.5f;
        float as2 = 4f;
        renderer.Translate(ax, ay);
        renderer.FillRect(new Rect(-as2, -as2 * 0.5f, as2 * 2, as2 * 2), UIColor.Transparent);
        renderer.DrawLine(-as2, -as2 * 0.4f, 0, as2 * 0.6f, 1.5f, theme.TextColor);
        renderer.DrawLine(0,    as2 * 0.6f,  as2, -as2 * 0.4f, 1.5f, theme.TextColor);
        renderer.Translate(-ax, -ay);

        // Dropdown panel — rendered by Screen as overlay to avoid parent clip issues.
        // DrawDropdown is called via DrawPopupOverlay when _open.
    }

    private void DrawDropdown(Renderer renderer, Theme theme, Rect bounds)
    {
        float itemH  = ThemeManager.Current.InputHeight;
        float ddH    = _items.Count * itemH;
        float ddY    = bounds.Height;
        var   ddRect = new Rect(0, ddY, bounds.Width, ddH);

        // Drop shadow under the popup
        renderer.DrawDropShadow(ddRect, theme.InputCornerRadius,
            new Vector2(0, 2), 8f, new UIColor(0f, 0f, 0f, 0.5f));

        renderer.FillRoundedRect(ddRect, theme.InputCornerRadius, theme.InputBackColor);
        renderer.StrokeRoundedRect(ddRect, theme.InputCornerRadius, 1f, theme.BorderDark);

        ApplyFont(renderer, theme);
        renderer.SetTextAlign(TextHAlign.Left);

        for (int i = 0; i < _items.Count; i++)
        {
            var rowR = new Rect(0, ddY + i * itemH, bounds.Width, itemH);
            if (i == _selectedIndex)
                renderer.FillRect(rowR, theme.SelectionColor);
            else if (i == _hoveredIndex)
                renderer.FillRect(rowR, theme.AccentColor.WithAlpha(0.12f));
            renderer.DrawText(rowR.X + 6, rowR.Y + rowR.Height * 0.5f, _items[i], theme.TextColor);
        }
    }

    // When the dropdown is open it renders below Bounds (via Screen overlay) — extend HitTest.
    public override bool HitTest(Vector2 localPoint)
    {
        if (_open)
        {
            float itemH  = ThemeManager.Current.InputHeight;
            float totalH = Bounds.Height + _items.Count * itemH;
            return localPoint.X >= 0 && localPoint.Y >= 0 &&
                   localPoint.X < Bounds.Width && localPoint.Y < totalH;
        }
        return base.HitTest(localPoint);
    }

    /// <summary>Draw the dropdown overlay (called by Screen on top of all widgets).</summary>
    public override void DrawPopupOverlay(Renderer renderer)
    {
        var theme  = ThemeManager.Current;
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        DrawDropdown(renderer, theme, bounds);
    }

    /// <summary>Called when a click outside dismisses the popup.</summary>
    public override void OnPopupDismiss()
    {
        _open = false;
        _hoveredIndex = -1;
        Sokol.SLog.Info($"ComboBox: dismissed", "Sokol.GUI");
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseDown(MouseEvent e)
    {
        var local = e.LocalPosition;
        float itemH = ThemeManager.Current.InputHeight;

        if (_open)
        {
            // Hit-test dropdown rows
            float ddY = Bounds.Height;
            if (local.Y >= ddY && local.Y < ddY + _items.Count * itemH)
            {
                int idx = (int)((local.Y - ddY) / itemH);
                Sokol.SLog.Info($"ComboBox: selected index {idx} ('{_items[idx]}')", "Sokol.GUI");
                SelectedIndex = idx;
            }
            _open = false;
            _hoveredIndex = -1;
            Screen.SetActivePopup(null);
            return true;
        }

        if (e.Button == MouseButton.Left && Enabled)
        {
            _open = true;
            _hoveredIndex = -1;
            Screen.SetActivePopup(this);
            Sokol.SLog.Info($"ComboBox: opened, registered as popup", "Sokol.GUI");
            return true;
        }
        return false;
    }

    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; _hoveredIndex = -1; return false; }

    public override bool OnMouseMove(MouseEvent e)
    {
        if (!_open) return false;
        var   local = e.LocalPosition;
        float itemH = ThemeManager.Current.InputHeight;
        float ddY   = Bounds.Height;
        if (local.Y >= ddY && local.Y < ddY + _items.Count * itemH)
            _hoveredIndex = (int)((local.Y - ddY) / itemH);
        else
            _hoveredIndex = -1;
        return true;
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
