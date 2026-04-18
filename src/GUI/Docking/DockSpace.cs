using System;
using System.Collections.Generic;

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

    private DockNode? _draggingDivider;
    private float     _dragStartPos;
    private float     _dragStartRatio;

    private DockPanel? _draggingTabPanel;
    private Vector2    _tabDragStartScreen;
    private bool       _tabDragBegun;

    /// <summary>Fired whenever the tree structure changes.</summary>
    public event Action? TreeChanged;

    internal void RaiseTreeChanged() => TreeChanged?.Invoke();

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
        owner.RemovePanel(panel);
        var root = Root;
        owner.CollapseIfDegenerate(ref root);
        // If root itself collapsed and became the keeper, Root stays the same
        // (keeper is copied into the existing root instance). No reassignment needed.
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
        Root.Arrange(new Rect(0, 0, Bounds.Width, Bounds.Height), DividerSize);
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

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;
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
        var dm = Screen.Instance?.DockManagerOrNull;
        if (dm != null && dm.ActiveDragPanel != null &&
            dm.HoveredDropNode != null && dm.HoveredDropZone != DockDropZone.None &&
            IsAncestorOf(dm.HoveredDropNode))
        {
            var zoneRect = ComputeDropZoneRect(dm.HoveredDropNode, dm.HoveredDropZone);
            renderer.FillRect(zoneRect, theme.Primary.WithAlpha(0.25f));
            renderer.StrokeRect(zoneRect, 2f, theme.Primary);
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
        float x = b.X + 4f;
        for (int i = 0; i < leaf.Panels.Count; i++)
        {
            var p = leaf.Panels[i];
            float textW = renderer.MeasureText(p.Title);
            float tabW = textW + TabPaddingH * 2f;
            var tabRect = new Rect(x, b.Y + 2f, tabW, TabBarHeight - 3f);
            var isActive = i == leaf.ActivePanelIndex;
            renderer.FillRect(tabRect, isActive ? theme.TabActive : theme.TabInactive);
            if (isActive)
                renderer.FillRect(new Rect(tabRect.X, tabRect.Bottom - 2f, tabRect.Width, 2f), theme.Primary);
            renderer.DrawText(tabRect.X + TabPaddingH, tabRect.Y + tabRect.Height * 0.5f, p.Title, theme.TabText);
            x += tabW + 2f;
        }

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

        // Tab hit?
        var (leaf, tabIdx, tabRect) = HitTabInternal(local);
        if (leaf != null && tabIdx >= 0)
        {
            leaf.ActivePanelIndex = tabIdx;
            _draggingTabPanel    = leaf.Panels[tabIdx];
            _tabDragStartScreen  = e.Position;
            _tabDragBegun        = false;
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
            return true;
        }

        if (_draggingTabPanel != null)
        {
            var delta = e.Position - _tabDragStartScreen;
            if (!_tabDragBegun && (MathF.Abs(delta.X) > 5f || MathF.Abs(delta.Y) > 5f))
            {
                _tabDragBegun = true;
                Screen.Instance?.DockManagerOrNull?.BeginDragPanel(_draggingTabPanel, e.Position);
            }
            if (_tabDragBegun)
                Screen.Instance?.DockManagerOrNull?.UpdateDrag(e.Position);
            return true;
        }
        return false;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        bool handled = _draggingDivider != null || _draggingTabPanel != null;
        if (_tabDragBegun && _draggingTabPanel != null)
            Screen.Instance?.DockManagerOrNull?.EndDrag(e.Position);
        _draggingDivider   = null;
        _draggingTabPanel  = null;
        _tabDragBegun      = false;
        return handled;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private DockNode? FindDividerAt(DockNode node, Vector2 local)
    {
        if (node.IsLeaf) return null;
        if (node.DividerRect(DividerSize).Contains(local)) return node;
        return (node.First  != null ? FindDividerAt(node.First,  local) : null)
            ?? (node.Second != null ? FindDividerAt(node.Second, local) : null);
    }

    internal (DockNode? Leaf, int TabIndex, Rect TabRect) HitTabInternal(Vector2 local)
    {
        foreach (var leaf in Root.EnumerateLeaves())
        {
            var b = leaf.ComputedBounds;
            var tabBar = new Rect(b.X, b.Y, b.Width, TabBarHeight);
            if (!tabBar.Contains(local)) continue;
            float x = b.X + 4f;
            var renderer = Screen.Instance?.Renderer;
            for (int i = 0; i < leaf.Panels.Count; i++)
            {
                string title = leaf.Panels[i].Title;
                float textW = renderer?.MeasureText(title) ?? (title.Length * 7f);
                float tabW = textW + TabPaddingH * 2f;
                var tabRect = new Rect(x, b.Y + 2f, tabW, TabBarHeight - 3f);
                if (tabRect.Contains(local)) return (leaf, i, tabRect);
                x += tabW + 2f;
            }
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
