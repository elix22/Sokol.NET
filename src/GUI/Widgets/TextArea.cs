using System;

namespace Sokol.GUI;

/// <summary>
/// Read-only multi-line text area.  For editable multi-line, wrap <see cref="TextBox"/> in a <see cref="ScrollView"/>.
/// </summary>
public class TextArea : Widget
{
    public string   Text      { get; set; } = string.Empty;
    public UIColor? ForeColor { get; set; }
    public UIColor? BackColor { get; set; }
    public Font?    Font      { get; set; }
    public float    FontSize  { get; set; } = 0f;

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme = ThemeManager.Current;
        var bg    = BackColor ?? theme.InputBackColor;
        renderer.FillRoundedRect(new Rect(0, 0, Bounds.Width, Bounds.Height),
            theme.InputCornerRadius, bg);

        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
        renderer.SetTextAlign(TextHAlign.Left, TextVAlign.Top);

        var inner = new Rect(0, 0, Bounds.Width, Bounds.Height).Deflate(Padding);
        renderer.DrawTextBox(inner.X, inner.Y, inner.Width, Text, ForeColor ?? theme.TextColor);
    }
}
