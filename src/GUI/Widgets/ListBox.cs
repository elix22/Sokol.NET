using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Scrollable list widget with keyboard and mouse selection.
/// </summary>
public class ListBox : Widget
{
    private readonly List<string> _items = [];
    private int   _selectedIndex = -1;
    private float _scrollY;
    private float _lastClickTime;
    private int   _lastClickIndex = -1;
    public const float ItemHeight = 24f;

    public IReadOnlyList<string> Items => _items;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int v = (_items.Count == 0) ? -1 : Math.Clamp(value, -1, _items.Count - 1);
            if (v == _selectedIndex) return;
            _selectedIndex = v;
            ScrollToSelected();
            SelectionChanged?.Invoke(_selectedIndex);
        }
    }

    public string? SelectedItem => (_selectedIndex >= 0 && _selectedIndex < _items.Count)
        ? _items[_selectedIndex] : null;

    public bool MultiSelect { get; set; } = false;

    public Font?  Font     { get; set; }
    public float  FontSize { get; set; } = 0f;

    public event Action<int>? SelectionChanged;
    public event Action<int>? ItemDoubleClicked;

    // ─── Data ─────────────────────────────────────────────────────────────────
    public void SetItems(IEnumerable<string> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _selectedIndex = -1;
        _scrollY = 0f;
    }

    public void AddItem(string item) => _items.Add(item);
    public void Clear() { _items.Clear(); _selectedIndex = -1; _scrollY = 0f; }

    // ─── Layout ──────────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        return new Vector2(200, Math.Min(_items.Count, 8) * ItemHeight);
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        float w    = Bounds.Width, h = Bounds.Height;
        float sb   = theme.ScrollBarWidth;
        float totalH = _items.Count * ItemHeight;
        bool  needSB = totalH > h;
        float viewW  = needSB ? w - sb : w;
        float cr     = theme.InputCornerRadius;

        // Background
        renderer.FillRoundedRect(new Rect(0, 0, w, h), cr, theme.InputBackColor);
        renderer.StrokeRoundedRect(new Rect(0, 0, w, h), cr, 1.5f,
            IsFocused ? theme.AccentColor : theme.BorderColor);

        // Clipped item area
        renderer.Save();
        renderer.IntersectClip(new Rect(0, 0, viewW, h));
        renderer.Translate(0, -_scrollY);

        ApplyFont(renderer, theme);
        for (int i = 0; i < _items.Count; i++)
        {
            float itemY = i * ItemHeight;
            if (itemY + ItemHeight < _scrollY) continue;
            if (itemY > _scrollY + h)          break;

            var  rowR = new Rect(0, itemY, viewW, ItemHeight);
            bool sel  = i == _selectedIndex;
            bool hov  = IsHovered && HoveredIndex() == i;

            if (sel)
                renderer.FillRect(rowR, theme.SelectionColor);
            else if (hov)
                renderer.FillRect(rowR, theme.AccentColor.WithAlpha(0.12f));

            renderer.SetTextAlign(TextHAlign.Left);
            renderer.DrawText(8f, itemY + ItemHeight * 0.5f, _items[i],
                sel ? theme.AccentColor : theme.TextColor);
        }

        renderer.Restore();

        // Scrollbar
        if (needSB)
        {
            float maxScroll = totalH - h;
            float ratio     = h / totalH;
            float thumbH    = MathF.Max(h * ratio, 20f);
            float thumbY    = (maxScroll > 0 ? _scrollY / maxScroll : 0f) * (h - thumbH);
            var trackR = new Rect(w - sb, 0, sb, h);
            renderer.FillRect(trackR, theme.ScrollBarTrackColor);
            renderer.FillRoundedRect(
                new Rect(trackR.X + 2f, thumbY, sb - 4f, thumbH),
                (sb - 4f) * 0.5f, theme.ScrollBarThumbColor);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    private Vector2 _mouseLocal;

    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; return true; }
    public override bool OnMouseMove(MouseEvent e)  { _mouseLocal = e.Position; return true; }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;

        int idx = IndexFromY(e.Position.Y);
        if (idx < 0 || idx >= _items.Count) return true;

        // Double-click detection
        float now   = (float)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0);
        bool  dbl   = (idx == _lastClickIndex) && (now - _lastClickTime < 0.4f);
        _lastClickTime  = now;
        _lastClickIndex = idx;

        _selectedIndex = idx;
        SelectionChanged?.Invoke(_selectedIndex);
        if (dbl) ItemDoubleClicked?.Invoke(idx);
        return true;
    }

    public override bool OnMouseScroll(MouseEvent e)
    {
        float totalH = _items.Count * ItemHeight;
        float maxScroll = MathF.Max(0f, totalH - Bounds.Height);
        _scrollY = Math.Clamp(_scrollY - e.Scroll.Y * ItemHeight * 2f, 0f, maxScroll);
        return true;
    }

    public override bool OnKeyDown(KeyEvent e)
    {
        int count = _items.Count;
        if (count == 0) return false;
        int next = _selectedIndex;
        switch (e.KeyCode)
        {
            case 265:
                next = Math.Max(0, _selectedIndex - 1); break;
            case 264:
                next = Math.Min(count - 1, _selectedIndex + 1); break;
            case 268:
                next = 0; break;
            case 269:
                next = count - 1; break;
            default: return false;
        }
        if (next != _selectedIndex)
        { _selectedIndex = next; ScrollToSelected(); SelectionChanged?.Invoke(next); }
        return true;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private int IndexFromY(float localY)   => (int)((localY + _scrollY) / ItemHeight);
    private int HoveredIndex()             => (int)((_mouseLocal.Y + _scrollY) / ItemHeight);

    private void ScrollToSelected()
    {
        if (_selectedIndex < 0) return;
        float itemTop = _selectedIndex * ItemHeight;
        float itemBot = itemTop + ItemHeight;
        float viewH   = Bounds.Height > 0 ? Bounds.Height : 200f;
        float maxScr  = MathF.Max(0f, _items.Count * ItemHeight - viewH);
        if (itemTop < _scrollY)             _scrollY = itemTop;
        else if (itemBot > _scrollY + viewH) _scrollY = itemBot - viewH;
        _scrollY = Math.Clamp(_scrollY, 0f, maxScr);
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
