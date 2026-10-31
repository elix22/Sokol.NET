using System;

namespace Sokol.GUI;

/// <summary>
/// Scrollable container with vertical (and optionally horizontal) scrollbars.
/// <para><see cref="ScrollX"/> is measured from the content's START edge: the left in left-to-right, the RIGHT in
/// right-to-left, where the content is laid out mirrored and begins at its right edge. So 0 always shows the start
/// (like a browser's RTL <c>scrollLeft</c>), and the start stays in view when the content grows. The physical offset
/// used for drawing, the scrollbar and <see cref="ScrollOffset"/> is derived from it; in left-to-right the two are
/// the same number.</para>
/// </summary>
public class ScrollView : Panel
{
    private float _scrollX, _scrollY;
    private float _maxScrollX;   // the last Draw's horizontal scroll range — maps an RTL ScrollX to a physical offset between draws
    private float _sbLeft;       // the last Draw's content shift for a vertical scrollbar on the LEFT (RTL) — hit-testing must shift the same way
    private bool  _dragV, _dragH;
    private float _dragStartY, _dragStartScrollY;
    private float _dragStartX, _dragStartScrollX;
    private bool  _sbHoveredV, _sbHoveredH;

    public bool CanScrollHorizontal { get; set; } = true;
    public bool CanScrollVertical   { get; set; } = true;

    /// <summary>Draw the sunken frame only while the content overflows (a scrollbar shows). When everything fits there
    /// is nothing to scroll, and the frame only makes body text read as a text field; then only
    /// <see cref="Panel.BackgroundColor"/> is filled, when set. Off (default): the frame is always drawn.</summary>
    public bool FrameOnlyWhenScrolling { get; set; }

    public float ScrollX { get => _scrollX; set => _scrollX = MathF.Max(0, value); }
    public float ScrollY { get => _scrollY; set => _scrollY = MathF.Max(0, value); }

    // Content widget — the single child we scroll.
    public Widget? Content
    {
        get => Children.Count > 0 ? Children[0] : null;
        set
        {
            ClearChildren();
            if (value != null) AddChild(value);
        }
    }

    /// <summary>
    /// The content is laid out only by <see cref="Draw"/>, in the viewport it is drawn in. A parent's layout pass
    /// used to arrange it first at its own preferred width; a wrapped <see cref="Label"/> measures at its last
    /// width, so every frame it was sized for that width and then drawn at the narrower viewport width — one or
    /// more lines short, its tail drawn over the next widget (an RL Env help text, robotics plan 3f.2).
    /// </summary>
    public override void PerformLayout(Renderer renderer, bool force = false) { }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        float sb   = theme.ScrollBarWidth;

        // ⛔ Performance: the content's preferred size is a full measure of everything in it, on screen or
        // not — take it ONCE before the layout below (it was evaluated up to six times here), and once more
        // after it for the scrollbars, which have always shown the post-layout size.
        var   pre      = ContentSize;
        float contentH = pre.Y, contentW = pre.X;

        // Clip to viewport (shrunk for scrollbars if visible)
        bool showV = CanScrollVertical   && contentH > Bounds.Height;
        bool showH = CanScrollHorizontal && contentW > Bounds.Width;
        bool rtl   = ResolvedFlowDirection == FlowDirection.RightToLeft;

        // NanoGUI-style sunken container
        float cr = theme.InputCornerRadius;
        if (!FrameOnlyWhenScrolling || showV || showH)
        {
            var bg = BackgroundColor ?? theme.InputBackColor;
            renderer.FillRoundedRect(bounds, cr, bg);
            var svInset = renderer.BoxGradient(
                new Rect(1, 2, bounds.Width - 2, bounds.Height - 2), cr, 4f,
                new UIColor(1f, 1f, 1f, 0.06f),
                new UIColor(0f, 0f, 0f, 0.15f));
            renderer.FillRoundedRectWithPaint(bounds, cr, svInset);
            renderer.StrokeRoundedRect(
                new Rect(0.5f, 0.5f, bounds.Width - 1f, bounds.Height - 1f),
                MathF.Max(cr - 0.5f, 0f), 1f,
                IsFocused ? theme.AccentColor : UIColor.Black.WithAlpha(0.188f));
        }
        else if (BackgroundColor is { } bgOnly) renderer.FillRoundedRect(bounds, cr, bgOnly);

        // Clamp scroll offset so content doesn't stay shifted when viewport grows
        float maxScrollY = MathF.Max(0, contentH - Bounds.Height + (showH ? sb : 0));
        float maxScrollX = MathF.Max(0, contentW - Bounds.Width  + (showV ? sb : 0));
        _scrollY = MathF.Min(_scrollY, maxScrollY);
        _scrollX = MathF.Min(_scrollX, maxScrollX);
        _maxScrollX = maxScrollX;
        float physX = PhysicalScrollX(rtl);

