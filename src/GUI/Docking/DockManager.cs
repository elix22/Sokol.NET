using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Orchestrates docking: owns the root <see cref="DockSpace"/> and the
/// <see cref="FloatingPanelHost"/>, and tracks the current drag-and-dock
/// operation. Accessed via <see cref="Screen.DockManager"/>.
/// </summary>
public sealed class DockManager
{
    public DockSpace         RootDockSpace { get; }
    public FloatingPanelHost FloatingHost  { get; }

    /// <summary>All panels known to the manager (docked or floating).</summary>
    private readonly List<DockPanel> _panels = [];
    public IReadOnlyList<DockPanel> AllPanels => _panels;

    // ─── Drag state ──────────────────────────────────────────────────────────
    public DockPanel?    ActiveDragPanel { get; private set; }
    public DockNode?     HoveredDropNode { get; private set; }
    public DockDropZone  HoveredDropZone { get; private set; }

    public event Action? LayoutChanged;

    public DockManager(DockSpace rootDockSpace, FloatingPanelHost floatingHost)
    {
        RootDockSpace = rootDockSpace ?? throw new ArgumentNullException(nameof(rootDockSpace));
        FloatingHost  = floatingHost  ?? throw new ArgumentNullException(nameof(floatingHost));
        RootDockSpace.Manager = this;
        FloatingHost._manager = this;
        RootDockSpace.TreeChanged += () => LayoutChanged?.Invoke();
    }

    // ─── Panel lifecycle ─────────────────────────────────────────────────────

    public DockPanel CreatePanel(string id, string title, Widget content, DockNode? target = null, DockDropZone zone = DockDropZone.Center)
    {
        var panel = new DockPanel(id, title, content);
        _panels.Add(panel);
        RootDockSpace.AddPanel(panel, target, zone);
        LayoutChanged?.Invoke();
        return panel;
    }

    public void Close(DockPanel panel)
    {
        if (panel.IsFloating) FloatingHost.Remove(panel);
        else                  RootDockSpace.RemovePanel(panel);
        _panels.Remove(panel);
        panel.RaiseClosed();
        LayoutChanged?.Invoke();
    }

    /// <summary>
    /// Registers an existing panel with the manager without modifying the dock
    /// tree. Used by <see cref="LayoutManager"/> after restoring a layout so
    /// that <see cref="AllPanels"/> stays accurate.
    /// </summary>
    internal void RegisterPanel(DockPanel panel)
    {
        if (!_panels.Contains(panel))
            _panels.Add(panel);
    }

    /// <summary>Detach a panel from its docking tree leaf and put it into the floating overlay.</summary>
    public void Float(DockPanel panel, Rect floatingScreenBounds)
    {
        if (panel.Owner != null)
            RootDockSpace.RemovePanel(panel);
        FloatingHost.Add(panel, floatingScreenBounds);
        LayoutChanged?.Invoke();
    }

    /// <summary>Move a floating panel back into the dock tree at the given target / zone.</summary>
    public void Dock(DockPanel panel, DockNode target, DockDropZone zone)
    {
        if (panel.IsFloating) FloatingHost.Remove(panel);
        else if (panel.Owner != null) RootDockSpace.RemovePanel(panel);
        RootDockSpace.AddPanel(panel, target, zone);
        LayoutChanged?.Invoke();
    }

    // ─── Drag from a dock tab ────────────────────────────────────────────────

    public void BeginDragPanel(DockPanel panel, Vector2 screenPos)
    {
        ActiveDragPanel = panel;
        HoveredDropNode = null;
        HoveredDropZone = DockDropZone.None;
        var ownerPanels = panel.Owner != null ? string.Join(",", panel.Owner.Panels.ConvertAll(p => p.Title)) : "(floating)";
        Sokol.SLog.Info($"[Dock] BeginDrag panel='{panel.Title}' ownerPanels=[{ownerPanels}] ownerIsRoot={panel.Owner == RootDockSpace.Root}", "Dock");
    }

    public void UpdateDrag(Vector2 screenPos)
    {
        if (ActiveDragPanel == null) return;
        var (node, zone) = RootDockSpace.ClassifyDropZone(screenPos);
        HoveredDropNode = node;
        HoveredDropZone = zone;
    }

