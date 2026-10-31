using System;
using System.Collections.Generic;
using static Sokol.SApp;

namespace Sokol.GUI;

/// <summary>
/// Widget that draws a docking tree of panels with user-draggable dividers
/// and per-leaf tab strips.
/// </summary>
public sealed class DockSpace : Widget
{
    public const float DividerSize = 6f;
    public const float TabBarHeight = 26f;
    public const float TabPaddingH  = 10f;
    public const float DropZoneHalfWidth = 48f;

    /// <summary>Root of the docking tree. Always non-null; an empty tree is a leaf with no panels.</summary>
    public DockNode Root { get; private set; } = new();

    /// <summary>
    /// Pixels reserved at the top of the DockSpace for a toolbar or menu bar.
    /// ArrangeTree() offsets all leaves so they start below this inset.
    /// </summary>
    public float TopInset { get; set; } = 0f;

    private DockNode? _draggingDivider;
    private float     _dragStartPos;
    private float     _dragStartRatio;

    private DockPanel? _draggingTabPanel;
    private Vector2    _tabDragStartScreen;
    private bool       _tabDragBegun;
    private DockNode?  _draggingTabLeaf;     // leaf where the tab drag originated
    private bool       _tabDragIsReorder;    // true = reorder within leaf, false = detach
    private float      _tabReorderInsertX;   // local-x of the insertion caret (-1 = none)
    private int        _tabReorderInsertIdx; // insert-before index

    // Cached per-leaf tab widths, populated each frame in DrawLeaf (font is set there).
    private readonly Dictionary<DockNode, float[]> _tabWidthCache = new();

    private Vector2 _tabReorderMouseLocal; // current mouse position (local) during reorder

    // Hover state for tab highlighting (no drag in progress).
    private DockNode? _hoveredTabLeaf;
    private int       _hoveredTabIdx = -1;

    /// <summary>The DockManager that owns this DockSpace (set by the DockManager constructor).</summary>
    internal DockManager? Manager { get; set; }

    /// <summary>Fired whenever the tree structure changes.</summary>
    public event Action? TreeChanged;

    internal void RaiseTreeChanged() => TreeChanged?.Invoke();

    // Expose active panel content widgets so FocusManager can Tab-traverse into them.
    public override IEnumerable<Widget> FocusTraversalChildren
    {
        get
        {
            foreach (var c in base.FocusTraversalChildren) yield return c;
            foreach (var leaf in Root.EnumerateLeaves())
            {
                int idx = leaf.ActivePanelIndex;
                if (idx >= 0 && idx < leaf.Panels.Count)
                    yield return leaf.Panels[idx].Content;
            }
        }
    }

    // ─── Public tree operations ──────────────────────────────────────────────

    public DockNode AddPanel(DockPanel panel, DockNode? target = null, DockDropZone zone = DockDropZone.Center)
    {
        target ??= Root;
        if (!target.IsLeaf) target = target.EnumerateLeaves().GetEnumerator() is var e && e.MoveNext() ? e.Current : Root;

        if (zone == DockDropZone.Center || target.Panels.Count == 0)
        {
            target.AddPanel(panel);
        }
        else
        {
            var (split, newFirst) = zone switch
            {
                DockDropZone.Left   => (DockNodeType.SplitHorizontal, true),
                DockDropZone.Right  => (DockNodeType.SplitHorizontal, false),
                DockDropZone.Top    => (DockNodeType.SplitVertical,   true),
                DockDropZone.Bottom => (DockNodeType.SplitVertical,   false),
                _                   => (DockNodeType.SplitHorizontal, false),
            };
            target.SplitLeaf(split, panel, newFirst);
        }
        InvalidateLayout();
        RaiseTreeChanged();
        return target;
    }

    public void RemovePanel(DockPanel panel)
    {
        var owner = panel.Owner;
        if (owner == null) return;
        var parent = owner.Parent;
        owner.RemovePanel(panel);
        // CollapseIfDegenerate must be called on a split node (the parent) to detect
        // an empty leaf child. Calling it on the leaf itself is a no-op.
        var root = Root;
        if (parent != null)
            parent.CollapseIfDegenerate(ref root);
        InvalidateLayout();
        RaiseTreeChanged();
    }

