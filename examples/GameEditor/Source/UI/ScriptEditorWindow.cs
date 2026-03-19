// ScriptEditorWindow.cs — Dockable multi-tab C# script editor panel.
//
// Double-clicking a .cs file in AssetsPanel calls Open(path).
// Ctrl+S saves the current tab.
// A dirty indicator (•) appears on the tab when unsaved changes exist.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Imgui;
using static Imgui.ImguiNative;
using GameEditor.Framework.Core;

namespace GameEditor.UI
{
    public static unsafe class ScriptEditorWindow
    {
        // ── Per-tab state ─────────────────────────────────────────────────────
        private sealed class OpenFile
        {
            public string                         FilePath;
            public string                         FileName;
            public CodeEditor.TextEditorWidget    Editor = new();
            public bool                           IsDirty;
            public DateTime                       LastDiskWriteUtc;
            public bool                           ReloadBannerVisible;

            public OpenFile(string path, string text)
            {
                FilePath          = path;
                FileName          = Path.GetFileName(path);
                LastDiskWriteUtc  = File.GetLastWriteTimeUtc(path);
                Editor.SetText(text);
                IsDirty           = false;
            }
        }

        private static readonly List<OpenFile>   _tabs       = new();
        private static          int               _activeTab  = 0;
        private static          bool              _isVisible  = false;

        // Tab scheduled for close (confirmed or no unsaved changes)
        private static int    _closeTabIdx     = -1;
        private static bool   _showSaveModal   = false;

        // ── Public API ────────────────────────────────────────────────────────

        public static bool IsVisible => _isVisible;

        /// <summary>
        /// Open a .cs file in a new tab (or switch to it if already open).
        /// Called by AssetsPanel on double-click.
        /// </summary>
        public static void Open(string filePath)
        {
            // Switch to existing tab if already open
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (string.Equals(_tabs[i].FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    _activeTab = i;
                    _isVisible = true;
                    return;
                }
            }

            try
            {
                string text = File.ReadAllText(filePath, Encoding.UTF8);
                var tab = new OpenFile(filePath, text);
                _tabs.Add(tab);
                _activeTab = _tabs.Count - 1;
                _isVisible = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[ScriptEditor] Could not open {filePath}: {ex.Message}");
            }
        }

        /// <summary>Called each frame from EditorLayout.</summary>
        public static void Draw()
        {
            ImGuiWindowClass cls = default;
            cls.DockingAlwaysTabBar = 1;
            igSetNextWindowClass(&cls);

            byte open = 1;
            bool visible = igBegin("Script Editor", ref open,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            _isVisible = visible;

            if (!visible)
            {
                igEnd();
                return;
            }

            if (_tabs.Count == 0)
            {
                igTextDisabled("No scripts open. Double-click a .cs file in the Assets panel.");
                DrawStatusBar();
                igEnd();
                return;
            }

            // Check for on-disk changes for the active tab
            CheckDiskReload();

            // ── Tab bar ───────────────────────────────────────────────────────
            DrawTabBar();

            // ── Save modal ────────────────────────────────────────────────────
            DrawSaveModal();

            // ── Editor area ───────────────────────────────────────────────────
            if (_activeTab >= 0 && _activeTab < _tabs.Count)
            {
                var tab = _tabs[_activeTab];

                // Reload banner
                if (tab.ReloadBannerVisible)
                    DrawReloadBanner(tab);

                // Available space, minus status bar height
                Vector2 avail = default;
                igGetContentRegionAvail(ref avail);
                float statusH = igGetFrameHeight() + 4f;
                Vector2 editorSize = new Vector2(avail.X, MathF.Max(4f, avail.Y - statusH));

                // Mark dirty when text changed (compare version before/after render)
                int versionBefore = tab.Editor.LineCount; // used as change proxy below
                tab.Editor.Render("##scriptEditor", editorSize);
                // Any keyboard input inside Render increments LineCount or text
                // — check via a simpler "is window focused + key pressed" flag
                // We track dirty via the OnTextChanged event approach below.

                // Handle Ctrl+S
                var io = igGetIO_Nil();
                bool ctrlS = (io->KeyMods & ImGuiKey.ImGuiMod_Ctrl) != 0 &&
                             igIsKeyPressed_Bool(ImGuiKey.S, false) &&
                             igIsWindowFocused(ImGuiFocusedFlags.ChildWindows);
                if (ctrlS)
                    SaveTab(tab);
            }

            DrawStatusBar();
            igEnd();
        }

        // ── Tab bar ───────────────────────────────────────────────────────────
        private static void DrawTabBar()
        {
            if (!igBeginTabBar("##scriptTabs", ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.AutoSelectNewTabs))
                return;

            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                string label = (tab.IsDirty ? "\u2022 " : "") + tab.FileName + $"##{i}";

                ImGuiTabItemFlags flags = ImGuiTabItemFlags.None;
                if (tab.IsDirty)
                    flags |= ImGuiTabItemFlags.UnsavedDocument;

                byte tabOpen = 1;
                bool selected = igBeginTabItem(label, ref tabOpen, flags);
                if (tabOpen == 0) RequestClose(i);  // X button clicked

                if (selected)
                {
                    if (_activeTab != i) _activeTab = i;
                    igEndTabItem();
                }

                // Middle-click to close
                if (igIsItemHovered(ImGuiHoveredFlags.None) &&
                    igIsMouseClicked_Bool(ImGuiMouseButton.Middle, false))
                    RequestClose(i);

                // Context menu on tab
                if (igBeginPopupContextItem($"##tabctx_{i}", ImGuiPopupFlags.MouseButtonRight))
                {
                    if (igMenuItem_Bool("Save", "Ctrl+S", false, tab.IsDirty))
                        SaveTab(tab);
                    if (igMenuItem_Bool("Close", null, false, true))
                        RequestClose(i);
                    if (igMenuItem_Bool("Reveal in Finder", null, false, true))
                        RevealInFinder(tab.FilePath);
                    igEndPopup();
                }
            }

            igEndTabBar();
        }

