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

    /// <summary>Show the tooltip near <paramref name="screenPos"/>.</summary>
    public void Show(string text, Vector2 screenPos)
    {
        Text    = text;
        Visible = true;
        PositionNear(screenPos);
    }

    public void Hide() => Visible = false;

    private void PositionNear(Vector2 pos)
    {
        float offsetX = 14f, offsetY = 14f;
        float x = pos.X + offsetX;
        float y = pos.Y + offsetY;

        // keep on screen
        if (Screen.Instance != null)
        {
            if (x + Bounds.Width  > Screen.Instance.LogicalWidth)  x = pos.X - Bounds.Width  - offsetX;
            if (y + Bounds.Height > Screen.Instance.LogicalHeight)  y = pos.Y - Bounds.Height - offsetY;
        }

        Bounds = new Rect(x, y, Bounds.Width, Bounds.Height);
    }

    public override Vector2 PreferredSize(Renderer renderer)
    {
        var theme = ThemeManager.Current;
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.TooltipFontSize);
        float w = renderer.MeasureText(Text) + theme.TooltipPaddingH * 2;
        var m  = renderer.MeasureTextMetrics();
        return new Vector2(w, m.lineHeight + theme.TooltipPaddingV * 2);
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible || string.IsNullOrEmpty(Text)) return;

        var theme = ThemeManager.Current;
        var sz = PreferredSize(renderer);
        Bounds = new Rect(Bounds.X, Bounds.Y, sz.X, sz.Y);

        var bg = new Rect(0, 0, sz.X, sz.Y);
        renderer.FillRoundedRect(bg, theme.TooltipCornerRadius, theme.TooltipBackColor);
        renderer.StrokeRoundedRect(bg, theme.TooltipCornerRadius, 1f, theme.BorderColor);

        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.TooltipFontSize);
        bool rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
        if (rtl)
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
