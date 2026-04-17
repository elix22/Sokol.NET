using BidiSharp;

namespace Sokol.GUI;

/// <summary>
/// Thin wrapper around BidiSharp for bidirectional text processing.
/// All GUI text rendering flows through Renderer, which calls these helpers
/// so widgets get RTL support automatically.
/// </summary>
public static class BidiHelper
{
    /// <summary>
    /// Reorder logical-order text to visual-order for rendering.
    /// Pure-LTR text passes through unchanged for performance.
    /// </summary>
    public static string ToVisual(string logicalText)
    {
        if (string.IsNullOrEmpty(logicalText)) return logicalText;
        if (!ContainsRTL(logicalText)) return logicalText;

        // Process each paragraph (line) independently so each gets its own
        // paragraph embedding level.  Without this, a single RTL line makes
        // ALL subsequent neutral-only lines (e.g. "?") inherit RTL order.
        var lines = logicalText.Split('\n');
        if (lines.Length <= 1)
            return Bidi.LogicalToVisual(logicalText);

        var sb = new System.Text.StringBuilder(logicalText.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            var line = lines[i];
            sb.Append(ContainsRTL(line) ? Bidi.LogicalToVisual(line) : line);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Quick scan for any Unicode RTL codepoints to skip BiDi processing on pure-LTR text.
    /// Checks Arabic (U+0600–U+06FF), Hebrew (U+0590–U+05FF), Arabic Supplement (U+0750–U+077F),
    /// Arabic Extended (U+08A0–U+08FF), and RTL embedding/override marks.
    /// </summary>
    public static bool ContainsRTL(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c >= '\u0590' && c <= '\u05FF') return true; // Hebrew
            if (c >= '\u0600' && c <= '\u06FF') return true; // Arabic
            if (c >= '\u0750' && c <= '\u077F') return true; // Arabic Supplement
            if (c >= '\u08A0' && c <= '\u08FF') return true; // Arabic Extended-A
            if (c >= '\uFB50' && c <= '\uFDFF') return true; // Arabic Presentation Forms-A
            if (c >= '\uFE70' && c <= '\uFEFF') return true; // Arabic Presentation Forms-B
            if (c >= '\uFB1D' && c <= '\uFB4F') return true; // Hebrew Presentation Forms
            if (c == '\u200F' || c == '\u202B' || c == '\u202E' || c == '\u2067') return true; // RTL marks
        }
        return false;
    }

    /// <summary>
    /// Returns true if the first strong directional character is RTL.
    /// Used for automatic alignment decisions (e.g., Label default alignment).
    /// </summary>
    public static bool IsRTLParagraph(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            // RTL strong characters
            if (c >= '\u0590' && c <= '\u05FF') return true;
            if (c >= '\u0600' && c <= '\u06FF') return true;
            if (c >= '\u0750' && c <= '\u077F') return true;
            if (c >= '\u08A0' && c <= '\u08FF') return true;
            if (c >= '\uFB50' && c <= '\uFDFF') return true;
            if (c >= '\uFE70' && c <= '\uFEFF') return true;
            if (c >= '\uFB1D' && c <= '\uFB4F') return true;
            // LTR strong characters (Latin, Greek, Cyrillic, CJK, etc.)
            if (c >= 'A' && c <= 'Z') return false;
            if (c >= 'a' && c <= 'z') return false;
            if (c >= '\u0400' && c <= '\u04FF') return false; // Cyrillic
            if (c >= '\u0370' && c <= '\u03FF') return false; // Greek
            // Skip weak/neutral characters and continue scanning
        }
        return false;
    }

    /// <summary>
    /// Returns true if a single character belongs to an RTL script (Hebrew, Arabic).
    /// Used for caret positioning in BiDi-aware text editing.
    /// </summary>
    public static bool IsRtlChar(char c)
    {
        if (c >= '\u0590' && c <= '\u05FF') return true;  // Hebrew
        if (c >= '\u0600' && c <= '\u06FF') return true;  // Arabic
        if (c >= '\u0750' && c <= '\u077F') return true;  // Arabic Supplement
        if (c >= '\u08A0' && c <= '\u08FF') return true;  // Arabic Extended-A
        if (c >= '\uFB50' && c <= '\uFDFF') return true;  // Arabic Presentation Forms-A
        if (c >= '\uFE70' && c <= '\uFEFF') return true;  // Arabic Presentation Forms-B
        if (c >= '\uFB1D' && c <= '\uFB4F') return true;  // Hebrew Presentation Forms
        return false;
    }

    /// <summary>
    /// Reorder a single line of text to visual order and return both the visual string
    /// and the visual-to-logical index mapping (map[visualPos] = logicalPos).
    /// For pure-LTR text, returns identity mapping for performance.
    /// </summary>
    public static (string visual, int[] visualToLogical) ToVisualWithMap(string lineText)
    {
        if (string.IsNullOrEmpty(lineText))
        {
            return (lineText ?? "", Array.Empty<int>());
        }
        if (!ContainsRTL(lineText))
        {
            var identity = new int[lineText.Length];
            for (int i = 0; i < identity.Length; i++) identity[i] = i;
            return (lineText, identity);
        }
        return Bidi.LogicalToVisualWithMap(lineText);
    }
}
