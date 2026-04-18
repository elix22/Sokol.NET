using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Tabbed container.  Each tab is a header button + a content widget.
/// Supports scroll arrows when tabs overflow the available width.
/// </summary>
public class TabView : Widget
{
    private int _selectedIndex = -1;
    private float _tabScrollOffset;        // horizontal scroll for the tab strip
    private const float ArrowBtnW = 22f;   // width of each scroll arrow button

    private readonly List<(string Title, Widget Content)> _tabs = [];

    // Cached from Draw so OnMouseDown can use reliable values (MeasureText
    // may not work correctly outside an active NanoVG frame).
    private float[] _cachedTabWidths = [];
    private float   _cachedTotalTabW;
    private bool    _cachedNeedArrows;
    private float   _cachedTabAreaLeft, _cachedTabAreaRight, _cachedMaxScroll;
    private bool    _ensureTabVisible;   // set when SelectedIndex changes

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int prev = _selectedIndex;
            _selectedIndex = _tabs.Count == 0 ? -1 : Math.Clamp(value, 0, _tabs.Count - 1);
            if (prev != _selectedIndex)
            {
                if (prev >= 0 && prev < _tabs.Count)           _tabs[prev].Content.Visible       = false;
                if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count) _tabs[_selectedIndex].Content.Visible = true;
                _ensureTabVisible = true;
                SelectionChanged?.Invoke(_selectedIndex);
            }
        }
    }

    public event Action<int>? SelectionChanged;
    public Font?   Font     { get; set; }
    public float   FontSize { get; set; } = 0f;

    public void AddTab(string title, Widget content)
    {
        content.Parent  = this;
        content.Visible = (_tabs.Count == 0);
        _tabs.Add((title, content));
        if (_selectedIndex < 0) { _selectedIndex = 0; _ensureTabVisible = true; }
    }

    public void RemoveTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _tabs.RemoveAt(index);
        if (_selectedIndex >= _tabs.Count) _selectedIndex = _tabs.Count - 1;
    }

    // ─── Drag-to-reorder tabs ────────────────────────────────────────────────
    /// <summary>Allow the user to reorder tabs by dragging their headers.</summary>
    public bool AllowReorder
    {
        get => _allowReorder;
        set
        {
            _allowReorder = value;
            IsDragSource  = value;
            IsDropTarget  = value;
        }
    }
    private bool _allowReorder;

    /// <summary>Drag wire-format for intra-TabView reorder.</summary>
    public const string ReorderFormat = "sokol.tabview/reorder";

    private int TabIndexFromLocalX(float localX)
    {
        if (_cachedTabWidths.Length == 0) return -1;
        float x = _cachedTabAreaLeft - _tabScrollOffset;
        for (int i = 0; i < _cachedTabWidths.Length && i < _tabs.Count; i++)
        {
            float tw = _cachedTabWidths[i];
            if (localX >= MathF.Max(x, _cachedTabAreaLeft) && localX < MathF.Min(x + tw, _cachedTabAreaRight))
                return i;
            x += tw + 1f;
        }
        return -1;
    }

    public override DragDropData? OnDragBegin(Vector2 localPos)
    {
        if (!_allowReorder) return null;
        float hdrH = ThemeManager.Current.TabBarHeight;
        if (localPos.Y >= hdrH) return null;
        int idx = TabIndexFromLocalX(localPos.X);
        if (idx < 0) return null;
        return new DragDropData
        {
            Format         = ReorderFormat,
            Payload        = (this, idx),
            Source         = this,
            DragLabel      = _tabs[idx].Title,
            AllowedEffects = DragDropEffect.Move,
        };
    }

    public override void OnDragOver(DragDropEventArgs e)
    {
        if (!_allowReorder || e.Data.Format != ReorderFormat) return;
        if (e.Data.Payload is (TabView src, int _) && src == this)
            e.Effect = DragDropEffect.Move;
    }

    public override void OnDrop(DragDropEventArgs e)
    {
        if (!_allowReorder || e.Data.Format != ReorderFormat) return;
        if (e.Data.Payload is not (TabView src, int srcIdx)) return;
        if (src != this) return;
        float hdrH = ThemeManager.Current.TabBarHeight;
        int dstIdx = e.LocalPosition.Y < hdrH
            ? TabIndexFromLocalX(e.LocalPosition.X)
            : _tabs.Count - 1;
        if (dstIdx < 0) dstIdx = _tabs.Count - 1;
        if (dstIdx == srcIdx) { e.Handled = true; e.Effect = DragDropEffect.Move; return; }
        var item = _tabs[srcIdx];
        _tabs.RemoveAt(srcIdx);
        if (dstIdx > srcIdx) dstIdx--;
        _tabs.Insert(dstIdx, item);
        SelectedIndex = dstIdx;
        e.Handled = true;
        e.Effect  = DragDropEffect.Move;
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        float hdrH = theme.TabBarHeight;
        float w    = Bounds.Width, h = Bounds.Height;

        // ── 1. Full-widget content background ─────────────────────────────────
        renderer.FillRect(new Rect(0, 0, w, h), theme.SurfaceColor);

        // ── 2. Tab bar strip background ────────────────────────────────────────
        renderer.FillRect(new Rect(0, 0, w, hdrH), theme.TabBarColor);

        // ── 3. Measure all tab widths ──────────────────────────────────────────
        ApplyFont(renderer, theme);
        renderer.SetTextAlign(TextHAlign.Left, TextVAlign.Middle);
        float[] tabWidths = new float[_tabs.Count];
        float totalTabW = 0;
        for (int i = 0; i < _tabs.Count; i++)
        {
            tabWidths[i] = renderer.MeasureText(_tabs[i].Title) + theme.TabPaddingH * 2;
            totalTabW += tabWidths[i] + 1f;
        }

        bool needArrows = totalTabW > w - 8f;
        float tabAreaLeft  = needArrows ? ArrowBtnW : 4f;
        float tabAreaRight = needArrows ? w - ArrowBtnW : w - 4f;
        float tabAreaW     = tabAreaRight - tabAreaLeft;
        float maxScroll    = needArrows ? MathF.Max(0f, totalTabW - tabAreaW) : 0f;
        _tabScrollOffset = Math.Clamp(_tabScrollOffset, 0f, maxScroll);

        // Cache for OnMouseDown / OnMouseScroll (MeasureText may differ outside NVG frame)
        _cachedTabWidths    = tabWidths;
        _cachedTotalTabW    = totalTabW;
        _cachedNeedArrows   = needArrows;
        _cachedTabAreaLeft  = tabAreaLeft;
        _cachedTabAreaRight = tabAreaRight;
        _cachedMaxScroll    = maxScroll;

        // Ensure the selected tab is visible (only after selection change, not every frame
        // — otherwise arrow-scroll is immediately undone by EnsureTabVisible).
        if (_ensureTabVisible)
        {
            _ensureTabVisible = false;
            EnsureTabVisible(tabWidths, tabAreaW);
        }

        // ── 4. Separator line ──────────────────────────────────────────────────
        renderer.DrawLine(0, hdrH, w, hdrH, 1f, theme.TabBorder);

        // ── 5. Draw scroll arrows if needed ────────────────────────────────────
        if (needArrows)
        {
            DrawArrow(renderer, theme, new Rect(0, 0, ArrowBtnW, hdrH), true,
                      _tabScrollOffset > 0.5f);
            DrawArrow(renderer, theme, new Rect(w - ArrowBtnW, 0, ArrowBtnW, hdrH), false,
                      _tabScrollOffset < maxScroll - 0.5f);
        }

        // ── 6. Draw each tab (clipped to tab area) ────────────────────────────
        float cr = 4f;
        const float tabTopPad = 2f;

        renderer.Save();
        renderer.IntersectClip(new Rect(tabAreaLeft, 0, tabAreaW, hdrH + 1));

        bool  rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
        float x   = tabAreaLeft - _tabScrollOffset;
        if (rtl) x = tabAreaLeft + (tabAreaW - totalTabW) + _tabScrollOffset;

        for (int i = 0; i < _tabs.Count; i++)
        {
            float tw  = tabWidths[i];
            bool  sel = i == _selectedIndex;
            float tabY = tabTopPad;
            float tabH = sel ? (hdrH - tabTopPad + 1f) : (hdrH - tabTopPad - 2f);
            var   tabR = new Rect(x, tabY, tw, tabH);

            if (sel)
            {
                var topC = theme.SurfaceColor.Lighten(0.18f);
                var botC = theme.SurfaceColor;
                var grad = renderer.LinearGradient(
                    new Vector2(tabR.X, tabR.Y),
                    new Vector2(tabR.X, tabR.Bottom),
                    topC, botC);
                renderer.FillRoundedRectTopWithPaint(tabR, cr, grad);
                renderer.DrawLine(tabR.X,     tabR.Y + cr, tabR.X,     tabR.Bottom, 1f, theme.TabBorder);
                renderer.DrawLine(tabR.Right, tabR.Y + cr, tabR.Right, tabR.Bottom, 1f, theme.TabBorder);
                renderer.DrawLine(tabR.X + cr, tabR.Y + 0.5f, tabR.Right - cr, tabR.Y + 0.5f, 1f,
                    theme.SurfaceColor.Lighten(0.45f).WithAlpha(0.9f));
            }
            else
            {
                var insetGrad = renderer.BoxGradient(tabR, cr, 4f,
                    theme.TabBarColor.Darken(0.12f), theme.TabBarColor.Lighten(0.04f));
                renderer.FillRoundedRectTopWithPaint(tabR, cr, insetGrad);
                renderer.StrokeRoundedRectTop(tabR, cr, 1f, theme.TabBorder.WithAlpha(0.6f));
            }

            var labelColor = sel ? theme.TextColor : theme.TextMutedColor;
            renderer.DrawText(x + theme.TabPaddingH, tabY + tabH * 0.5f, _tabs[i].Title, labelColor);

            x += tw + 1f;
        }
        renderer.Restore();

        // ── 7. Content area ────────────────────────────────────────────────────
        if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
        {
            var content  = _tabs[_selectedIndex].Content;
            content.Bounds = new Rect(0, hdrH, w, h - hdrH);

            renderer.Save();
            renderer.Translate(0, hdrH);
            renderer.IntersectClip(new Rect(0f, 0f, w, h - hdrH));
            content.PerformLayout(renderer, true);
            content.Draw(renderer);
            renderer.Restore();
        }
    }

    private void DrawArrow(Renderer renderer, Theme theme, Rect r, bool left, bool active)
    {
        var bg = active ? theme.ButtonHoverColor.WithAlpha(0.4f) : theme.TabBarColor.Darken(0.05f);
        renderer.FillRect(r, bg);
        var fg = active ? theme.TextColor : theme.TextMutedColor.WithAlpha(0.5f);
        float cx = r.X + r.Width * 0.5f, cy = r.Y + r.Height * 0.5f;
        float sz = 7f;
        if (left)
            renderer.FillTriangle(
                new Vector2(cx + sz * 0.4f, cy - sz), new Vector2(cx - sz * 0.6f, cy), new Vector2(cx + sz * 0.4f, cy + sz), fg);
        else
            renderer.FillTriangle(
                new Vector2(cx - sz * 0.4f, cy - sz), new Vector2(cx + sz * 0.6f, cy), new Vector2(cx - sz * 0.4f, cy + sz), fg);
        // Separator line between arrow button and tabs
        float sx = left ? r.Right : r.X;
        renderer.DrawLine(sx, r.Y + 4, sx, r.Bottom - 4, 1f, theme.TabBorder.WithAlpha(0.5f));
    }

    private void EnsureTabVisible(float[] tabWidths, float tabAreaW)
    {
        if (_selectedIndex < 0 || _selectedIndex >= tabWidths.Length) return;
        float left = 0;
        for (int i = 0; i < _selectedIndex; i++) left += tabWidths[i] + 1f;
        float right = left + tabWidths[_selectedIndex];
        if (left < _tabScrollOffset)
            _tabScrollOffset = left;
        else if (right > _tabScrollOffset + tabAreaW)
            _tabScrollOffset = right - tabAreaW;
    }

    public override Widget? HitTestDeep(Vector2 screenPoint)
    {
        if (!Visible || !Enabled) return null;

        var local = ToLocal(screenPoint);
        if (!HitTest(local)) return null;

        float hdrH = ThemeManager.Current.TabBarHeight;

        if (_selectedIndex >= 0 && local.Y >= hdrH)
        {
            var content = _tabs[_selectedIndex].Content;
            var hit = content.HitTestDeep(screenPoint);
            if (hit != null) return hit;
        }

        return this;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        var local  = e.LocalPosition;
        float hdrH = ThemeManager.Current.TabBarHeight;

        if (local.Y < hdrH)
        {
            // Use cached layout from last Draw (MeasureText is unreliable outside NVG frame)
            bool  needArrows   = _cachedNeedArrows;
            float tabAreaLeft  = _cachedTabAreaLeft;
            float tabAreaRight = _cachedTabAreaRight;
            float maxScroll    = _cachedMaxScroll;

            // Arrow button clicks
            if (needArrows)
            {
                if (local.X < ArrowBtnW)
                {
                    _tabScrollOffset = MathF.Max(0f, _tabScrollOffset - 80f);
                    return true;
                }
                if (local.X > Bounds.Width - ArrowBtnW)
                {
                    _tabScrollOffset = MathF.Min(maxScroll, _tabScrollOffset + 80f);
                    return true;
                }
            }

            // Tab hit-test within the scrolled tab area
            float x = tabAreaLeft - _tabScrollOffset;
            for (int i = 0; i < _cachedTabWidths.Length && i < _tabs.Count; i++)
            {
                float tw = _cachedTabWidths[i];
                if (local.X >= MathF.Max(x, tabAreaLeft) && local.X < MathF.Min(x + tw, tabAreaRight))
                {
                    SelectedIndex = i;
                    return true;
                }
                x += tw + 1f;
            }
            return true;
        }
        else if (_selectedIndex >= 0)
        {
            return _tabs[_selectedIndex].Content.OnMouseDown(e);
        }

        return false;
    }

    public override bool OnMouseScroll(MouseEvent e)
    {
        var local = e.LocalPosition;
        float hdrH = ThemeManager.Current.TabBarHeight;
        if (local.Y < hdrH && _cachedNeedArrows)
        {
            _tabScrollOffset = Math.Clamp(_tabScrollOffset - e.Scroll.Y * 40f, 0f, _cachedMaxScroll);
            return true;
        }
        return false;
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
