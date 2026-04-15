using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Type of a toolbar item.
/// </summary>
public enum ToolBarItemType { Button, Separator, Toggle }

/// <summary>
/// An item in a <see cref="ToolBar"/>.
/// </summary>
public sealed class ToolBarItem
{
    public string         Label    { get; set; } = string.Empty;
    public string?        Tooltip   { get; set; }
    public ToolBarItemType Type    { get; set; } = ToolBarItemType.Button;
    public bool            Pressed { get; set; } = false;  // for Toggle type
    public bool            Enabled { get; set; } = true;
    public Action?         OnClick { get; set; }
}

/// <summary>
/// Horizontal (or vertical) strip of compact icon/text buttons.
/// </summary>
public class ToolBar : Widget
{
    private readonly List<ToolBarItem> _items = [];
    private int _hoveredIdx = -1;

    public const float DefaultItemSize = 28f;

    public IReadOnlyList<ToolBarItem> Items => _items;
    public float ItemSize { get; set; } = DefaultItemSize;
    public SliderOrientation Orientation { get; set; } = SliderOrientation.Horizontal;

    public Font?  Font     { get; set; }
    public float  FontSize { get; set; } = 0f;

    // ─── API ─────────────────────────────────────────────────────────────────
    public ToolBarItem AddButton(string label, Action? onClick = null, string? tooltip = null)
    {
        var item = new ToolBarItem { Label = label, OnClick = onClick, Tooltip = tooltip };
        _items.Add(item);
        return item;
    }

    public ToolBarItem AddToggle(string label, Action? onClick = null, string? tooltip = null)
    {
        var item = new ToolBarItem { Label = label, OnClick = onClick, Tooltip = tooltip,
                                      Type = ToolBarItemType.Toggle };
        _items.Add(item);
        return item;
    }

    public void AddSeparator()
        => _items.Add(new ToolBarItem { Type = ToolBarItemType.Separator });

    // ─── Layout ──────────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        float total = 0;
        foreach (var it in _items)
            total += it.Type == ToolBarItemType.Separator ? SepSize() : ItemWidth(it, renderer);
        float thickness = ItemSize + 4f;
        return Orientation == SliderOrientation.Horizontal
            ? new Vector2(total, thickness)
            : new Vector2(thickness, total);
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme = ThemeManager.Current;
        float w   = Bounds.Width, h = Bounds.Height;
        float cr  = theme.ButtonCornerRadius;

        // Background strip
        renderer.FillRect(new Rect(0, 0, w, h), theme.SurfaceVariant);
        renderer.DrawLine(0, h, w, h, 1f, theme.BorderColor);

        ApplyFont(renderer, theme);

        float pos = 2f;
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.Type == ToolBarItemType.Separator)
            {
                float sep = SepSize();
                if (Orientation == SliderOrientation.Horizontal)
                    renderer.DrawLine(pos + sep * 0.5f, 4f, pos + sep * 0.5f, h - 4f, 1f, theme.BorderColor);
                else
                    renderer.DrawLine(4f, pos + sep * 0.5f, w - 4f, pos + sep * 0.5f, 1f, theme.BorderColor);
                pos += sep;
                continue;
            }

            float iw  = ItemWidth(item, renderer);
            var   itemR = Orientation == SliderOrientation.Horizontal
                ? new Rect(pos, 2f, iw, h - 4f)
                : new Rect(2f, pos, w - 4f, iw);

            bool hov     = i == _hoveredIdx && item.Enabled;
            bool pressed = item.Pressed && item.Type == ToolBarItemType.Toggle;

            if (pressed)
                renderer.FillRoundedRect(itemR, cr, theme.AccentColor.WithAlpha(0.25f));
            else if (hov)
                renderer.FillRoundedRect(itemR, cr, theme.ButtonHoverColor.WithAlpha(0.4f));

            // Label
            var labelColor = !item.Enabled ? theme.TextDisabledColor
                            : pressed ? theme.AccentColor
                            : hov     ? theme.TextColor
                            :           theme.TextMutedColor;

            float cx = itemR.X + itemR.Width  * 0.5f;
            float cy = itemR.Y + itemR.Height * 0.5f;
            renderer.SetTextAlign(TextHAlign.Center);
            renderer.DrawText(cx, cy, item.Label, labelColor);

            pos += iw + 2f;
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; _hoveredIdx = -1; return true; }

    public override bool OnMouseMove(MouseEvent e)
    {
        _hoveredIdx = HitItem(e.Position);
        return _hoveredIdx >= 0;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        int idx = HitItem(e.Position);
        if (idx < 0) return false;
        var item = _items[idx];
        if (!item.Enabled) return true;

        if (item.Type == ToolBarItemType.Toggle)
            item.Pressed = !item.Pressed;

        item.OnClick?.Invoke();
        return true;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private float SepSize() => Orientation == SliderOrientation.Horizontal ? 10f : 10f;

    private float ItemWidth(ToolBarItem item, Renderer? renderer)
    {
        if (renderer == null) return ItemSize;
        float tw = renderer.MeasureText(item.Label);
        return MathF.Max(ItemSize, tw + 12f);
    }

    private int HitItem(Vector2 pos)
    {
        float coord = Orientation == SliderOrientation.Horizontal ? pos.X : pos.Y;
        float cur   = 2f;
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            float size = item.Type == ToolBarItemType.Separator
                ? SepSize()
                : ItemWidth(item, null) + 2f;
            if (coord >= cur && coord < cur + size) return i;
            cur += size;
        }
        return -1;
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.SmallFontSize);
    }
}
