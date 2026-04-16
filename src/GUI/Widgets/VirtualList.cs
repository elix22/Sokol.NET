using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// A virtualized scrollable list that renders only visible items.
/// Items are drawn using a user-supplied <see cref="ItemTemplate"/> factory; widget
/// instances are cached in a reuse pool to avoid per-frame allocation.
/// Inherits scroll state, scrollbar rendering and common mouse handling from
/// <see cref="ScrollableList"/>.
/// </summary>
public class VirtualList : ScrollableList
{
    // ─── Data source ─────────────────────────────────────────────────────────
    private IReadOnlyList<object>? _itemsSource;
    private Func<object, Widget>?  _itemTemplate;
    private int                    _selectedIndex = -1;

    /// <summary>The data source. Setting this clears selection and scroll position.</summary>
    public IReadOnlyList<object>? ItemsSource
    {
        get => _itemsSource;
        set { _itemsSource = value; _selectedIndex = -1; _scrollY = 0f; _pool.Clear(); }
    }

    /// <summary>
    /// Factory that creates (or reconfigures) a Widget for a given data item.
    /// Called whenever a new data item enters the visible window.
    /// </summary>
    public Func<object, Widget>? ItemTemplate
    {
        get => _itemTemplate;
        set { _itemTemplate = value; _pool.Clear(); }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int count = _itemsSource?.Count ?? 0;
            int next  = count == 0 ? -1 : Math.Clamp(value, 0, count - 1);
            if (next == _selectedIndex) return;
            _selectedIndex = next;
            ScrollToIndex(_selectedIndex);
            RaiseSelectionChanged(_selectedIndex);
        }
    }

    public VirtualList()
    {
        ItemHeight = 28f;   // override base default of 24f
    }

    // ─── Pool ─────────────────────────────────────────────────────────────────
    private readonly Dictionary<int, Widget> _pool = new();

    private Widget GetOrCreate(int index)
    {
        var item = _itemsSource![index];
        if (_pool.TryGetValue(index, out var w)) return w;
        w = _itemTemplate != null
            ? _itemTemplate(item)
            : new Label { Text = item?.ToString() ?? string.Empty };
        _pool[index] = w;
        return w;
    }

    // ─── Abstract overrides ──────────────────────────────────────────────────
    protected override int ItemCount => _itemsSource?.Count ?? 0;

    protected override void DrawItems(Renderer renderer, float viewW, float viewH)
    {
        int n = ItemCount;
        if (n == 0) return;

        var theme = ThemeManager.Current;
        int first = (int)(_scrollY / ItemHeight);
        int last  = Math.Min(n - 1, (int)((_scrollY + viewH) / ItemHeight) + 1);

        for (int i = first; i <= last; i++)
        {
            float rowY = i * ItemHeight;    // list-space y (base already translated by -_scrollY)
            var   rowR = new Rect(0, rowY, viewW, ItemHeight);

            if (i == _selectedIndex)
                renderer.FillRect(rowR, theme.SelectionColor);

            var widget = GetOrCreate(i);
            widget.Bounds = new Rect(0, 0, viewW, ItemHeight);
            widget.PerformLayout(renderer);
            renderer.Save();
            renderer.Translate(0, rowY);
            widget.Draw(renderer);
            renderer.Restore();
        }
    }

    protected override bool OnItemClick(MouseEvent e, int index)
    {
        if (index >= 0 && index < ItemCount)
            SelectedIndex = index;
        return true;
    }

    // ─── Keyboard ─────────────────────────────────────────────────────────────
    public override bool OnKeyDown(KeyEvent e)
    {
        int count = _itemsSource?.Count ?? 0;
        if (count == 0) return false;

        int next = _selectedIndex < 0 ? 0 : _selectedIndex;
        switch (e.KeyCode)
        {
            case 265: next = Math.Max(0, next - 1);         break;  // Up
            case 264: next = Math.Min(count - 1, next + 1); break;  // Down
            case 268: next = 0;                              break;  // Home
            case 269: next = count - 1;                     break;  // End
            default:  return false;
        }
        SelectedIndex = next;
        return true;
    }
}
