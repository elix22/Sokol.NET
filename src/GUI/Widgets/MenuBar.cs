using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>A single menu item (button, separator, or sub-menu header).</summary>
public sealed class MenuItem
{
    public string        Label     { get; set; } = string.Empty;
    /// <summary>Optional icon identifier for custom-rendered menu glyphs (e.g. "cube").</summary>
    public string?       IconId    { get; set; }
    public string?       Shortcut  { get; set; }
    public bool          IsSep     { get; set; }
    public bool          Enabled   { get; set; } = true;
    public bool          IsChecked { get; set; }
    public Action?       OnClick   { get; set; }
    /// <summary>Non-null → this item opens a sub-menu.</summary>
    public List<MenuItem>? SubItems { get; set; }
    /// <summary>Called once when the sub-menu is about to be shown (populate SubItems here).</summary>
    public Action?         OnOpen   { get; set; }
}

/// <summary>
/// A menu row (or menu-bar header) as it was last drawn, in screen space. Lets UI automation find a
/// menu item by its label path ("File/New Scene", "3D Object/Box") and click it with real input.
/// </summary>
public readonly record struct DrawnMenuItem(string Path, Rect ScreenRect, bool Enabled, bool HasSubItems);

/// <summary>
/// Horizontal menu bar with drop-down menus.
/// Each top-level entry is a string that opens a list of <see cref="MenuItem"/>s.
/// </summary>
public class MenuBar : Widget
{
    public sealed class MenuEntry
    {
        public string           Header  { get; set; } = string.Empty;
        public List<MenuItem>   Items   { get; }      = [];
    }

    private readonly List<MenuEntry> _menus   = [];
    private int        _openIdx        = -1;   // index of open top-level menu
    private int        _hoveredIdx     = -1;  // hovered top-level entry
    private int        _openSubItemIdx = -1;  // index (in items list) of item with open sub-menu
    private int        _subHoveredIdx  = -1;  // index of hovered item within the sub-menu

    public const float DefaultHeight = 26f;

    public IReadOnlyList<MenuEntry> Menus => _menus;

    // Headers (rebuilt by Draw) followed by the open drop-down's rows (appended by DrawPopupOverlay).
    private readonly List<DrawnMenuItem> _drawn = [];
    private int _drawnHeaders;   // DrawPopupOverlay may run more than once per frame: rows restart after the headers
    /// <summary>Headers and open drop-down rows as last drawn (see <see cref="DrawnMenuItem"/>).</summary>
    public IReadOnlyList<DrawnMenuItem> DrawnItems => _drawn;

    public Font?  Font     { get; set; }
    public float  FontSize { get; set; } = 0f;

    // ─── API ─────────────────────────────────────────────────────────────────
    public MenuEntry AddMenu(string header)
    {
        var entry = new MenuEntry { Header = header };
        _menus.Add(entry);
        return entry;
    }

    // ─── PreferredSize ────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
        => FixedSize ?? new Vector2(Bounds.Width, DefaultHeight);

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var   theme = ThemeManager.Current;
        float w = Bounds.Width, h = Bounds.Height;

        renderer.FillRect(new Rect(0, 0, w, h), theme.SurfaceVariant);
        renderer.DrawLine(0, h, w, h, 1f, theme.BorderColor);

        ApplyFont(renderer, theme);

        bool rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
        // Measure total width for RTL start position
        float totalMenuW = 0;
        for (int i = 0; i < _menus.Count; i++)
            totalMenuW += renderer.MeasureText(_menus[i].Header) + 16f + 2f;