        // ── Save modal ────────────────────────────────────────────────────────
        private static void DrawSaveModal()
        {
            if (_showSaveModal)
            {
                igOpenPopup_Str("Save Changes?", ImGuiPopupFlags.None);
                _showSaveModal = false;
            }

            igSetNextWindowPos(default, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            byte modalOpen = 1;
            if (igBeginPopupModal("Save Changes?", ref modalOpen,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                if (_closeTabIdx >= 0 && _closeTabIdx < _tabs.Count)
                {
                    igText($"Save '{_tabs[_closeTabIdx].FileName}' before closing?");
                    igSeparator();
                    if (igButton("Save", new Vector2(80, 0)))
                    {
                        SaveTab(_tabs[_closeTabIdx]);
                        CloseTab(_closeTabIdx);
                        igCloseCurrentPopup();
                    }
                    igSameLine(0, 8);
                    if (igButton("Don't Save", new Vector2(100, 0)))
                    {
                        CloseTab(_closeTabIdx);
                        igCloseCurrentPopup();
                    }
                    igSameLine(0, 8);
                    if (igButton("Cancel", new Vector2(80, 0)))
                    {
                        _closeTabIdx = -1;
                        igCloseCurrentPopup();
                    }
                }
                igEndPopup();
            }
        }

        // ── Reload banner ─────────────────────────────────────────────────────
        private static void DrawReloadBanner(OpenFile tab)
        {
            igPushStyleColor_Vec4(ImGuiCol.ChildBg, new Vector4(0.25f, 0.2f, 0f, 1f));
            igBeginChild_Str("##reloadBanner", new Vector2(0, igGetFrameHeight() + 6f),
                ImGuiChildFlags.None, ImGuiWindowFlags.None);
            igText("\uF071  File changed on disk.");        // FA warning
            igSameLine(0, 16);
            if (igSmallButton("Reload"))
            {
                ReloadFromDisk(tab);
                tab.ReloadBannerVisible = false;
            }
            igSameLine(0, 8);
            if (igSmallButton("Dismiss"))
                tab.ReloadBannerVisible = false;
            igEndChild();
            igPopStyleColor(1);
        }

        // ── Status bar ────────────────────────────────────────────────────────
        private static void DrawStatusBar()
        {
            Vector2 avail = default;
            igGetContentRegionAvail(ref avail);

            igSeparator();
            if (_activeTab >= 0 && _activeTab < _tabs.Count)
            {
                var tab = _tabs[_activeTab];
                var (line, col) = tab.Editor.CursorPosition;
                igText($"Ln {line}, Col {col}");
                igSameLine(0, 16);
                if (tab.IsDirty)
                {
                    igPushStyleColor_Vec4(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.2f, 1f));
                    igText("\u2022 Unsaved changes");
                    igPopStyleColor(1);
                }
                else
                {
                    igPushStyleColor_Vec4(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1f));
                    igText("Saved");
                    igPopStyleColor(1);
                }
            }
            else
            {
                igText("C# Script Editor");
            }
        }

