using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// A virtualized scrollable list that renders only visible items.
/// Items are drawn using a user-supplied <see cref="ItemTemplate"/> factory; widget
/// instances are cached in a reuse pool to avoid per-frame allocation.
/// </summary>
public class VirtualList : Widget
{
    // ─── Public API ──────────────────────────────────────────────────────────
    private IReadOnlyList<object>? _itemsSource;
    private Func<object, Widget>?  _itemTemplate;
    private int   _selectedIndex = -1;
    private float _scrollY;

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

    public float ItemHeight    { get; set; }  = 28f;
    public int   SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int count = _itemsSource?.Count ?? 0;
            int next  = count == 0 ? -1 : Math.Clamp(value, 0, count - 1);
            if (next == _selectedIndex) return;
            _selectedIndex = next;
            ScrollToIndex(_selectedIndex);
            SelectionChanged?.Invoke(_selectedIndex);
        }
    }

    public event Action<int>? SelectionChanged;

    // ─── Pool ─────────────────────────────────────────────────────────────────
    // Maps item-index → cached Widget. Cleared when ItemsSource / ItemTemplate changes.
    private readonly Dictionary<int, Widget> _pool = new();

    private Widget GetOrCreate(int index)
    {
        var item = _itemsSource![index];
        if (_pool.TryGetValue(index, out var w)) return w;
        if (_itemTemplate != null)
        {
            w = _itemTemplate(item);
        }
        else
        {
            // Fallback: plain label
            w = new Label { Text = item?.ToString() ?? string.Empty };
        }
        _pool[index] = w;
        return w;
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme = ThemeManager.Current;
        float w   = Bounds.Width;
        float h   = Bounds.Height;
        int   n   = _itemsSource?.Count ?? 0;

        // Background
        renderer.FillRect(new Rect(0, 0, w, h), theme.InputBackColor);

        if (n == 0) return;

        int  first = (int)(_scrollY / ItemHeight);
        int  last  = Math.Min(n - 1, (int)((_scrollY + h) / ItemHeight) + 1);

        float sbW = theme.ScrollBarWidth;
        float contentW = w - (TotalH(n) > h ? sbW : 0f);

        renderer.Save();
        renderer.IntersectClip(new Rect(0, 0, contentW, h));

        for (int i = first; i <= last; i++)
        {
            float rowY  = i * ItemHeight - _scrollY;
            var   rowR  = new Rect(0, rowY, contentW, ItemHeight);

            // Selection / hover highlight
            if (i == _selectedIndex)
                renderer.FillRect(rowR, theme.SelectionColor);

            // Draw item widget
            var widget = GetOrCreate(i);
            widget.Bounds = new Rect(0, 0, contentW, ItemHeight);
            renderer.Save();
            renderer.Translate(0, rowY);
            widget.Draw(renderer);
            renderer.Restore();
        }

        renderer.Restore();

        // ── Scrollbar ──────────────────────────────────────────────────────
        float totalH    = TotalH(n);
        float maxScroll = MathF.Max(0f, totalH - h);
        if (maxScroll > 0f)
        {
            float thumbH  = MathF.Max(20f, h * (h / totalH));
            float thumbY  = (maxScroll > 0 ? _scrollY / maxScroll : 0f) * (h - thumbH);
            var   trackR  = new Rect(w - sbW, 0, sbW, h);
            renderer.FillRect(trackR, theme.ScrollBarTrackColor);
            renderer.FillRoundedRect(
                new Rect(w - sbW + 2, thumbY + 2, sbW - 4, thumbH - 4),
                (sbW - 4) * 0.5f,
                theme.ScrollBarThumbColor);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        int idx = (int)((e.Position.Y + _scrollY) / ItemHeight);
        int n   = _itemsSource?.Count ?? 0;
        if (idx >= 0 && idx < n)
            SelectedIndex = idx;
        return true;
    }

    public override bool OnMouseScroll(MouseEvent e)
    {
        int   n        = _itemsSource?.Count ?? 0;
        float maxScroll = MathF.Max(0f, TotalH(n) - Bounds.Height);
        _scrollY = Math.Clamp(_scrollY - e.Scroll.Y * ItemHeight * 2f, 0f, maxScroll);
        return true;
    }

    public override bool OnKeyDown(KeyEvent e)
    {
        int count = _itemsSource?.Count ?? 0;
        if (count == 0) return false;

        const int KEY_UP   = 265;
        const int KEY_DOWN = 264;
        const int KEY_HOME = 268;
        const int KEY_END  = 269;

        int next = _selectedIndex < 0 ? 0 : _selectedIndex;
        switch (e.KeyCode)
        {
            case KEY_UP:   next = Math.Max(0, next - 1); break;
            case KEY_DOWN: next = Math.Min(count - 1, next + 1); break;
            case KEY_HOME: next = 0; break;
            case KEY_END:  next = count - 1; break;
            default:       return false;
        }
        SelectedIndex = next;
        return true;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private float TotalH(int count) => count * ItemHeight;

    private void ScrollToIndex(int idx)
    {
        if (idx < 0) return;
        float itemTop    = idx * ItemHeight;
        float itemBottom = itemTop + ItemHeight;
        float viewH      = Bounds.Height > 0 ? Bounds.Height : 200f;
        if (itemTop < _scrollY)
            _scrollY = itemTop;
        else if (itemBottom > _scrollY + viewH)
            _scrollY = itemBottom - viewH;
    }
}
