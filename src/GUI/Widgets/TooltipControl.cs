namespace Sokol.GUI;

/// <summary>
/// Floating tooltip shown on hover.  Managed by the Screen's tooltip pass.
/// Attach to any widget via <see cref="Widget.Tooltip"/>.
/// </summary>
public class TooltipControl : Widget
{
    private static readonly TooltipControl _shared = new();
    public static TooltipControl Shared => _shared;

    public string Text   { get; set; } = string.Empty;
    public Font?  Font   { get; set; }
    public float  FontSize { get; set; } = 0f;

    private TooltipControl() { Visible = false; }

    /// <summary>The widest a tooltip's text gets (logical px): a longer text wraps onto more lines.</summary>
    public const float MaxTextWidth = 360f;

    private Vector2 _anchor;

    /// <summary>Show the tooltip near <paramref name="screenPos"/> (placed by its size when drawn: <see cref="Place"/>).</summary>
    public void Show(string text, Vector2 screenPos)
    {
        Text    = text;
        Visible = true;
        _anchor = screenPos;
    }

    public void Hide() => Visible = false;

    /// <summary>Its bounds for this frame: below right of the pointer, or on the pointer's other side when it would leave
    /// the window, and never outside it.</summary>
    internal void Place(Renderer renderer)
    {
        var sz = PreferredSize(renderer);
        const float offset = 14f;
        float x = _anchor.X + offset, y = _anchor.Y + offset;
        if (Screen.Instance != null)
        {
            float w = Screen.Instance.LogicalWidth, h = Screen.Instance.LogicalHeight;
            if (x + sz.X > w) x = _anchor.X - sz.X - offset;
            if (y + sz.Y > h) y = _anchor.Y - sz.Y - offset;
            x = Math.Clamp(x, 0f, Math.Max(0f, w - sz.X));
            y = Math.Clamp(y, 0f, Math.Max(0f, h - sz.Y));
        }
        Bounds = new Rect(x, y, sz.X, sz.Y);
    }

    // The text's size: one line, or wrapped at MaxTextWidth
    private (float W, float H, bool Wrapped) TextSize(Renderer renderer)
    {
        var theme = ThemeManager.Current;
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.TooltipFontSize);
        float w = renderer.MeasureText(Text);
        if (w <= MaxTextWidth) return (w, renderer.MeasureTextMetrics().lineHeight, false);
        renderer.SetTextAlign(TextHAlign.Left, TextVAlign.Top);
        var (bw, bh) = renderer.MeasureTextBounds(0f, 0f, MaxTextWidth, Text);
        return (bw, bh, true);
    }

    public override Vector2 PreferredSize(Renderer renderer)
    {
        var theme = ThemeManager.Current;
        var (w, h, _) = TextSize(renderer);
        return new Vector2(w + theme.TooltipPaddingH * 2, h + theme.TooltipPaddingV * 2);
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible || string.IsNullOrEmpty(Text)) return;

        var theme = ThemeManager.Current;
        var (tw, th, wrapped) = TextSize(renderer);
        var sz = new Vector2(tw + theme.TooltipPaddingH * 2, th + theme.TooltipPaddingV * 2);
        Bounds = new Rect(Bounds.X, Bounds.Y, sz.X, sz.Y);

        var bg = new Rect(0, 0, sz.X, sz.Y);
        renderer.FillRoundedRect(bg, theme.TooltipCornerRadius, theme.TooltipBackColor);
        renderer.StrokeRoundedRect(bg, theme.TooltipCornerRadius, 1f, theme.BorderColor);

        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.TooltipFontSize);
        bool rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
        if (wrapped)
        {
            // the lines broken at MaxTextWidth as measured; right-aligned ones end at the text's right edge
            renderer.SetTextAlign(rtl ? TextHAlign.Right : TextHAlign.Left, TextVAlign.Top);
            float x = rtl ? theme.TooltipPaddingH + tw - MaxTextWidth : theme.TooltipPaddingH;
            renderer.SetFillColor(theme.TooltipTextColor);
            renderer.DrawTextBox(x, theme.TooltipPaddingV, MaxTextWidth, Text);
        }
        else if (rtl)
        {
            renderer.SetTextAlign(TextHAlign.Right);
            renderer.DrawText(sz.X - theme.TooltipPaddingH, sz.Y * 0.5f, Text, theme.TooltipTextColor);
        }
        else
        {
            renderer.SetTextAlign(TextHAlign.Left);
            renderer.DrawText(theme.TooltipPaddingH, sz.Y * 0.5f, Text, theme.TooltipTextColor);
        }
    }
}
