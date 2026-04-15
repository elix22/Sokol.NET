namespace Sokol.GUI;

/// <summary>
/// A layout algorithm is responsible for measuring desired size and
/// arranging a parent widget's children within a given rect.
/// </summary>
public interface ILayout
{
    /// <summary>
    /// Measure the total size this layout needs given the available space.
    /// Does NOT change any widget state.
    /// </summary>
    Vector2 Measure(Widget parent, Renderer renderer, Vector2 availableSize);

    /// <summary>
    /// Position and size each child of <paramref name="parent"/> within
    /// <paramref name="finalRect"/>.  Sets <c>child.Bounds</c>.
    /// </summary>
    void Arrange(Widget parent, Renderer renderer, Rect finalRect);
}
