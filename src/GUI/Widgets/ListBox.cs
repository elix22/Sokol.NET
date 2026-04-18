using System;
using System.Collections.Generic;
using System.Linq;
using static Sokol.SApp;

namespace Sokol.GUI;

/// <summary>
/// Scrollable list of string or widget items with single or multi-select support.
/// Inherits scroll state, scrollbar rendering, font helpers and common mouse
/// handling from <see cref="ScrollableList"/>.
/// Items can be plain text strings or arbitrary <see cref="Widget"/> instances.
/// </summary>
public class ListBox : ScrollableList
{
    private readonly List<string>  _items       = [];
    private List<Widget>?          _widgets;
    private int                    _selectedIndex  = -1;
    private int                    _anchorIndex    = -1;
    private float                  _lastClickTime;
    private int                    _lastClickIndex = -1;
    private readonly HashSet<int>  _selectedSet    = new();

    public IReadOnlyList<string> Items => _items;

    /// <summary>Widget items set via <see cref="SetWidgetItems"/>. Null when using string items.</summary>
    public IReadOnlyList<Widget>? WidgetItems => _widgets;

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
    protected override int ItemCount => _widgets != null ? _widgets.Count : _items.Count;

    // ─── Data ─────────────────────────────────────────────────────────────────
    public void SetItems(IEnumerable<string> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _widgets = null;
        _selectedIndex = -1;
        _selectedSet.Clear();
        _anchorIndex = -1;
        _scrollY = 0f;
    }

    /// <summary>
    /// Set widget items. Each widget becomes one row in the list (Avalonia-style templated items).
    /// Replaces any previous string items.
    /// </summary>
    public void SetWidgetItems(IEnumerable<Widget> widgets)
    {
        _widgets = [..widgets];
        _items.Clear();
        _selectedIndex = -1;
        _selectedSet.Clear();
        _anchorIndex = -1;
        _scrollY = 0f;
    }

    /// <summary>Add a widget item to the list.</summary>
    public void AddWidgetItem(Widget widget)
    {
        _widgets ??= [];
        _widgets.Add(widget);
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
        _widgets = null;
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
        int count = ItemCount;

        for (int i = 0; i < count; i++)
        {
            float itemY = i * ItemHeight;
            if (itemY + ItemHeight <= _scrollY) continue;
            if (itemY >= _scrollY + viewH)      break;

            var  rowR = new Rect(0, itemY, viewW, ItemHeight);
            bool sel  = _selectedSet.Count > 0 ? _selectedSet.Contains(i) : i == _selectedIndex;
            bool hov  = IsHovered && HoveredIndex() == i;

            if (sel)
            {
                var selGrad = renderer.LinearGradient(
                    new Vector2(rowR.X, rowR.Y), new Vector2(rowR.X, rowR.Bottom),
                    theme.AccentColor.WithAlpha(0.38f), theme.AccentColor.WithAlpha(0.20f));
                renderer.FillRoundedRectWithPaint(rowR, 2f, selGrad);
                renderer.FillRect(new Rect(rowR.X, rowR.Y + 3f, 3f, rowR.Height - 6f), theme.AccentColor);
            }
            else if (hov) renderer.FillRect(rowR, theme.AccentColor.WithAlpha(0.10f));

            // Widget item or text item
            if (_widgets != null && i < _widgets.Count)
            {
                float contentX = sel ? 12f : 8f;
                float contentW = viewW - contentX - 2f;
                var widget = _widgets[i];
                widget.Bounds = new Rect(contentX, itemY, contentW, ItemHeight);
                renderer.Save();
                renderer.Translate(contentX, itemY);
                widget.Draw(renderer);
                renderer.Restore();
            }
            else if (i < _items.Count)
            {
                renderer.SetTextAlign(TextHAlign.Left);
                float textX = sel ? 12f : 8f;
                if (sel)
                    renderer.DrawText(textX, itemY + ItemHeight * 0.5f + 1f, _items[i], UIColor.Black.WithAlpha(0.28f));
                renderer.DrawText(textX, itemY + ItemHeight * 0.5f, _items[i],
                    sel ? theme.AccentColor.Lighten(0.15f) : theme.TextColor);
            }
        }

        // Drag-reorder insertion indicator
        if (_dropLineIndex >= 0)
        {
            var accent = ThemeManager.Current.AccentColor;
            float lineY = _dropLineIndex * ItemHeight;
            float dotR  = 3.5f;
            renderer.FillCircle(dotR, lineY, dotR, accent);
            renderer.DrawLine(dotR * 2f, lineY, viewW, lineY, 2f, accent);
        }
    }

