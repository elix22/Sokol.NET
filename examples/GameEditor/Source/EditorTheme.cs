// EditorTheme.cs — Manages the ImGui colour theme for the GameEditor.
// Three built-in themes: Dark, Light, Classic.

using static Imgui.ImguiNative;

namespace GameEditor
{
    public static unsafe class EditorTheme
    {
        public static readonly string[] Names = { "Dark", "Light", "Classic" };

        /// <summary>Currently active theme name.</summary>
        public static string Current { get; private set; } = "Dark";

        /// <summary>Apply a theme by name (case-insensitive). Falls back to Dark.</summary>
        public static void Apply(string name)
        {
            switch (name)
            {
                case "Light":
                    igStyleColorsLight(null);
                    Current = "Light";
                    break;
                case "Classic":
                    igStyleColorsClassic(null);
                    Current = "Classic";
                    break;
                default:
                    igStyleColorsDark(null);
                    Current = "Dark";
                    break;
            }
        }
    }
}