    public void EndDrag(Vector2 screenPos)
    {
        if (ActiveDragPanel == null) return;

        var (node, zone) = RootDockSpace.ClassifyDropZone(screenPos);
        Sokol.SLog.Info($"[Dock] EndDrag panel='{ActiveDragPanel.Title}' dropNode={node?.Id[..8] ?? "null"} zone={zone} sameNode={ActiveDragPanel.Owner == node} ownerCount={ActiveDragPanel.Owner?.Panels.Count}", "Dock");
        if (node != null && zone != DockDropZone.None && zone != DockDropZone.Floating)
        {
            // Re-dock at target.
            if (ActiveDragPanel.Owner != node || ActiveDragPanel.Owner?.Panels.Count > 1 || zone != DockDropZone.Center)
            {
                Sokol.SLog.Info($"[Dock] EndDrag → Remove then AddPanel zone={zone}", "Dock");
                // Snapshot an anchor panel from the target node BEFORE RemovePanel.
                // CollapseIfDegenerate may orphan `node` by copying a sibling's data
                // into the parent in-place; anchorPanel.Owner resolves the surviving node.
                //
                // Priority: pick any panel in `node` that is NOT the drag panel.
                // If none exists (node only holds the drag panel), pick from the
                // sibling subtree — after collapse its panels' Owner will point to
                // the parent node that absorbed the sibling, which is the correct target.
                DockPanel? anchorPanel = null;
                foreach (var p in node.Panels)
                    if (p != ActiveDragPanel) { anchorPanel = p; break; }
                if (anchorPanel == null && node.Parent != null)
                {
                    var sibling = (node.Parent.First == node) ? node.Parent.Second : node.Parent.First;
                    var siblingLeaf = sibling?.IsLeaf == true ? sibling
                                   : sibling?.EnumerateLeaves().FirstOrDefault();
                    anchorPanel = siblingLeaf?.Panels.Count > 0 ? siblingLeaf.Panels[0] : null;
                }
                Sokol.SLog.Info($"[Dock] EndDrag anchor={anchorPanel?.Title ?? "null"} (from {(anchorPanel == null ? "none" : anchorPanel.Owner?.Id[..8] ?? "?")})", "Dock");
                RootDockSpace.RemovePanel(ActiveDragPanel);
                var resolvedTarget = anchorPanel?.Owner ?? node;
                Sokol.SLog.Info($"[Dock] EndDrag resolvedTarget={resolvedTarget.Id[..8]} (node={node.Id[..8]} anchorOwner={anchorPanel?.Owner?.Id[..8] ?? "null"})", "Dock");
                RootDockSpace.AddPanel(ActiveDragPanel, resolvedTarget, zone);
            }
            else
            {
                Sokol.SLog.Info($"[Dock] EndDrag → same node/center, no-op", "Dock");
            }
        }
        else
        {
            // Drop outside → float.
            Sokol.SLog.Info($"[Dock] EndDrag → Float (outside dockspace)", "Dock");
            var floatRect = new Rect(screenPos.X - 80f, screenPos.Y - 12f, 280f, 180f);
            RootDockSpace.RemovePanel(ActiveDragPanel);
            FloatingHost.Add(ActiveDragPanel, floatRect);
        }
        ActiveDragPanel = null;
        HoveredDropNode = null;
        HoveredDropZone = DockDropZone.None;
        LayoutChanged?.Invoke();
    }

    public void CancelDrag()
    {
        ActiveDragPanel = null;
        HoveredDropNode = null;
        HoveredDropZone = DockDropZone.None;
    }

    // ─── Drag from a floating panel ──────────────────────────────────────────

    public void UpdateFloatingDrag(DockPanel panel, Vector2 screenPos)
    {
        ActiveDragPanel = panel;
        var (node, zone) = RootDockSpace.ClassifyDropZone(screenPos);
        HoveredDropNode = node;
        HoveredDropZone = zone;
    }

    public void EndFloatingDrag(DockPanel panel, Vector2 screenPos)
    {
        var (node, zone) = RootDockSpace.ClassifyDropZone(screenPos);
        if (node != null && zone != DockDropZone.None && zone != DockDropZone.Floating)
        {
            FloatingHost.Remove(panel);
            RootDockSpace.AddPanel(panel, node, zone);
        }
        ActiveDragPanel = null;
        HoveredDropNode = null;
        HoveredDropZone = DockDropZone.None;
        LayoutChanged?.Invoke();
    }
}