        // ── File operations ───────────────────────────────────────────────────
        private static void SaveTab(OpenFile tab)
        {
            try
            {
                File.WriteAllText(tab.FilePath, tab.Editor.GetText(), Encoding.UTF8);
                tab.IsDirty            = false;
                tab.LastDiskWriteUtc   = File.GetLastWriteTimeUtc(tab.FilePath);
                tab.ReloadBannerVisible = false;
                Logger.Info($"[ScriptEditor] Saved {tab.FileName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[ScriptEditor] Failed to save {tab.FilePath}: {ex.Message}");
            }
        }

        private static void ReloadFromDisk(OpenFile tab)
        {
            try
            {
                string text = File.ReadAllText(tab.FilePath, Encoding.UTF8);
                tab.Editor.SetText(text);
                tab.IsDirty            = false;
                tab.LastDiskWriteUtc   = File.GetLastWriteTimeUtc(tab.FilePath);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ScriptEditor] Failed to reload {tab.FilePath}: {ex.Message}");
            }
        }

        private static void RequestClose(int idx)
        {
            if (idx < 0 || idx >= _tabs.Count) return;
            if (_tabs[idx].IsDirty)
            {
                _closeTabIdx   = idx;
                _showSaveModal = true;
            }
            else
            {
                CloseTab(idx);
            }
        }

        private static void CloseTab(int idx)
        {
            _tabs.RemoveAt(idx);
            _closeTabIdx = -1;
            _activeTab   = Math.Min(_activeTab, _tabs.Count - 1);
            if (_activeTab < 0) _activeTab = 0;
        }

        private static void CheckDiskReload()
        {
            if (_activeTab < 0 || _activeTab >= _tabs.Count) return;
            var tab = _tabs[_activeTab];
            if (tab.ReloadBannerVisible) return; // already showing

            try
            {
                var currentWriteTime = File.GetLastWriteTimeUtc(tab.FilePath);
                if (currentWriteTime > tab.LastDiskWriteUtc)
                {
                    tab.LastDiskWriteUtc = currentWriteTime;
                    if (!tab.IsDirty)
                        ReloadFromDisk(tab);
                    else
                        tab.ReloadBannerVisible = true;
                }
            }
            catch { /* file may be temporarily locked */ }
        }

        private static void RevealInFinder(string path)
        {
            try
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.OSX))
                    System.Diagnostics.Process.Start("open", $"-R \"{path}\"");
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.Windows))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                else
                    System.Diagnostics.Process.Start("xdg-open",
                        $"\"{System.IO.Path.GetDirectoryName(path)}\"");
            }
            catch { }
        }

        // ── Mark dirty when the editor text changes ───────────────────────────
        // Called by EditorLayout each frame after Draw() — compares hash of text.
        // Simpler: we set tab.IsDirty on any key that could modify text.
        // The TextEditorWidget exposes LineCount which changes on edits.
        // A frame-to-frame version check is done via _textDirtyTracker below.

        private static readonly Dictionary<int, string> _lastTextHash = new();

        public static void MarkDirtyIfChanged()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                if (tab.IsDirty) continue; // already dirty, skip hash

                string current = tab.Editor.GetText();
                if (!_lastTextHash.TryGetValue(i, out string? prev) || prev != current)
                {
                    _lastTextHash[i] = current;
                    if (prev != null) // null on first setup — don't mark dirty on load
                        tab.IsDirty = true;
                }
            }
        }
    }
}
