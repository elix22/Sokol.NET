namespace Sokol.GUI;

/// <summary>
/// Convenience vertical <see cref="BoxLayout"/> with full-width (Stretch) children.
/// </summary>
public sealed class StackLayout : ILayout
{
    public float   Spacing { get; set; } = 0f;
    private readonly BoxLayout _inner;

    public StackLayout(float spacing = 0f)
    {
        Spacing = spacing;
        _inner  = new BoxLayout(Orientation.Vertical, Alignment.Stretch, spacing);
    }

    public Vector2 Measure(Widget parent, Renderer renderer, Vector2 availableSize)
    {
        _inner.Spacing = Spacing;
        return _inner.Measure(parent, renderer, availableSize);
    }

    public void Arrange(Widget parent, Renderer renderer, Rect finalRect)
    {
        _inner.Spacing = Spacing;
        _inner.Arrange(parent, renderer, finalRect);
    }
}
