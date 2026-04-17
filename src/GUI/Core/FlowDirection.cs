namespace Sokol.GUI;

/// <summary>
/// Specifies the direction in which text and UI elements flow.
/// </summary>
public enum FlowDirection
{
    /// <summary>Left-to-right (default for Latin, Cyrillic, etc.).</summary>
    LeftToRight,
    /// <summary>Right-to-left (for Arabic, Hebrew, etc.).</summary>
    RightToLeft,
    /// <summary>Inherit from parent; if root, use LeftToRight.</summary>
    Auto
}