    // ─── Layout ──────────────────────────────────────────────────────────────

    public override void PerformLayout(Renderer renderer, bool force = false)
    {
        base.PerformLayout(renderer, force);
        ArrangeTree();
    }

    private void ArrangeTree()
    {
        Root.Arrange(new Rect(0, TopInset, Bounds.Width, MathF.Max(0, Bounds.Height - TopInset)), DividerSize);
        // Size each panel's content widget to fill the leaf minus tab bar.
        foreach (var leaf in Root.EnumerateLeaves())
        {
            var b = leaf.ComputedBounds;
            var content = new Rect(b.X, b.Y + TabBarHeight,
                                   b.Width, MathF.Max(0, b.Height - TabBarHeight));
            for (int i = 0; i < leaf.Panels.Count; i++)
            {
                var p = leaf.Panels[i];
                p.Content.Bounds = content;
                p.Content.Visible = i == leaf.ActivePanelIndex;
            }
        }
    }

    // ─── Draw ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The tab headers drawn in the last frame (Path = panel title, screen rect, Enabled = active tab), in draw
    /// order. Read-only, for UI automation: tab headers are drawn, not widgets.
    /// </summary>
    public IReadOnlyList<DrawnMenuItem> DrawnTabs => _drawnTabs;
    private readonly List<DrawnMenuItem> _drawnTabs = [];

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;
        _drawnTabs.Clear();
        ArrangeTree();
        var theme = ThemeManager.Current;

        // Background.
        renderer.FillRect(new Rect(0, 0, Bounds.Width, Bounds.Height), theme.Surface);

        foreach (var leaf in Root.EnumerateLeaves())
        {
            DrawLeaf(renderer, leaf, theme);
        }

        // Dividers (drawn after leaves so they appear on top).
        DrawDividers(renderer, Root, theme);

        // Drop-zone highlight from DockManager if a drag is in progress.
        var dm = Manager;
        if (dm != null && dm.ActiveDragPanel != null &&
            dm.HoveredDropNode != null && dm.HoveredDropZone != DockDropZone.None &&
            IsAncestorOf(dm.HoveredDropNode))
        {
            var zoneRect = ComputeDropZoneRect(dm.HoveredDropNode, dm.HoveredDropZone);
            renderer.FillRect(zoneRect, theme.Primary.WithAlpha(0.25f));
            renderer.StrokeRect(zoneRect, 2f, theme.Primary);
        }

