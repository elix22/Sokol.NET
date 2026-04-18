namespace Sokol.GUI;

/// <summary>
/// Zone a dragged <see cref="DockPanel"/> is hovering over a potential drop target.
/// </summary>
public enum DockDropZone
{
    /// <summary>Cursor is not over any drop target.</summary>
    None,
    /// <summary>Split the target leaf horizontally; dropped panel becomes the left child.</summary>
    Left,
    /// <summary>Split the target leaf horizontally; dropped panel becomes the right child.</summary>
    Right,
    /// <summary>Split the target leaf vertically; dropped panel becomes the top child.</summary>
    Top,
    /// <summary>Split the target leaf vertically; dropped panel becomes the bottom child.</summary>
    Bottom,
    /// <summary>Add the panel to the target leaf's tab group.</summary>
    Center,
    /// <summary>Drop into the floating overlay at the cursor position.</summary>
    Floating,
}
