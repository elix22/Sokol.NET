using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Imgui;
using static Imgui.ImguiNative;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Core;
using GameEditor;
using GameEditor.CodeEditor;

namespace GameEditor.UI
{
    public static unsafe class Toolbar
    {
        private enum DialogMode { None, SaveAs, Open, NewProject, OpenProject, ProjectSettings, KeyboardShortcuts, Preferences }
        private static DialogMode _dialog = DialogMode.None;

        // Which window was focused just before the user pressed Play
        private static string _prePlayFocusWindow = "Scene";
        private static byte[] _dialogPathBuf    = new byte[512];
        private static byte[] _newProjectNameBuf = new byte[128];

        // Project Settings edit buffers
        private static byte[] _cfgNameBuf   = new byte[128];
        private static int    _cfgWidth     = 1280;
        private static int    _cfgHeight    = 720;

        // Project Settings dropdown state
        private static string[]  _psSceneFiles    = System.Array.Empty<string>();
        private static int       _psSceneIdx      = 0;
        private static string[]  _psCameraNames   = System.Array.Empty<string>();
        private static int       _psCameraIdx     = 0;
        private static int       _psPhys3DIdx     = 0;
        private static int       _psPhys2DIdx     = 0;
        private static readonly string[] _psPhys3DOptions = { "none", "jolt" };
        private static readonly string[] _psPhys2DOptions = { "none", "box2d" };

        // Keyboard-shortcuts dialog recording state
        private static string?  _recordingAction = null;

        // New-project background process
        private static Process? _createProcess = null;
        private static bool     _isCreating    = false;
        private static string?  _createResultFolder = null;
        private static string   _createProjectName  = "";
        private static readonly ConcurrentQueue<(bool isErr, string msg)> _createOutput = new();

        /// <summary>Opens the Save As dialog — callable from the global keyboard handler.</summary>
        public static void TriggerSaveAs()
            => OpenDialog(DialogMode.SaveAs, SceneManager.ActiveScene?.Name ?? "Untitled");

