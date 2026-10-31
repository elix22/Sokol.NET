using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// A floating context menu (right-click menu) that can be shown at any screen position.
/// Usage: <c>ContextMenu.Show(items, screenPosition);</c>
/// </summary>
public sealed class ContextMenu : Widget
{
    // ─── Static singleton popup ───────────────────────────────────────────────
    private static ContextMenu? _instance;

    /// <summary>Show a context menu at <paramref name="screenPos"/> with <paramref name="items"/>.</summary>
    public static void Show(IEnumerable<MenuItem> items, Vector2 screenPos)
    {
        _instance ??= new ContextMenu();
        _instance._items.Clear();
        _instance._items.AddRange(items);
        _instance._screenPos = screenPos;
        _instance._hoveredIdx = -1;
        _instance._submenuHoveredIdx = -1;
        _instance._openSubmenuFromIdx = -1;
        _instance._openSubmenuItems = null;
        _instance._submenuRect = default;
        _instance._rootW = 0f;
        _instance._rootH = 0f;
        // Position: Screen will translate to ScreenPosition before drawing popup overlay.
        // We fake Bounds.X/Y so ToLocal works correctly:
        _instance.Bounds = new Rect(screenPos.X, screenPos.Y, 0f, 0f);
        Screen.SetActivePopup(_instance);
    }

    /// <summary>The context menu currently shown, or null when none is open.</summary>
    public static ContextMenu? Open => _instance != null && Screen.ActivePopup == _instance ? _instance : null;

    // ─── Instance state ───────────────────────────────────────────────────────
    private readonly List<MenuItem> _items = [];
    private readonly List<DrawnMenuItem> _drawn = [];
    /// <summary>Rows as last drawn, root then open sub-menu (see <see cref="DrawnMenuItem"/>).</summary>
    public IReadOnlyList<DrawnMenuItem> DrawnItems => _drawn;
    private Vector2 _screenPos;
    private int _hoveredIdx = -1;
    private int _submenuHoveredIdx = -1;
    private int _openSubmenuFromIdx = -1;
    private List<MenuItem>? _openSubmenuItems;
    private Rect _submenuRect;
    private float _rootW;
    private float _rootH;

    // Unity-like tighter spacing.
    private const float ItemH = 22f;
    private const float SepH = 6f;
    private const float Pad = 2f;

    public Font?  Font     { get; set; }
    public float  FontSize { get; set; } = 0f;

    // ─── Popup drawing ────────────────────────────────────────────────────────
    public override void DrawPopupOverlay(Renderer renderer)
    {
        var theme = ThemeManager.Current;
        float w = CalcWidth(_items, renderer, theme);
        float h = CalcHeight(_items);
        _rootW = w;
        _rootH = h;

        // Clamp popup position so it stays fully visible within the screen.
        // Screen already translated to the original _screenPos, so we apply a correction delta.
        var screen = Screen.Instance;
        float sx = _screenPos.X;
        float sy = _screenPos.Y;
        if (sx + w > screen.LogicalWidth) sx = MathF.Max(0f, screen.LogicalWidth - w);
        if (sy + h > screen.LogicalHeight) sy = MathF.Max(0f, screen.LogicalHeight - h);
        float dx = sx - _screenPos.X;
        float dy = sy - _screenPos.Y;
        if (dx != 0f || dy != 0f)
        {
            _screenPos = new Vector2(sx, sy);
            Bounds = new Rect(sx, sy, 0f, 0f);
            renderer.Translate(dx, dy);
        }

        var popR = new Rect(0, 0, w, h);

        renderer.DrawDropShadow(popR, 2f, new Vector2(1f, 2f), 8f,
            new UIColor(0f, 0f, 0f, 0.35f));
        renderer.FillRoundedRect(popR, 2f, theme.SurfaceVariant.Darken(0.03f));
        renderer.StrokeRoundedRect(popR, 2f, 1f, theme.BorderColor.WithAlpha(0.9f));

        ApplyFont(renderer, theme);
        _drawn.Clear();
        DrawMenu(renderer, _items, new Rect(0, 0, w, h), _hoveredIdx, theme, drawSubmenuArrows: true, parentPath: null);
        DrawSubmenuIfNeeded(renderer, theme);
    }

    public override bool HitTest(Vector2 localPoint)
    {
        bool inRoot = new Rect(0, 0, _rootW, _rootH).Contains(localPoint);
        bool inSub = _openSubmenuItems != null && _submenuRect.Contains(localPoint);
        return inRoot || inSub;
    }

