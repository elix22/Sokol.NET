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

    public bool CanScrollHorizontal { get; set; } = false;
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
        var viewport = new Rect(0, 0,
            Bounds.Width  - (showV ? sb : 0),
            Bounds.Height - (showH ? sb : 0));

        renderer.Save();
        renderer.IntersectClip(viewport);
        renderer.Translate(-_scrollX, -_scrollY);

        if (Content != null)
        {
            Content.Bounds = new Rect(0, 0, ContentWidth, ContentHeight);
            Content.PerformLayout(renderer);
            renderer.Save();
            Content.Draw(renderer);
            renderer.Restore();
        }

        renderer.Restore();

        // Vertical scrollbar
        if (showV)
        {
            float cH = MathF.Max(ContentHeight, 1f);
            ScrollbarRenderer.DrawVertical(renderer, viewport.Width, 0, sb, viewport.Height,
                _scrollY, cH, viewport.Height);
        }

        // Horizontal scrollbar
        if (showH)
        {
            float cW = MathF.Max(ContentWidth, 1f);
            ScrollbarRenderer.DrawHorizontal(renderer, 0, viewport.Height, viewport.Width, sb,
                _scrollX, cW, viewport.Width);
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
        if (showV && local.X >= Bounds.Width  - sbW) return this;
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
    public override bool OnMouseScroll(MouseEvent e)
    {
        float spd = ThemeManager.Current.ScrollSpeed;
        if (CanScrollVertical)   ScrollY = MathF.Max(0, _scrollY - e.Scroll.Y * spd);
        if (CanScrollHorizontal) ScrollX = MathF.Max(0, _scrollX - e.Scroll.X * spd);
        return true;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        // Check if clicked on vertical scrollbar
        var theme = ThemeManager.Current;
        float sb  = theme.ScrollBarWidth;
        bool showV = CanScrollVertical && ContentHeight > Bounds.Height;
        if (showV && e.LocalPosition.X >= Bounds.Width - sb)
        {
            _dragV            = true;
            _dragStartY       = e.LocalPosition.Y;
            _dragStartScrollY = _scrollY;
            return true;
        }
        return Content?.OnMouseDown(e) ?? false;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        if (_dragV)
        {
            float cH = MathF.Max(ContentHeight, 1f);
            float ratio = Bounds.Height / cH;
            float dy = (e.LocalPosition.Y - _dragStartY) / ratio;
            ScrollY = MathF.Max(0, _dragStartScrollY + dy);
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
