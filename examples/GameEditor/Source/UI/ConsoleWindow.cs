using System;
using System.IO;
using System.Numerics;
using System.Text;
using Imgui;
using static Imgui.ImguiNative;
using static Sokol.SApp;
using GameEditor.Framework.Core;

namespace GameEditor.UI
{
    public static unsafe class ConsoleWindow
    {
        private static bool _scrollToBottom = true;
        private static bool _showInfo    = true;
        private static bool _showWarning = true;
        private static bool _showError   = true;
        private static int  _selectedIndex = -1;
        private static string _savedLogPath = string.Empty;

        public static void Draw()
        {
            byte open = 1;
            if (!igBegin("Console", ref open, ImGuiWindowFlags.None))
            {
                igEnd();
                return;
            }

            // ── Filter toggles ────────────────────────────────────────────────
            byte info = _showInfo ? (byte)1 : (byte)0;
            if (igCheckbox("Info", ref info)) _showInfo = info != 0;
            igSameLine(0, 8);
            byte warn = _showWarning ? (byte)1 : (byte)0;
            if (igCheckbox("Warn", ref warn)) _showWarning = warn != 0;
            igSameLine(0, 8);
            byte err = _showError ? (byte)1 : (byte)0;
            if (igCheckbox("Error", ref err)) _showError = err != 0;
            igSameLine(0, 8);
            if (igButton("Clear", Vector2.Zero))
            {
                EditorState.ConsoleEntries.Clear();
                _selectedIndex = -1;
                _savedLogPath  = string.Empty;
            }
            igSameLine(0, 8);
            if (igButton("Copy All", Vector2.Zero))
                CopyAllToClipboard();
            igSameLine(0, 8);
            if (igButton("Save Log", Vector2.Zero))
                SaveLogToFile();

            // Show saved-path feedback on the same line
            if (_savedLogPath.Length > 0)
            {
                igSameLine(0, 8);
                igTextColored(new Vector4(0.4f, 1f, 0.4f, 1f), _savedLogPath);
            }

            igSeparator();

            igBeginChild_Str("##console_scroll", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);

            for (int i = 0; i < EditorState.ConsoleEntries.Count; i++)
            {
                var (level, msg) = EditorState.ConsoleEntries[i];
                if (level == LogLevel.Info    && !_showInfo)    continue;
                if (level == LogLevel.Warning && !_showWarning) continue;
                if (level == LogLevel.Error   && !_showError)   continue;

                var col = level switch
                {
                    LogLevel.Warning => new Vector4(1f, 0.9f, 0.1f, 1f),
                    LogLevel.Error   => new Vector4(1f, 0.3f, 0.3f, 1f),
                    _                => new Vector4(0.9f, 0.9f, 0.9f, 1f)
                };

                string prefix = level switch
                {
                    LogLevel.Warning => "[WARN] ",
                    LogLevel.Error   => "[ERR]  ",
                    _                => "[INFO] "
                };

                string fullLine = prefix + msg;

                // Push a unique integer ID so duplicate log strings don't conflict
                igPushID_Int(i);

                // Invisible selectable spanning the full row width for hit-testing
                bool isSelected = _selectedIndex == i;
                if (isSelected)
                    igPushStyleColor_Vec4(ImGuiCol.Header, new Vector4(0.25f, 0.25f, 0.45f, 1f));
                if (igSelectable_Bool("##row", isSelected, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    _selectedIndex = i;
                    sapp_set_clipboard_string(fullLine);   // single-click copies the line
                }
                if (isSelected)
                    igPopStyleColor(1);

                // Right-click context menu (uses current item which has unique PushID scope)
                if (igBeginPopupContextItem("##ctx", ImGuiPopupFlags.MouseButtonRight))
                {
                    if (igMenuItem_Bool("Copy Line", null, false, true))
                        sapp_set_clipboard_string(fullLine);
                    igSeparator();
                    if (igMenuItem_Bool("Copy All Visible", null, false, true))
                        CopyAllToClipboard();
                    if (igMenuItem_Bool("Save Log to File", null, false, true))
                        SaveLogToFile();
                    igEndPopup();
                }

                // Overlay the colored text on the same line as the selectable
                igSameLine(0, 0);
                igPushStyleColor_Vec4(ImGuiCol.Text, col);
                igTextColored(col, fullLine);
                igPopStyleColor(1);

                igPopID();
            }

            if (_scrollToBottom)
                igSetScrollHereY(1.0f);
            _scrollToBottom = false;

            // Auto-scroll when new entries arrive
            if (EditorState.ConsoleEntries.Count > 0)
                _scrollToBottom = true;

            igEndChild();
            igEnd();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void CopyAllToClipboard()
        {
            var sb = new StringBuilder();
            foreach (var (level, msg) in EditorState.ConsoleEntries)
            {
                if (level == LogLevel.Info    && !_showInfo)    continue;
                if (level == LogLevel.Warning && !_showWarning) continue;
                if (level == LogLevel.Error   && !_showError)   continue;

                string prefix = level switch
                {
                    LogLevel.Warning => "[WARN] ",
                    LogLevel.Error   => "[ERR]  ",
                    _                => "[INFO] "
                };
                sb.AppendLine(prefix + msg);
            }
            sapp_set_clipboard_string(sb.ToString());
        }

        private static void SaveLogToFile()
        {
            try
            {
                string path = Path.Combine(
                    Path.GetTempPath(),
                    $"gameeditor_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                var sb = new StringBuilder();
                foreach (var (level, msg) in EditorState.ConsoleEntries)
                {
                    string prefix = level switch
                    {
                        LogLevel.Warning => "[WARN] ",
                        LogLevel.Error   => "[ERR]  ",
                        _                => "[INFO] "
                    };
                    sb.AppendLine(prefix + msg);
                }
                File.WriteAllText(path, sb.ToString());
                _savedLogPath = path;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Console] Failed to save log: {ex.Message}");
            }
        }
    }
}