    public override void OnPopupDismiss() { }   // nothing extra to clean up

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseMove(MouseEvent e)
    {
        var local = e.LocalPosition;

        _submenuHoveredIdx = -1;
        if (_openSubmenuItems != null && _submenuRect.Contains(local))
        {
            _submenuHoveredIdx = HitIndex(_openSubmenuItems, local, _submenuRect);
            return true;
        }

        _hoveredIdx = HitIndex(_items, local, new Rect(0, 0, _rootW, _rootH));
        if (_hoveredIdx >= 0 && _items[_hoveredIdx].SubItems is { Count: > 0 } sub)
        {
            _openSubmenuFromIdx = _hoveredIdx;
            _openSubmenuItems = sub;
        }
        else
        {
            _openSubmenuFromIdx = -1;
            _openSubmenuItems = null;
            _submenuRect = default;
        }
        return true;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        var local = e.LocalPosition;

        if (_openSubmenuItems != null && _submenuRect.Contains(local))
        {
            int idx = HitIndex(_openSubmenuItems, local, _submenuRect);
            if (idx >= 0)
            {
                var item = _openSubmenuItems[idx];
                Screen.SetActivePopup(null);
                if (item.Enabled) item.OnClick?.Invoke();
            }
            return true;
        }

        int rootIdx = HitIndex(_items, local, new Rect(0, 0, _rootW, _rootH));
        if (rootIdx >= 0)
        {
            var item = _items[rootIdx];
            if (item.SubItems is { Count: > 0 })
            {
                _openSubmenuFromIdx = rootIdx;
                _openSubmenuItems = item.SubItems;
                return true;
            }

            Screen.SetActivePopup(null);
            if (item.Enabled) item.OnClick?.Invoke();
            return true;
        }

        Screen.SetActivePopup(null);
        return true;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private float CalcWidth(List<MenuItem> items, Renderer renderer, Theme theme)
    {
        float max = 120f;
        foreach (var item in items)
        {
            if (item.IsSep) continue;
            float w = renderer.MeasureText(item.Label) + 24f;
            if (!string.IsNullOrEmpty(item.IconId))
                w += 14f;
            if (!string.IsNullOrEmpty(item.Shortcut))
                w += renderer.MeasureText(item.Shortcut) + 20f;
            if (item.SubItems is { Count: > 0 })
                w += 12f;
            if (w > max) max = w;
        }
        return max;
    }

    private float CalcHeight(List<MenuItem> items)
    {
        float h = Pad * 2f;
        foreach (var item in items) h += item.IsSep ? SepH : ItemH;
        return h;
    }

    private int HitIndex(List<MenuItem> items, Vector2 local, Rect menuRect)
    {
        float y = menuRect.Y + Pad;
        for (int i = 0; i < items.Count; i++)
        {
            float rh = items[i].IsSep ? SepH : ItemH;
            if (!items[i].IsSep && new Rect(menuRect.X, y, menuRect.Width, rh).Contains(local))
                return i;
            y += rh;
        }
        return -1;
    }

    private void DrawMenu(Renderer renderer, List<MenuItem> items, Rect rect, int hoveredIdx, Theme theme, bool drawSubmenuArrows,
                          string? parentPath)
    {
        float y = rect.Y + Pad;
        var sp = ScreenPosition;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.IsSep)
            {
                renderer.DrawLine(rect.X + 6, y + SepH * 0.5f, rect.X + rect.Width - 6, y + SepH * 0.5f,
                    1f, theme.BorderColor.WithAlpha(0.5f));
                y += SepH;
                continue;
            }

            _drawn.Add(new DrawnMenuItem(parentPath == null ? item.Label : parentPath + "/" + item.Label,
                new Rect(sp.X + rect.X, sp.Y + y, rect.Width, ItemH), item.Enabled, item.SubItems is { Count: > 0 }));

            if (i == hoveredIdx && item.Enabled)
                renderer.FillRoundedRect(new Rect(rect.X + 1, y, rect.Width - 2, ItemH), 2f, theme.SelectionColor);

            var textC = !item.Enabled ? theme.TextDisabledColor : theme.TextColor;
            float textX = rect.X + 10f;
            if (!string.IsNullOrEmpty(item.IconId))
            {
                float glyphSize = 10f;
                float glyphX = rect.X + 7f;
                float glyphY = y + (ItemH - glyphSize) * 0.5f;
                var iconColor = item.Enabled ? theme.TextMutedColor : theme.TextDisabledColor;
                if (item.IconId == "cube")
                    DrawCubeGlyph(renderer, glyphX, glyphY, glyphSize, iconColor);
                textX += 14f;
            }

            renderer.SetTextAlign(TextHAlign.Left);
            renderer.DrawText(textX, y + ItemH * 0.5f, item.Label, textC);

            if (!string.IsNullOrEmpty(item.Shortcut))
            {
                renderer.SetTextAlign(TextHAlign.Right);
                renderer.DrawText(rect.X + rect.Width - 20, y + ItemH * 0.5f, item.Shortcut, theme.TextMutedColor);
            }
            if (item.IsChecked)
                renderer.DrawText(rect.X + rect.Width - 20, y + ItemH * 0.5f, "✓", theme.AccentColor);

            if (drawSubmenuArrows && item.SubItems is { Count: > 0 })
            {
                renderer.SetTextAlign(TextHAlign.Right);
                renderer.DrawText(rect.X + rect.Width - 10, y + ItemH * 0.5f, "▸", theme.TextMutedColor);
            }

            y += ItemH;
        }
    }

    private static void DrawCubeGlyph(Renderer renderer, float x, float y, float size, UIColor color)
    {
        // Tiny isometric wireframe cube.
        float cx = x + size * 0.5f;
        float topY = y;
        float midY = y + size * 0.28f;
        float botY = y + size * 0.56f;
        float lowY = y + size * 0.84f;
        float rightX = x + size;
        float leftX = x;

        // Top diamond
        renderer.DrawLine(cx, topY, rightX, midY, 1f, color);
        renderer.DrawLine(rightX, midY, cx, botY, 1f, color);
        renderer.DrawLine(cx, botY, leftX, midY, 1f, color);
        renderer.DrawLine(leftX, midY, cx, topY, 1f, color);

        // Down edges
        renderer.DrawLine(leftX, midY, leftX, lowY, 1f, color);
        renderer.DrawLine(rightX, midY, rightX, lowY, 1f, color);
        renderer.DrawLine(cx, botY, cx, y + size, 1f, color);

        // Bottom edges
        renderer.DrawLine(leftX, lowY, cx, y + size, 1f, color);
        renderer.DrawLine(cx, y + size, rightX, lowY, 1f, color);
        renderer.DrawLine(leftX, lowY, rightX, lowY, 1f, color.WithAlpha(0.45f));
    }

    private void DrawSubmenuIfNeeded(Renderer renderer, Theme theme)
    {
        if (_openSubmenuItems is not { Count: > 0 }) return;
        if (_openSubmenuFromIdx < 0 || _openSubmenuFromIdx >= _items.Count) return;

        float rowTop = Pad;
        for (int i = 0; i < _openSubmenuFromIdx; i++)
            rowTop += _items[i].IsSep ? SepH : ItemH;

        float subW = CalcWidth(_openSubmenuItems, renderer, theme);
        float subH = CalcHeight(_openSubmenuItems);

        float subX = _rootW - 1f;
        if (_screenPos.X + subX + subW > Screen.Instance.LogicalWidth)
            subX = -subW + 1f;

        float subY = rowTop;
        float absY = _screenPos.Y + subY;
        if (absY + subH > Screen.Instance.LogicalHeight)
            subY -= (absY + subH - Screen.Instance.LogicalHeight);
        if (_screenPos.Y + subY < 0f)
            subY = -_screenPos.Y;

        _submenuRect = new Rect(subX, subY, subW, subH);

        renderer.DrawDropShadow(_submenuRect, 2f, new Vector2(1f, 2f), 8f,
            new UIColor(0f, 0f, 0f, 0.35f));
        renderer.FillRoundedRect(_submenuRect, 2f, theme.SurfaceVariant.Darken(0.03f));
        renderer.StrokeRoundedRect(_submenuRect, 2f, 1f, theme.BorderColor.WithAlpha(0.9f));

        DrawMenu(renderer, _openSubmenuItems, _submenuRect, _submenuHoveredIdx, theme, drawSubmenuArrows: false,
                 parentPath: _items[_openSubmenuFromIdx].Label);
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0f ? FontSize : theme.SmallFontSize);
    }
}
