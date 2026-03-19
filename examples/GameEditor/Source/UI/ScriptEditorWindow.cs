// ScriptEditorWindow.cs — Dockable multi-tab C# script editor panel.
//
// Double-clicking a .cs file in AssetsPanel calls Open(path).
// Ctrl+S saves the current tab and triggers a background build.
// A dirty indicator (•) appears on the tab when unsaved changes exist.
// Roslyn provides real-time diagnostics and autocomplete.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Imgui;
using static Imgui.ImguiNative;
using GameEditor;
using GameEditor.CodeEditor;
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

        /// <summary>True if the Script Editor window had keyboard focus last frame.</summary>
        public static bool IsWindowFocused { get; private set; }

        // Tab scheduled for close (confirmed or no unsaved changes)
        private static int    _closeTabIdx     = -1;
        private static bool   _showSaveModal   = false;

        // ── Build / diagnostics state ─────────────────────────────────────────
        private static bool   _eventsSubscribed;

        // Build diagnostics arriving from the thread-pool — applied next Draw()
        private static IReadOnlyList<BuildDiagnostic>? _pendingBuildDiags;
        private static readonly object                  _pendingBuildLock = new();

        // Roslyn real-time diagnostics arriving from the thread-pool
        private static readonly ConcurrentQueue<(string FilePath, IReadOnlyList<BuildDiagnostic> Diags)>
            _pendingRoslynDiags = new();

        // Cross-file navigations arriving from the thread-pool — applied next Draw()
        private static readonly ConcurrentQueue<(string File, int Line, int Col)>
            _pendingNavigations = new();

        // Tab index to force-select on the next DrawTabBar() call (-1 = none).
        // Set when navigation switches to an already-open tab so ImGui's own
        // internal selection state (which still points at the previously active tab)
        // doesn't overwrite the programmatic switch.
        private static int _forceSelectTab = -1;

        // ── Public API ────────────────────────────────────────────────────────

        public static bool IsVisible => _isVisible;

        /// <summary>
        /// Open a .cs file in a new tab (or switch to it if already open).
        /// Called by AssetsPanel on double-click.
        /// </summary>
        public static void Open(string filePath)
        {
            Logger.Info($"[ScriptEditor] Open: {System.IO.Path.GetFileName(filePath)}");
            // Switch to existing tab if already open
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (string.Equals(_tabs[i].FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info($"[ScriptEditor] Open: already open at tab {i}");
                    _activeTab = i;
                    _forceSelectTab = i;  // tell DrawTabBar to force-select this tab
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

                // Register with Roslyn for real-time diagnostics (no debounce on open)
                string fp = tab.FilePath;
                RoslynHost.Instance.UpdateDocument(fp, text, debounceMs: 0);

                // Scan the whole project directory so all .cs files are in the workspace.
                // This enables cross-file Go-to-Definition and Find-References.
                string? projectDir = FindProjectDirectory(fp);
                string? sokolNetHome = FindSokolNetHome();
                if (projectDir != null || sokolNetHome != null)
                    Task.Run(() =>
                    {
                        if (projectDir != null)
                        {
                            Logger.Info($"[ScriptEditor] Scanning project dir: {projectDir}");
                            RoslynHost.Instance.ScanProjectDirectory(projectDir);
                        }
                        // Also scan the Sokol.NET framework source so symbols like
                        // GameBehaviour.Transform resolve to their .cs source file
                        // (they are compiled as source via <Compile Include> in Directory.Build.props)
                        if (sokolNetHome != null)
                        {
                            string fwRoot = System.IO.Path.Combine(sokolNetHome, "src", "Framework");
                            if (Directory.Exists(fwRoot))
                            {
                                Logger.Info($"[ScriptEditor] Scanning framework: {fwRoot}");
                                RoslynHost.Instance.ScanProjectDirectory(fwRoot);
                            }
                        }
                    });

                // Subscribe to Roslyn diagnostics for this editor
                RoslynHost.Instance.DiagnosticsChanged += (path, diags) =>
                {
                    if (!string.Equals(path, fp, StringComparison.OrdinalIgnoreCase)) return;
                    _pendingRoslynDiags.Enqueue((path, diags));
                };

                // Wire completion trigger — fires on '.' or Ctrl+Space (render thread).
                // The event also carries the trigger line+col captured at fire time so
                // ShowCompletions can store them without byte-offset arithmetic.
                tab.Editor.CompletionRequested += caretOffset =>
                {
                    Logger.Info($"[ScriptEditor] CompletionRequested: {System.IO.Path.GetFileName(fp)} offset={caretOffset}");
                    string currentText = tab.Editor.GetText();
                    RoslynHost.Instance.UpdateDocument(fp, currentText, debounceMs: 0);
                    int  tLine  = tab.Editor.CompletionTriggerLine;
                    int  tCol   = tab.Editor.CompletionTriggerCol;
                    char tChar  = tab.Editor.CompletionTriggerChar;
                    _ = RoslynHost.Instance.GetCompletionsAsync(fp, caretOffset, tChar)
                        .ContinueWith(t =>
                        {
                            Console.Error.WriteLine($"[ScriptEditor] GetCompletions result: {(t.IsFaulted ? "FAULTED " + t.Exception?.InnerException?.Message : t.Result.Count + " items")}");
                            if (!t.IsFaulted && t.Result.Count > 0)
                                tab.Editor.ShowCompletions(t.Result, tLine, tCol);
                        }, System.Threading.Tasks.TaskScheduler.Default);
                };

                // Wire signature help — fires on '(' and ',' (render thread).
                tab.Editor.SignatureHelpRequested += caretOffset =>
                {
                    string currentText = tab.Editor.GetText();
                    RoslynHost.Instance.UpdateDocument(fp, currentText, debounceMs: 0);
                    _ = RoslynHost.Instance.GetSignatureHelpAsync(fp, caretOffset)
                        .ContinueWith(t =>
                        {
                            if (!t.IsFaulted)
                                tab.Editor.ShowSignatureHelp(t.Result); // null = hide
                        }, System.Threading.Tasks.TaskScheduler.Default);
                };

                // Set editor file path for Roslyn navigation
                tab.Editor.FilePath = fp;

                // Cross-file navigation (Go to Definition / Find References result in another file).
                // The event may fire from a thread-pool thread (Roslyn ContinueWith), so we
                // enqueue the request and drain it on the render thread in Draw().
                tab.Editor.NavigationRequested += (targetFile, line, col) =>
                    _pendingNavigations.Enqueue((targetFile, line, col));
            }
            catch (Exception ex)
            {
                Logger.Error($"[ScriptEditor] Could not open {filePath}: {ex.Message}");
            }
        }

        /// <summary>Called each frame from EditorLayout.</summary>
        public static void Draw()
        {
            EnsureEventsSubscribed();

            // ── Drain pending background diagnostics (thread-safe) ────────────
            IReadOnlyList<BuildDiagnostic>? buildDiags;
            lock (_pendingBuildLock)
            {
                buildDiags       = _pendingBuildDiags;
                _pendingBuildDiags = null;
            }
            if (buildDiags != null)
                ApplyBuildDiagnostics(buildDiags);

            while (_pendingRoslynDiags.TryDequeue(out var ritem))
                ApplyRoslynDiagnostics(ritem.FilePath, ritem.Diags);

            // Drain cross-file navigation requests queued by the thread-pool
            while (_pendingNavigations.TryDequeue(out var nav))
            {
                Logger.Info($"[ScriptEditor] Navigation dequeued → {System.IO.Path.GetFileName(nav.File)} L{nav.Line}:{nav.Col}");
                Open(nav.File);
                for (int ti = 0; ti < _tabs.Count; ti++)
                {
                    if (string.Equals(_tabs[ti].FilePath, nav.File, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Info($"[ScriptEditor] ScrollToLine: tab {ti} → L{nav.Line - 1}");
                        _tabs[ti].Editor.ScrollToLine(nav.Line - 1, nav.Col - 1);
                        break;
                    }
                }
            }

            ImGuiWindowClass cls = default;
            cls.DockingAlwaysTabBar = 1;
            igSetNextWindowClass(&cls);

            byte open = 1;
            bool visible = igBegin("Script Editor", ref open,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            _isVisible = visible;
            IsWindowFocused = visible && igIsWindowFocused(ImGuiFocusedFlags.ChildWindows);

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
                if (_forceSelectTab == i)
                {
                    flags |= ImGuiTabItemFlags.SetSelected;
                    _forceSelectTab = -1;  // consume — only needed for one frame
                }

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

            // ── Build status indicator ────────────────────────────────────────
            igSameLine(0, 24);
            if (GameAssemblyRunner.IsBuilding)
            {
                // Animate a rotating spinner using ⣾⣽⣻⢿⡿⣟⣯⣷ braille dots
                string[] spinnerFrames = { "\u28fe", "\u28fd", "\u28fb", "\u28bf",
                                           "\u287f", "\u28df", "\u28ef", "\u28f7" };
                int frame = (int)(igGetTime() * 8.0) & 7;
                igPushStyleColor_Vec4(ImGuiCol.Text, new Vector4(0.6f, 0.8f, 1f, 1f));
                igText($"{spinnerFrames[frame]}  Building\u2026");
                igPopStyleColor(1);
            }
            else if (ConfigManager.HasProject)
            {
                int errorCount = GameAssemblyRunner.LastBuildDiagnostics.Count(d => d.IsError);
                if (GameAssemblyRunner.LastBuildSucceeded)
                {
                    igPushStyleColor_Vec4(ImGuiCol.Text, new Vector4(0.4f, 0.9f, 0.4f, 1f));
                    igText("\uf058  Build OK");        // FA check-circle
                    igPopStyleColor(1);
                }
                else
                {
                    igPushStyleColor_Vec4(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
                    igText($"\uf057  Build FAILED \u2014 {errorCount} error{(errorCount != 1 ? "s" : "")}");
                    igPopStyleColor(1);
                }
            }
        }

        // ── File operations ───────────────────────────────────────────────────

        /// <summary>
        /// Save every dirty tab. Called before entering Play so the build sees latest edits.
        /// </summary>
        public static void SaveAll()
        {
            foreach (var tab in _tabs)
                if (tab.IsDirty) SaveTab(tab);
        }

        private static void SaveTab(OpenFile tab)
        {
            try
            {
                string text = tab.Editor.GetText();
                File.WriteAllText(tab.FilePath, text, Encoding.UTF8);
                tab.IsDirty            = false;
                tab.LastDiskWriteUtc   = File.GetLastWriteTimeUtc(tab.FilePath);
                tab.ReloadBannerVisible = false;
                Logger.Info($"[ScriptEditor] Saved {tab.FileName}");

                // Update Roslyn immediately so completion/diagnostics reflect latest save
                RoslynHost.Instance.UpdateDocument(tab.FilePath, text, debounceMs: 0);

                // Trigger background rebuild if a project is open
                if (ConfigManager.HasProject)
                    GameAssemblyRunner.TriggerBuild(ConfigManager.ProjectFolder!);
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
            // Note: we intentionally do NOT call RoslynHost.Instance.RemoveDocument here.
            // The Roslyn workspace acts as a project-wide symbol graph; removing a document
            // when its UI tab is closed would break "Go to Definition" for any symbol defined
            // in that file (it would fall back to the DLL metadata instead of source).
            // Files accumulate in the workspace for the session lifetime, which is fine for
            // an IDE. If the file is re-opened, UpdateDocument refreshes its content.
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

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Walk up from <paramref name="filePath"/> until a directory containing a
        /// .csproj file is found. Returns that directory, or the file's own directory
        /// as a fallback.
        /// </summary>
        private static string? FindProjectDirectory(string filePath)
        {
            string? dir = Path.GetDirectoryName(filePath);
            while (dir != null)
            {
                if (Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0)
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return Path.GetDirectoryName(filePath);
        }

        /// <summary>
        /// Returns the Sokol.NET root dir by reading ~/.sokolnet_config/sokolnet_home,
        /// which is how the MSBuild SokolNetHome property is resolved at build time.
        /// </summary>
        private static string? FindSokolNetHome()
        {
            try
            {
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string configFile = System.IO.Path.Combine(homeDir, ".sokolnet_config", "sokolnet_home");
                if (File.Exists(configFile))
                {
                    string home = File.ReadAllText(configFile, Encoding.UTF8).Trim();
                    if (Directory.Exists(home))
                        return home;
                }
            }
            catch { }
            return null;
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
                    {
                        tab.IsDirty = true;
                        // Notify Roslyn about the change (debounced)
                        RoslynHost.Instance.UpdateDocument(tab.FilePath, current);
                    }
                }
            }
        }

        // ── Event subscription ────────────────────────────────────────────────

        private static void EnsureEventsSubscribed()
        {
            if (_eventsSubscribed) return;
            _eventsSubscribed = true;
            GameAssemblyRunner.BuildCompleted += OnBuildCompleted;
        }

        private static void OnBuildCompleted(IReadOnlyList<BuildDiagnostic> diags)
        {
            lock (_pendingBuildLock)
                _pendingBuildDiags = diags;
        }

        // ── Diagnostics application ───────────────────────────────────────────

        private static void ApplyBuildDiagnostics(IReadOnlyList<BuildDiagnostic> diags)
        {
            var (errsByFile, warnsByFile) = BuildErrorParser.GroupByFile(diags);

            // Apply to all open tabs, including clearing stale markers when no diags
            foreach (var tab in _tabs)
            {
                errsByFile.TryGetValue(tab.FilePath, out var errs);
                warnsByFile.TryGetValue(tab.FilePath, out var warns);
                tab.Editor.SetErrorMarkers(errs   ?? new Dictionary<int, string>());
                tab.Editor.SetWarningMarkers(warns ?? new Dictionary<int, string>());
            }
        }

        private static void ApplyRoslynDiagnostics(string filePath, IReadOnlyList<BuildDiagnostic> diags)
        {
            foreach (var tab in _tabs)
            {
                if (!string.Equals(tab.FilePath, filePath, StringComparison.OrdinalIgnoreCase)) continue;

                var errs = new Dictionary<int, string>();
                var warns = new Dictionary<int, string>();
                foreach (var d in diags)
                {
                    string msg = $"{d.Code}: {d.Message}";
                    var target = d.IsError ? errs : warns;
                    if (target.TryGetValue(d.Line, out string? existing))
                        target[d.Line] = existing + "\n" + msg;
                    else
                        target[d.Line] = msg;
                }

                tab.Editor.SetErrorMarkers(errs);
                tab.Editor.SetWarningMarkers(warns);
                break;
            }
        }
    }
}