        public static void Draw()
        {
            if (!igBeginMainMenuBar())
                return;

            // File menu
            if (igBeginMenu("File", true))
            {
                if (igMenuItem_Bool("New Scene", "Ctrl+N", false, true))
                    SceneManager.NewScene();

                igSeparator();

                bool hasScene = SceneManager.ActiveScene != null;

                if (igMenuItem_Bool("Save Scene", "Ctrl+S", false, hasScene))
                {
                    var path = SceneManager.ActiveScene?.FilePath;
                    if (!string.IsNullOrEmpty(path))
                        SceneManager.SaveScene(path!);
                    else
                        OpenDialog(DialogMode.SaveAs, SceneManager.ActiveScene?.Name ?? "Untitled");
                }

                if (igMenuItem_Bool("Save Scene As...", null, false, hasScene))
                    OpenDialog(DialogMode.SaveAs, SceneManager.ActiveScene?.Name ?? "Untitled");

                if (igMenuItem_Bool("Open Scene...", "Ctrl+O", false, true))
                    OpenDialog(DialogMode.Open, SceneManager.ActiveScene?.FilePath ?? "");

                igSeparator();

                if (igMenuItem_Bool("New Project...", null, false, true))
                    OpenDialog(DialogMode.NewProject, "");

                if (igMenuItem_Bool("Open Project...", null, false, true))
                    OpenDialog(DialogMode.OpenProject, "");

                if (igMenuItem_Bool("Project Settings...", null, false, ConfigManager.HasProject))
                    OpenDialog(DialogMode.ProjectSettings, "");

                igSeparator();
                if (igMenuItem_Bool("Exit", null, false, true))
                    Sokol.SApp.sapp_request_quit();

                igEndMenu();
            }

            // Edit menu
            if (igBeginMenu("Edit", true))
            {
                if (igMenuItem_Bool("Create Entity", null, false, SceneManager.ActiveScene != null))
                {
                    int id = SceneManager.ActiveScene!.CreateEntity("Entity");
                    EditorState.SelectEntity(id);
                }
                igSeparator();
                if (igMenuItem_Bool("Keyboard Shortcuts...", null, false, true))
                    _dialog = DialogMode.KeyboardShortcuts;
                if (igMenuItem_Bool("Preferences...", null, false, true))
                    _dialog = DialogMode.Preferences;
                igEndMenu();
            }

            // Play/Pause/Stop buttons centred (Font Awesome icons)
            var state = SceneManager.PlayMode;
            igSetCursorPosX((igGetWindowWidth() - 110f) * 0.5f);

            bool playing = state == PlayModeState.Playing;
            bool paused  = state == PlayModeState.Paused;
            bool stopped = state == PlayModeState.Stopped;

            if (stopped || paused)
            {
                // Disable while building or after a failed build (with a project loaded)
                bool playBlocked = GameAssemblyRunner.IsBuilding ||
                    (!GameAssemblyRunner.LastBuildSucceeded && ConfigManager.HasProject);
                if (playBlocked) igBeginDisabled(true);
                if (igButton("\uF04B##play", new Vector2(32, 0)))
                {
                    // Record which window was focused so we can restore it on Stop
                    if (stopped)
                    {
                        // Check visibility (active tab) rather than keyboard focus, because
                        // clicking Play moves ImGui focus to the toolbar before this executes.
                        if (ScriptEditorWindow.IsVisible)      _prePlayFocusWindow = "Script Editor";
                        else if (SceneWindow.IsVisible)        _prePlayFocusWindow = "Scene";
                        else                                   _prePlayFocusWindow = "Scene";
                    }

                    // Save all dirty script tabs so the build picks up latest changes
                    if (stopped) ScriptEditorWindow.SaveAll();

                    // Auto-save modified scene before entering Play so the on-disk
                    // copy is up to date and the pre-play snapshot is correct.
                    if (stopped)
                    {
                        var activeScene = SceneManager.ActiveScene;
                        if (activeScene != null && activeScene.IsDirty
                            && !string.IsNullOrWhiteSpace(activeScene.FilePath))
                        {
                            SceneManager.SaveScene(activeScene.FilePath!);
                            Logger.Info($"[AutoSave] Scene saved before Play: {activeScene.FilePath}");
                        }
                    }

                    // When transitioning from Stopped → Playing, build and load
                    // the game project's script assembly if a project is open.
                    if (stopped && ConfigManager.HasProject)
                        GameAssemblyRunner.EnsureLoaded(ConfigManager.ProjectFolder!);
                    SceneManager.Play();
                    GameWindow.FocusWindow();
                }
                if (igIsItemHovered(ImGuiHoveredFlags.None))
                    igSetTooltip(stopped ? "Play" : "Resume");
                if (playBlocked) igEndDisabled();
            }
            else
            {
                igBeginDisabled(false);
                igButton("\uF04B##play_dis", new Vector2(32, 0));
                igEndDisabled();
            }

            igSameLine(0, 4);

            if (playing)
            {
                if (igButton("\uF04C##pause", new Vector2(32, 0)))
                    SceneManager.Pause();
                if (igIsItemHovered(ImGuiHoveredFlags.None))
                    igSetTooltip("Pause");
            }

            if (playing || paused)
            {
                igSameLine(0, 4);
                if (igButton("\uF04D##stop", new Vector2(32, 0)))
                {
                    SceneManager.Stop();
                    // Restore focus to whichever window was active before Play
                    if (_prePlayFocusWindow == "Script Editor")
                        igSetWindowFocus_Str("Script Editor");
                    else
                        SceneWindow.FocusWindow();
                }
                if (igIsItemHovered(ImGuiHoveredFlags.None))
                    igSetTooltip("Stop");
            }

            // Build status indicator
            if (GameAssemblyRunner.IsBuilding)
            {
                igSameLine(0, 12);
                igTextColored(new Vector4(1f, 0.85f, 0.2f, 1f), "[Building...]");
            }
            else if (!GameAssemblyRunner.LastBuildSucceeded)
            {
                igSameLine(0, 12);
                igTextColored(new Vector4(1f, 0.35f, 0.35f, 1f), "[Build Failed]");
            }

            // ── Gizmo operation buttons (right side of menu bar) ──────────────
            float gizmoAreaWidth = 290f;
            igSetCursorPosX(igGetWindowWidth() - gizmoAreaWidth);

            bool isT = EditorState.CurrentGizmoOp == ImGuizmo.Operation.Translate;
            bool isR = EditorState.CurrentGizmoOp == ImGuizmo.Operation.Rotate;
            bool isSc = EditorState.CurrentGizmoOp == ImGuizmo.Operation.Scale;
            var activeCol = new Vector4(0.26f, 0.70f, 0.98f, 1f);

            if (isT) igPushStyleColor_Vec4(ImGuiCol.Button, activeCol);
            if (igButton("T##gT", new Vector2(26, 0))) { EditorState.CurrentGizmoOp = ImGuizmo.Operation.Translate; EditorPersistence.Save(); }
            if (isT) igPopStyleColor(1);
            if (igIsItemHovered(ImGuiHoveredFlags.None)) igSetTooltip($"Translate ({GameEditor.CodeEditor.KeyBindings.Get(GameEditor.CodeEditor.KeyBindings.GizmoTranslate)})");

            igSameLine(0, 2);
            if (isR) igPushStyleColor_Vec4(ImGuiCol.Button, activeCol);
            if (igButton("R##gR", new Vector2(26, 0))) { EditorState.CurrentGizmoOp = ImGuizmo.Operation.Rotate; EditorPersistence.Save(); }
            if (isR) igPopStyleColor(1);
            if (igIsItemHovered(ImGuiHoveredFlags.None)) igSetTooltip($"Rotate ({GameEditor.CodeEditor.KeyBindings.Get(GameEditor.CodeEditor.KeyBindings.GizmoRotate)})");

            igSameLine(0, 2);
            if (isSc) igPushStyleColor_Vec4(ImGuiCol.Button, activeCol);
            if (igButton("S##gS", new Vector2(26, 0))) { EditorState.CurrentGizmoOp = ImGuizmo.Operation.Scale; EditorPersistence.Save(); }
            if (isSc) igPopStyleColor(1);
            if (igIsItemHovered(ImGuiHoveredFlags.None)) igSetTooltip($"Scale ({GameEditor.CodeEditor.KeyBindings.Get(GameEditor.CodeEditor.KeyBindings.GizmoScale)})");

            igSameLine(0, 8);
            bool isLocal = EditorState.CurrentGizmoMode == ImGuizmo.Mode.Local;
            if (isLocal) igPushStyleColor_Vec4(ImGuiCol.Button, activeCol);
            if (igButton(isLocal ? "Local" : "World", new Vector2(50, 0)))
            {
                EditorState.CurrentGizmoMode = isLocal ? ImGuizmo.Mode.World : ImGuizmo.Mode.Local;
                EditorPersistence.Save();
            }
            if (isLocal) igPopStyleColor(1);

            // ── Overlays dropdown (like Unity's "Gizmos" button) ─────────────
            igSameLine(0, 8);
            if (igButton("Overlays  \uF078", new Vector2(92, 0)))  // \uF078 = FA chevron-down; two spaces so icon doesn't crowd 's'
                igOpenPopup_Str("##overlays_popup", ImGuiPopupFlags.None);
            if (igIsItemHovered(ImGuiHoveredFlags.None)) igSetTooltip("Toggle scene overlays");

            // Anchor the popup directly below the button using its screen rect.
            // This must be done before igBeginPopup so ImGui uses the explicit position.
            Vector2 _ovBtnMin  = default;
            Vector2 _ovBtnSize = default;
            igGetItemRectMin(ref _ovBtnMin);
            igGetItemRectSize(ref _ovBtnSize);
            igSetNextWindowPos(
                new Vector2(_ovBtnMin.X, _ovBtnMin.Y + _ovBtnSize.Y),
                ImGuiCond.Always, Vector2.Zero);

            if (igBeginPopup("##overlays_popup", ImGuiWindowFlags.None))
            {
                var ov = EditorState.Overlays;
                var prevOv = ov;
                DrawOverlayToggle(ref ov, GizmoOverlayFlags.Grid,            "Grid");
                DrawOverlayToggle(ref ov, GizmoOverlayFlags.WorldAxes,       "World Axes");
                DrawOverlayToggle(ref ov, GizmoOverlayFlags.OrientationCube, "Orientation Cube");
                DrawOverlayToggle(ref ov, GizmoOverlayFlags.EntityGizmos,    "Entity Gizmos");
                DrawOverlayToggle(ref ov, GizmoOverlayFlags.CameraGizmos,    "Camera Gizmos");
                DrawOverlayToggle(ref ov, GizmoOverlayFlags.LightGizmos,     "Light Gizmos");
                EditorState.Overlays = ov;
                if (ov != prevOv)
                    EditorPersistence.Save();
                igEndPopup();
            }

            igEndMainMenuBar();

            // Draw modal dialogs (must be outside BeginMainMenuBar)
            DrawDialogs();
        }

