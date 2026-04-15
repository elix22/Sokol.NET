namespace Sokol.GUI;

public sealed class TouchPoint
{
    public int     Id       { get; init; }
    public Vector2 Position { get; init; }  // screen-space logical pixels
    public bool    Changed  { get; init; }
}

public sealed class TouchEvent : InputEvent
{
    public TouchPoint[] Touches { get; init; } = [];
}
