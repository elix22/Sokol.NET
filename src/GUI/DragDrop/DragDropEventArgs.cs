namespace Sokol.GUI;

/// <summary>
/// Event payload delivered to a widget's drag / drop virtual methods.
/// </summary>
public sealed class DragDropEventArgs
{
    /// <summary>The in-flight drag data.</summary>
    public DragDropData Data { get; }

    /// <summary>Cursor position in screen space.</summary>
    public Vector2 ScreenPosition { get; }

    /// <summary>Cursor position in the receiving widget's local space.</summary>
    public Vector2 LocalPosition { get; }

    /// <summary>
    /// Effect chosen / advertised by the handler. <see cref="Widget.OnDragOver"/>
    /// sets this to indicate what would happen on drop at the current position;
    /// <see cref="DragManager"/> uses it to render the correct cursor feedback.
    /// </summary>
    public DragDropEffect Effect { get; set; } = DragDropEffect.None;

    /// <summary>Set by drop targets to signal the drop was handled.</summary>
    public bool Handled { get; set; }

    public DragDropEventArgs(DragDropData data, Vector2 screenPos, Vector2 localPos)
    {
        Data           = data;
        ScreenPosition = screenPos;
        LocalPosition  = localPos;
    }
}
