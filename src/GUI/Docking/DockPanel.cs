using System;

namespace Sokol.GUI;

/// <summary>
/// A single dockable panel: a titled frame containing one user widget.
/// Panels live in either a <see cref="DockNode"/> tab group or the
/// <see cref="FloatingPanelHost"/> overlay.
/// </summary>
public sealed class DockPanel
{
    /// <summary>Stable identifier used for layout persistence.</summary>
    public string Id { get; }

    /// <summary>Title rendered on the tab strip.</summary>
    public string Title { get; set; }

    /// <summary>The content widget drawn inside this panel. Non-null.</summary>
    public Widget Content { get; set; }

    /// <summary>When true, the panel lives in the <see cref="FloatingPanelHost"/> instead of a leaf node.</summary>
    public bool IsFloating { get; internal set; }

    /// <summary>Floating-window bounds when <see cref="IsFloating"/> is true.</summary>
    public Rect FloatingBounds { get; set; }

    /// <summary>Leaf node that owns the panel, or null if floating / detached.</summary>
    public DockNode? Owner { get; internal set; }

    /// <summary>Whether the panel can be closed by the user.</summary>
    public bool CanClose { get; set; } = true;

    /// <summary>Fired when the user closes the tab.</summary>
    public event Action<DockPanel>? Closed;

    public DockPanel(string id, string title, Widget content)
    {
        Id      = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
        Title   = title;
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    internal void RaiseClosed() => Closed?.Invoke(this);
}
