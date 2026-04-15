namespace Sokol.GUI;

/// <summary>
/// Displays a <see cref="UIImage"/> inside its bounds.
/// </summary>
public class Image : Widget
{
    public UIImage? Source     { get; set; }
    public float    Alpha      { get; set; } = 1f;
    public bool     KeepAspect { get; set; } = true;

    public int Width  { get; set; } = 0;  // 0 = natural
    public int Height { get; set; } = 0;

    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        if (Source == null) return new Vector2(Width > 0 ? Width : 64, Height > 0 ? Height : 64);
        return new Vector2(
            Width  > 0 ? Width  : Source.Width,
            Height > 0 ? Height : Source.Height);
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible || Source == null) return;

        var dest = new Rect(0, 0, Bounds.Width, Bounds.Height);

        if (KeepAspect && Source.Width > 0 && Source.Height > 0)
        {
            float scaleX = Bounds.Width  / Source.Width;
            float scaleY = Bounds.Height / Source.Height;
            float scale  = System.MathF.Min(scaleX, scaleY);
            float dw = Source.Width  * scale;
            float dh = Source.Height * scale;
            dest = new Rect((Bounds.Width - dw) * 0.5f, (Bounds.Height - dh) * 0.5f, dw, dh);
        }

        renderer.DrawImage(Source, dest, Alpha);
    }
}