        private static void DrawOverlayToggle(ref GizmoOverlayFlags flags, GizmoOverlayFlags bit, string label)
        {
            byte on = (flags & bit) != 0 ? (byte)1 : (byte)0;
            if (igCheckbox(label, ref on))
                flags = on != 0 ? flags | bit : flags & ~bit;
        }

        private static void OpenDialog(DialogMode mode, string initialPath)
        {
            _dialog = mode;
            _dialogPathBuf = new byte[512];

            if (mode == DialogMode.SaveAs)
            {
                string initial = initialPath.EndsWith(".scene.json") ? initialPath : initialPath + ".scene.json";
                string dir  = System.IO.Path.GetDirectoryName(initial) ?? "";
                string fname = System.IO.Path.GetFileName(initial);

                // Default save location: project's Assets/ folder when a project is loaded
                if (!System.IO.Directory.Exists(dir) || string.IsNullOrEmpty(dir))
                {
                    if (ConfigManager.HasProject)
                    {
                        dir = System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets");
                        System.IO.Directory.CreateDirectory(dir);
                    }
                    else
                    {
                        dir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                    }
                }

                ImFileDialog.OpenDialog("ifd_saveas", "Save Scene As",
                    "Scene Files{.scene.json},All Files{.*}", dir, fname, ImFileDialog.Mode.SaveFile);
            }
            else if (mode == DialogMode.Open)
            {
                string dir = !string.IsNullOrEmpty(initialPath)
                    ? (System.IO.Directory.Exists(initialPath) ? initialPath
                       : System.IO.Path.GetDirectoryName(initialPath) ?? "")
                    : "";
                ImFileDialog.OpenDialog("ifd_open", "Open Scene",
                    "Scene Files{.scene.json},All Files{.*}", dir, "", ImFileDialog.Mode.OpenFile);
            }
            else if (mode == DialogMode.OpenProject)
            {
                ImFileDialog.OpenDialog("ifd_openproject", "Open Project Folder",
                    null, initialPath, "", ImFileDialog.Mode.SelectFolder);
            }
            else if (mode == DialogMode.NewProject)
            {
                _newProjectNameBuf = new byte[128];
                FillBuffer(ref _dialogPathBuf,
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile));
            }
            else if (mode == DialogMode.ProjectSettings)
            {
                var cfg = ConfigManager.Config;
                if (cfg != null)
                {
                    FillBuffer(ref _cfgNameBuf, cfg.ProjectName);
                    _cfgWidth  = cfg.ScreenWidth;
                    _cfgHeight = cfg.ScreenHeight;

                    // Discover scene files and match current value
                    _psSceneFiles = DiscoverSceneFiles();
                    _psSceneIdx   = 0;
                    for (int i = 0; i < _psSceneFiles.Length; i++)
                        if (string.Equals(_psSceneFiles[i], cfg.DefaultScene, StringComparison.OrdinalIgnoreCase))
                        { _psSceneIdx = i; break; }

                    // Discover cameras in the selected scene
                    string? sel = _psSceneFiles.Length > 0 ? _psSceneFiles[_psSceneIdx] : null;
                    _psCameraNames = DiscoverCameraNames(sel);
                    _psCameraIdx   = 0;
                    for (int i = 0; i < _psCameraNames.Length; i++)
                        if (string.Equals(_psCameraNames[i], cfg.DefaultCamera, StringComparison.OrdinalIgnoreCase))
                        { _psCameraIdx = i; break; }

                    // Match physics engine options
                    _psPhys3DIdx = System.Array.IndexOf(_psPhys3DOptions, cfg.Physics3D);
                    if (_psPhys3DIdx < 0) _psPhys3DIdx = 0;
                    _psPhys2DIdx = System.Array.IndexOf(_psPhys2DOptions, cfg.Physics2D);
                    if (_psPhys2DIdx < 0) _psPhys2DIdx = 0;
                }
            }
        }

