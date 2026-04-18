using System;
using System.Collections.Generic;

namespace Sokol.GUI;

public enum DockPosition { Left, Right, Top, Bottom, Fill }

/// <summary>
/// Docking layout.  Children declare their dock side via
/// <see cref="DockLayout.GetDock"/> / <see cref="DockLayout.SetDock"/>.
/// The last child with <c>Fill</c> takes the remaining space.
/// </summary>
public sealed class DockLayout : ILayout
{
    // Attached-property storage per widget instance.
    private static readonly Dictionary<Widget, DockPosition> _docks = new();

    public static void      SetDock(Widget w, DockPosition pos) => _docks[w] = pos;
    public static DockPosition GetDock(Widget w) =>
        _docks.TryGetValue(w, out var p) ? p : DockPosition.Fill;

    public Vector2 Measure(Widget parent, Renderer renderer, Vector2 availableSize) =>
        availableSize; // Dock fills parent by definition.

    public void Arrange(Widget parent, Renderer renderer, Rect finalRect)
    {
        var pad   = parent.Padding;
        var inner = new Rect(pad.Left, pad.Top,
            finalRect.Width  - pad.Horizontal,
            finalRect.Height - pad.Vertical);

        float l = inner.Left, t = inner.Top, r = inner.Right, b = inner.Bottom;
        bool rtl = parent.ResolvedFlowDirection == FlowDirection.RightToLeft;

        foreach (var child in parent.Children)
        {
            if (!child.Visible) continue;
            var dock = GetDock(child);
            // In RTL, swap Left/Right so "Left" means the leading edge.
            if (rtl)
            {
                if (dock == DockPosition.Left)       dock = DockPosition.Right;
                else if (dock == DockPosition.Right) dock = DockPosition.Left;
            }
            var size = child.FixedSize ?? child.PreferredSize(renderer);

            switch (dock)
            {
                case DockPosition.Top:
                    float h = size.Y;
                    child.Bounds = new Rect(l, t, r - l, h);
                    t += h;
                    break;
                case DockPosition.Bottom:
                    float bh = size.Y;
                    child.Bounds = new Rect(l, b - bh, r - l, bh);
                    b -= bh;
                    break;
                case DockPosition.Left:
                    float w = size.X;
                    child.Bounds = new Rect(l, t, w, b - t);
                    l += w;
                    break;
                case DockPosition.Right:
                    float rw = size.X;
                    child.Bounds = new Rect(r - rw, t, rw, b - t);
                    r -= rw;
                    break;
                case DockPosition.Fill:
                default:
                    child.Bounds = new Rect(l, t, r - l, b - t);
                    break;
            }
        }
    }
}
