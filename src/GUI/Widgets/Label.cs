namespace Sokol.GUI;

public enum TextAlign { Left, Center, Right }
public enum TextWrap  { None, Wrap }

/// <summary>
/// Single- or multi-line read-only text widget.
/// </
public class Label : Widget
{
    public string  Text      { get; set; } = string.Empty;
    public UIColor? ForeColor { get; set; }
    public Font?   Font      { get; set; }
    public float   FontSize  { get; set; } = 0f;   // 0 = theme default
    public TextAlign Align   { get; set; } = TextAlign.Left;
    public TextWrap  Wrap    { get; set; } = TextWrap.None;

    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        ApplyFont(renderer);
        if (Wrap == TextWrap.Wrap && Bounds.Width > 0)
        {
            var (w, h) = renderer.MeasureTextBounds(0, 0, Bounds.Width, Text);
            return new Vector2(w + Padding.Horizontal, h + Padding.Vertical);
        }
        float tw = renderer.MeasureText(Text);
        var   m  = renderer.MeasureTextMetrics();
        return new Vector2(tw + Padding.Horizontal, m.lineHeight + Padding.Vertical);
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible || string.IsNullOrEmpty(Text)) return;

        var theme    = ThemeManager.Current;
        var fg       = ForeColor ?? theme.TextColor;
        var fsize    = FontSize > 0 ? FontSize : theme.FontSize;
        var bounds   = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var inner    = bounds.Deflate(Padding);

        ApplyFont(renderer);
        renderer.SetTextAlign(Align switch
        {
            TextAlign.Center => TextHAlign.Center,
            TextAlign.Right  => TextHAlign.Right,
            _                => TextHAlign.Left,
        });

        float x = Align switch
        {
            TextAlign.Center => inner.X + inner.Width * 0.5f,
            TextAlign.Right  => inner.Right,
            _                => inner.X,
        };
        float y = inner.Y + inner.Height * 0.5f;

        if (Wrap == TextWrap.Wrap)
            renderer.DrawTextBox(x, inner.Y, inner.Width, Text, fg);
        else
            renderer.DrawText(x, y, Text, fg);

        base.Draw(renderer);
    }

    private void ApplyFont(Renderer renderer)
    {
        var theme = ThemeManager.Current;
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
