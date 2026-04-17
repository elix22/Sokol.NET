using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// A <see cref="Label"/> that renders inline markup.
/// Supported tags: [b]bold[/b], [color=#RRGGBB]…[/color], [size=N]…[/size],
/// [link=url]…[/link].  The [i]…[/i] tag is accepted and rendered in the muted
/// text color (no dedicated italic font).
/// </summary>
public class RichLabel : Label
{
    private List<Span>   _spans     = [];
    private int          _hoverSpan = -1;

    // Override Label.Text so any assignment triggers a re-parse of markup.
    public override string Text
    {
        get => base.Text;
        set { base.Text = value ?? string.Empty; _spans = Parse(base.Text); }
    }

    /// <summary>Fires when the user clicks on a [link=url] span.</summary>
    public event Action<string>? LinkClicked;

    // ─── Data model ──────────────────────────────────────────────────────────
    private sealed class Span
    {
        public string   Content  { get; set; } = string.Empty;
        public bool     Bold     { get; set; }
        public bool     Italic   { get; set; }
        public UIColor? Color    { get; set; }
        public float    FontSize { get; set; }  // 0 = use widget default
        public string?  Link     { get; set; }

        // Layout cache (populated during Draw; used for hit-testing)
        public float X, Y, Width;
        public float SpanH;       // this span's own line height
        public float LineH;       // max line height of the line this span belongs to
        public bool  IsLineBreak;   // synthetic span for \n — nothing to draw
    }

    // ─── Parser ──────────────────────────────────────────────────────────────
    private static List<Span> Parse(string text)
    {
        var spans = new List<Span>();
        if (string.IsNullOrEmpty(text)) return spans;

        bool     bold   = false;
        bool     italic = false;
        UIColor? color  = null;
        float    size   = 0f;
        string?  link   = null;

        int pos = 0;
        while (pos < text.Length)
        {
            int tagStart = text.IndexOf('[', pos);
            if (tagStart < 0)
            {
                AppendSpan(spans, text[pos..], bold, italic, color, size, link);
                break;
            }
            if (tagStart > pos)
                AppendSpan(spans, text[pos..tagStart], bold, italic, color, size, link);

            int tagEnd = text.IndexOf(']', tagStart + 1);
            if (tagEnd < 0) { AppendSpan(spans, text[tagStart..], bold, italic, color, size, link); break; }

            string tag = text[(tagStart + 1)..tagEnd].Trim();
            pos = tagEnd + 1;

            if (tag.Equals("b",      StringComparison.OrdinalIgnoreCase)) { bold   = true;  continue; }
            if (tag.Equals("/b",     StringComparison.OrdinalIgnoreCase)) { bold   = false; continue; }
            if (tag.Equals("i",      StringComparison.OrdinalIgnoreCase)) { italic = true;  continue; }
            if (tag.Equals("/i",     StringComparison.OrdinalIgnoreCase)) { italic = false; continue; }
            if (tag.Equals("/color", StringComparison.OrdinalIgnoreCase)) { color  = null;  continue; }
            if (tag.Equals("/size",  StringComparison.OrdinalIgnoreCase)) { size   = 0f;    continue; }
            if (tag.Equals("/link",  StringComparison.OrdinalIgnoreCase)) { link   = null;  continue; }

            if (tag.StartsWith("color=", StringComparison.OrdinalIgnoreCase))
                { color = TryParseColor(tag[6..].Trim()); continue; }
            if (tag.StartsWith("size=", StringComparison.OrdinalIgnoreCase))
                { if (float.TryParse(tag[5..], out float fs)) size = fs; continue; }
            if (tag.StartsWith("link=", StringComparison.OrdinalIgnoreCase))
                { link = tag[5..].Trim(); continue; }

            // Unknown tag — emit as literal text
            AppendSpan(spans, "[" + tag + "]", bold, italic, color, size, link);
        }
        return spans;
    }

