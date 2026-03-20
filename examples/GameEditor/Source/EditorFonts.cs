// EditorFonts.cs — Stores ImFont pointers loaded at startup.
// CodeFont is set by Init() and used by TextEditorWidget to push/pop the
// monospace font while rendering code.

using Imgui;

namespace GameEditor
{
    public static unsafe class EditorFonts
    {
        /// <summary>JetBrains Mono — code editor font. Null until Init() runs.</summary>
        public static ImFont* CodeFont { get; internal set; }

        /// <summary>Current UI font size in pixels. Default 14. Change + set RebuildRequested to apply.</summary>
        public static float UiFontSize   { get; set; } = 14f;
        /// <summary>Current code editor font size in pixels. Default 14.</summary>
        public static float CodeFontSize { get; set; } = 14f;
        /// <summary>Set to true to trigger atlas clear + reload before the next frame.</summary>
        public static bool  RebuildRequested { get; set; }
    }
}
