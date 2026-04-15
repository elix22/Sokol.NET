namespace Sokol.GUI;

/// <summary>
/// A filled, optionally bordered container widget.
/// </summary>
public class Panel : Widget
{
    public UIColor?    BackgroundColor { get; set; }
    public UIColor?    BorderColor     { get; set; }
    public float       BorderWidth     { get; set; } = 0f;
    public CornerRadius CornerRadius   { get; set; }
    public bool        DrawShadow      { get; set; } = false;

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var bg     = BackgroundColor ?? theme.SurfaceColor;
        var cr     = CornerRadius;
        bool uniform = cr.IsUniform;

        // Drop shadow.
        if (DrawShadow)
            renderer.DrawDropShadow(bounds, cr.TopLeft, theme.ShadowOffset, theme.ShadowBlur, theme.ShadowColor);

        // Background fill.
        if (bg.A > 0f)
        {
            if (uniform)
                renderer.FillRoundedRect(bounds, cr.TopLeft, bg);
            else
                renderer.FillRect(bounds, bg);
        }

        // Border.
        if (BorderWidth > 0f)
        {
            var bc = BorderColor ?? theme.BorderColor;
            if (uniform)
                renderer.StrokeRoundedRect(bounds, cr.TopLeft, BorderWidth, bc);
            else
                renderer.StrokeRect(bounds, BorderWidth, bc);
        }

        // Draw children (base Widget handles transform/clip).
        base.Draw(renderer);
    }
}