        float x = rtl ? (w - 4f - totalMenuW) : 4f;
        _drawn.Clear();
        _drawnHeaders = 0;
        var sp = ScreenPosition;
        for (int i = 0; i < _menus.Count; i++)
        {
            string header = _menus[i].Header;
            float  tw     = renderer.MeasureText(header);
            float  iw     = tw + 16f;
            var    itemR  = new Rect(x, 2f, iw, h - 4f);
            _drawn.Add(new DrawnMenuItem(header, new Rect(sp.X + x, sp.Y, iw, h), true, true));
            _drawnHeaders = _drawn.Count;

            bool open = i == _openIdx;
            bool hov  = i == _hoveredIdx;

            if (open)
                renderer.FillRoundedRect(itemR, 3f, theme.AccentColor);
            else if (hov)
                renderer.FillRoundedRect(itemR, 3f, theme.ButtonHoverColor.WithAlpha(0.4f));

            var textC = open ? UIColor.White : theme.TextColor;
            renderer.SetTextAlign(TextHAlign.Center);
            renderer.DrawText(x + iw * 0.5f, h * 0.5f, header, textC);

            x += iw + 2f;
        }
    }

    /// <summary>Renders the open drop-down menu as a popup overlay.</summary>
    public override void DrawPopupOverlay(Renderer renderer)
    {
        if (_openIdx < 0 || _openIdx >= _menus.Count) return;
        var   theme  = ThemeManager.Current;
        var   items  = _menus[_openIdx].Items;
        float itemH  = 26f;
        float sepH   = 8f;
        float ddW    = CalcDropWidth(items, renderer, theme);
        float ddH    = CalcDropHeight(items, itemH, sepH);
        float ddX    = MenuHeaderX(_openIdx, renderer);
        float ddY    = Bounds.Height;

        var ddRect = new Rect(ddX, ddY, ddW, ddH);
        var sp     = ScreenPosition;
        _drawn.RemoveRange(_drawnHeaders, _drawn.Count - _drawnHeaders);
        renderer.DrawDropShadow(ddRect, 4f, new Vector2(2f, 3f), 8f,
            new UIColor(0f, 0f, 0f, 0.25f));
        renderer.FillRoundedRect(ddRect, 4f, theme.SurfaceVariant);
        renderer.StrokeRoundedRect(ddRect, 4f, 1f, theme.BorderColor);

        ApplyFont(renderer, theme);
        float y = ddY;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.IsSep)
            {
                renderer.DrawLine(ddX + 6, y + sepH * 0.5f, ddX + ddW - 6, y + sepH * 0.5f,
                    1f, theme.BorderColor.WithAlpha(0.5f));
                y += sepH;
                continue;
            }
            var  rowR       = new Rect(ddX, y, ddW, itemH);
            _drawn.Add(new DrawnMenuItem(_menus[_openIdx].Header + "/" + item.Label,
                new Rect(sp.X + ddX, sp.Y + y, ddW, itemH), item.Enabled, item.SubItems != null));
            bool isSubOpen  = i == _openSubItemIdx;
            bool isHov      = isSubOpen || rowR.Contains(new Vector2(_lastMousePos.X, _lastMousePos.Y));
            if (isHov && item.Enabled)
                renderer.FillRoundedRect(new Rect(ddX + 2, y, ddW - 4, itemH), 3f,
                    theme.SelectionColor);

            var textC = !item.Enabled ? theme.TextDisabledColor : theme.TextColor;
            renderer.SetTextAlign(TextHAlign.Left);
            renderer.DrawText(ddX + 10, y + itemH * 0.5f, item.Label, textC);

            if (item.SubItems != null)
            {
                renderer.SetTextAlign(TextHAlign.Right);
                renderer.DrawText(ddX + ddW - 8, y + itemH * 0.5f, "▶", textC);
            }
            else
            {
                if (!string.IsNullOrEmpty(item.Shortcut))
                {
                    renderer.SetTextAlign(TextHAlign.Right);
                    renderer.DrawText(ddX + ddW - 10, y + itemH * 0.5f,
                        item.Shortcut, theme.TextMutedColor);
                }
                if (item.IsChecked)
                {
                    renderer.SetTextAlign(TextHAlign.Left);
                    renderer.DrawText(ddX + ddW - 18, y + itemH * 0.5f, "✓", theme.AccentColor);
                }
            }

            y += itemH;
        }

        // ── Sub-menu panel ───────────────────────────────────────────────────
        if (_openSubItemIdx >= 0 && _openSubItemIdx < items.Count)
        {
            var subRect = CalcSubMenuRect(_openSubItemIdx, renderer);
            if (subRect.HasValue)
            {
                var  sr       = subRect.Value;
                var  subItems = items[_openSubItemIdx].SubItems!;
                float subW    = sr.Width;

                renderer.DrawDropShadow(sr, 4f, new Vector2(2f, 3f), 8f,
                    new UIColor(0f, 0f, 0f, 0.25f));
                renderer.FillRoundedRect(sr, 4f, theme.SurfaceVariant);
                renderer.StrokeRoundedRect(sr, 4f, 1f, theme.BorderColor);

                float sy = sr.Y;
                for (int j = 0; j < subItems.Count; j++)
                {
                    var sit = subItems[j];
                    if (sit.IsSep)
                    {
                        renderer.DrawLine(sr.X + 6, sy + sepH * 0.5f, sr.X + subW - 6, sy + sepH * 0.5f,
                            1f, theme.BorderColor.WithAlpha(0.5f));
                        sy += sepH;
                        continue;
                    }
                    var  sRowR = new Rect(sr.X, sy, subW, itemH);
                    _drawn.Add(new DrawnMenuItem(
                        _menus[_openIdx].Header + "/" + items[_openSubItemIdx].Label + "/" + sit.Label,
                        new Rect(sp.X + sr.X, sp.Y + sy, subW, itemH), sit.Enabled, false));
                    bool sHov  = j == _subHoveredIdx || sRowR.Contains(new Vector2(_lastMousePos.X, _lastMousePos.Y));
                    if (sHov && sit.Enabled)
                        renderer.FillRoundedRect(new Rect(sr.X + 2, sy, subW - 4, itemH), 3f,
                            theme.SelectionColor);

                    var stextC = !sit.Enabled ? theme.TextDisabledColor : theme.TextColor;
                    renderer.SetTextAlign(TextHAlign.Left);
                    renderer.DrawText(sr.X + 10, sy + itemH * 0.5f, sit.Label, stextC);

                    if (!string.IsNullOrEmpty(sit.Shortcut))
                    {
                        renderer.SetTextAlign(TextHAlign.Right);
                        renderer.DrawText(sr.X + subW - 10, sy + itemH * 0.5f,
                            sit.Shortcut, theme.TextMutedColor);
                    }

                    sy += itemH;
                }
            }
        }
    }

    public override bool HitTest(Vector2 localPoint)
    {
        if (_openIdx >= 0)
        {
            // Extend hit-test to include the drop-down area (and sub-menu if open)
            var renderer = Screen.Instance?.Renderer;
            if (renderer != null)
            {
                var  items  = _menus[_openIdx].Items;
                float itemH = 26f, sepH = 8f;
                float ddW   = CalcDropWidth(items, renderer, ThemeManager.Current);
                float ddH   = CalcDropHeight(items, itemH, sepH);
                float ddX   = MenuHeaderX(_openIdx, renderer);
                var   ddRect = new Rect(ddX, Bounds.Height, ddW, ddH);
                if (ddRect.Contains(localPoint)) return true;
                // Sub-menu area
                if (_openSubItemIdx >= 0)
                {
                    var subRect = CalcSubMenuRect(_openSubItemIdx, renderer);
                    if (subRect.HasValue && subRect.Value.Contains(localPoint)) return true;
                }
            }
        }
        return base.HitTest(localPoint);
    }

    public override void OnPopupDismiss()
    {
        _openIdx        = -1;
        _hoveredIdx     = -1;
        _openSubItemIdx = -1;
        _subHoveredIdx  = -1;
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    private Vector2 _lastMousePos;

    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e)
    {
        IsHovered = false;
        if (_openIdx < 0) _hoveredIdx = -1;
        return false;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        _lastMousePos = e.LocalPosition;
        int idx = HitHeader(e.Position);
        if (idx >= 0) _hoveredIdx = idx;

        // If a menu is open and user hovers another header → switch to it
        if (_openIdx >= 0 && idx >= 0 && idx != _openIdx)
        {
            _openIdx        = idx;
            _hoveredIdx     = idx;
            _openSubItemIdx = -1;
            _subHoveredIdx  = -1;
        }

        // Track sub-menu state when a dropdown is open and no header is hovered
        if (_openIdx >= 0 && idx < 0)
        {
            var renderer = Screen.Instance?.Renderer;
            if (renderer != null)
            {
                var local = _lastMousePos;

                // If in the sub-menu area, track which sub-item is hovered
                if (_openSubItemIdx >= 0)
                {
                    var subRect = CalcSubMenuRect(_openSubItemIdx, renderer);
                    if (subRect.HasValue && subRect.Value.Contains(local))
                    {
                        var   subItems = _menus[_openIdx].Items[_openSubItemIdx].SubItems!;
                        float itemH = 26f, sepH = 8f;
                        float sy    = subRect.Value.Y;
                        _subHoveredIdx = -1;
                        for (int j = 0; j < subItems.Count; j++)
                        {
                            float rowH = subItems[j].IsSep ? sepH : itemH;
                            if (new Rect(subRect.Value.X, sy, subRect.Value.Width, rowH).Contains(local))
                            { _subHoveredIdx = j; break; }
                            sy += rowH;
                        }
                        return true;
                    }
                }

                // In the main drop-down: update which sub-menu (if any) is open
                int newSubIdx = GetDropItemIndex(local, renderer);
                if (newSubIdx >= 0)
                {
                    var item = _menus[_openIdx].Items[newSubIdx];
                    if (item.SubItems != null)
                    {
                        if (_openSubItemIdx != newSubIdx)
                        {
                            _openSubItemIdx = newSubIdx;
                            _subHoveredIdx  = -1;
                            item.OnOpen?.Invoke();
                        }
                    }
                    else
                    {
                        _openSubItemIdx = -1;
                        _subHoveredIdx  = -1;
                    }
                }
            }
        }

        return true;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;

        int idx = HitHeader(e.Position);

        if (_openIdx >= 0)
        {
            // Check if click hit a drop-down item
            var renderer = Screen.Instance?.Renderer;
            if (renderer != null && HitDropItem(e.Position, renderer, out var item))
            {
                // Sub-menu parent: keep menu open (sub-menu opens on hover, not click)
                if (item != null && item.SubItems != null)
                    return true;
                if (item != null && item.Enabled && !item.IsSep)
                    item.OnClick?.Invoke();
            }
            _openIdx        = -1;
            _hoveredIdx     = -1;
            _openSubItemIdx = -1;
            _subHoveredIdx  = -1;
            Screen.SetActivePopup(null);
            return true;
        }

        if (idx >= 0)
        {
            _openIdx    = idx;
            _hoveredIdx = idx;
            Screen.SetActivePopup(this);
            return true;
        }
        return false;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private float MenuHeaderX(int idx, Renderer renderer)
    {
        ApplyFont(renderer, ThemeManager.Current);   // the headers are measured in the font they are drawn in
        float x = 4f;
        for (int i = 0; i < idx; i++)
        {
            float tw = renderer.MeasureText(_menus[i].Header);
            x += tw + 16f + 2f;
        }
        return x;
    }

    private int HitHeader(Vector2 screenPos)
    {
        var   local = ToLocal(screenPos);
        var   renderer = Screen.Instance?.Renderer;
        if (renderer == null) return -1;
        ApplyFont(renderer, ThemeManager.Current);   // the headers are measured in the font they are drawn in

        float x = 4f;
        float h = Bounds.Height;
        for (int i = 0; i < _menus.Count; i++)
        {
            float tw = renderer.MeasureText(_menus[i].Header);
            float iw = tw + 16f;
            var   r  = new Rect(x, 0, iw, h);
            if (r.Contains(local)) return i;
            x += iw + 2f;
        }
        return -1;
    }

    private bool HitDropItem(Vector2 screenPos, Renderer renderer, out MenuItem? item)
    {
        item = null;
        if (_openIdx < 0) return false;
        var local = ToLocal(screenPos);

        // Check sub-menu first (takes priority over the main dropdown)
        if (_openSubItemIdx >= 0)
        {
            var subRect = CalcSubMenuRect(_openSubItemIdx, renderer);
            if (subRect.HasValue && subRect.Value.Contains(local))
            {
                var   subItems = _menus[_openIdx].Items[_openSubItemIdx].SubItems!;
                float itemH = 26f, sepH = 8f;
                float sy    = subRect.Value.Y;
                foreach (var it in subItems)
                {
                    float rowH = it.IsSep ? sepH : itemH;
                    if (new Rect(subRect.Value.X, sy, subRect.Value.Width, rowH).Contains(local))
                    { item = it; return true; }
                    sy += rowH;
                }
                return false;
            }
        }

        // Main dropdown
        var items = _menus[_openIdx].Items;
        float ddW = CalcDropWidth(items, renderer, ThemeManager.Current);
        float ddX = MenuHeaderX(_openIdx, renderer);
        float ddY = Bounds.Height;
        float y   = ddY;
        foreach (var it in items)
        {
            float rowH = it.IsSep ? 8f : 26f;
            var   rowR = new Rect(ddX, y, ddW, rowH);
            if (rowR.Contains(local)) { item = it; return true; }
            y += rowH;
        }
        return false;
    }

    private static float CalcDropWidth(List<MenuItem> items, Renderer renderer, Theme theme)
    {
        float max = 120f;
        foreach (var it in items)
        {
            if (it.IsSep) continue;
            float w = renderer.MeasureText(it.Label) + 20f;
            if (it.SubItems != null)
                w += 20f;  // space for ▶ arrow
            else if (!string.IsNullOrEmpty(it.Shortcut))
                w += renderer.MeasureText(it.Shortcut) + 20f;
            if (w > max) max = w;
        }
        return max;
    }

    private static float CalcDropHeight(List<MenuItem> items, float itemH, float sepH)
    {
        float h = 0f;
        foreach (var it in items) h += it.IsSep ? sepH : itemH;
        return h;
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0f ? FontSize : theme.SmallFontSize);
    }

    /// <summary>Returns the index of the drop-down item at <paramref name="localPos"/>, or -1.</summary>
    private int GetDropItemIndex(Vector2 localPos, Renderer renderer)
    {
        if (_openIdx < 0) return -1;
        var   items = _menus[_openIdx].Items;
        float itemH = 26f, sepH = 8f;
        float ddX   = MenuHeaderX(_openIdx, renderer);
        float ddW   = CalcDropWidth(items, renderer, ThemeManager.Current);
        float y     = Bounds.Height;
        for (int i = 0; i < items.Count; i++)
        {
            float rowH = items[i].IsSep ? sepH : itemH;
            if (new Rect(ddX, y, ddW, rowH).Contains(localPos)) return i;
            y += rowH;
        }
        return -1;
    }

    /// <summary>Returns the screen-local rect of the sub-menu for the given parent item index, or null.</summary>
    private Rect? CalcSubMenuRect(int parentIdx, Renderer renderer)
    {
        if (_openIdx < 0) return null;
        var items = _menus[_openIdx].Items;
        if (parentIdx < 0 || parentIdx >= items.Count) return null;
        var parent = items[parentIdx];
        if (parent.SubItems == null) return null;

        float itemH = 26f, sepH = 8f;
        float ddX   = MenuHeaderX(_openIdx, renderer);
        float ddW   = CalcDropWidth(items, renderer, ThemeManager.Current);
        float y     = Bounds.Height;
        for (int i = 0; i < parentIdx; i++)
            y += items[i].IsSep ? sepH : itemH;

        float subW = CalcDropWidth(parent.SubItems, renderer, ThemeManager.Current);
        float subH = CalcDropHeight(parent.SubItems, itemH, sepH);
        return new Rect(ddX + ddW, y, subW, subH);
    }
}
