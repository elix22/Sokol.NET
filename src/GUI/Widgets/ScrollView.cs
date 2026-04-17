using System;

namespace Sokol.GUI;

/// <summary>
/// Scrollable container with vertical (and optionally horizontal) scrollbars.
/// </summary>
public class ScrollView : Panel
{
    private float _scrollX, _scrollY;
    private bool  _dragV, _dragH;
    private float _dragStartY, _dragStartScrollY;
    private float _dragStartX, _dragStartScrollX;
    private bool  _sbHoveredV, _sbHoveredH;

    public bool CanScrollHorizontal { get; set; } = true;
    public bool CanScrollVertical   { get; set; } = true;

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

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        float sb   = theme.ScrollBarWidth;

        // NanoGUI-style sunken container
        float cr = theme.InputCornerRadius;
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

        // Clip to viewport (shrunk for scrollbars if visible)
        bool showV = CanScrollVertical   && ContentHeight > Bounds.Height;
        bool showH = CanScrollHorizontal && ContentWidth  > Bounds.Width;
        bool rtl   = ResolvedFlowDirection == FlowDirection.RightToLeft;

        // Clamp scroll offset so content doesn't stay shifted when viewport grows
        float maxScrollY = MathF.Max(0, ContentHeight - Bounds.Height + (showH ? sb : 0));
        float maxScrollX = MathF.Max(0, ContentWidth  - Bounds.Width  + (showV ? sb : 0));
        _scrollY = MathF.Min(_scrollY, maxScrollY);
        _scrollX = MathF.Min(_scrollX, maxScrollX);

        // RTL: vertical scrollbar goes on the left
        float sbLeft  = showV && rtl  ? sb : 0;
        float sbRight = showV && !rtl ? sb : 0;
        var viewport = new Rect(sbLeft, 0,
            Bounds.Width  - sbLeft - sbRight,
            Bounds.Height - (showH ? sb : 0));

        renderer.Save();
        renderer.IntersectClip(viewport);
        renderer.Translate(sbLeft - _scrollX, -_scrollY);

        if (Content != null)
        {
            // Content width should be at least the viewport width so children fill the visible area.
            float cw = MathF.Max(ContentWidth, viewport.Width);
            Content.Bounds = new Rect(0, 0, cw, ContentHeight);
            Content.PerformLayout(renderer, force: true);
            renderer.Save();
            Content.Draw(renderer);
            renderer.Restore();
        }

        renderer.Restore();

        // Vertical scrollbar
        if (showV)
        {
            float cH = MathF.Max(ContentHeight, 1f);
            float sbX = rtl ? 0 : viewport.Right;
            ScrollbarRenderer.DrawVertical(renderer, sbX, 0, sb, viewport.Height,
                _scrollY, cH, viewport.Height, _sbHoveredV);
        }

        // Horizontal scrollbar
        if (showH)
        {
            float cW = MathF.Max(ContentWidth, 1f);
            ScrollbarRenderer.DrawHorizontal(renderer, sbLeft, viewport.Height, viewport.Width, sb,
                _scrollX, cW, viewport.Width, _sbHoveredH);
        }
    }

    // ─── Content size ────────────────────────────────────────────────────────
    private float ContentHeight => Content?.PreferredSize(Screen.Instance.Renderer).Y ?? Bounds.Height;
    private float ContentWidth  => Content?.PreferredSize(Screen.Instance.Renderer).X ?? Bounds.Width;

    // ScrollOffset tells ScreenPosition to subtract our scroll from children's positions.
    public override Vector2 ScrollOffset => new Vector2(_scrollX, _scrollY);

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
        if (CanScrollHorizontal) ScrollX = MathF.Max(0, _scrollX - e.Scroll.X * spd);
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
            ScrollX = MathF.Max(0, _dragStartScrollX + dx);
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
