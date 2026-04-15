using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sokol.GUI;

/// <summary>
/// A label that renders inline markup.
/// Supported tags: [b]bold[/b], [color=#RRGGBB]...[/color], [size=N]...[/size],
/// [link=url]...[/link].
/// The [i]...[/i] tag is accepted but rendered in italic color (no dedicated italic font).
/// </summary>
public class RichLabel : Widget
{
    private string       _text   = string.Empty;
    private List<Span>   _spans  = [];
    private int          _hoverSpan = -1;

    public string Text
    {
        get => _text;
        set { _text = value ?? string.Empty; _spans = Parse(_text); }
    }

    /// <summary>Fires when the user clicks on a [link=url] span.</summary>
    public event Action<string>? LinkClicked;

    // ─── Data model ──────────────────────────────────────────────────────────

    private sealed class Span
    {
        public string   Content    { get; set; } = string.Empty;
        public bool     Bold       { get; set; }
        public bool     Italic     { get; set; }
        public UIColor? Color      { get; set; }
        public float    FontSize   { get; set; }  // 0 = default
        public string?  Link       { get; set; }

        // Run-time layout cache (filled during Draw)
        public float X, Y, Width;
    }

    // ─── Parser ──────────────────────────────────────────────────────────────

    private static List<Span> Parse(string text)
    {
        var spans = new List<Span>();
        if (string.IsNullOrEmpty(text)) return spans;

        // State stack
        bool   bold     = false;
        bool   italic   = false;
        UIColor? color  = null;
        float  size     = 0f;
        string? link    = null;

        // Tokenise: split on tags
        // A tag looks like [tag] or [/tag] or [tag=value]
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

            if (tag.Equals("b",  StringComparison.OrdinalIgnoreCase))  { bold   = true;  continue; }
            if (tag.Equals("/b", StringComparison.OrdinalIgnoreCase))  { bold   = false; continue; }
            if (tag.Equals("i",  StringComparison.OrdinalIgnoreCase))  { italic = true;  continue; }
            if (tag.Equals("/i", StringComparison.OrdinalIgnoreCase))  { italic = false; continue; }
            if (tag.Equals("/color", StringComparison.OrdinalIgnoreCase)) { color = null;  continue; }
            if (tag.Equals("/size",  StringComparison.OrdinalIgnoreCase)) { size  = 0f;    continue; }
            if (tag.Equals("/link",  StringComparison.OrdinalIgnoreCase)) { link  = null;  continue; }

            if (tag.StartsWith("color=", StringComparison.OrdinalIgnoreCase))
            {
                string hex = tag[6..].Trim();
                color = TryParseColor(hex);
                continue;
            }
            if (tag.StartsWith("size=", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(tag[5..], out float fs)) size = fs;
                continue;
            }
            if (tag.StartsWith("link=", StringComparison.OrdinalIgnoreCase))
            {
                link = tag[5..].Trim();
                continue;
            }

            // Unknown tag — emit as literal text
            AppendSpan(spans, "[" + tag + "]", bold, italic, color, size, link);
        }

        return spans;
    }

    private static void AppendSpan(List<Span> spans, string content,
        bool bold, bool italic, UIColor? color, float size, string? link)
    {
        if (string.IsNullOrEmpty(content)) return;
        spans.Add(new Span
        {
            Content  = content,
            Bold     = bold,
            Italic   = italic,
            Color    = color,
            FontSize = size,
            Link     = link,
        });
    }

    private static UIColor? TryParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint rgb))
            return new UIColor(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >>  8) & 0xFF) / 255f,
                ( rgb        & 0xFF) / 255f,
                1f);
        return null;
    }

    // ─── Layout ──────────────────────────────────────────────────────────────

    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        float maxW = Bounds.Width > 0 ? Bounds.Width : 400f;
        var (_, h) = MeasureSpans(renderer, maxW);
        float lineH = GetDefaultLineHeight(renderer);
        return new Vector2(maxW, MathF.Max(h, lineH) + Padding.Vertical);
    }

    private float GetDefaultLineHeight(Renderer renderer)
    {
        var theme = ThemeManager.Current;
        renderer.SetFont(theme.DefaultFont);
        renderer.SetFontSize(theme.FontSize);
        renderer.MeasureTextMetrics(out _, out _, out float lh);
        return lh;
    }

    // ─── Draw ────────────────────────────────────────────────────────────────

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var   theme  = ThemeManager.Current;
        float maxW   = Bounds.Width - Padding.Horizontal;
        float startX = Padding.Left;
        float startY = Padding.Top;

        float x      = startX;
        float y      = startY;
        float lineH  = GetDefaultLineHeight(renderer);

        for (int i = 0; i < _spans.Count; i++)
        {
            var span = _spans[i];
            ApplySpanFont(renderer, theme, span);

            renderer.MeasureTextMetrics(out _, out _, out float lh);
            float sw = renderer.MeasureText(span.Content);

            // Line wrap
            if (x > startX && x + sw > startX + maxW)
            {
                x  = startX;
                y += lh;
            }

            // Cache position for hit-testing
            span.X     = x;
            span.Y     = y;
            span.Width = sw;

            // Choose color
            UIColor fg;
            if (span.Link != null)
            {
                bool hovered = i == _hoverSpan;
                fg = hovered ? theme.AccentColor : theme.Primary;
                if (hovered)
                {
                    // Underline simulation: draw a thin rect below text
                    renderer.FillRect(new Rect(x, y + lh * 0.85f, sw, 1f), fg);
                }
            }
            else if (span.Color.HasValue)
                fg = span.Color.Value;
            else if (span.Italic)
                fg = theme.TextMutedColor;
            else
                fg = theme.TextColor;

            renderer.SetTextAlign(TextHAlign.Left);
            renderer.DrawText(x, y + lh * 0.7f, span.Content, fg);

            x += sw;
            lineH = MathF.Max(lineH, lh);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────

    public override bool OnMouseMove(MouseEvent e)
    {
        _hoverSpan = FindSpan(e.Position);
        return false;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        int idx = FindSpan(e.Position);
        if (idx >= 0 && _spans[idx].Link != null)
        {
            LinkClicked?.Invoke(_spans[idx].Link!);
            return true;
        }
        return false;
    }

    public override bool OnMouseLeave(MouseEvent e) { _hoverSpan = -1; return false; }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void ApplySpanFont(Renderer renderer, Theme theme, Span span)
    {
        if (span.Bold)
            renderer.SetFont(theme.BoldFont);
        else
            renderer.SetFont(theme.DefaultFont);

        renderer.SetFontSize(span.FontSize > 0 ? span.FontSize : theme.FontSize);
    }

    private (float w, float h) MeasureSpans(Renderer renderer, float maxW)
    {
        var   theme  = ThemeManager.Current;
        float x      = 0, y = 0, lineH = 0;
        foreach (var span in _spans)
        {
            ApplySpanFont(renderer, theme, span);
            renderer.MeasureTextMetrics(out _, out _, out float lh);
            float sw = renderer.MeasureText(span.Content);
            if (x > 0 && x + sw > maxW) { x = 0; y += lh; }
            x    += sw;
            lineH = MathF.Max(lineH, lh);
        }
        return (maxW, y + lineH);
    }

    private int FindSpan(Vector2 pos)
    {
        var theme = ThemeManager.Current;
        for (int i = 0; i < _spans.Count; i++)
        {
            var s = _spans[i];
            if (s.Width <= 0) continue;
            float lh = theme.FontSize;
            var r = new Rect(s.X + Padding.Left, s.Y + Padding.Top, s.Width, lh);
            if (r.Contains(pos)) return i;
        }
        return -1;
    }
}
