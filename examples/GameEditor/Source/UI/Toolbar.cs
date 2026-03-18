using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Imgui;
using static Imgui.ImguiNative;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Core;
using GameEditor;

namespace GameEditor.UI
{
    public static unsafe class Toolbar
    {
        private enum DialogMode { None, SaveAs, Open, NewProject, OpenProject, ProjectSettings }
        private static DialogMode _dialog = DialogMode.None;
        private static byte[] _dialogPathBuf    = new byte[512];
        private static byte[] _newProjectNameBuf = new byte[128];

        // Project Settings edit buffers
        private static byte[] _cfgNameBuf   = new byte[128];
        private static byte[] _cfgSceneBuf  = new byte[256];
        private static byte[] _cfgCamBuf    = new byte[128];
        private static int    _cfgWidth     = 1280;
        private static int    _cfgHeight    = 720;
        private static byte[] _cfgPhys3DBuf = new byte[32];
        private static byte[] _cfgPhys2DBuf = new byte[32];

        // New-project background process
        private static Process? _createProcess = null;
        private static bool     _isCreating    = false;
        private static string?  _createResultFolder = null;
        private static string   _createProjectName  = "";
        private static readonly ConcurrentQueue<(bool isErr, string msg)> _createOutput = new();

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
                    // Keep the assembly loaded (warm cache) — Unload() only on project close
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
            if (igButton("T##gT", new Vector2(26, 0))) EditorState.CurrentGizmoOp = ImGuizmo.Operation.Translate;
            if (isT) igPopStyleColor(1);
            if (igIsItemHovered(ImGuiHoveredFlags.None)) igSetTooltip("Translate (W)");

            igSameLine(0, 2);
            if (isR) igPushStyleColor_Vec4(ImGuiCol.Button, activeCol);
            if (igButton("R##gR", new Vector2(26, 0))) EditorState.CurrentGizmoOp = ImGuizmo.Operation.Rotate;
            if (isR) igPopStyleColor(1);
            if (igIsItemHovered(ImGuiHoveredFlags.None)) igSetTooltip("Rotate (E)");

            igSameLine(0, 2);
            if (isSc) igPushStyleColor_Vec4(ImGuiCol.Button, activeCol);
            if (igButton("S##gS", new Vector2(26, 0))) EditorState.CurrentGizmoOp = ImGuizmo.Operation.Scale;
            if (isSc) igPopStyleColor(1);
            if (igIsItemHovered(ImGuiHoveredFlags.None)) igSetTooltip("Scale (R)");

            igSameLine(0, 8);
            bool isLocal = EditorState.CurrentGizmoMode == ImGuizmo.Mode.Local;
            if (isLocal) igPushStyleColor_Vec4(ImGuiCol.Button, activeCol);
            if (igButton(isLocal ? "Local" : "World", new Vector2(50, 0)))
                EditorState.CurrentGizmoMode = isLocal ? ImGuizmo.Mode.World : ImGuizmo.Mode.Local;
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
                DrawOverlayToggle(ref ov, GizmoOverlayFlags.Grid,            "Grid");
                DrawOverlayToggle(ref ov, GizmoOverlayFlags.WorldAxes,       "World Axes");
                DrawOverlayToggle(ref ov, GizmoOverlayFlags.OrientationCube, "Orientation Cube");
                DrawOverlayToggle(ref ov, GizmoOverlayFlags.EntityGizmos,    "Entity Gizmos");
                DrawOverlayToggle(ref ov, GizmoOverlayFlags.CameraGizmos,    "Camera Gizmos");
                DrawOverlayToggle(ref ov, GizmoOverlayFlags.LightGizmos,     "Light Gizmos");
                EditorState.Overlays = ov;
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
                    FillBuffer(ref _cfgNameBuf,   cfg.ProjectName);
                    FillBuffer(ref _cfgSceneBuf,  cfg.DefaultScene);
                    FillBuffer(ref _cfgCamBuf,    cfg.DefaultCamera);
                    _cfgWidth  = cfg.ScreenWidth;
                    _cfgHeight = cfg.ScreenHeight;
                    FillBuffer(ref _cfgPhys3DBuf, cfg.Physics3D);
                    FillBuffer(ref _cfgPhys2DBuf, cfg.Physics2D);
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

            // ── Window-based dialogs (NewProject, ProjectSettings) ────────────
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
                        igText("Default Scene:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(260f);
                        igInputText("##psScene", ref _cfgSceneBuf[0], (uint)_cfgSceneBuf.Length,
                            ImGuiInputTextFlags.None, null, null);
                        igText("Default Camera:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(260f);
                        igInputText("##psCam", ref _cfgCamBuf[0], (uint)_cfgCamBuf.Length,
                            ImGuiInputTextFlags.None, null, null);
                        igText("Screen Width:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(100f);
                        igInputInt("##psW", ref _cfgWidth, 1, 10, ImGuiInputTextFlags.None);
                        igText("Screen Height:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(100f);
                        igInputInt("##psH", ref _cfgHeight, 1, 10, ImGuiInputTextFlags.None);
                        igText("Physics 3D:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(100f);
                        igInputText("##psP3", ref _cfgPhys3DBuf[0], (uint)_cfgPhys3DBuf.Length,
                            ImGuiInputTextFlags.None, null, null);
                        igText("Physics 2D:");
                        igSameLine(0, 6);
                        igSetNextItemWidth(100f);
                        igInputText("##psP2", ref _cfgPhys2DBuf[0], (uint)_cfgPhys2DBuf.Length,
                            ImGuiInputTextFlags.None, null, null);
                        igSpacing();
                        if (igButton("Save##psSave", new Vector2(80, 0)))
                        {
                            var cfg = ConfigManager.Config;
                            if (cfg != null)
                            {
                                cfg.ProjectName   = GetStringFromBuffer(_cfgNameBuf);
                                cfg.DefaultScene  = GetStringFromBuffer(_cfgSceneBuf);
                                cfg.DefaultCamera = GetStringFromBuffer(_cfgCamBuf);
                                cfg.ScreenWidth   = _cfgWidth;
                                cfg.ScreenHeight  = _cfgHeight;
                                cfg.Physics3D     = GetStringFromBuffer(_cfgPhys3DBuf);
                                cfg.Physics2D     = GetStringFromBuffer(_cfgPhys2DBuf);
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

        private static string GetStringFromBuffer(byte[] buf)
        {
            int len = System.Array.IndexOf(buf, (byte)0);
            return len > 0 ? System.Text.Encoding.UTF8.GetString(buf, 0, len) : string.Empty;
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
