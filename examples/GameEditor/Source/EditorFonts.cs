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
    }
}