        // Ghost label during tab reorder.
        if (_tabDragIsReorder && _draggingTabPanel != null && _tabReorderInsertX >= 0f)
        {
            string label = _draggingTabPanel.Title;
            float  pad   = 6f;
            renderer.SetFont(theme.DefaultFont);
            renderer.SetFontSize(theme.FontSize);
            float textW = renderer.MeasureText(label);
            float gw = textW + pad * 2f;
            float gh = theme.FontSize + pad;
            var   gp = _tabReorderMouseLocal + new Vector2(12f, 8f);
            renderer.Save();
            renderer.Translate(gp.X, gp.Y);
            renderer.FillRect(new Rect(0, 0, gw, gh), theme.Surface.WithAlpha(0.92f));
            renderer.StrokeRect(new Rect(0, 0, gw, gh), 1f, theme.Primary);
            renderer.SetTextAlign(TextHAlign.Left);
            renderer.DrawText(pad, gh * 0.5f, label, theme.TextColor);
            renderer.Restore();
        }
    }

    private bool IsAncestorOf(DockNode node)
    {
        // All nodes of this DockSpace's tree are descendants of Root.
        for (var cur = node; cur != null; cur = cur.Parent)
            if (cur == Root) return true;
        return false;
    }

    private void DrawLeaf(Renderer renderer, DockNode leaf, Theme theme)
    {
        var b = leaf.ComputedBounds;
        if (b.Width <= 0 || b.Height <= 0) return;

        // Tab bar background.
        var tabBar = new Rect(b.X, b.Y, b.Width, TabBarHeight);
        renderer.FillRect(tabBar, theme.TabInactive);
        renderer.DrawLine(b.X, b.Y + TabBarHeight, b.Right, b.Y + TabBarHeight, 1f, theme.Border);

        // Panel body.
        var body = new Rect(b.X, b.Y + TabBarHeight, b.Width, MathF.Max(0, b.Height - TabBarHeight));
        renderer.FillRect(body, theme.Background);
        renderer.StrokeRect(b, 1f, theme.Border);

        // Tabs.
        renderer.SetFont(theme.DefaultFont);
        renderer.SetFontSize(theme.FontSize);
        renderer.SetTextAlign(TextHAlign.Left);
        // Cache tab widths now that the font is properly set.
        var cachedWidths = new float[leaf.Panels.Count];
        for (int i = 0; i < leaf.Panels.Count; i++)
            cachedWidths[i] = renderer.MeasureText(leaf.Panels[i].Title) + TabPaddingH * 2f;
        _tabWidthCache[leaf] = cachedWidths;
        float x = b.X + 4f;
        float cr = 4f;
        for (int i = 0; i < leaf.Panels.Count; i++)
        {
            var p = leaf.Panels[i];
            float tabW = cachedWidths[i];
            var isActive = i == leaf.ActivePanelIndex;
            float tabH = isActive ? (TabBarHeight - 2f) : (TabBarHeight - 4f);
            var tabRect = new Rect(x, b.Y + 2f, tabW, tabH);

            if (isActive)
            {
                var topC = theme.SurfaceColor.Lighten(0.18f);
                var botC = theme.SurfaceColor;
                var grad = renderer.LinearGradient(
                    new Vector2(tabRect.X, tabRect.Y),
                    new Vector2(tabRect.X, tabRect.Bottom),
                    topC, botC);
                renderer.FillRoundedRectTopWithPaint(tabRect, cr, grad);
                renderer.DrawLine(tabRect.X,     tabRect.Y + cr, tabRect.X,     tabRect.Bottom, 1f, theme.TabBorder);
                renderer.DrawLine(tabRect.Right, tabRect.Y + cr, tabRect.Right, tabRect.Bottom, 1f, theme.TabBorder);
                renderer.DrawLine(tabRect.X + cr, tabRect.Y + 0.5f, tabRect.Right - cr, tabRect.Y + 0.5f, 1f,
                    theme.SurfaceColor.Lighten(0.45f).WithAlpha(0.9f));
            }
            else
            {
                bool isHovered = _hoveredTabLeaf == leaf && _hoveredTabIdx == i;
                var baseColor = isHovered ? theme.TabBarColor.Lighten(0.12f) : theme.TabBarColor;
                var insetGrad = renderer.BoxGradient(tabRect, cr, 4f,
                    baseColor.Darken(0.12f), baseColor.Lighten(0.04f));
                renderer.FillRoundedRectTopWithPaint(tabRect, cr, insetGrad);
                renderer.StrokeRoundedRectTop(tabRect, cr, 1f, theme.TabBorder.WithAlpha(isHovered ? 0.9f : 0.6f));
            }

            var labelColor = isActive ? theme.TabText : theme.TextMutedColor;
            renderer.DrawText(tabRect.X + TabPaddingH, tabRect.Y + tabRect.Height * 0.5f, p.Title, labelColor);
            var sp = ScreenPosition;
            _drawnTabs.Add(new DrawnMenuItem(p.Title, new Rect(sp.X + tabRect.X, sp.Y + tabRect.Y, tabRect.Width, tabRect.Height), isActive, false));
            x += tabW + 2f;
        }

        // Tab-reorder insertion caret.
        if (_tabDragIsReorder && _draggingTabLeaf == leaf && _tabReorderInsertX >= 0f)
            renderer.DrawLine(_tabReorderInsertX, b.Y + 2f, _tabReorderInsertX, b.Y + TabBarHeight - 2f, 2f, theme.Primary);

        // Active content — draw into body rect (translate so content draws at 0,0).
        var active = leaf.ActivePanel;
        if (active != null)
        {
            renderer.Save();
            renderer.Translate(body.X, body.Y);
            renderer.IntersectClip(new Rect(0, 0, body.Width, body.Height));
            // Ensure the content bounds match the body.
            active.Content.Bounds = new Rect(0, 0, body.Width, body.Height);
            active.Content.PerformLayout(renderer, force: true);
            active.Content.Draw(renderer);
            renderer.Restore();
            // Back to the body rect (as HitTestDeep sets it): events between frames — e.g. a mouse-up
            // with no move since the down — localize against the content's real place, not (0,0).
            active.Content.Bounds = body;
        }
    }

    private void DrawDividers(Renderer renderer, DockNode node, Theme theme)
    {
        if (node.IsLeaf) return;
        var r = node.DividerRect(DividerSize);
        if (!r.IsEmpty)
            renderer.FillRect(r, theme.Border);
        if (node.First  != null) DrawDividers(renderer, node.First,  theme);
        if (node.Second != null) DrawDividers(renderer, node.Second, theme);
    }

    // ─── Hit-testing / Input ─────────────────────────────────────────────────

    public override bool HitTest(Vector2 localPoint) =>
        localPoint.X >= 0 && localPoint.Y >= 0 &&
        localPoint.X < Bounds.Width && localPoint.Y < Bounds.Height;

    public override Widget? HitTestDeep(Vector2 screenPoint)
    {
        if (!Visible || !Enabled) return null;
        var local = ToLocal(screenPoint);
        if (!HitTest(local)) return null;

        // Panel content widgets get priority if the point is inside them.
        foreach (var leaf in Root.EnumerateLeaves())
        {
            var b = leaf.ComputedBounds;
            var body = new Rect(b.X, b.Y + TabBarHeight, b.Width, MathF.Max(0, b.Height - TabBarHeight));
            if (!body.Contains(local)) continue;
            var active = leaf.ActivePanel;
            if (active == null) continue;
            // Content draws translated by body.(X,Y) to its own (0,0).
            active.Content.Bounds = new Rect(body.X, body.Y, body.Width, body.Height);
            var deep = active.Content.HitTestDeep(screenPoint);
            if (deep != null) return deep;
        }
        return this;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        var local = e.LocalPosition;

        // Divider hit?
        var divider = FindDividerAt(Root, local);
        if (divider != null)
        {
            _draggingDivider = divider;
            _dragStartPos    = divider.Type == DockNodeType.SplitHorizontal ? e.Position.X : e.Position.Y;
            _dragStartRatio  = divider.SplitRatio;
            return true;
        }

        // Tab hit? Use nearest-fallback so clicking in a small gap still works,
        // but not past the last tab's right edge.
        var (leaf, tabIdx, tabRect) = HitTabInternal(local, nearestFallback: true);
        if (leaf != null && tabIdx >= 0)
        {
            leaf.ActivePanelIndex = tabIdx;
            _draggingTabPanel    = leaf.Panels[tabIdx];
            _tabDragStartScreen  = e.Position;
            _tabDragBegun        = false;
            _draggingTabLeaf     = leaf;
            _tabDragIsReorder    = false;
            _tabReorderInsertX   = -1f;
            _tabReorderInsertIdx = -1;
            return true;
        }

        return false;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        if (_draggingDivider != null)
        {
            var parentBounds = _draggingDivider.ComputedBounds;
            float total = _draggingDivider.Type == DockNodeType.SplitHorizontal ? parentBounds.Width : parentBounds.Height;
            float travel = (_draggingDivider.Type == DockNodeType.SplitHorizontal ? e.Position.X : e.Position.Y) - _dragStartPos;
            float denom = MathF.Max(1f, total - DividerSize);
            float newR = Math.Clamp(_dragStartRatio + travel / denom, 0.05f, 0.95f);
            _draggingDivider.SplitRatio = newR;
            InvalidateLayout();
            sapp_set_mouse_cursor(_draggingDivider.Type == DockNodeType.SplitHorizontal
                ? sapp_mouse_cursor.SAPP_MOUSECURSOR_RESIZE_EW
                : sapp_mouse_cursor.SAPP_MOUSECURSOR_RESIZE_NS);
            return true;
        }

        if (_draggingTabPanel != null)
        {
            _hoveredTabLeaf = null;
            _hoveredTabIdx  = -1;
            var delta = e.Position - _tabDragStartScreen;
            if (!_tabDragBegun && (MathF.Abs(delta.X) > 5f || MathF.Abs(delta.Y) > 5f))
            {
                _tabDragBegun = true;
                // If the mouse is still inside the originating tab bar, treat as a
                // within-leaf reorder; otherwise escalate to a full panel detach.
                var localPos = ToLocal(e.Position);
                var lb = _draggingTabLeaf!.ComputedBounds;
                _tabDragIsReorder = new Rect(lb.X, lb.Y, lb.Width, TabBarHeight).Contains(localPos)
                                    && _draggingTabLeaf.Panels.Count > 1;
                if (!_tabDragIsReorder)
                    Manager?.BeginDragPanel(_draggingTabPanel, e.Position);
            }
            if (_tabDragBegun)
            {
                if (_tabDragIsReorder)
                {
                    var lp = ToLocal(e.Position);
                    var lb2 = _draggingTabLeaf!.ComputedBounds;
                    if (!new Rect(lb2.X, lb2.Y, lb2.Width, TabBarHeight).Contains(lp))
                    {
                        // Mouse left the tab bar — switch to full detach.
                        _tabDragIsReorder    = false;
                        _tabReorderInsertX   = -1f;
                        _tabReorderInsertIdx = -1;
                        Manager?.BeginDragPanel(_draggingTabPanel, e.Position);
                        Manager?.UpdateDrag(e.Position);
                    }
                    else
                    {
                        UpdateTabReorderIndicator(lp);
                    }
                }
                else
                {
                    // Detach mode — but if the mouse returned to the originating tab bar, revert to reorder.
                    var lp2 = ToLocal(e.Position);
                    var lb3 = _draggingTabLeaf!.ComputedBounds;
                    if (new Rect(lb3.X, lb3.Y, lb3.Width, TabBarHeight).Contains(lp2)
                        && _draggingTabLeaf.Panels.Count > 1)
                    {
                        Manager?.CancelDrag();
                        _tabDragIsReorder = true;
                        UpdateTabReorderIndicator(lp2);
                    }
                    else
                        Manager?.UpdateDrag(e.Position);
                }
            }
            return true;
        }

        // Track which tab the cursor is over for hover highlighting.
        var (hLeaf, hIdx, _) = HitTabInternal(ToLocal(e.Position));
        _hoveredTabLeaf = hLeaf;
        _hoveredTabIdx  = hIdx;

        // Update cursor for divider hover.
        var hoverDiv = FindDividerAt(Root, ToLocal(e.Position));
        sapp_set_mouse_cursor(hoverDiv != null
            ? (hoverDiv.Type == DockNodeType.SplitHorizontal
                ? sapp_mouse_cursor.SAPP_MOUSECURSOR_RESIZE_EW
                : sapp_mouse_cursor.SAPP_MOUSECURSOR_RESIZE_NS)
            : sapp_mouse_cursor.SAPP_MOUSECURSOR_DEFAULT);
        return false;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        bool wasDividerDrag = _draggingDivider != null;
        bool handled = _draggingDivider != null || _draggingTabPanel != null;
        if (_tabDragBegun && _draggingTabPanel != null)
        {
            if (_tabDragIsReorder)
                CommitTabReorder(ToLocal(e.Position));
            else
                Manager?.EndDrag(e.Position);
        }
        _draggingDivider     = null;
        _draggingTabPanel    = null;
        _tabDragBegun        = false;
        _draggingTabLeaf     = null;
        _tabDragIsReorder    = false;
        _tabReorderInsertX   = -1f;
        _tabReorderInsertIdx = -1;
        if (wasDividerDrag)
            sapp_set_mouse_cursor(sapp_mouse_cursor.SAPP_MOUSECURSOR_DEFAULT);
        return handled;
    }

    public override bool OnMouseLeave(MouseEvent e)
    {
        _hoveredTabLeaf = null;
        _hoveredTabIdx  = -1;
        sapp_set_mouse_cursor(sapp_mouse_cursor.SAPP_MOUSECURSOR_DEFAULT);
        return false;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    // ─── Tab reorder helpers ──────────────────────────────────────────────────

    private void UpdateTabReorderIndicator(Vector2 local)
    {
        if (_draggingTabLeaf == null) return;
        _tabReorderMouseLocal = local;
        (_tabReorderInsertIdx, _tabReorderInsertX) = GetTabInsertPoint(_draggingTabLeaf, local.X);
    }

    private void CommitTabReorder(Vector2 local)
    {
        if (_draggingTabLeaf == null || _draggingTabPanel == null) return;
        var (dstIdx, _) = GetTabInsertPoint(_draggingTabLeaf, local.X);
        int srcIdx = _draggingTabLeaf.Panels.IndexOf(_draggingTabPanel);
        if (srcIdx < 0 || dstIdx == srcIdx || dstIdx == srcIdx + 1)
        {
            _tabReorderInsertX = -1f; _tabReorderInsertIdx = -1;
            return;
        }
        _draggingTabLeaf.Panels.RemoveAt(srcIdx);
        int insertAt = dstIdx > srcIdx ? dstIdx - 1 : dstIdx;
        _draggingTabLeaf.Panels.Insert(insertAt, _draggingTabPanel);
        _draggingTabLeaf.ActivePanelIndex = insertAt;
        _tabReorderInsertX = -1f; _tabReorderInsertIdx = -1;
        RaiseTreeChanged();
    }

    /// <summary>
    /// Returns the insertion index and its x position (local space) for a given
    /// horizontal cursor position within a leaf's tab bar.
    /// </summary>
    private (int InsertIdx, float InsertX) GetTabInsertPoint(DockNode leaf, float localX)
    {
        var b = leaf.ComputedBounds;
        // Use widths cached during the last DrawLeaf call (font was set correctly there).
        _tabWidthCache.TryGetValue(leaf, out var widths);
        float x = b.X + 4f;
        int count = leaf.Panels.Count;
        for (int i = 0; i < count; i++)
        {
            float tabW = (widths != null && i < widths.Length)
                ? widths[i]
                : (leaf.Panels[i].Title.Length * 7f + TabPaddingH * 2f);
            if (localX < x + tabW * 0.5f)
            {
                // Left edge of this tab — sits right on the border with the previous tab.
                return (i, x);
            }
            x += tabW + 2f;
        }
        // Right edge of the last tab.
        return (count, x - 2f);
    }

    private DockNode? FindDividerAt(DockNode node, Vector2 local)
    {
        if (node.IsLeaf) return null;
        if (node.DividerRect(DividerSize).Contains(local)) return node;
        return (node.First  != null ? FindDividerAt(node.First,  local) : null)
            ?? (node.Second != null ? FindDividerAt(node.Second, local) : null);
    }

    /// <param name="nearestFallback">
    /// When true the function returns the nearest tab if the point is in the tab
    /// bar but between/before tabs — but only if the point is within the span of
    /// all tabs (not past the last tab's right edge). Set false for hover tracking
    /// so empty space never lights up a tab.
    /// </param>
    internal (DockNode? Leaf, int TabIndex, Rect TabRect) HitTabInternal(Vector2 local, bool nearestFallback = false)
    {
        foreach (var leaf in Root.EnumerateLeaves())
        {
            var b = leaf.ComputedBounds;
            var tabBar = new Rect(b.X, b.Y, b.Width, TabBarHeight);
            if (!tabBar.Contains(local)) continue;

            // Use the per-frame cached widths when available (populated by DrawLeaf).
            if (!_tabWidthCache.TryGetValue(leaf, out var cachedWidths))
            {
                var r2 = Screen.Instance?.Renderer;
                cachedWidths = new float[leaf.Panels.Count];
                for (int i = 0; i < leaf.Panels.Count; i++)
                {
                    string t = leaf.Panels[i].Title;
                    cachedWidths[i] = (r2?.MeasureText(t) ?? t.Length * 7f) + TabPaddingH * 2f;
                }
            }

            float x = b.X + 4f;
            int   nearestIdx  = -1;
            float nearestDist = float.MaxValue;
            Rect  nearestRect = Rect.Empty;
            float tabsEndX    = x; // will be updated to the right edge of the last tab

            for (int i = 0; i < cachedWidths.Length && i < leaf.Panels.Count; i++)
            {
                float tabW = cachedWidths[i];
                // Use full tab-bar height for hit-testing so clicks anywhere in the
                // bar row (top, bottom, small gaps) register on the right tab.
                var tabRect = new Rect(x, b.Y, tabW, TabBarHeight);
                if (tabRect.Contains(local)) return (leaf, i, tabRect);

                // Track the nearest tab by horizontal distance for fallback.
                float dist = MathF.Abs(local.X - (x + tabW * 0.5f));
                if (dist < nearestDist) { nearestDist = dist; nearestIdx = i; nearestRect = tabRect; }
                tabsEndX = x + tabW;
                x += tabW + 2f;
            }

            // Fallback: point is in the tab bar but landed in a gap between/before
            // tabs. Only activate when the cursor is within the horizontal span of all
            // tabs so that clicking/hovering in the empty area past the last tab does
            // NOT highlight or activate anything.
            if (nearestFallback && nearestIdx >= 0 && local.X <= tabsEndX)
                return (leaf, nearestIdx, nearestRect);
        }
        return (null, -1, Rect.Empty);
    }

    /// <summary>
    /// Classify a screen-space point over the DockSpace into a drop-zone against a
    /// specific leaf. Used by <see cref="DockManager"/>.
    /// </summary>
    public (DockNode? Node, DockDropZone Zone) ClassifyDropZone(Vector2 screenPoint)
    {
        var local = ToLocal(screenPoint);
        if (!HitTest(local)) return (null, DockDropZone.None);

        var leaf = Root.HitTestLeaf(local);
        if (leaf == null) return (null, DockDropZone.None);

        // If leaf is empty just drop-target center.
        if (leaf.Panels.Count == 0) return (leaf, DockDropZone.Center);

        var b = leaf.ComputedBounds;
        float dx = local.X - b.X;
        float dy = local.Y - b.Y;
        float rx = b.Right  - local.X;
        float ry = b.Bottom - local.Y;
        float edge = MathF.Min(DropZoneHalfWidth, MathF.Min(b.Width, b.Height) * 0.35f);

        if (dx < edge && dx <= dy && dx <= ry) return (leaf, DockDropZone.Left);
        if (rx < edge && rx <= dy && rx <= ry) return (leaf, DockDropZone.Right);
        if (dy < edge && dy <= dx && dy <= rx) return (leaf, DockDropZone.Top);
        if (ry < edge && ry <= dx && ry <= rx) return (leaf, DockDropZone.Bottom);
        return (leaf, DockDropZone.Center);
    }

    private static Rect ComputeDropZoneRect(DockNode leaf, DockDropZone zone)
    {
        var b = leaf.ComputedBounds;
        return zone switch
        {
            DockDropZone.Left   => new Rect(b.X, b.Y, b.Width * 0.5f, b.Height),
            DockDropZone.Right  => new Rect(b.X + b.Width * 0.5f, b.Y, b.Width * 0.5f, b.Height),
            DockDropZone.Top    => new Rect(b.X, b.Y, b.Width, b.Height * 0.5f),
            DockDropZone.Bottom => new Rect(b.X, b.Y + b.Height * 0.5f, b.Width, b.Height * 0.5f),
            DockDropZone.Center => b,
            _                   => Rect.Empty,
        };
    }
}
