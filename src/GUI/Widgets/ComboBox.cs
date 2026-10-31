using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Drop-down combo box.
/// </summary>
public class ComboBox : Widget
{
    private int                 _selectedIndex = -1;
    private bool                _open;
    private int                 _hoveredIndex  = -1;
    private readonly List<string> _items = [];

    // Scrollbar state
    private float _scrollOffset;       // pixels scrolled inside the dropdown
    private bool  _sbDragging;
    private float _sbDragStartY;
    private float _sbDragStartScroll;
    private bool  _sbHovered;
    private bool  _hasPopupAnchor;
    private Vector2 _popupAnchorScreenPos;

    // Touch drag-to-scroll: a press on the rows is a *candidate* tap that becomes a pan
    // once it moves past the slop, so a finger-flick scrolls the list instead of picking
    // a row. Selection is therefore deferred to mouse-up (and skipped if we panned).
    private bool  _pressInList;
    private bool  _dragScrolling;
    private float _pressStartY, _pressLastY;
    private const float DragSlop      = 8f;   // px before a press becomes a pan
    private const float ScrollbarHitW = 24f;  // touch-friendly grab width for the scrollbar

    public IReadOnlyList<string> Items => _items;

    private readonly List<DrawnMenuItem> _drawn = [];
    /// <summary>The open drop-down's visible rows as last drawn, in screen space (for UI automation).</summary>
    public IReadOnlyList<DrawnMenuItem> DrawnItems => _drawn;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int clamped = (value < 0 || _items.Count == 0) ? -1
                        : Math.Clamp(value, 0, _items.Count - 1);
            if (clamped == _selectedIndex) return;
            _selectedIndex = clamped;
            SelectionChanged?.Invoke(_selectedIndex, SelectedItem);
        }
    }

    public string? SelectedItem => (_selectedIndex >= 0 && _selectedIndex < _items.Count)
        ? _items[_selectedIndex] : null;

    public void AddItem(string item) { _items.Add(item); }
    public void SetItems(IEnumerable<string> items) { _items.Clear(); _items.AddRange(items); _selectedIndex = -1; }

    public event Action<int, string?>? SelectionChanged;

    public Font?   Font     { get; set; }
    public float   FontSize { get; set; } = 0f;
    /// <summary>Height of one dropdown row; 0 = theme <c>InputHeight</c>. Set it with a larger <see cref="FontSize"/> so rows don't overlap.</summary>
    public float   ItemHeight { get; set; } = 0f;
    float RowHeight => ItemHeight > 0 ? ItemHeight : ThemeManager.Current.InputHeight;

    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        return new Vector2(180, ThemeManager.Current.InputHeight);
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme   = ThemeManager.Current;
        var bounds  = new Rect(0, 0, Bounds.Width, Bounds.Height);
        float cr    = theme.InputCornerRadius;
        float arrowW = bounds.Height;

        // Box — NanoGUI bevel button (ComboBox main control is button-like)
        var fillR = new Rect(1, 1, bounds.Width - 2, bounds.Height - 2);
        UIColor gradTop, gradBot;
        if (IsHovered) { gradTop = theme.ButtonHoverTop;   gradBot = theme.ButtonHoverBottom; }
        else           { gradTop = theme.ButtonGradientTop; gradBot = theme.ButtonGradientBottom; }
        var bgGrad = renderer.LinearGradient(
            new Vector2(0, 0), new Vector2(0, bounds.Height),
            gradTop, gradBot);
        renderer.FillRoundedRectWithPaint(fillR, MathF.Max(0f, cr - 1f), bgGrad);

        // Inner highlight
        renderer.StrokeRoundedRect(
            new Rect(0.5f, 1.5f, bounds.Width - 1f, bounds.Height - 2f), cr,
            1f, theme.BorderLight);
        // Outer dark border
        renderer.StrokeRoundedRect(
            new Rect(0.5f, 0.5f, bounds.Width - 1f, bounds.Height - 2f), cr,
            1f, theme.BorderDark);

        // Selected text
        ApplyFont(renderer, theme);
        bool rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
        var text = SelectedItem ?? string.Empty;
        renderer.Save();
        if (rtl)
        {
            renderer.SetTextAlign(TextHAlign.Right);
            renderer.IntersectClip(new Rect(arrowW + 4, 0, bounds.Width - arrowW - 8, bounds.Height));
            renderer.DrawText(bounds.Width - 4, bounds.Height * 0.5f, text, theme.TextColor);
        }
        else
        {
            renderer.SetTextAlign(TextHAlign.Left);
            renderer.IntersectClip(new Rect(4, 0, bounds.Width - arrowW - 8, bounds.Height));
            renderer.DrawText(4, bounds.Height * 0.5f, text, theme.TextColor);
        }
        renderer.Restore();

        // Arrow: RTL = left side, LTR = right side
        float ax = rtl ? arrowW * 0.5f : bounds.Width - arrowW * 0.5f;
        float ay = bounds.Height * 0.5f;
        float as2 = 4f;
        renderer.Translate(ax, ay);
        renderer.FillRect(new Rect(-as2, -as2 * 0.5f, as2 * 2, as2 * 2), UIColor.Transparent);
        renderer.DrawLine(-as2, -as2 * 0.4f, 0, as2 * 0.6f, 1.5f, theme.TextColor);
        renderer.DrawLine(0,    as2 * 0.6f,  as2, -as2 * 0.4f, 1.5f, theme.TextColor);
        renderer.Translate(-ax, -ay);

        // Dropdown panel — rendered by Screen as overlay to avoid parent clip issues.
        // DrawDropdown is called via DrawPopupOverlay when _open.
    }

    // Returns how tall the visible part of the dropdown should be, and whether a scrollbar is needed.
    private (float visibleH, bool needsScroll, float ddY) ComputeDropdownMetrics()
    {
        float itemH      = RowHeight;
        float totalH     = _items.Count * itemH;
        // Use the popup anchor (the true screen Y captured at open) — the dropdown is RENDERED
        // relative to it (see PopupOffsetFromCurrentScreenPosition), and in a docked panel it
        // differs from ScreenPosition.Y. Deciding the flip on ScreenPosition.Y while drawing at
        // the anchor made bottom-of-window combos open downward and clip off-screen.
        float sp         = _hasPopupAnchor ? _popupAnchorScreenPos.Y : ScreenPosition.Y;
        float screenH    = Screen.Instance.LogicalHeight;
        float availBelow = screenH - (sp + Bounds.Height) - 4f;
        float availAbove = sp - 4f;
        bool  openAbove  = totalH > MathF.Max(itemH, availBelow) && availAbove > availBelow;
        float avail      = MathF.Max(itemH, openAbove ? availAbove : availBelow);
        float visibleH   = MathF.Min(totalH, avail);
        float ddY        = openAbove ? -visibleH : Bounds.Height;
        return (visibleH, totalH > visibleH, ddY);
    }

    private void DrawDropdown(Renderer renderer, Theme theme, Rect bounds, Vector2 screenOrigin)
    {
        float itemH   = RowHeight;
        float totalH  = _items.Count * itemH;
        float sbW     = theme.ScrollBarWidth;

        var (visibleH, needsScroll, ddY) = ComputeDropdownMetrics();

        // Clamp scroll so we never go past the end.
        float maxScroll = MathF.Max(0f, totalH - visibleH);
        _scrollOffset = Math.Clamp(_scrollOffset, 0f, maxScroll);

        var ddRect = new Rect(0, ddY, bounds.Width, visibleH);

        // Drop shadow
        renderer.DrawDropShadow(ddRect, theme.InputCornerRadius,
            new Vector2(0, 2), 8f, new UIColor(0f, 0f, 0f, 0.5f));

        renderer.FillRoundedRect(ddRect, theme.InputCornerRadius, theme.InputBackColor);
        renderer.StrokeRoundedRect(ddRect, theme.InputCornerRadius, 1f, theme.BorderDark);

        // Clip item rendering to the visible dropdown area.
        renderer.Save();
        renderer.IntersectClip(ddRect);

        ApplyFont(renderer, theme);
        renderer.SetTextAlign(TextHAlign.Left);

        float textAreaW = needsScroll ? bounds.Width - sbW : bounds.Width;
        _drawn.Clear();

        for (int i = 0; i < _items.Count; i++)
        {
            float rowTop = ddY + i * itemH - _scrollOffset;
            var   rowR   = new Rect(0, rowTop, textAreaW, itemH);

            if (i == _selectedIndex)
                renderer.FillRect(rowR, theme.SelectionColor);
            else if (i == _hoveredIndex)
                renderer.FillRect(rowR, theme.AccentColor.WithAlpha(0.12f));

            renderer.DrawText(rowR.X + 6, rowR.Y + rowR.Height * 0.5f, _items[i], theme.TextColor);

            // Record the visible part of the row (scrolled rows are clipped by the drop-down).
            float y0 = MathF.Max(rowTop, ddY), y1 = MathF.Min(rowTop + itemH, ddY + visibleH);
            if (y1 - y0 >= itemH * 0.5f)
                _drawn.Add(new DrawnMenuItem(_items[i],
                    new Rect(screenOrigin.X, screenOrigin.Y + y0, textAreaW, y1 - y0), Enabled, false));
        }

        renderer.Restore();

        // Scrollbar (drawn on top of clip, so outside the save/restore)
        if (needsScroll)
        {
            ScrollbarRenderer.DrawVertical(renderer,
                trackX: bounds.Width - sbW, trackY: ddY,
                trackW: sbW, trackH: visibleH,
                scrollOffset: _scrollOffset, contentSize: totalH, viewSize: visibleH,
                isHovered: _sbHovered);
        }
    }

    private Vector2 PopupOffsetFromCurrentScreenPosition()
    {
        if (!_hasPopupAnchor) return Vector2.Zero;
        var sp = ScreenPosition;
        return new Vector2(_popupAnchorScreenPos.X - sp.X, _popupAnchorScreenPos.Y - sp.Y);
    }

    private Vector2 PopupLocalFromTargetLocal(Vector2 targetLocal)
    {
        var off = PopupOffsetFromCurrentScreenPosition();
        return new Vector2(targetLocal.X - off.X, targetLocal.Y - off.Y);
    }

    // When the dropdown is open it renders above or below Bounds (via Screen overlay) — extend HitTest.
    public override bool HitTest(Vector2 localPoint)
    {
        if (_open)
        {
            var popupLocal = PopupLocalFromTargetLocal(localPoint);
            var (visibleH, _, ddY) = ComputeDropdownMetrics();
            float yMin = MathF.Min(0f, ddY);
            float yMax = MathF.Max(Bounds.Height, ddY + visibleH);
            return popupLocal.X >= 0 && popupLocal.Y >= yMin &&
                   popupLocal.X < Bounds.Width && popupLocal.Y < yMax;
        }
        return base.HitTest(localPoint);
    }

    /// <summary>Draw the dropdown overlay (called by Screen on top of all widgets).</summary>
    public override void DrawPopupOverlay(Renderer renderer)
    {
        var theme  = ThemeManager.Current;
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var off = PopupOffsetFromCurrentScreenPosition();
        var sp  = ScreenPosition;
        renderer.Save();
        renderer.Translate(off.X, off.Y);
        DrawDropdown(renderer, theme, bounds, new Vector2(sp.X + off.X, sp.Y + off.Y));
        renderer.Restore();
    }

    /// <summary>Called when a click outside dismisses the popup.</summary>
    public override void OnPopupDismiss()
    {
        _open = false;
        _hoveredIndex = -1;
        _hasPopupAnchor = false;
        Sokol.SLog.Info($"ComboBox: dismissed", "Sokol.GUI");
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseDown(MouseEvent e)
    {
        var   local   = _open ? PopupLocalFromTargetLocal(e.LocalPosition) : e.LocalPosition;
        float sbW     = ThemeManager.Current.ScrollBarWidth;

        if (_open)
        {
            var (visibleH, needsScroll, ddY) = ComputeDropdownMetrics();
            float sbHit = MathF.Max(sbW, ScrollbarHitW);

            // Grab the scrollbar? (wider hit area so it's touch-reachable)
            if (needsScroll && local.X >= Bounds.Width - sbHit && local.Y >= ddY && local.Y < ddY + visibleH)
            {
                _sbDragging        = true;
                _sbDragStartY      = local.Y;
                _sbDragStartScroll = _scrollOffset;
                return true;
            }

            // Press on the rows: a candidate tap. Don't select yet — a drag becomes a pan
            // (see OnMouseMove), and a tap selects on release (see OnMouseUp).
            if (local.Y >= ddY && local.Y < ddY + visibleH && local.X >= 0 && local.X < Bounds.Width)
            {
                _pressInList   = true;
                _dragScrolling = false;
                _pressStartY   = _pressLastY = local.Y;
                return true;
            }

            // Press elsewhere (e.g. back on the closed box) → dismiss.
            _open = false;
            _hoveredIndex = -1;
            _sbDragging   = false;
            Screen.SetActivePopup(null);
            return true;
        }

        if (e.Button == MouseButton.Left && Enabled)
        {
            _popupAnchorScreenPos = new Vector2(e.Position.X - e.LocalPosition.X, e.Position.Y - e.LocalPosition.Y);
            _hasPopupAnchor = true;
            _open         = true;
            _hoveredIndex = -1;
            _scrollOffset = 0f;     // reset scroll each time we open
            Screen.SetActivePopup(this);
            Sokol.SLog.Info($"ComboBox: opened, registered as popup", "Sokol.GUI");
            return true;
        }
        return false;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        bool panned = _dragScrolling;

        // A tap (press that didn't become a pan) selects the row under the release point.
        if (_open && _pressInList && !_dragScrolling)
        {
            var   local = PopupLocalFromTargetLocal(e.LocalPosition);
            float itemH = RowHeight;
            var (visibleH, _, ddY) = ComputeDropdownMetrics();
            if (local.Y >= ddY && local.Y < ddY + visibleH)
            {
                int idx = (int)((local.Y - ddY + _scrollOffset) / itemH);
                if (idx >= 0 && idx < _items.Count)
                {
                    Sokol.SLog.Info($"ComboBox: selected index {idx} ('{_items[idx]}')", "Sokol.GUI");
                    SelectedIndex = idx;
                }
            }
            _open = false;
            _hoveredIndex = -1;
            Screen.SetActivePopup(null);
        }

        _sbDragging    = false;
        _pressInList   = false;
        _dragScrolling = false;
        return panned;   // if we panned, swallow the up so it isn't treated as a click
    }

    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; _hoveredIndex = -1; _sbHovered = false; return false; }

    public override bool OnMouseMove(MouseEvent e)
    {
        if (!_open) return false;
        var   local   = PopupLocalFromTargetLocal(e.LocalPosition);
        float itemH   = RowHeight;
        float sbW     = ThemeManager.Current.ScrollBarWidth;
        var (visibleH, needsScroll, ddY) = ComputeDropdownMetrics();
        float totalH  = _items.Count * itemH;

        // Scrollbar drag
        if (_sbDragging)
        {
            float maxScroll = MathF.Max(0f, totalH - visibleH);
            float ratio     = visibleH / MathF.Max(totalH, 1f);
            float dy        = (local.Y - _sbDragStartY) / ratio;
            _scrollOffset   = Math.Clamp(_sbDragStartScroll + dy, 0f, maxScroll);
            return true;
        }

        // Drag-anywhere panning: a press on the rows that moves past the slop scrolls the
        // list (finger-flick) and suppresses the would-be tap. Only when there's overflow.
        if (_pressInList)
        {
            if (!_dragScrolling && needsScroll && MathF.Abs(local.Y - _pressStartY) > DragSlop)
            {
                _dragScrolling = true;
                _hoveredIndex  = -1;
            }
            if (_dragScrolling)
            {
                float maxScroll = MathF.Max(0f, totalH - visibleH);
                _scrollOffset   = Math.Clamp(_scrollOffset - (local.Y - _pressLastY), 0f, maxScroll);
                _pressLastY     = local.Y;
            }
            return true;   // swallow the move so an outer ScrollView can't hijack the gesture
        }

        // Scrollbar hover
        _sbHovered = needsScroll && local.X >= Bounds.Width - sbW &&
                     local.Y >= ddY && local.Y < ddY + visibleH;

        // Row hover
        if (local.Y >= ddY && local.Y < ddY + visibleH)
        {
            int idx = (int)((local.Y - ddY + _scrollOffset) / itemH);
            _hoveredIndex = (idx >= 0 && idx < _items.Count) ? idx : -1;
        }
        else
            _hoveredIndex = -1;
        return true;
    }

    public override bool OnMouseScroll(MouseEvent e)
    {
        if (!_open) return false;
        float itemH   = RowHeight;
        float totalH  = _items.Count * itemH;
        var (visibleH, _, _ddY) = ComputeDropdownMetrics();
        float maxScroll   = MathF.Max(0f, totalH - visibleH);
        _scrollOffset     = Math.Clamp(_scrollOffset - e.Scroll.Y * itemH, 0f, maxScroll);
        return true;
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
