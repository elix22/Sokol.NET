using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>A single cell/segment in a <see cref="StatusBar"/>.</summary>
public sealed class StatusBarItem
{
    /// <summary>Text to display.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>
    /// Relative weight for proportional sizing. Null means fixed width equal to text.
    /// </summary>
    public float? Weight { get; set; }
    /// <summary>Optional click handler.</summary>
    public Action? OnClick { get; set; }
    public TextHAlign Align { get; set; } = TextHAlign.Left;
}

/// <summary>
/// Fixed-height strip at the bottom of a screen showing status segments.
/// Typically added to a <see cref="Screen"/> and drawn above the floor.
/// </summary>
public class StatusBar : Widget
{
    private readonly List<StatusBarItem> _items = [];
    private int _hoveredIdx = -1;

    public const float DefaultHeight = 22f;

    public IReadOnlyList<StatusBarItem> Items => _items;
    public float BarHeight { get; set; } = DefaultHeight;

    public Font?  Font     { get; set; }
    public float  FontSize { get; set; } = 0f;

    // ─── API ─────────────────────────────────────────────────────────────────
    public StatusBarItem AddItem(string text, float? weight = 1f, Action? onClick = null)
    {
        var item = new StatusBarItem { Text = text, Weight = weight, OnClick = onClick };
        _items.Add(item);
        return item;
    }

    public void SetText(int index, string text)
    {
        if (index >= 0 && index < _items.Count)
            _items[index].Text = text;
    }

    // ─── PreferredSize ────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
        => FixedSize ?? new Vector2(Bounds.Width, BarHeight);

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var   theme = ThemeManager.Current;
        float w     = Bounds.Width, h = Bounds.Height;

        // Background
        renderer.FillRect(new Rect(0, 0, w, h), theme.SurfaceVariant);
        renderer.DrawLine(0, 0, w, 0, 1f, theme.BorderColor);

        ApplyFont(renderer, theme);

        // Compute segment widths
        Span<float> widths = _items.Count <= 16
            ? stackalloc float[_items.Count]
            : new float[_items.Count];

        float totalWeight = 0f;
        float fixedWidth  = 0f;

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.Weight is float wt)
            {
                totalWeight += wt;
            }
            else
            {
                float tw = renderer.MeasureText(item.Text) + 12f;
                widths[i] = tw;
                fixedWidth += tw;
            }
        }

        float remaining = w - fixedWidth;
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Weight is float wt)
                widths[i] = totalWeight > 0 ? remaining * (wt / totalWeight) : 0f;
        }

        // Draw each segment
        float x = 0f;
        for (int i = 0; i < _items.Count; i++)
        {
            var   item  = _items[i];
            float sw    = widths[i];
            var   segR  = new Rect(x, 0, sw, h);

            // Hover
            if (i == _hoveredIdx && item.OnClick != null)
                renderer.FillRect(segR, theme.ButtonHoverColor.WithAlpha(0.25f));

            // Separator before segment (skip first)
            if (i > 0)
                renderer.DrawLine(x, 2f, x, h - 2f, 1f, theme.BorderColor.WithAlpha(0.5f));

            // Text
            float tx = item.Align == TextHAlign.Center ? x + sw * 0.5f
                     : item.Align == TextHAlign.Right   ? x + sw - 6f
                     : x + 6f;
            float ty = h * 0.5f;
            renderer.SetTextAlign(item.Align);
            renderer.DrawText(tx, ty, item.Text, theme.TextMutedColor);

            x += sw;
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; _hoveredIdx = -1; return true; }

    public override bool OnMouseMove(MouseEvent e)
    {
        _hoveredIdx = HitSegment(e.LocalPosition.X);
        return true;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        int idx = HitSegment(e.LocalPosition.X);
        if (idx >= 0) _items[idx].OnClick?.Invoke();
        return true;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private int HitSegment(float x)
    {
        // Recompute widths without renderer (fast approximation)
        float totalWeight = 0f, fixedWidth = 0f;
        var approxWidths = new float[_items.Count];
        const float charW = 7f;
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.Weight is float wt) { totalWeight += wt; }
            else { approxWidths[i] = item.Text.Length * charW + 12f; fixedWidth += approxWidths[i]; }
        }
        float remaining = Bounds.Width - fixedWidth;
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Weight is float wt)
                approxWidths[i] = totalWeight > 0 ? remaining * (wt / totalWeight) : 0f;

        float cur = 0f;
        for (int i = 0; i < _items.Count; i++)
        {
            cur += approxWidths[i];
            if (x < cur) return i;
        }
        return -1;
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.SmallFontSize);
    }
}