        // RTL: vertical scrollbar goes on the left
        float sbLeft  = showV && rtl  ? sb : 0;
        float sbRight = showV && !rtl ? sb : 0;
        _sbLeft = sbLeft;
        var viewport = new Rect(sbLeft, 0,
            Bounds.Width  - sbLeft - sbRight,
            Bounds.Height - (showH ? sb : 0));

        renderer.Save();
        renderer.IntersectClip(viewport);
        renderer.Translate(sbLeft - physX, -_scrollY);

        if (Content != null)
        {
            // When horizontal scroll is disabled, content must be exactly viewport width so
            // Expand children distribute the actual available space, not their preferred size.
            float cw = CanScrollHorizontal
                ? MathF.Max(contentW, viewport.Width)
                : viewport.Width;
            Content.Bounds = new Rect(0, 0, cw, contentH);
            Content.PerformLayout(renderer, force: true);
            renderer.Save();
            Content.Draw(renderer);
            renderer.Restore();
        }

        renderer.Restore();

        var post = showV || showH ? ContentSize : default;

        // Vertical scrollbar
        if (showV)
        {
            float cH = MathF.Max(post.Y, 1f);
            float sbX = rtl ? 0 : viewport.Right;
            ScrollbarRenderer.DrawVertical(renderer, sbX, 0, sb, viewport.Height,
                _scrollY, cH, viewport.Height, _sbHoveredV);
        }

