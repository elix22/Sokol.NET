using System;

namespace Sokol.GUI;

public enum ProgressBarOrientation { Horizontal, Vertical }

/// <summary>
/// Read-only progress bar [0..1].
/// </summary>
public class ProgressBar : Widget
{
    private float _value;

    public float Value
    {
        get => _value;
        set => _value = MathF.Max(0f, MathF.Min(1f, value));
    }

    public ProgressBarOrientation Orientation { get; set; } = ProgressBarOrientation.Horizontal;
    public bool ShowLabel { get; set; } = false;
    public Font? Font     { get; set; }
    public float FontSize { get; set; } = 0f;

    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        var theme = ThemeManager.Current;
        return Orientation == ProgressBarOrientation.Horizontal
            ? new Vector2(200, theme.ProgressBarThickness)
            : new Vector2(theme.ProgressBarThickness, 200);
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme = ThemeManager.Current;
        var bg    = new Rect(0, 0, Bounds.Width, Bounds.Height);
        float cr  = theme.ProgressBarCornerRadius;

        // Track: NanoGUI-style BoxGradient (sunken groove)
        var trackInset = renderer.BoxGradient(
            new Rect(bg.X + 1, bg.Y + 1, bg.Width - 2, bg.Height - 2),
            cr, 3f,
            UIColor.Black.WithAlpha(0.32f),
            UIColor.Black.WithAlpha(0.05f));
        renderer.FillRoundedRectWithPaint(bg, cr, trackInset);
        renderer.StrokeRoundedRect(bg, cr, 1f, UIColor.Black.WithAlpha(0.25f));

        if (_value > 0f)
        {
            bool rtl = Orientation == ProgressBarOrientation.Horizontal
                       && ResolvedFlowDirection == FlowDirection.RightToLeft;
            Rect fill;
            if (Orientation == ProgressBarOrientation.Horizontal)
            {
                float fillW = bg.Width * _value;
                float fillX = rtl ? (bg.Right - fillW) : bg.X;
                fill = new Rect(fillX, bg.Y, fillW, bg.Height);
            }
            else
                fill = new Rect(bg.X, bg.Bottom - bg.Height * _value, bg.Width, bg.Height * _value);

            // Fill: accent gradient (lighter top → darker bottom = raised bar)
            var fillGrad = renderer.LinearGradient(
                new Vector2(fill.X, fill.Y), new Vector2(fill.X, fill.Bottom),
                theme.AccentColor.Lighten(0.18f), theme.AccentColor.Darken(0.12f));
            renderer.FillRoundedRectWithPaint(fill, cr, fillGrad);
        }

        if (ShowLabel)
        {
            renderer.SetFont(Font?.Name ?? theme.DefaultFont);
            renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize * 0.85f);
            renderer.SetTextAlign(TextHAlign.Center);
            renderer.DrawText(bg.X + bg.Width * 0.5f, bg.Y + bg.Height * 0.5f,
                $"{_value * 100:F0}%", theme.TextColor);
        }
    }
}
