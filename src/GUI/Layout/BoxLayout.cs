using System;
using System.Collections.Generic;

namespace Sokol.GUI;

public enum Orientation { Horizontal, Vertical }
public enum Alignment   { Start, Center, End, Stretch }

/// <summary>
/// Stacks children horizontally or vertically with optional alignment and spacing.
/// </summary>
public sealed class BoxLayout : ILayout
{
    public Orientation Orientation { get; set; } = Orientation.Vertical;
    public Alignment   Alignment   { get; set; } = Alignment.Stretch;
    public Thickness   Margin      { get; set; } = Thickness.Zero;
    public float       Spacing     { get; set; } = 0f;

    public BoxLayout() { }
    public BoxLayout(Orientation orientation, Alignment alignment = Alignment.Stretch,
        float spacing = 0f, Thickness margin = default)
    {
        Orientation = orientation;
        Alignment   = alignment;
        Spacing     = spacing;
        Margin      = margin;
    }

    /// <summary>
    /// Resolves a child's effective size. FixedSize components of 0 mean "auto"
    /// (use PreferredSize for that axis). This lets callers write
    /// <c>FixedSize = new Vector2(0, 36)</c> to mean "auto-width, fixed-height 36".
    /// </summary>
    private static Vector2 EffectiveSize(Widget child, Renderer renderer)
    {
        if (!child.FixedSize.HasValue) return child.PreferredSize(renderer);
        var fs   = child.FixedSize.Value;
        var pref = (fs.X == 0f || fs.Y == 0f) ? child.PreferredSize(renderer) : default;
        return new Vector2(
            fs.X > 0f ? fs.X : pref.X,
            fs.Y > 0f ? fs.Y : pref.Y);
    }

    public Vector2 Measure(Widget parent, Renderer renderer, Vector2 availableSize)
    {
        float main = 0f, cross = 0f;
        bool vertical = Orientation == Orientation.Vertical;
        bool first = true;

        foreach (var child in parent.Children)
        {
            if (!child.Visible) continue;
            if (!first) main += Spacing;
            first = false;
            var size = EffectiveSize(child, renderer);
            size = new Vector2(
                size.X + child.Margin.Horizontal,
                size.Y + child.Margin.Vertical);

            if (vertical)
            {
                main  += size.Y;
                cross  = MathF.Max(cross, size.X);
            }
            else
            {
                main  += size.X;
                cross  = MathF.Max(cross, size.Y);
            }
        }

        main  += vertical ? Margin.Vertical   : Margin.Horizontal;
        cross += vertical ? Margin.Horizontal : Margin.Vertical;

        // Include parent padding
        var pad = parent.Padding;
        if (vertical)
            return new Vector2(cross + pad.Horizontal, main + pad.Vertical);
        else
            return new Vector2(main + pad.Horizontal, cross + pad.Vertical);
    }

    public void Arrange(Widget parent, Renderer renderer, Rect finalRect)
    {
        bool log = Screen.DbgFrame <= 5 || Screen.DbgFrame % 300 == 0;
        bool vertical  = Orientation == Orientation.Vertical;
        var  pad       = parent.Padding;
        // LOCAL inner rect: positions are relative to the parent's (0,0) origin, not finalRect.X/Y.
        // finalRect.Width/Height still drive size calculations.
        var  inner     = new Rect(pad.Left, pad.Top,
            finalRect.Width  - pad.Horizontal,
            finalRect.Height - pad.Vertical);

        // Collect visible children and their preferred sizes.
        var children = new List<(Widget w, float mainSize, float crossSize)>();
        float totalMain = 0f;
        int expandCount = 0;
        bool first = true;

        foreach (var child in parent.Children)
        {
            if (!child.Visible) continue;
            if (!first) totalMain += Spacing;
            first = false;
            var size = EffectiveSize(child, renderer);
            float ms = vertical ? size.Y + child.Margin.Vertical   : size.X + child.Margin.Horizontal;
            float cs = vertical ? size.X + child.Margin.Horizontal : size.Y + child.Margin.Vertical;
            if (!child.Expand) totalMain += ms;
            children.Add((child, ms, cs));
            if (child.Expand) expandCount++;
        }

        // Distribute remaining space among Expand children
        float mainAvail = vertical
            ? inner.Height - Margin.Vertical
            : inner.Width  - Margin.Horizontal;
        float remaining = mainAvail - totalMain;
        float expandSize = (expandCount > 0 && remaining > 0f) ? remaining / expandCount : 0f;

        float cursor = vertical ? inner.Top + Margin.Top : inner.Left + Margin.Left;
        float crossMax = vertical ? inner.Width - Margin.Horizontal : inner.Height - Margin.Vertical;

        // RTL: reverse horizontal cursor direction
        bool rtlH = !vertical && parent.ResolvedFlowDirection == FlowDirection.RightToLeft;
        if (rtlH)
            cursor = inner.Right - Margin.Right;

        foreach (var (child, mainSize, crossSize) in children)
        {
            float cm = child.Margin.Left;
            float cmt = child.Margin.Top;
            float childMarginMain = vertical ? child.Margin.Vertical : child.Margin.Horizontal;
            float childMain = child.Expand
                ? MathF.Max(0f, expandSize - childMarginMain)
                : mainSize - childMarginMain;
            float childCrossReq= crossSize - (vertical ? child.Margin.Horizontal : child.Margin.Vertical);
            float childCross   = (Alignment == Alignment.Stretch || child.Expand) ? crossMax : childCrossReq;
            // FixedSize component of 0 in the cross axis means "fill available"
            if (child.FixedSize.HasValue)
            {
                var fs = child.FixedSize.Value;
                if (vertical  && fs.X == 0f) childCross = crossMax;
                if (!vertical && fs.Y == 0f) childCross = crossMax;
            }

            float crossPos;
            switch (Alignment)
            {
                case Alignment.Center:
                    crossPos = vertical
                        ? inner.Left + Margin.Left + (crossMax - childCrossReq) * 0.5f
                        : inner.Top  + Margin.Top  + (crossMax - childCrossReq) * 0.5f;
                    break;
                case Alignment.End:
                    crossPos = vertical
                        ? inner.Right  - Margin.Right  - childCrossReq
                        : inner.Bottom - Margin.Bottom - childCrossReq;
                    break;
                default: // Start / Stretch
                    crossPos = vertical ? inner.Left + Margin.Left : inner.Top + Margin.Top;
                    break;
            }

            if (vertical)
            {
                child.Bounds = new Rect(crossPos + cm, cursor + cmt, childCross - child.Margin.Horizontal, childMain);
            }
            else if (rtlH)
            {
                // RTL: cursor is the right edge; advance leftward
                float childLeft = cursor - childMain - child.Margin.Right;
                child.Bounds = new Rect(childLeft, crossPos + cmt, childMain, childCross - child.Margin.Vertical);
            }
            else
            {
                child.Bounds = new Rect(cursor + cm, crossPos + cmt, childMain, childCross - child.Margin.Vertical);
            }

            if (log)
                Sokol.SLog.Info($"BoxLayout[{Screen.DbgFrame}]: {parent.GetType().Name}/{child.GetType().Name} FixedSize={child.FixedSize} Expand={child.Expand} → Bounds={child.Bounds}", "Sokol.GUI");

            float actualMain = childMain + (vertical ? child.Margin.Vertical : child.Margin.Horizontal);
            if (rtlH)
                cursor -= actualMain + Spacing;
            else
                cursor += actualMain + Spacing;
        }
    }
}
