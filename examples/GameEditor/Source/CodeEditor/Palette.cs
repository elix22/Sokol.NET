// Palette.cs — Color themes for the C# text editor widget.
// Colors are stored as ABGR uint (ImGui's native format).
using System;

namespace GameEditor.CodeEditor
{
    /// <summary>
    /// Indices into the palette color array.  Keep in sync with Palette.Count.
    /// </summary>
    public enum PaletteIndex : byte
    {
        Default       = 0,
        Keyword       = 1,
        Number        = 2,
        String        = 3,
        CharLiteral   = 4,
        Punctuation   = 5,
        Preprocessor  = 6,
        Identifier    = 7,
        KnownType     = 8,   // recognised built-in type names (int, float, bool, …)
        Comment       = 9,
        MultiLineComment = 10,
        Background    = 11,
        Cursor        = 12,
        Selection     = 13,
        ErrorMarker   = 14,
        WarningMarker = 15,
        LineNumber    = 16,
        CurrentLine   = 17,
        Count         = 18
    }

    /// <summary>
    /// A set of ABGR colors — one per <see cref="PaletteIndex"/>.
    /// </summary>
    public readonly struct Palette
    {
        private readonly uint[] _colors;

        private Palette(uint[] colors)
        {
            if (colors.Length != (int)PaletteIndex.Count)
                throw new ArgumentException("Palette must have exactly PaletteIndex.Count entries.");
            _colors = colors;
        }

        public uint this[PaletteIndex idx] => _colors[(int)idx];
        public uint this[int idx]          => _colors[idx];

        // ── ABGR helpers ──────────────────────────────────────────────────────
        private static uint ABGR(byte a, byte b, byte g, byte r)
            => (uint)a << 24 | (uint)b << 16 | (uint)g << 8 | r;

        private static uint RGB(byte r, byte g, byte b) => ABGR(0xFF, b, g, r);

        // ── Built-in themes ──────────────────────────────────────────────────
        public static readonly Palette Dark = new Palette(new uint[]
        {
            /* Default       */ RGB(0xD4, 0xD4, 0xD4),
            /* Keyword       */ RGB(0x56, 0x9C, 0xD6),
            /* Number        */ RGB(0xB5, 0xCE, 0xA8),
            /* String        */ RGB(0xCE, 0x91, 0x78),
            /* CharLiteral   */ RGB(0xCE, 0x91, 0x78),
            /* Punctuation   */ RGB(0xD4, 0xD4, 0xD4),
            /* Preprocessor  */ RGB(0xBD, 0x63, 0xC5),
            /* Identifier    */ RGB(0x9C, 0xDC, 0xFE),
            /* KnownType     */ RGB(0x4E, 0xC9, 0xB0),
            /* Comment       */ RGB(0x6A, 0x99, 0x55),
            /* MultiLineComment */ RGB(0x6A, 0x99, 0x55),
            /* Background    */ RGB(0x1E, 0x1E, 0x1E),
            /* Cursor        */ RGB(0xFF, 0xFF, 0xFF),
            /* Selection     */ ABGR(0x60, 0x66, 0x44, 0x26),
            /* ErrorMarker   */ ABGR(0x80, 0x00, 0x10, 0xFF),
            /* WarningMarker */ ABGR(0x60, 0x00, 0xAA, 0xFF),
            /* LineNumber    */ RGB(0x85, 0x85, 0x85),
            /* CurrentLine   */ ABGR(0x18, 0x2F, 0x2F, 0x2F),
        });

        public static readonly Palette Light = new Palette(new uint[]
        {
            /* Default       */ RGB(0x20, 0x20, 0x20),
            /* Keyword       */ RGB(0x00, 0x00, 0xFF),
            /* Number        */ RGB(0x00, 0x80, 0x00),
            /* String        */ RGB(0xA3, 0x15, 0x15),
            /* CharLiteral   */ RGB(0xA3, 0x15, 0x15),
            /* Punctuation   */ RGB(0x20, 0x20, 0x20),
            /* Preprocessor  */ RGB(0x80, 0x00, 0x80),
            /* Identifier    */ RGB(0x00, 0x10, 0x80),
            /* KnownType     */ RGB(0x00, 0x80, 0x80),
            /* Comment       */ RGB(0x00, 0x80, 0x00),
            /* MultiLineComment */ RGB(0x00, 0x80, 0x00),
            /* Background    */ RGB(0xFF, 0xFF, 0xFF),
            /* Cursor        */ RGB(0x00, 0x00, 0x00),
            /* Selection     */ ABGR(0x60, 0xA0, 0xA0, 0xA0),
            /* ErrorMarker   */ ABGR(0x60, 0x20, 0x00, 0xFF),
            /* WarningMarker */ ABGR(0x40, 0x00, 0xA0, 0xFF),
            /* LineNumber    */ RGB(0x70, 0x70, 0x70),
            /* CurrentLine   */ ABGR(0x20, 0xF0, 0xF0, 0xF0),
        });
    }
}
