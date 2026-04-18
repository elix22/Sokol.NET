namespace Sokol.GUI;

/// <summary>
/// Kind of node in the docking tree.
/// </summary>
public enum DockNodeType
{
    /// <summary>Holds a tab group of <see cref="DockPanel"/>s. Terminal node.</summary>
    Leaf,
    /// <summary>Split with two children stacked left/right. Split ratio applies to width.</summary>
    SplitHorizontal,
    /// <summary>Split with two children stacked top/bottom. Split ratio applies to height.</summary>
    SplitVertical,
}
