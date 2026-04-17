using System;

namespace Sokol.GUI;

/// <summary>
/// Abstract base class for all scrollable-list widgets (e.g. <see cref="ListBox"/>,
/// <see cref="VirtualList"/>).  Provides shared scroll state, scrollbar rendering and
/// dragging, font helpers, hover tracking, and common mouse input handling.
/// Subclasses implement <see cref="ItemCount"/>, <see cref="DrawItems"/>, and
/// <see cref="OnItemClick"/> for their specific data model and rendering strategy.
/// </summary>
public abstract class ScrollableList : Widget
{
    // ─── Scroll state (accessible to subclasses) ─────────────────────────────
    protected float _scrollY;
    private   bool  _sbDragging;
    private   float _sbDragStartY;
    private   float _sbDragStartScroll;
    private   bool  _sbHovered;

    // ─── Configuration ────────────────────────────────────────────────────────
    public float  ItemHeight { get; set; } = 24f;
    public Font?  Font       { get; set; }
    public float  FontSize   { get; set; } = 0f;

    // ─── Mouse tracking ───────────────────────────────────────────────────────
    private Vector2 _mouseLocal;
    protected Vector2 MouseLocal => _mouseLocal;

    // ─── Abstract contract ────────────────────────────────────────────────────
    /// <summary>Total number of items in the data source.</summary>
    protected abstract int ItemCount { get; }

    /// <summary>
    /// Draw all items into the already-clipped, scroll-translated rendering context.
    /// Called with <c>renderer.IntersectClip</c> and <c>renderer.Translate(0,−scrollY)</c>
    /// already active.  Use <see cref="_scrollY"/> and <paramref name="viewH"/> to
    /// compute the visible range.
    /// </summary>
    protected abstract void DrawItems(Renderer renderer, float viewW, float viewH);

    /// <summary>Called when a non-scrollbar left-click lands on the item at
    /// <paramref name="index"/>. Return true if the event was consumed.</summary>
    protected abstract bool OnItemClick(MouseEvent e, int index);

    // ─── Selection event ─────────────────────────────────────────────────────
    public event Action<int>? SelectionChanged;
    protected void RaiseSelectionChanged(int idx) => SelectionChanged?.Invoke(idx);

    // ─── Layout ──────────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        return new Vector2(200, Math.Min(ItemCount, 8) * MathF.Max(ItemHeight, 1f));
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible || ItemHeight <= 0f) return;

        var   theme  = ThemeManager.Current;
        float w      = Bounds.Width;
        float h      = Bounds.Height;
        float sb     = theme.ScrollBarWidth;
        float totalH = ItemCount * ItemHeight;
        bool  needSB = totalH > h;
        float viewW  = needSB ? w - sb : w;
        float cr     = theme.InputCornerRadius;

        // NanoGUI-style sunken container
        var listBg = theme.InputBackColor;
        renderer.FillRoundedRect(new Rect(0, 0, w, h), cr, listBg);

        var listInset = renderer.BoxGradient(
            new Rect(1, 2, w - 2, h - 2), cr, 4f,
            new UIColor(1f, 1f, 1f, 0.06f),
            new UIColor(0f, 0f, 0f, 0.15f));
        renderer.FillRoundedRectWithPaint(new Rect(0, 0, w, h), cr, listInset);

        // Dark border stroke
        renderer.StrokeRoundedRect(
            new Rect(0.5f, 0.5f, w - 1f, h - 1f),
            MathF.Max(cr - 0.5f, 0f), 1f,
            IsFocused ? theme.AccentColor : UIColor.Black.WithAlpha(0.188f));

        // Clipped, scroll-translated item area
        renderer.Save();
        renderer.IntersectClip(new Rect(0, 0, viewW, h));
        renderer.Translate(0, -_scrollY);
        DrawItems(renderer, viewW, h);
        renderer.Restore();

        if (needSB) DrawScrollbar(renderer, w, h, sb, totalH);
    }

    protected void DrawScrollbar(Renderer renderer, float w, float h, float sb, float totalH)
    {
        ScrollbarRenderer.DrawVertical(renderer, w - sb, 0, sb, h, _scrollY, totalH, h, _sbHovered);
    }

    // ─── Mouse input ─────────────────────────────────────────────────────────
    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; _sbHovered = false; return true; }

    public override bool OnMouseMove(MouseEvent e)
    {
        _mouseLocal = e.LocalPosition;
        float sb     = ThemeManager.Current.ScrollBarWidth;
        float totalH = ItemCount * ItemHeight;
        _sbHovered   = totalH > Bounds.Height && _mouseLocal.X >= Bounds.Width - sb;
        if (_sbDragging)
        {
            float h          = Bounds.Height;
            float maxScroll  = MathF.Max(0f, totalH - h);
            float thumbH     = MathF.Max(h * h / MathF.Max(totalH, 1f), 20f);
            float trackRange = h - thumbH;
            if (trackRange > 0f)
            {
                float delta = e.Position.Y - _sbDragStartY;
                _scrollY = Math.Clamp(_sbDragStartScroll + delta * maxScroll / trackRange, 0f, maxScroll);
            }
            return true;
        }
        return true;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        _mouseLocal = e.LocalPosition;

        float sb     = ThemeManager.Current.ScrollBarWidth;
        float totalH = ItemCount * ItemHeight;
        float h      = Bounds.Height;

        // Scrollbar drag start
        if (totalH > h && _mouseLocal.X >= Bounds.Width - sb)
        {
            _sbDragging        = true;
            _sbDragStartY      = e.Position.Y;
            _sbDragStartScroll = _scrollY;
            return true;
        }

        return OnItemClick(e, IndexFromY(_mouseLocal.Y));
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        if (_sbDragging) { _sbDragging = false; return true; }
        return false;
    }

    public override bool OnMouseScroll(MouseEvent e)
    {
        float totalH    = ItemCount * ItemHeight;
        float maxScroll = MathF.Max(0f, totalH - Bounds.Height);
        _scrollY = Math.Clamp(_scrollY - e.Scroll.Y * ItemHeight * 2f, 0f, maxScroll);
        return true;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    protected int IndexFromY(float localY)
        => ItemHeight > 0f ? (int)((localY + _scrollY) / ItemHeight) : 0;

    protected int HoveredIndex()
        => ItemHeight > 0f ? (int)((_mouseLocal.Y + _scrollY) / ItemHeight) : -1;

    protected void ScrollToIndex(int idx)
    {
        if (idx < 0) return;
        float itemTop    = idx * ItemHeight;
        float itemBottom = itemTop + ItemHeight;
        float viewH      = MathF.Max(Bounds.Height, 1f);
        float maxScr     = MathF.Max(0f, ItemCount * ItemHeight - viewH);
        if (itemTop < _scrollY)              _scrollY = itemTop;
        else if (itemBottom > _scrollY + viewH) _scrollY = itemBottom - viewH;
        _scrollY = Math.Clamp(_scrollY, 0f, maxScr);
    }

    protected void ClampScroll()
    {
        float maxScroll = MathF.Max(0f, ItemCount * ItemHeight - Bounds.Height);
        _scrollY = Math.Clamp(_scrollY, 0f, maxScroll);
    }

    protected void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0f ? FontSize : theme.FontSize);
    }
}