        private static void FillBuffer(ref byte[] buf, string text)
        {
            buf = new byte[buf.Length];
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            int len = Math.Min(bytes.Length, buf.Length - 1);
            System.Array.Copy(bytes, buf, len);
        }

        private static void DrawDialogs()
        {
            // Drain new-project process output on main thread
            while (_createOutput.TryDequeue(out var item))
            {
                if (item.isErr) Logger.Warning(item.msg);
                else            Logger.Info(item.msg);
            }

            // Detect new-project process completion
            if (_isCreating && _createProcess != null && _createProcess.HasExited)
            {
                _isCreating = false;
                if (_createProcess.ExitCode == 0 && _createResultFolder != null)
                {
                    // The template already ships a config.json — just load it.
                    // Patch the project name to match what the user typed.
                    var cfg = ConfigManager.Load(_createResultFolder);
                    if (cfg != null)
                    {
                        cfg.ProjectName = _createProjectName;
                        ConfigManager.Save();
                        LoadProjectDefaultScene();
                        string watchFolder = _createResultFolder;
                        _createResultFolder = null;
                        _dialog = DialogMode.None;
                        GameAssemblyRunner.StartWatcher(watchFolder);
                    }
                    else
                    {
                        _createResultFolder = null;
                        _dialog = DialogMode.None;
                    }
                }
            }

            // New-project destination folder browser (can be open alongside NewProject dialog)
            if (ImFileDialog.Display("ifd_np_dest"))
            {
                if (ImFileDialog.IsOk())
                    FillBuffer(ref _dialogPathBuf, ImFileDialog.GetFilePathName());
                ImFileDialog.Close();
            }

            if (_dialog == DialogMode.None) return;

            // ── File-picker dialogs (render their own window) ─────────────────
            if (_dialog == DialogMode.SaveAs)
            {
                if (ImFileDialog.Display("ifd_saveas"))
                {
                    if (ImFileDialog.IsOk())
                        SceneManager.SaveScene(ImFileDialog.GetFilePathName());
                    ImFileDialog.Close();
                    _dialog = DialogMode.None;
                }
                return;
            }

            if (_dialog == DialogMode.Open)
            {
                if (ImFileDialog.Display("ifd_open"))
                {
                    if (ImFileDialog.IsOk())
                        SceneManager.LoadScene(ImFileDialog.GetFilePathName());
                    ImFileDialog.Close();
                    _dialog = DialogMode.None;
                }
                return;
            }

            if (_dialog == DialogMode.OpenProject)
            {
                if (ImFileDialog.Display("ifd_openproject"))
                {
                    if (ImFileDialog.IsOk())
                    {
                        string projFolder = ImFileDialog.GetFilePathName();
                        // Unload any old game assembly before switching projects
                        GameAssemblyRunner.StopWatcher();
                        GameAssemblyRunner.Unload();
                        var cfg = ConfigManager.Load(projFolder);
                        if (cfg != null)
                        {
                            EditorPersistence.AddRecentProject(projFolder);
                            LoadProjectDefaultScene();
                            GameAssemblyRunner.StartWatcher(projFolder);
                        }
                    }
                    ImFileDialog.Close();
                    _dialog = DialogMode.None;
                }
                return;
            }

            // ── Keyboard Shortcuts dialog ─────────────────────────────────────
            if (_dialog == DialogMode.KeyboardShortcuts)
            {
                var kvp = igGetMainViewport();
                var kCenter = new Vector2(kvp->Pos.X + kvp->Size.X * 0.5f,
                                         kvp->Pos.Y + kvp->Size.Y * 0.5f);
                igSetNextWindowPos(kCenter, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
                igSetNextWindowSize(new Vector2(500, 0), ImGuiCond.Always);
                byte kbOpen = 1;
                bool kbShowing = igBegin("Keyboard Shortcuts##kbs_dlg", ref kbOpen,
                    ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoDocking  | ImGuiWindowFlags.NoSavedSettings);

                if (kbOpen == 0)
                {
                    KeyBindings.SaveToFile();
                    _recordingAction = null;
                    _dialog = DialogMode.None;
                    igEnd();
                    return;
                }

                if (kbShowing)
                {
                    if (igBeginTable("##kbt", 3,
                        ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
                        ImGuiTableFlags.SizingStretchProp,
                        Vector2.Zero, 0))
                    {
                        igTableSetupColumn("Action",   ImGuiTableColumnFlags.WidthStretch, 0.45f, 0);
                        igTableSetupColumn("Shortcut", ImGuiTableColumnFlags.WidthStretch, 0.40f, 0);
                        igTableSetupColumn("Reset",    ImGuiTableColumnFlags.WidthFixed,   56f,   0);
                        igTableHeadersRow();

                        foreach (var action in KeyBindings.Actions)
                        {
                            igTableNextRow(ImGuiTableRowFlags.None, 0);
                            igTableSetColumnIndex(0);
                            igText(action);

                            igTableSetColumnIndex(1);
                            bool isRec = _recordingAction == action;
                            if (isRec)
                            {
                                igTextColored(new Vector4(1f, 0.85f, 0.2f, 1f), "Press key... (Esc=cancel)");
                                igSetNextFrameWantCaptureKeyboard(true);
                                ImGuiKey hit = ScanForPressedKey();
                                if (hit == ImGuiKey.Escape)
                                {
                                    _recordingAction = null;
                                }
                                else if (hit != ImGuiKey.None)
                                {
                                    unsafe
                                    {
                                        var io = igGetIO_Nil();
                                        bool c = (io->KeyMods & ImGuiKey.ImGuiMod_Ctrl)  != 0;
                                        bool s = (io->KeyMods & ImGuiKey.ImGuiMod_Shift) != 0;
                                        bool a = (io->KeyMods & ImGuiKey.ImGuiMod_Alt)   != 0;
                                        KeyBindings.Set(action, new KeyChord { Key = hit, Ctrl = c, Shift = s, Alt = a });
                                    }
                                    _recordingAction = null;
                                    KeyBindings.SaveToFile();
                                }
                            }
                            else
                            {
                                string chordStr = KeyBindings.Get(action).ToString();
                                if (igSelectable_Bool(chordStr + "##kbs_" + action, false,
                                    ImGuiSelectableFlags.None, Vector2.Zero))
                                    _recordingAction = action;
                                if (igIsItemHovered(ImGuiHoveredFlags.None))
                                    igSetTooltip("Click to rebind");
                            }

                            igTableSetColumnIndex(2);
                            if (igSmallButton("Reset##r_" + action))
                            {
                                KeyBindings.Reset(action);
                                KeyBindings.SaveToFile();
                            }
                        }
                        igEndTable();
                    }

                    igSpacing();
                    if (igButton("Reset All##kbsAll", new Vector2(90, 0)))
                    {
                        KeyBindings.ResetAll();
                        KeyBindings.SaveToFile();
                    }
                    igSameLine(0, 10);
                    if (igButton("Close##kbsClose", new Vector2(80, 0)))
                    {
                        KeyBindings.SaveToFile();
                        _recordingAction = null;
                        _dialog = DialogMode.None;
                    }
                }
                igEnd();
                return;
            }

        // ── Preferences dialog ──────────────────────────────────────────
        if (_dialog == DialogMode.Preferences)
        {
            var pvp = igGetMainViewport();
            var pCenter = new Vector2(pvp->Pos.X + pvp->Size.X * 0.5f,
                                      pvp->Pos.Y + pvp->Size.Y * 0.5f);
            igSetNextWindowPos(pCenter, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            igSetNextWindowSize(new Vector2(340, 0), ImGuiCond.Always);
            byte prefOpen = 1;
            bool prefShowing = igBegin("Preferences##pref_dlg", ref prefOpen,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize |
                ImGuiWindowFlags.NoDocking  | ImGuiWindowFlags.NoSavedSettings);

            if (prefOpen == 0) { _dialog = DialogMode.None; igEnd(); return; }

            if (prefShowing)
            {
                // ── Appearance ──────────────────────────────────────────────
                igText("Appearance");
                igSeparator();
                igSpacing();

                int themeIdx = System.Array.IndexOf(EditorTheme.Names, EditorTheme.Current);
                if (themeIdx < 0) themeIdx = 0;
                igSetNextItemWidth(160f);
                if (igBeginCombo("GUI Theme##pref_theme", EditorTheme.Names[themeIdx], ImGuiComboFlags.None))
                {
                    for (int i = 0; i < EditorTheme.Names.Length; i++)
                    {
                        bool sel = i == themeIdx;
                        if (igSelectable_Bool(EditorTheme.Names[i] + "##th" + i, sel,
                                ImGuiSelectableFlags.None, Vector2.Zero))
                            EditorTheme.Apply(EditorTheme.Names[i]);
                        if (sel) igSetItemDefaultFocus();
                    }
                    igEndCombo();
                }

                igSpacing();

                // ── Font Sizes ───────────────────────────────────────────────
                igText("Font Sizes");
                igSeparator();
                igSpacing();

                float uiSize   = EditorFonts.UiFontSize;
                float codeSize = EditorFonts.CodeFontSize;

                igSetNextItemWidth(160f);
                if (igSliderFloat("UI Font Size##pref_ui", ref uiSize, 8f, 32f, "%.0f px", ImGuiSliderFlags.None))
                {
                    EditorFonts.UiFontSize   = uiSize;
                    EditorFonts.RebuildRequested = true;
                }
                if (igIsItemHovered(ImGuiHoveredFlags.None))
                    igSetTooltip("Also: Ctrl + / Ctrl -  in the viewport");

                igSetNextItemWidth(160f);
                if (igSliderFloat("Code Font Size##pref_code", ref codeSize, 8f, 32f, "%.0f px", ImGuiSliderFlags.None))
                {
                    EditorFonts.CodeFontSize = codeSize;
                    EditorFonts.RebuildRequested = true;
                }

                igSpacing();
                if (igButton("Reset##pref_rst", new Vector2(80, 0)))
                {
                    EditorFonts.UiFontSize   = 14f;
                    EditorFonts.CodeFontSize = 14f;
                    EditorFonts.RebuildRequested = true;
                }
                igSameLine(0, 10);
                if (igButton("Close##pref_close", new Vector2(80, 0)))
                {
                    EditorPersistence.Save();
                    _dialog = DialogMode.None;
                }
            }
            igEnd();
            return;
        }

        // ── Window-based dialogs (NewProject, ProjectSettings) ──────────────
        var vp = igGetMainViewport();
        var center = new Vector2(vp->Pos.X + vp->Size.X * 0.5f,
                                 vp->Pos.Y + vp->Size.Y * 0.5f);

        igSetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        igSetNextWindowSize(Vector2.Zero, ImGuiCond.Always);

        string title = _dialog switch
        {
            DialogMode.NewProject      => "New Project",
            DialogMode.ProjectSettings => "Project Settings",
            _                          => "Dialog"
        };

        byte dlgOpen = 1;
        bool showing = igBegin(title + "##Editor_dlg", ref dlgOpen,
            ImGuiWindowFlags.NoCollapse       |
            ImGuiWindowFlags.AlwaysAutoResize |
            ImGuiWindowFlags.NoDocking        |
            ImGuiWindowFlags.NoSavedSettings);

        if (dlgOpen == 0) { _dialog = DialogMode.None; igEnd(); return; }

        if (showing)
        {
            switch (_dialog)
            {
                    case DialogMode.NewProject:
                    {
                        igText("Name:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(280f);
                        igInputText("##npName", ref _newProjectNameBuf[0], (uint)_newProjectNameBuf.Length,
                            ImGuiInputTextFlags.None, null, null);
                        igText("Destination:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(250f);
                        igInputText("##npDest", ref _dialogPathBuf[0], (uint)_dialogPathBuf.Length,
                            ImGuiInputTextFlags.None, null, null);
                        igSameLine(0, 4);
                        if (igButton("...##npBrowse", new Vector2(28, 0)))
                        {
                            string cur = GetStringFromBuffer(_dialogPathBuf);
                            ImFileDialog.OpenDialog("ifd_np_dest", "Select Destination Folder",
                                null, cur, "", ImFileDialog.Mode.SelectFolder);
                        }
                        igSpacing();
                        if (_isCreating)
                        {
                            igTextColored(new Vector4(0.3f, 1f, 0.3f, 1f), "Creating project...");
                            igSameLine(0, 8);
                            if (igButton("Cancel##npc", new Vector2(70, 0)))
                            {
                                try { _createProcess?.Kill(entireProcessTree: true); } catch { }
                                Logger.Warning("Project creation cancelled.");
                                _isCreating = false;
                                _dialog = DialogMode.None;
                            }
                        }
                        else
                        {
                            if (igButton("Create##np", new Vector2(80, 0)))
                            {
                                string name = GetStringFromBuffer(_newProjectNameBuf);
                                string dest = GetStringFromBuffer(_dialogPathBuf);
                                if (name.Length > 0 && dest.Length > 0)
                                    StartCreateProject(name, dest);
                            }
                            igSameLine(0, 8);
                            if (igButton("Cancel##npcancel", new Vector2(80, 0)))
                                _dialog = DialogMode.None;
                        }
                        break;
                    }
                    case DialogMode.ProjectSettings:
                    {
                        igText("Project Name:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(260f);
                        igInputText("##psName", ref _cfgNameBuf[0], (uint)_cfgNameBuf.Length,
                            ImGuiInputTextFlags.None, null, null);

                        // Default Scene dropdown
                        igText("Default Scene:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(260f);
                        string scenePreview = _psSceneFiles.Length > 0 ? _psSceneFiles[_psSceneIdx] : "(no scenes found)";
                        if (igBeginCombo("##psScene", scenePreview, ImGuiComboFlags.None))
                        {
                            for (int i = 0; i < _psSceneFiles.Length; i++)
                            {
                                bool scSel = i == _psSceneIdx;
                                if (igSelectable_Bool(_psSceneFiles[i] + "##sc" + i, scSel,
                                        ImGuiSelectableFlags.None, Vector2.Zero))
                                {
                                    if (_psSceneIdx != i)
                                    {
                                        _psSceneIdx    = i;
                                        _psCameraNames = DiscoverCameraNames(_psSceneFiles[_psSceneIdx]);
                                        _psCameraIdx   = 0;
                                    }
                                }
                                if (scSel) igSetItemDefaultFocus();
                            }
                            igEndCombo();
                        }

                        // Default Camera dropdown
                        igText("Default Camera:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(260f);
                        string camPreview = _psCameraNames.Length > 0 ? _psCameraNames[_psCameraIdx] : "(no cameras found)";
                        if (igBeginCombo("##psCam", camPreview, ImGuiComboFlags.None))
                        {
                            for (int i = 0; i < _psCameraNames.Length; i++)
                            {
                                bool camSel = i == _psCameraIdx;
                                if (igSelectable_Bool(_psCameraNames[i] + "##cam" + i, camSel,
                                        ImGuiSelectableFlags.None, Vector2.Zero))
                                    _psCameraIdx = i;
                                if (camSel) igSetItemDefaultFocus();
                            }
                            igEndCombo();
                        }

                        igText("Screen Width:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(100f);
                        igInputInt("##psW", ref _cfgWidth, 1, 10, ImGuiInputTextFlags.None);
                        igText("Screen Height:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(100f);
                        igInputInt("##psH", ref _cfgHeight, 1, 10, ImGuiInputTextFlags.None);

                        // Physics 3D dropdown
                        igText("Physics 3D:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(100f);
                        if (igBeginCombo("##psP3", _psPhys3DOptions[_psPhys3DIdx], ImGuiComboFlags.None))
                        {
                            for (int i = 0; i < _psPhys3DOptions.Length; i++)
                            {
                                bool p3Sel = i == _psPhys3DIdx;
                                if (igSelectable_Bool(_psPhys3DOptions[i] + "##p3" + i, p3Sel,
                                        ImGuiSelectableFlags.None, Vector2.Zero))
                                    _psPhys3DIdx = i;
                                if (p3Sel) igSetItemDefaultFocus();
                            }
                            igEndCombo();
                        }

                        // Physics 2D dropdown
                        igText("Physics 2D:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(100f);
                        if (igBeginCombo("##psP2", _psPhys2DOptions[_psPhys2DIdx], ImGuiComboFlags.None))
                        {
                            for (int i = 0; i < _psPhys2DOptions.Length; i++)
                            {
                                bool p2Sel = i == _psPhys2DIdx;
                                if (igSelectable_Bool(_psPhys2DOptions[i] + "##p2" + i, p2Sel,
                                        ImGuiSelectableFlags.None, Vector2.Zero))
                                    _psPhys2DIdx = i;
                                if (p2Sel) igSetItemDefaultFocus();
                            }
                            igEndCombo();
                        }

                        igSpacing();
                        if (igButton("Save##psSave", new Vector2(80, 0)))
                        {
                            var cfg = ConfigManager.Config;
                            if (cfg != null)
                            {
                                cfg.ProjectName   = GetStringFromBuffer(_cfgNameBuf);
                                cfg.DefaultScene  = _psSceneFiles.Length  > 0 ? _psSceneFiles[_psSceneIdx]   : "";
                                cfg.DefaultCamera = _psCameraNames.Length > 0 ? _psCameraNames[_psCameraIdx] : "";
                                cfg.ScreenWidth   = _cfgWidth;
                                cfg.ScreenHeight  = _cfgHeight;
                                cfg.Physics3D     = _psPhys3DOptions[_psPhys3DIdx];
                                cfg.Physics2D     = _psPhys2DOptions[_psPhys2DIdx];
                                ConfigManager.Save();
                            }
                            _dialog = DialogMode.None;
                        }
                        igSameLine(0, 8);
                        if (igButton("Cancel##psCancel", new Vector2(80, 0)))
                            _dialog = DialogMode.None;
                        break;
                    }
                }
            }

            igEnd();
        }

        private static void StartCreateProject(string name, string dest)
        {
            string builderPath = ConfigManager.GetSokolAppBuilderPath();
            string args = $"run --project \"{builderPath}\" -- --task createproject "
                        + $"--project \"{name}\" --destination \"{dest}\"";
            Logger.Info($"[Create Project] dotnet {args}");

            var psi = new ProcessStartInfo("dotnet", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            _createProcess = Process.Start(psi);
            if (_createProcess == null)
            {
                Logger.Error("Failed to start project creation process.");
                return;
            }

            _isCreating = true;
            _createProjectName  = name;
            _createResultFolder = System.IO.Path.Combine(dest, name);

            _createProcess.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) _createOutput.Enqueue((false, e.Data));
            };
            _createProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) _createOutput.Enqueue((true, e.Data));
            };
            _createProcess.BeginOutputReadLine();
            _createProcess.BeginErrorReadLine();
        }

        private static ImGuiKey ScanForPressedKey()
        {
            // Scan the keys that are valid as primary bindings (not modifiers)
            ReadOnlySpan<ImGuiKey> keys = [
                ImGuiKey.A,  ImGuiKey.B,  ImGuiKey.C,  ImGuiKey.D,  ImGuiKey.E,
                ImGuiKey.F,  ImGuiKey.G,  ImGuiKey.H,  ImGuiKey.I,  ImGuiKey.J,
                ImGuiKey.K,  ImGuiKey.L,  ImGuiKey.M,  ImGuiKey.N,  ImGuiKey.O,
                ImGuiKey.P,  ImGuiKey.Q,  ImGuiKey.R,  ImGuiKey.S,  ImGuiKey.T,
                ImGuiKey.U,  ImGuiKey.V,  ImGuiKey.W,  ImGuiKey.X,  ImGuiKey.Y, ImGuiKey.Z,
                ImGuiKey._0, ImGuiKey._1, ImGuiKey._2, ImGuiKey._3, ImGuiKey._4,
                ImGuiKey._5, ImGuiKey._6, ImGuiKey._7, ImGuiKey._8, ImGuiKey._9,
                ImGuiKey.F1,  ImGuiKey.F2,  ImGuiKey.F3,  ImGuiKey.F4,
                ImGuiKey.F5,  ImGuiKey.F6,  ImGuiKey.F7,  ImGuiKey.F8,
                ImGuiKey.F9,  ImGuiKey.F10, ImGuiKey.F11, ImGuiKey.F12,
                ImGuiKey.Space, ImGuiKey.Enter, ImGuiKey.Escape,
                ImGuiKey.Tab, ImGuiKey.Backspace, ImGuiKey.Delete,
                ImGuiKey.UpArrow, ImGuiKey.DownArrow,
                ImGuiKey.LeftArrow, ImGuiKey.RightArrow,
                ImGuiKey.Home, ImGuiKey.End, ImGuiKey.PageUp, ImGuiKey.PageDown,
                ImGuiKey.Slash, ImGuiKey.Period, ImGuiKey.Comma, ImGuiKey.Semicolon,
                ImGuiKey.Equal, ImGuiKey.Minus, ImGuiKey.Apostrophe,
                ImGuiKey.LeftBracket, ImGuiKey.RightBracket,
                ImGuiKey.Backslash, ImGuiKey.GraveAccent,
            ];
            foreach (var k in keys)
                if (igIsKeyPressed_Bool(k, false)) return k;
            return ImGuiKey.None;
        }

        private static string GetStringFromBuffer(byte[] buf)
        {
            int len = System.Array.IndexOf(buf, (byte)0);
            return len > 0 ? System.Text.Encoding.UTF8.GetString(buf, 0, len) : string.Empty;
        }

        /// <summary>Discovers all *.scene.json files relative to the project folder.</summary>
        private static string[] DiscoverSceneFiles()
        {
            string? folder = ConfigManager.ProjectFolder;
            if (folder == null) return System.Array.Empty<string>();
            try
            {
                string assetsDir = System.IO.Path.Combine(folder, "Assets");
                string searchDir = System.IO.Directory.Exists(assetsDir) ? assetsDir : folder;
                var files = System.IO.Directory.GetFiles(searchDir, "*.scene.json",
                    System.IO.SearchOption.AllDirectories);
                var result = new string[files.Length];
                for (int i = 0; i < files.Length; i++)
                    result[i] = System.IO.Path.GetRelativePath(folder, files[i]).Replace('\\', '/');
                System.Array.Sort(result);
                return result;
            }
            catch { return System.Array.Empty<string>(); }
        }

        /// <summary>Parses a scene JSON file and returns the names of entities with a Camera component.</summary>
        private static string[] DiscoverCameraNames(string? relativeScenePath)
        {
            string? folder = ConfigManager.ProjectFolder;
            if (folder == null || string.IsNullOrEmpty(relativeScenePath))
                return System.Array.Empty<string>();
            string fullPath = System.IO.Path.IsPathRooted(relativeScenePath)
                ? relativeScenePath
                : System.IO.Path.Combine(folder, relativeScenePath);
            if (!System.IO.File.Exists(fullPath)) return System.Array.Empty<string>();
            try
            {
                string json = System.IO.File.ReadAllText(fullPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("entities", out var entities))
                    return System.Array.Empty<string>();
                var names = new System.Collections.Generic.List<string>();
                foreach (var entity in entities.EnumerateArray())
                {
                    if (!entity.TryGetProperty("c", out var c)) continue;
                    if (!c.TryGetProperty("Camera", out _)) continue;
                    string name = "Camera";
                    if (c.TryGetProperty("NameTag", out var nt) && nt.TryGetProperty("Name", out var nm))
                        name = nm.GetString() ?? "Camera";
                    names.Add(name);
                }
                return names.ToArray();
            }
            catch { return System.Array.Empty<string>(); }
        }

        /// <summary>
        /// After a project is loaded, load its default scene (if any) into the editor.
        /// </summary>
        private static void LoadProjectDefaultScene()
        {
            var cfg = ConfigManager.Config;
            var folder = ConfigManager.ProjectFolder;
            if (cfg == null || folder == null) return;

            string? scenePath = cfg.DefaultScene;
            if (string.IsNullOrWhiteSpace(scenePath)) return;

            // DefaultScene may be relative to the project folder or absolute
            string fullPath = System.IO.Path.IsPathRooted(scenePath)
                ? scenePath
                : System.IO.Path.Combine(folder, scenePath);

            if (System.IO.File.Exists(fullPath))
            {
                Logger.Info($"[Project] Loading default scene: {fullPath}");
                SceneManager.LoadScene(fullPath);
            }
            else
            {
                Logger.Warning($"[Project] Default scene not found: {fullPath}");
            }
        }
    }
}
