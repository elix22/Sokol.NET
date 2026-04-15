using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Tabbed container.  Each tab is a header button + a content widget.
/// </summary>
public class TabView : Widget
{
    private int _selectedIndex = -1;

    private readonly List<(string Title, Widget Content)> _tabs = [];

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int prev = _selectedIndex;
            _selectedIndex = _tabs.Count == 0 ? -1 : Math.Clamp(value, 0, _tabs.Count - 1);
            if (prev != _selectedIndex)
            {
                // Toggle visibility so base HitTestDeep only finds the active tab's content.
                if (prev >= 0 && prev < _tabs.Count)           _tabs[prev].Content.Visible       = false;
                if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count) _tabs[_selectedIndex].Content.Visible = true;
                SelectionChanged?.Invoke(_selectedIndex);
            }
        }
    }

    public event Action<int>? SelectionChanged;
    public Font?   Font     { get; set; }
    public float   FontSize { get; set; } = 0f;

    public void AddTab(string title, Widget content)
    {
        // Wire content into the ScreenPosition parent chain so ToLocal / HitTestDeep work.
        // We intentionally bypass AddChild to keep content out of the CanvasLayout pass.
        content.Parent  = this;
        content.Visible = (_tabs.Count == 0);  // only the first tab starts visible
        _tabs.Add((title, content));
        if (_selectedIndex < 0) _selectedIndex = 0;
    }

    public void RemoveTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _tabs.RemoveAt(index);
        if (_selectedIndex >= _tabs.Count) _selectedIndex = _tabs.Count - 1;
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        float hdrH = theme.TabBarHeight;
        float w    = Bounds.Width, h = Bounds.Height;

        bool log = Screen.DbgFrame <= 5 || Screen.DbgFrame % 300 == 0;
        if (log)
            Sokol.SLog.Info($"TabView.Draw[{Screen.DbgFrame}]: Bounds={Bounds} w={w} h={h} hdrH={hdrH} tabs={_tabs.Count} sel={_selectedIndex}", "Sokol.GUI");

        // ── 1. Full-widget content background ─────────────────────────────────
        renderer.FillRect(new Rect(0, 0, w, h), theme.SurfaceColor);

        // ── 2. Tab bar strip background (slightly darker than surface) ─────────
        var tabStripRect = new Rect(0, 0, w, hdrH);
        renderer.FillRect(tabStripRect, theme.TabBarColor);

        // ── 3. Measure all tab widths first ────────────────────────────────────
        ApplyFont(renderer, theme);
        renderer.SetTextAlign(TextHAlign.Left, TextVAlign.Middle);
        float[] tabWidths = new float[_tabs.Count];
        for (int i = 0; i < _tabs.Count; i++)
            tabWidths[i] = renderer.MeasureText(_tabs[i].Title) + theme.TabPaddingH * 2;

        // ── 4. Separator line (drawn before tabs so active tab can cover it) ───
        renderer.DrawLine(0, hdrH, w, hdrH, 1f, theme.TabBorder);

        // ── 5. Draw each tab ───────────────────────────────────────────────────
        float cr = 4f;   // top-corner radius
        float x  = 4f;   // slight left inset so first tab doesn't hug the edge
        const float tabTopPad = 2f;   // gap at very top of bar

        for (int i = 0; i < _tabs.Count; i++)
        {
            float tw  = tabWidths[i];
            bool  sel = i == _selectedIndex;

            // Active tab is 1px taller to overlap (and thereby hide) the separator line.
            // Inactive tab is 1px shorter and sits inside the bar, leaving the separator visible.
            float tabY = tabTopPad;
            float tabH = sel ? (hdrH - tabTopPad + 1f) : (hdrH - tabTopPad - 2f);
            var   tabR = new Rect(x, tabY, tw, tabH);

            if (sel)
            {
                // ── Active: raised effect ─────────────────────────────────────
                // Gradient goes from a brighter surface tone at top → surface at bottom
                // so the active tab blends seamlessly into the content panel.
                var topC = theme.SurfaceColor.Lighten(0.18f);
                var botC = theme.SurfaceColor;
                var grad = renderer.LinearGradient(
                    new Vector2(tabR.X, tabR.Y),
                    new Vector2(tabR.X, tabR.Bottom),
                    topC, botC);
                renderer.FillRoundedRectTopWithPaint(tabR, cr, grad);

                // Left / right border (same as tab border but fades at bottom)
                renderer.DrawLine(tabR.X,     tabR.Y + cr, tabR.X,     tabR.Bottom, 1f, theme.TabBorder);
                renderer.DrawLine(tabR.Right, tabR.Y + cr, tabR.Right, tabR.Bottom, 1f, theme.TabBorder);

                // Top edge shine
                renderer.DrawLine(tabR.X + cr, tabR.Y + 0.5f, tabR.Right - cr, tabR.Y + 0.5f, 1f,
                    theme.SurfaceColor.Lighten(0.45f).WithAlpha(0.9f));
            }
            else
            {
                // ── Inactive: inset / sunken effect ─────────────────────────
                // BoxGradient gives a slight inner-shadow look.
                var insetGrad = renderer.BoxGradient(
                    tabR, cr, 4f,
                    theme.TabBarColor.Darken(0.12f),
                    theme.TabBarColor.Lighten(0.04f));
                renderer.FillRoundedRectTopWithPaint(tabR, cr, insetGrad);

                // Subtle border around the inactive tab
                renderer.StrokeRoundedRectTop(tabR, cr, 1f, theme.TabBorder.WithAlpha(0.6f));
            }

            // ── Tab label ─────────────────────────────────────────────────────
            var labelColor = sel ? theme.TextColor : theme.TextMutedColor;
            renderer.DrawText(x + theme.TabPaddingH, tabY + tabH * 0.5f, _tabs[i].Title, labelColor);

            x += tw + 1f;   // 1px gap between tabs
        }

        // Content area
        if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
        {
            var content  = _tabs[_selectedIndex].Content;
            // Bounds.Y = hdrH anchors content in the ScreenPosition chain for correct hit-testing.
            content.Bounds = new Rect(0, hdrH, w, h - hdrH);

            bool logContent = Screen.DbgFrame <= 5 || Screen.DbgFrame % 300 == 0;
            if (logContent)
                Sokol.SLog.Info($"TabView.Content[{Screen.DbgFrame}]: {content.GetType().Name} Bounds={content.Bounds} children={content.Children.Count}", "Sokol.GUI");

            renderer.Save();
            renderer.Translate(0, hdrH);                               // move NVG origin to content area
            renderer.IntersectClip(new Rect(0f, 0f, w, h - hdrH));    // clip to content area
            content.PerformLayout(renderer, true);                     // force since bounds may change
            content.Draw(renderer);
            renderer.Restore();
        }
    }

    /// <summary>
    /// Override to also hit-test the selected tab's content subtree.
    /// Content is not in Children (to skip CanvasLayout), but its Parent is set
    /// so ScreenPosition and ToLocal work for all descendant widgets.
    /// </summary>
    public override Widget? HitTestDeep(Vector2 screenPoint)
    {
        if (!Visible || !Enabled) return null;

        var local = ToLocal(screenPoint);
        if (!HitTest(local)) return null;

        float hdrH = ThemeManager.Current.TabBarHeight;

        // In the content area: delegate to the visible content widget's full subtree.
        if (_selectedIndex >= 0 && local.Y >= hdrH)
        {
            var content = _tabs[_selectedIndex].Content;
            // content.HitTestDeep uses content.ScreenPosition = tabView.ScreenPos + (0, hdrH)
            var hit = content.HitTestDeep(screenPoint);
            if (hit != null) return hit;
        }

        return this;  // tab header area — OnMouseDown handles tab switching
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        var local  = ToLocal(e.Position);
        float hdrH = ThemeManager.Current.TabBarHeight;

        if (local.Y < hdrH)
        {
            ApplyFont(Screen.Instance.Renderer, ThemeManager.Current);
            float x = 0;
            for (int i = 0; i < _tabs.Count; i++)
            {
                float tw = Screen.Instance.Renderer.MeasureText(_tabs[i].Title) + ThemeManager.Current.TabPaddingH * 2;
                if (local.X >= x && local.X < x + tw) { SelectedIndex = i; return true; }
                x += tw;
            }
        }
        else if (_selectedIndex >= 0)
        {
            return _tabs[_selectedIndex].Content.OnMouseDown(e);
        }

        return false;
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
