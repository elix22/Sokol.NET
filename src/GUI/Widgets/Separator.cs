namespace Sokol.GUI;

/// <summary>
/// Horizontal or vertical visual divider.
/// </summary>
public class Separator : Widget
{
    public bool     IsVertical  { get; set; } = false;
    public UIColor? Color       { get; set; }
    public float    Thickness   { get; set; } = 1f;

    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        return IsVertical ? new Vector2(Thickness + Padding.Horizontal, 20)
                          : new Vector2(20, Thickness + Padding.Vertical);
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme = ThemeManager.Current;
        var c     = Color ?? theme.SeparatorColor;
        float w   = Bounds.Width, h = Bounds.Height;

        if (IsVertical)
            renderer.DrawLine(w * 0.5f, Padding.Top, w * 0.5f, h - Padding.Bottom, Thickness, c);
        else
            renderer.DrawLine(Padding.Left, h * 0.5f, w - Padding.Right, h * 0.5f, Thickness, c);
    }
}
