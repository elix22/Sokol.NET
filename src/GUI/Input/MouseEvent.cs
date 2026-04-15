namespace Sokol.GUI;

public enum MouseButton { None, Left, Middle, Right }

public sealed class MouseEvent : InputEvent
{
    public Vector2     Position  { get; init; }  // screen-space logical pixels
    public Vector2     Delta     { get; init; }
    public Vector2     Scroll    { get; init; }
    public MouseButton Button    { get; init; }
    public int         Clicks    { get; init; }  // 1 = single, 2 = double
}
