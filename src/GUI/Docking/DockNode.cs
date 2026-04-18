using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Node in the docking binary tree.
/// A leaf holds a tab group of <see cref="DockPanel"/>s; a split node has two
/// children arranged horizontally or vertically with a user-draggable divider.
/// </summary>
public sealed class DockNode
{
    /// <summary>Stable id used for layout persistence (auto-generated if empty).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DockNodeType Type { get; set; } = DockNodeType.Leaf;

    /// <summary>0..1 — fraction of parent extent assigned to <see cref="First"/>.</summary>
    public float SplitRatio { get; set; } = 0.5f;

    public DockNode? First  { get; set; }
    public DockNode? Second { get; set; }
    public DockNode? Parent { get; internal set; }

    /// <summary>Panels in this leaf's tab group (empty for split nodes).</summary>
    public List<DockPanel> Panels { get; } = [];

    /// <summary>Selected tab index (leaf only).</summary>
    public int ActivePanelIndex { get; set; }

    /// <summary>Bounds assigned during the most recent layout pass (DockSpace-local).</summary>
    public Rect ComputedBounds { get; set; }

    public bool IsLeaf => Type == DockNodeType.Leaf;

    /// <summary>Convenience: currently visible panel, or null if none.</summary>
    public DockPanel? ActivePanel =>
        IsLeaf && ActivePanelIndex >= 0 && ActivePanelIndex < Panels.Count
            ? Panels[ActivePanelIndex]
            : null;

    // ─── Panel management (leaf) ─────────────────────────────────────────────

    public void AddPanel(DockPanel panel)
    {
        if (!IsLeaf) throw new InvalidOperationException("Cannot add panel to split node.");
        panel.Owner = this;
        panel.IsFloating = false;
        Panels.Add(panel);
        ActivePanelIndex = Panels.Count - 1;
    }

    public bool RemovePanel(DockPanel panel)
    {
        if (!IsLeaf) return false;
        int idx = Panels.IndexOf(panel);
        if (idx < 0) return false;
        Panels.RemoveAt(idx);
        if (panel.Owner == this) panel.Owner = null;
        if (ActivePanelIndex >= Panels.Count) ActivePanelIndex = Panels.Count - 1;
        if (ActivePanelIndex < 0) ActivePanelIndex = 0;
        return true;
    }

    /// <summary>
    /// Split this leaf into two children along <paramref name="splitType"/>.
    /// The existing tab group stays in one child; <paramref name="newPanel"/>
    /// goes to the other. <paramref name="newPanelFirst"/> selects which.
    /// </summary>
    public void SplitLeaf(DockNodeType splitType, DockPanel newPanel, bool newPanelFirst, float ratio = 0.5f)
    {
        if (!IsLeaf) throw new InvalidOperationException("Cannot split a non-leaf node.");
        if (splitType == DockNodeType.Leaf) throw new ArgumentException("splitType must be a split type.");

        // Move existing panels to a new leaf.
        var existing = new DockNode { Type = DockNodeType.Leaf, Parent = this };
        foreach (var p in Panels) { p.Owner = existing; existing.Panels.Add(p); }
        existing.ActivePanelIndex = Math.Clamp(ActivePanelIndex, 0, Math.Max(0, existing.Panels.Count - 1));
        Panels.Clear();
        ActivePanelIndex = 0;

        var other = new DockNode { Type = DockNodeType.Leaf, Parent = this };
        other.AddPanel(newPanel);

        Type = splitType;
        SplitRatio = Math.Clamp(ratio, 0.05f, 0.95f);
        if (newPanelFirst) { First = other; Second = existing; }
        else               { First = existing; Second = other; }
    }

