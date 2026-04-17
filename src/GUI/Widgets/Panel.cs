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
        var bg     = BackgroundColor;          // null = transparent (no fill)
        var cr     = CornerRadius;
        bool uniform = cr.IsUniform;

        // Drop shadow (NanoGUI-style: visible dark blur underneath).
        if (DrawShadow)
            renderer.DrawDropShadow(bounds, cr.TopLeft, theme.ShadowOffset, theme.ShadowBlur, theme.ShadowColor);

        // Background fill — only when an explicit BackgroundColor is set.
        if (bg.HasValue && bg.Value.A > 0f)
        {
            var bgColor   = bg.Value;
            var panelGrad = renderer.LinearGradient(
                new Vector2(0, 0),
                new Vector2(0, bounds.Height),
                bgColor.Lighten(0.06f), bgColor.Darken(0.06f));
            if (uniform)
                renderer.FillRoundedRectWithPaint(bounds, cr.TopLeft, panelGrad);
            else
                renderer.FillRoundedRectWithPaint(bounds, 0f, panelGrad);
        }

        // Border — double bevel for visible depth.
        if (BorderWidth > 0f)
        {
            var bc = BorderColor ?? theme.BorderColor;
            if (uniform)
            {
                // Light top-edge highlight
                renderer.StrokeRoundedRect(
                    new Rect(0.5f, 1.5f, bounds.Width - 1f, bounds.Height - 2f),
                    cr.TopLeft, 1f, ThemeManager.Current.BorderLight.WithAlpha(0.5f));
                // Dark outer border
                renderer.StrokeRoundedRect(
                    new Rect(0.5f, 0.5f, bounds.Width - 1f, bounds.Height - 1f),
                    cr.TopLeft, BorderWidth, bc);
            }
            else
                renderer.StrokeRect(bounds, BorderWidth, bc);
        }

        // Draw children (base Widget handles transform/clip).
        base.Draw(renderer);
    }
}
