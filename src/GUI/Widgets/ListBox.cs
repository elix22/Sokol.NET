using System;
using System.Collections.Generic;
using System.Linq;
using static Sokol.SApp;

namespace Sokol.GUI;

/// <summary>
/// Scrollable list of string items with single or multi-select support.
/// Inherits scroll state, scrollbar rendering, font helpers and common mouse
/// handling from <see cref="ScrollableList"/>.
/// </summary>
public class ListBox : ScrollableList
{
    private readonly List<string> _items       = [];
    private int                   _selectedIndex  = -1;
    private int                   _anchorIndex    = -1;
    private float                 _lastClickTime;
    private int                   _lastClickIndex = -1;
    private readonly HashSet<int> _selectedSet    = new();

    public IReadOnlyList<string> Items => _items;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int v = (_items.Count == 0) ? -1 : Math.Clamp(value, -1, _items.Count - 1);
            if (v == _selectedIndex) return;
            _selectedIndex = v;
            _selectedSet.Clear();
            if (v >= 0) _selectedSet.Add(v);
            ScrollToIndex(v);
            RaiseSelectionChanged(_selectedIndex);
        }
    }

    public string? SelectedItem => (_selectedIndex >= 0 && _selectedIndex < _items.Count)
        ? _items[_selectedIndex] : null;

    public bool MultiSelect { get; set; } = false;

    public event Action<int>? ItemDoubleClicked;

    // ─── Abstract overrides ───────────────────────────────────────────────────
    protected override int ItemCount => _items.Count;

    // ─── Data ─────────────────────────────────────────────────────────────────
    public void SetItems(IEnumerable<string> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _selectedIndex = -1;
        _selectedSet.Clear();
        _anchorIndex = -1;
        _scrollY = 0f;
    }

    public void AddItem(string item)
    {
        _items.Add(item);
    }

    public void PrependItem(string item)
    {
        _items.Insert(0, item);
        if (_selectedIndex >= 0) _selectedIndex++;
    }

    public void Clear()
    {
        _items.Clear();
        _selectedIndex = -1;
        _selectedSet.Clear();
        _anchorIndex = -1;
        _scrollY = 0f;
    }

    /// <summary>Scrolls to show the last item without changing selection.</summary>
    public void ScrollToBottom()
    {
        float totalH    = _items.Count * ItemHeight;
        float viewH     = MathF.Max(Bounds.Height, 1f);
        float maxScroll = MathF.Max(0f, totalH - viewH);
        _scrollY = maxScroll;
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    protected override void DrawItems(Renderer renderer, float viewW, float viewH)
    {
        var theme = ThemeManager.Current;
        ApplyFont(renderer, theme);

        for (int i = 0; i < _items.Count; i++)
        {
            float itemY = i * ItemHeight;
            if (itemY + ItemHeight <= _scrollY) continue;
            if (itemY >= _scrollY + viewH)      break;

            var  rowR = new Rect(0, itemY, viewW, ItemHeight);
            bool sel  = _selectedSet.Count > 0 ? _selectedSet.Contains(i) : i == _selectedIndex;
            bool hov  = IsHovered && HoveredIndex() == i;

            if (sel)      renderer.FillRect(rowR, theme.SelectionColor);
            else if (hov) renderer.FillRect(rowR, theme.AccentColor.WithAlpha(0.12f));

            renderer.SetTextAlign(TextHAlign.Left);
            renderer.DrawText(8f, itemY + ItemHeight * 0.5f, _items[i],
                sel ? theme.AccentColor : theme.TextColor);
        }
    }

    // ─── Item click ───────────────────────────────────────────────────────────
    protected override bool OnItemClick(MouseEvent e, int index)
    {
        if (index < 0 || index >= _items.Count) return true;

        float now   = (float)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0);
        bool  dbl   = (index == _lastClickIndex) && (now - _lastClickTime < 0.4f);
        _lastClickTime  = now;
        _lastClickIndex = index;

        bool ctrl  = (e.Modifiers & (KeyModifiers.Control | KeyModifiers.Super)) != 0;
        bool shift = (e.Modifiers & KeyModifiers.Shift) != 0;

        if (MultiSelect && shift && _anchorIndex >= 0)
        {
            int lo = Math.Min(_anchorIndex, index);
            int hi = Math.Max(_anchorIndex, index);
            if (!ctrl) _selectedSet.Clear();
            for (int i = lo; i <= hi; i++) _selectedSet.Add(i);
            _selectedIndex = index;
        }
        else if (MultiSelect && ctrl)
        {
            if (_selectedSet.Contains(index)) _selectedSet.Remove(index);
            else _selectedSet.Add(index);
            _selectedIndex = index;
            _anchorIndex   = index;
        }
        else
        {
            _selectedSet.Clear();
            _selectedIndex = index;
            _selectedSet.Add(index);
            _anchorIndex   = index;
        }

        RaiseSelectionChanged(_selectedIndex);
        if (dbl) ItemDoubleClicked?.Invoke(index);
        return true;
    }

    // ─── Keyboard ─────────────────────────────────────────────────────────────
    public override bool OnKeyDown(KeyEvent e)
    {
        int count = _items.Count;
        if (count == 0) return false;

        bool ctrl = (e.Modifiers & KeyModifiers.Control) != 0;
        bool cmd  = (e.Modifiers & KeyModifiers.Super)   != 0;

        // Ctrl/Cmd+C — copy selected items to clipboard
        if ((ctrl || cmd) && e.KeyCode == 67)
        {
            if (MultiSelect && _selectedSet.Count > 0)
            {
                var text = string.Join("\n", _selectedSet.OrderBy(i => i).Select(i => _items[i]));
                try { sapp_set_clipboard_string(text); } catch { }
            }
            else if (_selectedIndex >= 0 && _selectedIndex < count)
            {
                try { sapp_set_clipboard_string(_items[_selectedIndex]); } catch { }
            }
            return true;
        }

        int next = _selectedIndex < 0 ? 0 : _selectedIndex;
        switch (e.KeyCode)
        {
            case 265: next = Math.Max(0, next - 1);         break;  // Up
            case 264: next = Math.Min(count - 1, next + 1); break;  // Down
            case 268: next = 0;                              break;  // Home
            case 269: next = count - 1;                     break;  // End
            default:  return false;
        }
        if (next != _selectedIndex)
        {
            _selectedIndex = next;
            _selectedSet.Clear();
            _selectedSet.Add(next);
            _anchorIndex = next;
            ScrollToIndex(next);
            RaiseSelectionChanged(next);
        }
        return true;
    }
}
