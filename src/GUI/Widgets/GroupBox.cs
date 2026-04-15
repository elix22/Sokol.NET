using System;

namespace Sokol.GUI;

/// <summary>
/// A Panel subclass that renders a titled border around its children.
/// The title text is embedded in the top stroke line (with a gap).
/// </summary>
public class GroupBox : Panel
{
    private string _title = string.Empty;

    public string Title
    {
        get => _title;
        set { _title = value ?? string.Empty; InvalidateLayout(); }
    }

    public float  TitlePadding { get; set; } = 6f;
    public float  TitleFontSize { get; set; } = 0f;
    public Font?  TitleFont    { get; set; }
    public UIColor? TitleColor { get; set; }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        float w    = Bounds.Width, h = Bounds.Height;
        float cr   = theme.WindowCornerRadius;
        float lh   = theme.InputHeight * 0.7f;   // title bar height = where border gap sits
        float gap  = 2f;                          // top of border relative to title midpoint

        // ── Background fill ───────────────────────────────────────────────────
        var bg = BackgroundColor ?? theme.SurfaceColor;
        if (bg.A > 0f)
            renderer.FillRoundedRect(new Rect(0, lh * 0.5f, w, h - lh * 0.5f), cr, bg);

        // ── Measure title text ────────────────────────────────────────────────
        ApplyTitleFont(renderer, theme);
        float titleW = string.IsNullOrEmpty(_title) ? 0f : renderer.MeasureText(_title);
        float gapL   = (string.IsNullOrEmpty(_title)) ? 0f : TitlePadding;
        float gapR   = titleW + TitlePadding * 2f;   // total gap width
        float gapX   = 12f;                            // gap start X

        // ── Draw border manually with gap for title ───────────────────────────
        float ty = lh * 0.5f;  // Y where the top border line sits
        var   bc = BorderColor ?? theme.BorderColor;
        float bw = BorderWidth > 0f ? BorderWidth : 1.5f;

        // Left top segment (before title gap)
        renderer.DrawLine(cr, ty, gapX - gapL, ty, bw, bc);
        // Right top segment (after title gap)
        renderer.DrawLine(gapX + gapR, ty, w - cr, ty, bw, bc);
        // Bottom line
        renderer.DrawLine(cr, h, w - cr, h, bw, bc);
        // Left side
        renderer.DrawLine(0, ty + cr, 0, h - cr, bw, bc);
        // Right side
        renderer.DrawLine(w, ty + cr, w, h - cr, bw, bc);
        // Corners (approximated as diagonal lines; fine at small cr values)
        renderer.DrawLine(0,     ty + cr, cr,   ty,     bw, bc);
        renderer.DrawLine(w - cr, ty,     w,    ty + cr, bw, bc);
        renderer.DrawLine(0,     h - cr, cr,    h,      bw, bc);
        renderer.DrawLine(w - cr, h,     w,     h - cr, bw, bc);

        // ── Title text ────────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(_title))
        {
            ApplyTitleFont(renderer, theme);
            renderer.SetTextAlign(TextHAlign.Left);
            renderer.DrawText(gapX, ty, _title, TitleColor ?? theme.TextColor);
        }

        // ── Children (base Widget.Draw with inset bounds) ─────────────────────
        // We apply padding so children start below the title line.
        var savedPadding = Padding;
        if (Padding.Top < lh + 2f)
            Padding = new Thickness(Padding.Left, lh + 4f, Padding.Right, Padding.Bottom);
        base.Draw(renderer);
        Padding = savedPadding;
    }

    private void ApplyTitleFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(TitleFont?.Name ?? theme.DefaultFont);
        float sz = TitleFontSize > 0 ? TitleFontSize : theme.SmallFontSize;
        renderer.SetFontSize(sz);
    }
}