    /// <summary>
    /// If this split node has one empty leaf child, collapse: replace self in
    /// parent with the remaining child. Walks up as long as collapse applies.
    /// </summary>
    public void CollapseIfDegenerate(ref DockNode root)
    {
        if (IsLeaf)
        {
            Sokol.SLog.Info($"[Dock] CollapseIfDegenerate: node {Id[..8]} is a leaf — no-op", "Dock");
            return;
        }

        bool firstEmpty  = First  is { IsLeaf: true, Panels.Count: 0 };
        bool secondEmpty = Second is { IsLeaf: true, Panels.Count: 0 };
        Sokol.SLog.Info($"[Dock] CollapseIfDegenerate: node {Id[..8]} type={Type} firstEmpty={firstEmpty} secondEmpty={secondEmpty} firstType={First?.Type} secondType={Second?.Type}", "Dock");

        DockNode? keeper = null;
        if (firstEmpty && !secondEmpty) keeper = Second;
        else if (secondEmpty && !firstEmpty) keeper = First;
        else if (firstEmpty && secondEmpty)
        {
            // Both empty → become an empty leaf.
            Sokol.SLog.Info($"[Dock] CollapseIfDegenerate: both empty → become empty leaf", "Dock");
            Type = DockNodeType.Leaf;
            First = null;
            Second = null;
            return;
        }
        if (keeper == null)
        {
            Sokol.SLog.Info($"[Dock] CollapseIfDegenerate: no empty children — no collapse", "Dock");
            return;
        }
        Sokol.SLog.Info($"[Dock] CollapseIfDegenerate: keeper={keeper.Id[..8]} panels=[{string.Join(",", keeper.Panels.ConvertAll(p => p.Title))}] keeperType={keeper.Type}", "Dock");

        // Replace self with keeper: copy keeper into this.
        Type             = keeper.Type;
        SplitRatio       = keeper.SplitRatio;
        First            = keeper.First;
        Second           = keeper.Second;
        Panels.Clear();
        foreach (var p in keeper.Panels) { p.Owner = this; Panels.Add(p); }
        ActivePanelIndex = keeper.ActivePanelIndex;
        if (First  != null) First.Parent  = this;
        if (Second != null) Second.Parent = this;

        // Walk up.
        Parent?.CollapseIfDegenerate(ref root);
    }

    /// <summary>Enumerate leaves in draw / hit-test order.</summary>
    public IEnumerable<DockNode> EnumerateLeaves()
    {
        if (IsLeaf)
        {
            yield return this;
            yield break;
        }
        if (First != null)
            foreach (var n in First.EnumerateLeaves()) yield return n;
        if (Second != null)
            foreach (var n in Second.EnumerateLeaves()) yield return n;
    }

    /// <summary>Find the leaf under <paramref name="local"/> (DockSpace-local coords).</summary>
    public DockNode? HitTestLeaf(Vector2 local)
    {
        if (!ComputedBounds.Contains(local)) return null;
        if (IsLeaf) return this;
        return First?.HitTestLeaf(local) ?? Second?.HitTestLeaf(local);
    }

    /// <summary>Recompute layout starting at <paramref name="bounds"/>.</summary>
    public void Arrange(Rect bounds, float dividerSize)
    {
        ComputedBounds = bounds;
        if (IsLeaf) return;

        if (Type == DockNodeType.SplitHorizontal)
        {
            float inner = bounds.Width - dividerSize;
            if (inner < 0) inner = 0;
            float w1 = inner * SplitRatio;
            float w2 = inner - w1;
            First? .Arrange(new Rect(bounds.X,                        bounds.Y, w1, bounds.Height), dividerSize);
            Second?.Arrange(new Rect(bounds.X + w1 + dividerSize,     bounds.Y, w2, bounds.Height), dividerSize);
        }
        else // SplitVertical
        {
            float inner = bounds.Height - dividerSize;
            if (inner < 0) inner = 0;
            float h1 = inner * SplitRatio;
            float h2 = inner - h1;
            First? .Arrange(new Rect(bounds.X, bounds.Y,                    bounds.Width, h1), dividerSize);
            Second?.Arrange(new Rect(bounds.X, bounds.Y + h1 + dividerSize, bounds.Width, h2), dividerSize);
        }
    }

    /// <summary>Divider rect for this split node (DockSpace-local coords); empty for leaves.</summary>
    public Rect DividerRect(float dividerSize)
    {
        if (IsLeaf || First == null || Second == null) return Rect.Empty;
        return Type == DockNodeType.SplitHorizontal
            ? new Rect(First.ComputedBounds.Right, ComputedBounds.Y, dividerSize, ComputedBounds.Height)
            : new Rect(ComputedBounds.X, First.ComputedBounds.Bottom, ComputedBounds.Width, dividerSize);
    }
}