    // ─── Item click ───────────────────────────────────────────────────────────
    protected override bool OnItemClick(MouseEvent e, int index)
    {
        int count = ItemCount;
        if (index < 0 || index >= count) return true;

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
        int count = ItemCount;
        if (count == 0) return false;

        bool ctrl = (e.Modifiers & KeyModifiers.Control) != 0;
        bool cmd  = (e.Modifiers & KeyModifiers.Super)   != 0;

        // Ctrl/Cmd+C — copy selected items to clipboard
        if ((ctrl || cmd) && e.KeyCode == 67)
        {
            if (MultiSelect && _selectedSet.Count > 0 && _widgets == null)
            {
                var text = string.Join("\n", _selectedSet.OrderBy(i => i).Where(i => i < _items.Count).Select(i => _items[i]));
                try { sapp_set_clipboard_string(text); } catch { }
            }
            else if (_selectedIndex >= 0 && _selectedIndex < _items.Count && _widgets == null)
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

    // ─── Drag & drop (reorder) ───────────────────────────────────────────────

    /// <summary>When true, items may be reordered by dragging.</summary>
    public bool AllowReorder
    {
        get => _allowReorder;
        set
        {
            _allowReorder = value;
            IsDragSource  = value;
            IsDropTarget  = value;
        }
    }
    private bool _allowReorder;
    private int  _dropLineIndex = -1;  // insertion-point indicator during drag-over (-1 = none)

    /// <summary>Wire format used for in-list reorder drags.</summary>
    public const string ReorderFormat = "sokol.listbox/reorder";

    public override DragDropData? OnDragBegin(Vector2 localPos)
    {
        if (!_allowReorder) return null;
        int idx = IndexFromY(localPos.Y);
        if (idx < 0 || idx >= _items.Count) return null;
        return new DragDropData
        {
            Format          = ReorderFormat,
            Payload         = (this, idx),
            Source          = this,
            DragLabel       = _items[idx],
            AllowedEffects  = DragDropEffect.Move,
        };
    }

    public override void OnDragOver(DragDropEventArgs e)
    {
        if (!_allowReorder) return;
        if (e.Data.Format != ReorderFormat) return;
        if (e.Data.Payload is (ListBox src, int _) && src == this)
        {
            _dropLineIndex = Math.Clamp(IndexFromY(e.LocalPosition.Y), 0, _items.Count);
            e.Effect = DragDropEffect.Move;
        }
    }

    public override void OnDragLeave()
    {
        _dropLineIndex = -1;
    }

    public override void OnDrop(DragDropEventArgs e)
    {
        if (!_allowReorder || e.Data.Format != ReorderFormat) return;
        if (e.Data.Payload is not (ListBox src, int srcIdx)) return;
        if (src != this) return;
        int dstIdx = Math.Clamp(IndexFromY(e.LocalPosition.Y), 0, _items.Count);
        if (dstIdx == srcIdx) { e.Handled = true; e.Effect = DragDropEffect.Move; return; }
        var item = _items[srcIdx];
        _items.RemoveAt(srcIdx);
        if (dstIdx > srcIdx) dstIdx--;
        _items.Insert(dstIdx, item);
        SelectedIndex = dstIdx;
        _dropLineIndex = -1;
        e.Handled = true;
        e.Effect  = DragDropEffect.Move;
    }
}
