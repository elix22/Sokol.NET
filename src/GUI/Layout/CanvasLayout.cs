namespace Sokol.GUI;

/// <summary>
/// Absolute (canvas) positioning.  Each child is placed at its own
/// <see cref="Widget.Position"/> with its preferred or fixed size.
/// This is the default layout used when no layout is specified.
/// </summary>
public sealed class CanvasLayout : ILayout
{
    public static readonly CanvasLayout Instance = new();

    // When FixedSize has a 0 component, treat it as "auto" for that axis.
    // FixedSize=(0,36) means "fill available width, fixed 36px height".
    private static Vector2 EffectiveSize(Widget child, Renderer renderer, Vector2 available)
    {
        if (!child.FixedSize.HasValue) return child.PreferredSize(renderer);
        var fs = child.FixedSize.Value;
        var pref = (fs.X == 0f || fs.Y == 0f) ? child.PreferredSize(renderer) : default;
        return new Vector2(
            fs.X > 0f ? fs.X : (available.X > 0f ? available.X : pref.X),
            fs.Y > 0f ? fs.Y : pref.Y);
    }

    public Vector2 Measure(Widget parent, Renderer renderer, Vector2 availableSize)
    {
        // Canvas size = bounding box of all children.
        float maxX = 0f, maxY = 0f;
        foreach (var child in parent.Children)
        {
            if (!child.Visible) continue;
            var size = EffectiveSize(child, renderer, availableSize);
            maxX = MathF.Max(maxX, child.Position.X + size.X);
            maxY = MathF.Max(maxY, child.Position.Y + size.Y);
        }
        return new Vector2(maxX, maxY);
    }

    public void Arrange(Widget parent, Renderer renderer, Rect finalRect)
    {
        bool log = Screen.DbgFrame <= 5 || Screen.DbgFrame % 300 == 0;
        if (log)
            Sokol.SLog.Info($"CanvasLayout[{Screen.DbgFrame}]: parent={parent.GetType().Name} finalRect={finalRect} children={parent.Children.Count}", "Sokol.GUI");

        var available = new Vector2(finalRect.Width, finalRect.Height);
        foreach (var child in parent.Children)
        {
            if (!child.Visible) continue;
            var size = EffectiveSize(child, renderer, available);
            // Expand: fill remaining space from the child's position to the parent edge.
            if (child.Expand)
            {
                if (size.X < available.X - child.Position.X)
                    size = new Vector2(available.X - child.Position.X, size.Y);
                if (size.Y < available.Y - child.Position.Y)
                    size = new Vector2(size.X, available.Y - child.Position.Y);
            }
            child.Bounds = new Rect(
                child.Position.X,
                child.Position.Y,
                size.X, size.Y);
            if (log)
                Sokol.SLog.Info($"CanvasLayout[{Screen.DbgFrame}]:   {child.GetType().Name} FixedSize={child.FixedSize} size={size} Position={child.Position} → Bounds={child.Bounds}", "Sokol.GUI");
        }
    }
}
