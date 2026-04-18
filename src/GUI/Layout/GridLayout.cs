using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Arranges children into a fixed-column grid.  Children are placed
/// left-to-right, wrapping to the next row after <see cref="Columns"/> items.
/// </summary>
public sealed class GridLayout : ILayout
{
    public int   Columns  { get; set; } = 2;
    public float HSpacing { get; set; } = 0f;
    public float VSpacing { get; set; } = 0f;

    public GridLayout() { }
    public GridLayout(int columns, float hSpacing = 0f, float vSpacing = 0f)
    {
        Columns  = columns;
        HSpacing = hSpacing;
        VSpacing = vSpacing;
    }

    public Vector2 Measure(Widget parent, Renderer renderer, Vector2 availableSize)
    {
        var pad    = parent.Padding;
        float cellW = MeasureCellWidth(parent, renderer, availableSize.X - pad.Horizontal);
        var sizes  = GetChildSizes(parent, renderer, cellW);
        int rows   = (int)MathF.Ceiling((float)sizes.Count / MathF.Max(1, Columns));
        float maxH = 0f;
        for (int r = 0; r < rows; r++)
        {
            float rowH = 0f;
            for (int c = 0; c < Columns; c++)
            {
                int i = r * Columns + c;
                if (i < sizes.Count) rowH = MathF.Max(rowH, sizes[i].Y);
            }
            maxH += rowH + (r > 0 ? VSpacing : 0f);
        }
        return new Vector2(availableSize.X, maxH + pad.Vertical);
    }

    public void Arrange(Widget parent, Renderer renderer, Rect finalRect)
    {
        var pad   = parent.Padding;
        var inner = new Rect(pad.Left, pad.Top,
            finalRect.Width  - pad.Horizontal,
            finalRect.Height - pad.Vertical);
        float cellW = (inner.Width - HSpacing * (Columns - 1)) / MathF.Max(1, Columns);
        var sizes  = GetChildSizes(parent, renderer, cellW);

        var visible = new List<Widget>();
        foreach (var child in parent.Children)
            if (child.Visible) visible.Add(child);

        bool rtl = parent.ResolvedFlowDirection == FlowDirection.RightToLeft;
        int idx   = 0;
        float y   = inner.Top;
        int rows  = (int)MathF.Ceiling((float)visible.Count / MathF.Max(1, Columns));

        for (int r = 0; r < rows; r++)
        {
            float rowH = 0f;
            for (int c = 0; c < Columns; c++)
            {
                int i = r * Columns + c;
                if (i < sizes.Count) rowH = MathF.Max(rowH, sizes[i].Y);
            }

            for (int c = 0; c < Columns && idx < visible.Count; c++, idx++)
            {
                int col = rtl ? (Columns - 1 - c) : c;
                float x = inner.Left + col * (cellW + HSpacing);
                visible[idx].Bounds = new Rect(x, y, cellW, rowH);
            }
            y += rowH + VSpacing;
        }
    }

    private float MeasureCellWidth(Widget parent, Renderer renderer, float availW) =>
        (availW - HSpacing * (Columns - 1)) / MathF.Max(1, Columns);

    private List<Vector2> GetChildSizes(Widget parent, Renderer renderer, float cellW)
    {
        var sizes = new List<Vector2>();
        foreach (var child in parent.Children)
        {
            if (!child.Visible) continue;
            var s = child.FixedSize ?? child.PreferredSize(renderer);
            sizes.Add(new Vector2(cellW, s.Y));
        }
        return sizes;
    }
}
