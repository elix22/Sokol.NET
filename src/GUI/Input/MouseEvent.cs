namespace Sokol.GUI;

public enum MouseButton { None, Left, Middle, Right }

public sealed class MouseEvent : InputEvent
{
    /// <summary>Cursor position in screen (logical-pixel) coordinates.</summary>
    public Vector2     Position      { get; init; }
    /// <summary>
    /// Cursor position in the receiving widget's local coordinate space
    /// (origin = widget top-left). Populated automatically by InputRouter.
    /// Prefer this over <c>ToLocal(e.Position)</c> in all OnMouseXxx handlers.
    /// </summary>
    public Vector2     LocalPosition { get; init; }
    public Vector2     Delta         { get; init; }
    public Vector2     Scroll        { get; init; }
    public MouseButton Button        { get; init; }
    public int         Clicks        { get; init; }  // 1 = single, 2 = double
}