        // Horizontal scrollbar
        if (showH)
        {
            float cW = MathF.Max(post.X, 1f);
            ScrollbarRenderer.DrawHorizontal(renderer, sbLeft, viewport.Height, viewport.Width, sb,
                physX, cW, viewport.Width, _sbHoveredH);
        }
    }

    // ─── Content size ────────────────────────────────────────────────────────
    private float ContentHeight => Content?.PreferredSize(Screen.Instance.Renderer).Y ?? Bounds.Height;
    private float ContentWidth  => Content?.PreferredSize(Screen.Instance.Renderer).X ?? Bounds.Width;
    private Vector2 ContentSize => Content?.PreferredSize(Screen.Instance.Renderer) ?? new Vector2(Bounds.Width, Bounds.Height);

    /// <summary>True when there is overflow to pan in that axis (used by drag-to-scroll).</summary>
    public bool CanDragScrollV => CanScrollVertical   && ContentHeight > Bounds.Height;
    public bool CanDragScrollH => CanScrollHorizontal && ContentWidth  > Bounds.Width;

    /// <summary>Pan the view by a pixel delta (touch/mouse drag-to-scroll and fling),
    /// clamped to the content extent. Returns true if it actually moved.</summary>
    public bool DragScrollBy(float dx, float dy)
    {
        // dx is a physical pan (content moves left for dx > 0); in RTL ScrollX runs the other way (see the class summary)
        bool rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
        bool moved = false;
        if (CanScrollVertical && ContentHeight > Bounds.Height)
        {
            float max = MathF.Max(0f, ContentHeight - Bounds.Height);
            float ny  = MathF.Min(max, MathF.Max(0f, _scrollY + dy));
            if (ny != _scrollY) { _scrollY = ny; moved = true; }
        }
        if (CanScrollHorizontal && ContentWidth > Bounds.Width)
        {
            float max = rtl ? RtlMaxScrollX() : MathF.Max(0f, ContentWidth - Bounds.Width);
            float nx  = MathF.Min(max, MathF.Max(0f, _scrollX + (rtl ? -dx : dx)));
            if (nx != _scrollX) { _scrollX = nx; moved = true; }
        }
        return moved;
    }

    // ScrollOffset tells ScreenPosition to subtract our scroll from children's positions — the PHYSICAL offset.
    // (No horizontal range — nearly every vertical list — means both are the same: skip the direction walk, it runs per hit-test.)
    // ⛔ Minus the RTL scrollbar shift: Draw moves the content right by the left-side scrollbar's width, and without it here
    // every child was hit one scrollbar-width LEFT of where it is drawn — a right-edge checkbox in Hebrew Settings only
    // toggled when touched to the left of its box (owner, 2026-09-26). 0 in LTR.
    public override Vector2 ScrollOffset =>
        new Vector2((_maxScrollX > 0f ? PhysicalScrollX(ResolvedFlowDirection == FlowDirection.RightToLeft) : _scrollX) - _sbLeft, _scrollY);

    /// <summary>The physical offset of the content's left edge: <see cref="ScrollX"/> itself in LTR; in RTL, where
    /// ScrollX counts from the right edge, the rest of the range (the last Draw's).</summary>
    private float PhysicalScrollX(bool rtl) => rtl ? MathF.Max(0f, _maxScrollX - _scrollX) : _scrollX;

    /// <summary>The horizontal scroll range exactly as <see cref="Draw"/> computes it (a vertical scrollbar narrows
    /// the viewport) — the RTL mapping must use the same range, or the start edge would sit a scrollbar short.</summary>
    private float RtlMaxScrollX()
    {
        bool showV = CanScrollVertical && ContentHeight > Bounds.Height;
        return MathF.Max(0f, ContentWidth - Bounds.Width + (showV ? ThemeManager.Current.ScrollBarWidth : 0f));
    }

    // ─── Hit testing ─────────────────────────────────────────────────────
    public override Widget? HitTestDeep(Vector2 screenPoint)
    {
        if (!Visible || !Enabled) return null;
        var local = ToLocal(screenPoint);
        if (!HitTest(local)) return null;

        // Scrollbar areas belong to ScrollView — don’t let content steal those clicks.
        var   theme = ThemeManager.Current;
        float sbW   = theme.ScrollBarWidth;
        bool  showV = CanScrollVertical   && ContentHeight > Bounds.Height;
        bool  showH = CanScrollHorizontal && ContentWidth  > Bounds.Width;
        bool  rtlHT = ResolvedFlowDirection == FlowDirection.RightToLeft;
        if (showV && rtlHT  && local.X <= sbW)                   return this;  // RTL: sb on left
        if (showV && !rtlHT && local.X >= Bounds.Width  - sbW)   return this;  // LTR: sb on right
        if (showH && local.Y >= Bounds.Height - sbW) return this;

        // Children have scroll-aware ScreenPositions — recurse with original screenPoint.
        var kids = Children;
        for (int i = kids.Count - 1; i >= 0; i--)
        {
            var hit = kids[i].HitTestDeep(screenPoint);
            if (hit != null) return hit;
        }
        return this;
    }

    public override bool OnMouseEnter(MouseEvent e) { return true; }
    public override bool OnMouseLeave(MouseEvent e) { _sbHoveredV = false; _sbHoveredH = false; return true; }

    public override bool OnMouseScroll(MouseEvent e)
    {
        float spd = ThemeManager.Current.ScrollSpeed;
        if (CanScrollVertical)   ScrollY = MathF.Max(0, _scrollY - e.Scroll.Y * spd);
        if (CanScrollHorizontal)   // the wheel pans physically; RTL's ScrollX runs the other way
            ScrollX = ResolvedFlowDirection == FlowDirection.RightToLeft
                ? MathF.Min(RtlMaxScrollX(), _scrollX + e.Scroll.X * spd)
                : MathF.Max(0, _scrollX - e.Scroll.X * spd);
        return true;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        var theme = ThemeManager.Current;
        float sb  = theme.ScrollBarWidth;
        bool showV = CanScrollVertical   && ContentHeight > Bounds.Height;
        bool showH = CanScrollHorizontal && ContentWidth  > Bounds.Width;
        bool rtlDn = ResolvedFlowDirection == FlowDirection.RightToLeft;

        // Check if clicked on vertical scrollbar
        bool hitVSb = showV && (rtlDn ? e.LocalPosition.X <= sb : e.LocalPosition.X >= Bounds.Width - sb);
        if (hitVSb)
        {
            _dragV            = true;
            _dragStartY       = e.LocalPosition.Y;
            _dragStartScrollY = _scrollY;
            return true;
        }

        // Check if clicked on horizontal scrollbar
        bool hitHSb = showH && e.LocalPosition.Y >= Bounds.Height - sb;
        if (hitHSb)
        {
            _dragH            = true;
            _dragStartX       = e.LocalPosition.X;
            _dragStartScrollX = _scrollX;
            return true;
        }

        return Content?.OnMouseDown(e) ?? false;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        var theme = ThemeManager.Current;
        float sb  = theme.ScrollBarWidth;
        bool showV = CanScrollVertical   && ContentHeight > Bounds.Height;
        bool showH = CanScrollHorizontal && ContentWidth  > Bounds.Width;
        bool rtlMv = ResolvedFlowDirection == FlowDirection.RightToLeft;
        _sbHoveredV = showV && (rtlMv ? e.LocalPosition.X <= sb : e.LocalPosition.X >= Bounds.Width - sb);
        _sbHoveredH = showH && e.LocalPosition.Y >= Bounds.Height - sb;
        if (_dragV)
        {
            float cH = MathF.Max(ContentHeight, 1f);
            float ratio = Bounds.Height / cH;
            float dy = (e.LocalPosition.Y - _dragStartY) / ratio;
            ScrollY = MathF.Max(0, _dragStartScrollY + dy);
            return true;
        }
        if (_dragH)
        {
            float cW = MathF.Max(ContentWidth, 1f);
            float ratio = Bounds.Width / cW;
            float dx = (e.LocalPosition.X - _dragStartX) / ratio;
            // the thumb moves physically; in RTL ScrollX counts from the right, so it runs the other way
            ScrollX = ResolvedFlowDirection == FlowDirection.RightToLeft
                ? MathF.Max(0, _dragStartScrollX - dx)
                : MathF.Max(0, _dragStartScrollX + dx);
            return true;
        }
        return false;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        _dragV = false; _dragH = false;
        return false;
    }
}
