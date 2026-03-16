using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Imgui;
using static Imgui.ImguiNative;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Core;

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

            // Play/Pause/Stop buttons centred
            var state = SceneManager.PlayMode;
            igSetCursorPosX((igGetWindowWidth() - 110f) * 0.5f);

            bool playing = state == PlayModeState.Playing;
            bool paused  = state == PlayModeState.Paused;
            bool stopped = state == PlayModeState.Stopped;

            if (stopped || paused)
            {
                if (igButton(stopped ? "Play" : "Resume", new Vector2(60, 0)))
                    SceneManager.Play();
            }
            else
            {
                igBeginDisabled(false);
                igButton("Play", new Vector2(60, 0));
                igEndDisabled();
            }

            igSameLine(0, 4);

            if (playing)
            {
                if (igButton("||", new Vector2(30, 0)))
                    SceneManager.Pause();
            }

            if (playing || paused)
            {
                igSameLine(0, 4);
                if (igButton("Stop", new Vector2(50, 0)))
                    SceneManager.Stop();
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
            if (igButton("Overlays \u25bc", new Vector2(80, 0)))
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

            string popupId = mode switch
            {
                DialogMode.SaveAs          => "##SaveAs",
                DialogMode.Open            => "##OpenScene",
                DialogMode.NewProject      => "##NewProject",
                DialogMode.OpenProject     => "##OpenProject",
                DialogMode.ProjectSettings => "##ProjSettings",
                _                          => "##Dialog"
            };

            if (mode == DialogMode.SaveAs)
            {
                string initial = initialPath.EndsWith(".scene.json") ? initialPath : initialPath + ".scene.json";
                FillBuffer(ref _dialogPathBuf, initial);
            }
            else if (mode == DialogMode.NewProject)
            {
                _newProjectNameBuf = new byte[128];
                // _dialogPathBuf holds destination path — starts empty
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
            else
            {
                FillBuffer(ref _dialogPathBuf, initialPath);
            }

            igOpenPopup_Str(popupId, ImGuiPopupFlags.None);
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
                    ConfigManager.Load(_createResultFolder);
                    _createResultFolder = null;
                    igCloseCurrentPopup();
                }
            }

            byte unused = 1;

            // --- Save As ---
            if (igBeginPopupModal("##SaveAs", ref unused, ImGuiWindowFlags.AlwaysAutoResize))
            {
                igText("Save Scene As");
                igSeparator();
                igText("Path:");
                igSameLine(0, 6);
                igSetNextItemWidth(400f);
                igInputText("##savepath", ref _dialogPathBuf[0], (uint)_dialogPathBuf.Length,
                    ImGuiInputTextFlags.None, null, null);

                igSpacing();
                if (igButton("Save", new Vector2(80, 0)))
                {
                    string path = GetStringFromBuffer(_dialogPathBuf);
                    if (path.Length > 0)
                    {
                        SceneManager.SaveScene(path);
                        igCloseCurrentPopup();
                        _dialog = DialogMode.None;
                    }
                }
                igSameLine(0, 8);
                if (igButton("Cancel", new Vector2(80, 0)))
                {
                    igCloseCurrentPopup();
                    _dialog = DialogMode.None;
                }
                igEndPopup();
            }

            // --- Open ---
            if (igBeginPopupModal("##OpenScene", ref unused, ImGuiWindowFlags.AlwaysAutoResize))
            {
                igText("Open Scene");
                igSeparator();
                igText("Path:");
                igSameLine(0, 6);
                igSetNextItemWidth(400f);
                igInputText("##openpath", ref _dialogPathBuf[0], (uint)_dialogPathBuf.Length,
                    ImGuiInputTextFlags.None, null, null);

                igSpacing();
                if (igButton("Open", new Vector2(80, 0)))
                {
                    string path = GetStringFromBuffer(_dialogPathBuf);
                    if (path.Length > 0)
                    {
                        SceneManager.LoadScene(path);
                        igCloseCurrentPopup();
                        _dialog = DialogMode.None;
                    }
                }
                igSameLine(0, 8);
                if (igButton("Cancel", new Vector2(80, 0)))
                {
                    igCloseCurrentPopup();
                    _dialog = DialogMode.None;
                }
                igEndPopup();
            }

            // --- New Project ---
            if (igBeginPopupModal("##NewProject", ref unused, ImGuiWindowFlags.AlwaysAutoResize))
            {
                igText("New Project");
                igSeparator();

                igText("Name:");
                igSameLine(0, 6);
                igSetNextItemWidth(280f);
                igInputText("##npName", ref _newProjectNameBuf[0], (uint)_newProjectNameBuf.Length,
                    ImGuiInputTextFlags.None, null, null);

                igText("Destination:");
                igSameLine(0, 6);
                igSetNextItemWidth(280f);
                igInputText("##npDest", ref _dialogPathBuf[0], (uint)_dialogPathBuf.Length,
                    ImGuiInputTextFlags.None, null, null);

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
                    {
                        igCloseCurrentPopup();
                        _dialog = DialogMode.None;
                    }
                }

                igEndPopup();
            }

            // --- Open Project ---
            if (igBeginPopupModal("##OpenProject", ref unused, ImGuiWindowFlags.AlwaysAutoResize))
            {
                igText("Open Project");
                igSeparator();
                igText("Project folder (contains config.json):");
                igSetNextItemWidth(400f);
                igInputText("##opPath", ref _dialogPathBuf[0], (uint)_dialogPathBuf.Length,
                    ImGuiInputTextFlags.None, null, null);

                igSpacing();
                if (igButton("Open##op", new Vector2(80, 0)))
                {
                    string path = GetStringFromBuffer(_dialogPathBuf);
                    if (path.Length > 0)
                    {
                        ConfigManager.Load(path);
                        igCloseCurrentPopup();
                        _dialog = DialogMode.None;
                    }
                }
                igSameLine(0, 8);
                if (igButton("Cancel##opcancel", new Vector2(80, 0)))
                {
                    igCloseCurrentPopup();
                    _dialog = DialogMode.None;
                }
                igEndPopup();
            }

            // --- Project Settings ---
            if (igBeginPopupModal("##ProjSettings", ref unused, ImGuiWindowFlags.AlwaysAutoResize))
            {
                igText("Project Settings");
                igSeparator();

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
                    igCloseCurrentPopup();
                    _dialog = DialogMode.None;
                }
                igSameLine(0, 8);
                if (igButton("Cancel##psCancel", new Vector2(80, 0)))
                {
                    igCloseCurrentPopup();
                    _dialog = DialogMode.None;
                }
                igEndPopup();
            }
        }

        private static void StartCreateProject(string name, string dest)
        {
            string builderPath = ConfigManager.GetSokolAppBuilderPath();
            string args = $"run --project \"{builderPath}\" -- --task create "
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
    }
}
