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
    private int    _hoveredIdx = -1;
    private int    _pressedIdx = -1;   // mouse-down visual feedback
    private Rect[] _itemRects  = [];   // cached during Draw; index matches _items

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

        if (_itemRects.Length != _items.Count)
            _itemRects = new Rect[_items.Count];

        // RTL: horizontal toolbar items go right-to-left
        bool rtl = Orientation == SliderOrientation.Horizontal
                   && ResolvedFlowDirection == FlowDirection.RightToLeft;
        float totalItemW = 0;
        if (rtl)
            foreach (var it in _items)
                totalItemW += it.Type == ToolBarItemType.Separator ? SepSize() : ItemWidth(it, renderer);

        float pos = rtl ? (w - 2f - totalItemW) : 2f;
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
                _itemRects[i] = default;
                pos += sep;
                continue;
            }

            float iw  = ItemWidth(item, renderer);
            var   itemR = Orientation == SliderOrientation.Horizontal
                ? new Rect(pos, 2f, iw, h - 4f)
                : new Rect(2f, pos, w - 4f, iw);

            _itemRects[i] = itemR;

            bool hov      = i == _hoveredIdx && item.Enabled;
            bool toggled   = item.Pressed && item.Type == ToolBarItemType.Toggle;
            bool mouseDown = i == _pressedIdx && item.Enabled;

            if (mouseDown)
            {
                // Actively being pressed — sunken look
                var downGrad = renderer.LinearGradient(
                    new Vector2(itemR.X, itemR.Y), new Vector2(itemR.X, itemR.Bottom),
                    theme.SurfaceVariant.Darken(0.12f), theme.SurfaceVariant.Darken(0.04f));
                renderer.FillRoundedRectWithPaint(itemR, cr, downGrad);
                renderer.StrokeRoundedRect(itemR, cr, 1f, UIColor.Black.WithAlpha(0.25f));
            }
            else if (toggled)
            {
                // Active toggle — accent inset
                var pressGrad = renderer.LinearGradient(
                    new Vector2(itemR.X, itemR.Y), new Vector2(itemR.X, itemR.Bottom),
                    theme.AccentColor.Darken(0.15f), theme.AccentColor.WithAlpha(0.18f));
                renderer.FillRoundedRectWithPaint(itemR, cr, pressGrad);
                renderer.StrokeRoundedRect(itemR, cr, 1f, theme.AccentColor.WithAlpha(0.35f));
            }
            else if (hov)
            {
                // Hover — raised gradient + bevel
                var hovGrad = renderer.LinearGradient(
                    new Vector2(itemR.X, itemR.Y), new Vector2(itemR.X, itemR.Bottom),
                    theme.SurfaceVariant.Lighten(0.12f), theme.SurfaceVariant.Darken(0.04f));
                renderer.FillRoundedRectWithPaint(itemR, cr, hovGrad);
                renderer.StrokeRoundedRect(itemR, cr, 1f, UIColor.White.WithAlpha(0.10f));
            }
            else
            {
                // Normal — subtle raised button so items are visually distinct
                var normGrad = renderer.LinearGradient(
                    new Vector2(itemR.X, itemR.Y), new Vector2(itemR.X, itemR.Bottom),
                    theme.SurfaceVariant.Lighten(0.06f), theme.SurfaceVariant.Darken(0.03f));
                renderer.FillRoundedRectWithPaint(itemR, cr, normGrad);
                renderer.StrokeRoundedRect(itemR, cr, 1f, UIColor.Black.WithAlpha(0.12f));
            }

            // Label
            var labelColor = !item.Enabled ? theme.TextDisabledColor
                            : toggled   ? theme.AccentColor
                            : mouseDown ? theme.TextColor
                            : hov       ? theme.TextColor
                            :             theme.TextMutedColor;

            float cx = itemR.X + itemR.Width  * 0.5f;
            float cy = itemR.Y + itemR.Height * 0.5f;
            renderer.SetTextAlign(TextHAlign.Center);
            renderer.DrawText(cx, cy, item.Label, labelColor);

            pos += iw + 2f;
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; _hoveredIdx = -1; Tooltip = null; return true; }

    public override bool OnMouseMove(MouseEvent e)
    {
        _hoveredIdx = HitItem(e.LocalPosition);
        // Expose hovered item's tooltip so the Screen tooltip pass picks it up.
        Tooltip = _hoveredIdx >= 0 ? _items[_hoveredIdx].Tooltip : null;
        return _hoveredIdx >= 0;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        int idx = HitItem(e.LocalPosition);
        if (idx < 0) return false;
        var item = _items[idx];
        if (!item.Enabled) return true;

        _pressedIdx = idx;
        return true;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        if (_pressedIdx < 0) return false;
        int idx = _pressedIdx;
        _pressedIdx = -1;

        // Only fire if released over the same item
        int hitIdx = HitItem(e.LocalPosition);
        if (hitIdx == idx && idx >= 0 && idx < _items.Count)
        {
            var item = _items[idx];
            if (item.Enabled)
            {
                if (item.Type == ToolBarItemType.Toggle)
                    item.Pressed = !item.Pressed;
                item.OnClick?.Invoke();
            }
        }
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
        for (int i = 0; i < _itemRects.Length; i++)
        {
            if (_items[i].Type == ToolBarItemType.Separator) continue;
            if (_itemRects[i].Contains(pos)) return i;
        }
        return -1;
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.SmallFontSize);
    }
}
