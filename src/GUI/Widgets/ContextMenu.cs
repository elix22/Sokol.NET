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
        // Position: Screen will translate to ScreenPosition before drawing popup overlay.
        // We fake Bounds.X/Y so ToLocal works correctly:
        _instance.Bounds = new Rect(screenPos.X, screenPos.Y, 0f, 0f);
        Screen.SetActivePopup(_instance);
    }

    // ─── Instance state ───────────────────────────────────────────────────────
    private readonly List<MenuItem> _items  = [];
    private Vector2  _screenPos;
    private int      _hoveredIdx = -1;

    private const float ItemH = 26f;
    private const float SepH  = 8f;
    private const float Pad   = 4f;

    public Font?  Font     { get; set; }
    public float  FontSize { get; set; } = 0f;

    // ─── Popup drawing ────────────────────────────────────────────────────────
    public override void DrawPopupOverlay(Renderer renderer)
    {
        var   theme = ThemeManager.Current;
        float w     = CalcWidth(renderer, theme);
        float h     = CalcHeight();

        // Clamp popup position so it stays fully visible within the screen.
        // Screen already translated to the original _screenPos, so we apply a correction delta.
        var screen = Screen.Instance;
        float sx = _screenPos.X;
        float sy = _screenPos.Y;
        if (sx + w > screen.LogicalWidth)  sx = MathF.Max(0f, screen.LogicalWidth  - w);
        if (sy + h > screen.LogicalHeight) sy = MathF.Max(0f, screen.LogicalHeight - h);
        float dx = sx - _screenPos.X;
        float dy = sy - _screenPos.Y;
        if (dx != 0f || dy != 0f)
        {
            _screenPos = new Vector2(sx, sy);
            Bounds = new Rect(sx, sy, 0f, 0f);
            renderer.Translate(dx, dy);
        }

        var   popR  = new Rect(0, 0, w, h);

        renderer.DrawDropShadow(popR, 4f, new Vector2(2f, 3f), 8f,
            new UIColor(0f, 0f, 0f, 0.25f));
        renderer.FillRoundedRect(popR, 4f, theme.SurfaceVariant);
        renderer.StrokeRoundedRect(popR, 4f, 1f, theme.BorderColor);

        ApplyFont(renderer, theme);
        float y = Pad;
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.IsSep)
            {
                renderer.DrawLine(6, y + SepH * 0.5f, w - 6, y + SepH * 0.5f,
                    1f, theme.BorderColor.WithAlpha(0.5f));
                y += SepH;
                continue;
            }

            var rowR = new Rect(0, y, w, ItemH);
            if (i == _hoveredIdx && item.Enabled)
                renderer.FillRoundedRect(new Rect(2, y, w - 4, ItemH), 3f, theme.SelectionColor);

            var textC = !item.Enabled ? theme.TextDisabledColor : theme.TextColor;
            renderer.SetTextAlign(TextHAlign.Left);
            renderer.DrawText(10, y + ItemH * 0.5f, item.Label, textC);

            if (!string.IsNullOrEmpty(item.Shortcut))
            {
                renderer.SetTextAlign(TextHAlign.Right);
                renderer.DrawText(w - 10, y + ItemH * 0.5f, item.Shortcut, theme.TextMutedColor);
            }
            if (item.IsChecked)
                renderer.DrawText(w - 20, y + ItemH * 0.5f, "✓", theme.AccentColor);

            y += ItemH;
        }
    }

    public override bool HitTest(Vector2 localPoint)
    {
        var renderer = Screen.Instance?.Renderer;
        if (renderer == null) return false;
        float w = CalcWidth(renderer, ThemeManager.Current);
        float h = CalcHeight();
        return new Rect(0, 0, w, h).Contains(localPoint);
    }

    public override void OnPopupDismiss() { }   // nothing extra to clean up

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseMove(MouseEvent e)
    {
        var   local   = e.LocalPosition;
        float y       = Pad;
        int   newHov  = -1;
        for (int i = 0; i < _items.Count; i++)
        {
            float rh = _items[i].IsSep ? SepH : ItemH;
            if (!_items[i].IsSep)
            {
                var renderer = Screen.Instance?.Renderer;
                float w = renderer != null ? CalcWidth(renderer, ThemeManager.Current) : 200f;
                if (new Rect(0, y, w, rh).Contains(local)) { newHov = i; break; }
            }
            y += rh;
        }
        _hoveredIdx = newHov;
        return true;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        var   local   = e.LocalPosition;
        float y       = Pad;
        for (int i = 0; i < _items.Count; i++)
        {
            float rh = _items[i].IsSep ? SepH : ItemH;
            if (!_items[i].IsSep)
            {
                var renderer = Screen.Instance?.Renderer;
                float w = renderer != null ? CalcWidth(renderer, ThemeManager.Current) : 200f;
                if (new Rect(0, y, w, rh).Contains(local))
                {
                    var item = _items[i];
                    Screen.SetActivePopup(null);
                    if (item.Enabled) item.OnClick?.Invoke();
                    return true;
                }
            }
            y += rh;
        }
        Screen.SetActivePopup(null);
        return true;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private float CalcWidth(Renderer renderer, Theme theme)
    {
        float max = 120f;
        foreach (var item in _items)
        {
            if (item.IsSep) continue;
            float w = renderer.MeasureText(item.Label) + 20f;
            if (!string.IsNullOrEmpty(item.Shortcut))
                w += renderer.MeasureText(item.Shortcut) + 20f;
            if (w > max) max = w;
        }
        return max;
    }

    private float CalcHeight()
    {
        float h = Pad * 2f;
        foreach (var item in _items) h += item.IsSep ? SepH : ItemH;
        return h;
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0f ? FontSize : theme.SmallFontSize);
    }
}