    private static void AppendSpan(List<Span> spans, string content,
        bool bold, bool italic, UIColor? color, float size, string? link)
    {
        if (string.IsNullOrEmpty(content)) return;
        var parts = content.Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
                spans.Add(new Span { Content = parts[i], Bold = bold, Italic = italic,
                                     Color = color, FontSize = size, Link = link });
            if (i < parts.Length - 1)
                spans.Add(new Span { IsLineBreak = true });
        }
    }

    private static UIColor? TryParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint rgb))
            return new UIColor(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >>  8) & 0xFF) / 255f,
                ( rgb        & 0xFF) / 255f, 1f);
        return null;
    }

    // ─── Layout ──────────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        float maxW = Bounds.Width > 0 ? Bounds.Width - Padding.Horizontal : 400f;
        var (_, h) = MeasureSpans(renderer, maxW);
        float lineH = GetDefaultLineHeight(renderer);
        return new Vector2(maxW + Padding.Horizontal, MathF.Max(h, lineH) + Padding.Vertical);
    }

    private float GetDefaultLineHeight(Renderer renderer)
    {
        var theme = ThemeManager.Current;
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0f ? FontSize : theme.FontSize);
        renderer.MeasureTextMetrics(out _, out _, out float lh);
        return lh;
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var   theme        = ThemeManager.Current;
        float w            = Bounds.Width, h = Bounds.Height;
        float cr           = theme.InputCornerRadius;

        // NanoGUI-style sunken container background
        renderer.FillRoundedRect(new Rect(0, 0, w, h), cr, theme.InputBackColor);
        var insetPaint = renderer.BoxGradient(
            new Rect(1, 2, w - 2, h - 2), cr, 4f,
            new UIColor(1f, 1f, 1f, 0.06f),
            new UIColor(0f, 0f, 0f, 0.15f));
        renderer.FillRoundedRectWithPaint(new Rect(0, 0, w, h), cr, insetPaint);
        renderer.StrokeRoundedRect(
            new Rect(0.5f, 0.5f, w - 1f, h - 1f),
            MathF.Max(cr - 0.5f, 0f), 1f,
            UIColor.Black.WithAlpha(0.188f));

        float maxW         = Bounds.Width - Padding.Horizontal;
        float startX       = Padding.Left;
        float startY       = Padding.Top;
        float defaultLineH = GetDefaultLineHeight(renderer);

        // ── Pass 1: layout — compute span positions and per-line max height ──
        float x     = startX;
        float y     = startY;
        float lineH = defaultLineH;
        int   lineStart = 0;   // first span index on the current line

        for (int i = 0; i < _spans.Count; i++)
        {
            var span = _spans[i];

            if (span.IsLineBreak)
            {
                // Finalise current line: propagate lineH to all spans on this line
                for (int j = lineStart; j < i; j++)
                    _spans[j].LineH = lineH;
                x     = startX;
                y    += lineH;
                lineH = defaultLineH;
                lineStart = i + 1;
                continue;
            }

            ApplySpanFont(renderer, theme, span);
            renderer.MeasureTextMetrics(out _, out _, out float lh);
            float sw = renderer.MeasureText(span.Content);

            if (maxW > 0f && x + sw > startX + maxW && x > startX)
            {
                for (int j = lineStart; j < i; j++)
                    _spans[j].LineH = lineH;
                x  = startX;
                y += lineH;
                lineH = defaultLineH;
                lineStart = i;
            }

            span.X     = x;
            span.Y     = y;
            span.Width = sw;
            span.SpanH = lh;

            x    += sw;
            lineH = MathF.Max(lineH, lh);
        }
        // Finalise the last line
        for (int j = lineStart; j < _spans.Count; j++)
            _spans[j].LineH = lineH;

        // ── Pass 2: draw — centre every span vertically within its line ──
        bool rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
        renderer.Save();
        renderer.IntersectClip(new Rect(0, 0, Bounds.Width, Bounds.Height));

        for (int i = 0; i < _spans.Count; i++)
        {
            var span = _spans[i];
            if (span.IsLineBreak) continue;

            ApplySpanFont(renderer, theme, span);

            UIColor fg;
            if (span.Link != null)
            {
                bool hovered = (i == _hoverSpan);
                fg = hovered ? theme.AccentColor : theme.Primary;
                if (hovered)
                    renderer.FillRect(new Rect(span.X, span.Y + span.LineH * 0.85f, span.Width, 1f), fg);
            }
            else if (span.Color.HasValue) fg = span.Color.Value;
            else if (span.Italic)          fg = theme.TextMutedColor;
            else                           fg = ForeColor ?? theme.TextColor;

            // RTL: mirror X within the line so spans appear right-to-left
            float drawX = rtl
                ? startX + maxW - (span.X - startX) - span.Width
                : span.X;
            renderer.SetTextAlign(TextHAlign.Left);
            // Centre the span within the line's max height so all sizes align.
            renderer.DrawText(drawX, span.Y + span.LineH * 0.5f, span.Content, fg);
        }

        renderer.Restore();
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseMove(MouseEvent e)  { _hoverSpan = FindSpan(e.LocalPosition); return false; }
    public override bool OnMouseLeave(MouseEvent e) { _hoverSpan = -1; return false; }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        int idx = FindSpan(e.LocalPosition);
        if (idx >= 0 && _spans[idx].Link != null)
        {
            LinkClicked?.Invoke(_spans[idx].Link!);
            return true;
        }
        return false;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private void ApplySpanFont(Renderer renderer, Theme theme, Span span)
    {
        renderer.SetFont(span.Bold
            ? theme.BoldFont
            : Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(span.FontSize > 0f ? span.FontSize
                           : FontSize     > 0f ? FontSize
                           : theme.FontSize);
    }

    private (float w, float h) MeasureSpans(Renderer renderer, float maxW)
    {
        var   theme        = ThemeManager.Current;
        float defaultLineH = GetDefaultLineHeight(renderer);
        float x = 0f, y = 0f, lineH = defaultLineH;
        foreach (var span in _spans)
        {
            if (span.IsLineBreak) { x = 0f; y += lineH; lineH = defaultLineH; continue; }
            ApplySpanFont(renderer, theme, span);
            renderer.MeasureTextMetrics(out _, out _, out float lh);
            float sw = renderer.MeasureText(span.Content);
            if (x > 0f && x + sw > maxW) { x = 0f; y += lh; lineH = lh; }
            x    += sw;
            lineH = MathF.Max(lineH, lh);
        }
        return (maxW, y + lineH);
    }

    private int FindSpan(Vector2 pos)
    {
        float lhDef = ThemeManager.Current.FontSize;
        for (int i = 0; i < _spans.Count; i++)
        {
            var s = _spans[i];
            if (s.IsLineBreak || s.Width <= 0f) continue;
            // span.X/Y already include padding (set during Draw at startX = Padding.Left)
            var r = new Rect(s.X, s.Y, s.Width, lhDef);
            if (r.Contains(pos)) return i;
        }
        return -1;
    }
}
