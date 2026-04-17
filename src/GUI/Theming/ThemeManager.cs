using System;

namespace Sokol.GUI;

/// <summary>
/// Manages the active theme.  All widgets read from <see cref="Current"/>
/// at draw time so a theme switch takes effect immediately on the next frame.
/// </summary>
public static class ThemeManager
{
    private static Theme _current = new DarkTheme();

    /// <summary>The currently active theme.</summary>
    public static Theme Current => _current;

    /// <summary>
    /// Fired after <see cref="Apply"/> changes the active theme.
    /// Widgets can subscribe to invalidate cached visuals.
    /// </summary>
    public static event Action? ThemeChanged;

    /// <summary>Swap the active theme and notify all subscribers.</summary>
    public static void Apply(Theme theme)
    {
        _current = theme ?? throw new ArgumentNullException(nameof(theme));
        ThemeChanged?.Invoke();
    }

    /// <summary>
    /// Override the global flow direction without swapping the entire theme.
    /// All widgets whose <see cref="FlowDirection"/> is <see cref="FlowDirection.Auto"/>
    /// will resolve to this direction at the root.
    /// </summary>
    public static void SetFlowDirection(FlowDirection direction)
    {
        _globalFlowDirection = direction;
        ThemeChanged?.Invoke(); // reuse same notification channel to force relayout
    }

    private static FlowDirection _globalFlowDirection = FlowDirection.LeftToRight;

    /// <summary>The active global flow direction (used as root fallback for Auto widgets).</summary>
    public static FlowDirection GlobalFlowDirection => _globalFlowDirection;
}
