using System; 
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SImgui;
using static Sokol.SLog;
using Imgui;
using static Imgui.ImguiNative;
using GameEditor.Framework.Core;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Renderer;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.UI;
using GameEditor;
using GameEditor.CodeEditor;
using System.Diagnostics;

public static unsafe class GameeditorApp
{
    struct _state
    {
        public sg_pass_action swapchain_pass_action;
    }

    static _state state = default;
    static SharedBuffer? _iconGlyphRange; // pinned buffer for FA glyph range — must outlive atlas build

    [UnmanagedCallersOnly]
    private static void Init()
    {
        sg_setup(new sg_desc
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });

        simgui_setup(new simgui_desc_t
        {
            no_default_font = true,   // we supply Roboto + FA as font 0 below
            logger = { func = &slog_func }
        });

        // Restore font size preferences before building the atlas.
        EditorPersistence.Load();

        // Load Roboto Regular (Apache 2.0 — free for commercial use) as the UI font.
        // Must be done after simgui_setup() and before the first simgui_new_frame().
        var io = igGetIO_Nil();
        LoadFonts(io, EditorFonts.UiFontSize, EditorFonts.CodeFontSize);

        io->ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        // Keep a visible tab even for single-window dock nodes so panels remain draggable.
        io->ConfigDockingAlwaysTabBar = 1;
        igStyleColorsDark(null);

        // Swapchain pass: don't clear (passthru dockspace covers it)
        state.swapchain_pass_action = default;
        state.swapchain_pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.swapchain_pass_action.colors[0].clear_value = new sg_color { r = 0.1f, g = 0.1f, b = 0.1f, a = 1.0f };

        // Init scene and editor
        SceneManager.NewScene("Untitled");
        SceneWindow.Init();
        GameWindow.Init();
        GridRenderer.Init();
        SceneRenderer.Init();

        _ = EditorState.SelectedEntity; // trigger static ctor

        // Restore last project + scene from previous session (EditorPersistence.Load already called above)
        EditorPersistence.RestoreSession();

        Logger.Info("GameEditor initialized.");
    }

    /// <summary>
    /// Clears the ImGui font atlas and re-adds Roboto+FA (UI) and JetBrains Mono (code)
    /// at the requested pixel sizes. Keeps _iconGlyphRange pinned so the pointer survives
    /// until simgui_new_frame rebuilds the atlas texture.
    /// </summary>
    private static void LoadFonts(Imgui.ImGuiIO* io, float uiSize, float codeSize)
    {
        // Clamp to sensible range
        uiSize   = Math.Clamp(uiSize,   8f, 48f);
        codeSize = Math.Clamp(codeSize, 8f, 48f);

        string fontsDir = System.IO.Path.Combine(System.AppContext.BaseDirectory, "fonts");
        string roboPath = System.IO.Path.Combine(fontsDir, "Roboto-Regular.ttf");
        if (System.IO.File.Exists(roboPath))
        {
            ushort rangeEnd = 0;
            ImFontAtlas_AddFontFromFileTTF(io->Fonts, roboPath, uiSize, null, ref rangeEnd);

            string iconPath = System.IO.Path.Combine(fontsDir, "fa-solid-900.ttf");
            if (System.IO.File.Exists(iconPath))
            {
                float iconAdv = uiSize + 2f;
                var cfg = new Imgui.ImFontConfig
                {
                    MergeMode          = 1,
                    PixelSnapH         = 1,
                    OversampleH        = 3,
                    OversampleV        = 1,
                    RasterizerMultiply = 1.0f,
                    RasterizerDensity  = 1.0f,
                    GlyphMinAdvanceX   = iconAdv,
                    GlyphMaxAdvanceX   = iconAdv,
                    GlyphOffset        = new Vector2(1f, 0f),
                };
                // Re-pin glyph range buffer — must stay alive until atlas is built.
                _iconGlyphRange?.Dispose();
                _iconGlyphRange = SharedBuffer.Create(6u);
                var rgBuf = _iconGlyphRange.Buffer;
                rgBuf[0] = 0x00; rgBuf[1] = 0xF0;
                rgBuf[2] = 0xFF; rgBuf[3] = 0xF8;
                rgBuf[4] = 0x00; rgBuf[5] = 0x00;
                ushort* iconRange = (ushort*)_iconGlyphRange.GetBufferPointer();
                cfg.GlyphRanges = iconRange;
                ImFontAtlas_AddFontFromFileTTF(io->Fonts, iconPath, uiSize, &cfg, ref *iconRange);
            }
        }

        string codeFontPath = System.IO.Path.Combine(fontsDir, "JetBrainsMono-Regular.ttf");
        if (System.IO.File.Exists(codeFontPath))
        {
            ushort rangeEnd = 0;
            EditorFonts.CodeFont = ImFontAtlas_AddFontFromFileTTF(
                io->Fonts, codeFontPath, codeSize, null, ref rangeEnd);
        }

        EditorFonts.UiFontSize   = uiSize;
        EditorFonts.CodeFontSize = codeSize;
    }

    [UnmanagedCallersOnly]
    private static void Frame()
    {
        Time.Update();

        // Tick script system when in play mode (scripts run before rendering)
        if (SceneManager.PlayMode == PlayModeState.Playing)
            GameEditor.Framework.Scripting.ScriptSystem.UpdateAll(Time.DeltaTime);

        int w = sapp_width();
        int h = sapp_height();

        // --- Offscreen pass: render 3D scene into SceneWindow target ---
        if (SceneWindow.Target.IsValid)
        {
            var scenePass = SceneWindow.Target.MakePass(
                new sg_color { r = 0.15f, g = 0.17f, b = 0.2f, a = 1.0f });
            sg_begin_pass(scenePass);
            if ((EditorState.Overlays & GizmoOverlayFlags.Grid) != 0)
                GridRenderer.Draw(SceneWindow.Camera.ViewProj, SceneWindow.Camera.EyePos, 0.1f, 1000f);
            SceneRenderer.DrawScene(SceneWindow.Camera.ViewProj);
            sg_end_pass();
        }

        // --- Offscreen pass: render selected camera entity preview ---
        {
            int selEnt = EditorState.SelectedEntity;
            if (selEnt >= 0 &&
                TryGetEntityCameraMatrices(selEnt, SceneWindow.PreviewW, SceneWindow.PreviewH,
                    out Matrix4x4 previewVP, out _) &&
                SceneWindow.CameraPreviewTarget.IsValid)
            {
                var previewPass = SceneWindow.CameraPreviewTarget.MakePass(
                    new sg_color { r = 0.05f, g = 0.07f, b = 0.1f, a = 1.0f });
                sg_begin_pass(previewPass);
                SceneRenderer.DrawScene(previewVP);
                sg_end_pass();
            }
        }

        // --- Offscreen pass: render game view from active Camera entity ---
        bool isPlayingOrPaused = SceneManager.PlayMode == PlayModeState.Playing ||
                                  SceneManager.PlayMode == PlayModeState.Paused;
        Matrix4x4 gameVP    = Matrix4x4.Identity;
        Vector3   gameEyePos = Vector3.Zero;
        bool hasCamera = isPlayingOrPaused &&
            TryGetMainCameraMatrices(GameWindow.ViewWidth, GameWindow.ViewHeight,
                out gameVP, out gameEyePos);

        // PrepareRenderTarget resizes the offscreen texture BEFORE we render into it,
        // eliminating the frame-delay / magenta-flash that occurred when Draw() resized it.
        GameWindow.PrepareRenderTarget(hasCamera);

        if (isPlayingOrPaused && GameWindow.Target.IsValid)
        {
            var gamePass = GameWindow.Target.MakePass(
                new sg_color { r = 0.05f, g = 0.05f, b = 0.1f, a = 1.0f });
            sg_begin_pass(gamePass);
            if (hasCamera)
            {
                SceneRenderer.DrawScene(gameVP);
            }
            sg_end_pass();
        }

        // --- Apply UI font size (ImGui 1.92: dynamic atlas — no rebuild needed, just set FontSizeBase) ---
        igGetStyle()->FontSizeBase = EditorFonts.UiFontSize;

        // --- ImGui frame on swapchain ---
        sg_begin_pass(new sg_pass
        {
            action = state.swapchain_pass_action,
            swapchain = sglue_swapchain()
        });

        simgui_new_frame(new simgui_frame_desc_t
        {
            width = w,
            height = h,
            delta_time = sapp_frame_duration(),
            dpi_scale = sapp_dpi_scale()
        });

        // ImGuizmo must be reset once per frame right after simgui_new_frame
        Imgui.ImGuizmo.BeginFrame();

        // Gizmo operation shortcuts — checked via ImGui key state (supports user rebinding)
        unsafe
        {
            var kio = igGetIO_Nil();
            if (kio->WantTextInput == 0 && SceneManager.PlayMode != PlayModeState.Playing)
            {
                if      (KeyBindings.IsPressed(KeyBindings.GizmoTranslate))
                    EditorState.CurrentGizmoOp = Imgui.ImGuizmo.Operation.Translate;
                else if (KeyBindings.IsPressed(KeyBindings.GizmoRotate))
                    EditorState.CurrentGizmoOp = Imgui.ImGuizmo.Operation.Rotate;
                else if (KeyBindings.IsPressed(KeyBindings.GizmoScale))
                    EditorState.CurrentGizmoOp = Imgui.ImGuizmo.Operation.Scale;

                // Ctrl+= (same physical key as +) and Ctrl+- — resize UI font
                if (kio->KeyCtrl != 0)
                {
                    bool plusPressed  = igIsKeyPressed_Bool(ImGuiKey.Equal, false) ||
                                       igIsKeyPressed_Bool(ImGuiKey.KeypadAdd, false);
                    bool minusPressed = igIsKeyPressed_Bool(ImGuiKey.Minus, false) ||
                                       igIsKeyPressed_Bool(ImGuiKey.KeypadSubtract, false);
                    if (plusPressed || minusPressed)
                    {
                        float newSize = EditorFonts.UiFontSize + (plusPressed ? 1f : -1f);
                        newSize = Math.Clamp(newSize, 8f, 48f);
                        if (newSize != EditorFonts.UiFontSize)
                        {
                            EditorFonts.UiFontSize   = newSize;
                            EditorFonts.RebuildRequested = true;
                            EditorPersistence.Save();
                        }
                    }
                }

                // F — frame / focus selected entity in scene window
                if (SceneWindow.IsWindowFocused &&
                    igIsKeyPressed_Bool(ImGuiKey.F, false) &&
                    EditorState.SelectionCount > 0)
                {
                    SceneWindow.FrameSelection();
                }
            }
        }

        // Draw full-window dockspace
        EditorLayout.BeginDockspace();

        // Draw editor panels
        Toolbar.Draw();
        HierarchyPanel.Draw();
        InspectorPanel.Draw();
        SceneWindow.Draw();
        GameWindow.Draw();
        ConsoleWindow.Draw();
        BuildDeployPanel.Draw();
        AssetsPanel.Draw();
        ScriptEditorWindow.Draw();
        ScriptEditorWindow.MarkDirtyIfChanged();

        simgui_render();
        sg_end_pass();

        sg_commit();
    }

    /// <summary>Builds the view/proj matrices for a specific camera entity.</summary>
    private static bool TryGetEntityCameraMatrices(int id, int width, int height,
        out Matrix4x4 viewProj, out Vector3 eyePos)
    {
        viewProj = Matrix4x4.Identity;
        eyePos   = Vector3.Zero;
        if (width <= 0 || height <= 0) return false;

        var world = ECSWorld.Instance;
        if (!world.TryGetComponent<CameraComponent>(id, out var cam)) return false;
        if (cam.NearZ <= 0f || cam.FarZ <= cam.NearZ) return false;
        if (!world.TryGetComponent<Transform>(id, out var tr)) return false;

        Matrix4x4 rotMat = Matrix4x4.CreateFromYawPitchRoll(
            tr.EulerAngles.Y * MathF.PI / 180f,
            tr.EulerAngles.X * MathF.PI / 180f,
            tr.EulerAngles.Z * MathF.PI / 180f);

        Vector3 forward = new Vector3(rotMat.M31, rotMat.M32, rotMat.M33);
        Vector3 up      = new Vector3(rotMat.M21, rotMat.M22, rotMat.M23);

        eyePos = tr.Position;
        Matrix4x4 view = Matrix4x4.CreateLookAt(eyePos, eyePos + forward, up);
        Matrix4x4 proj;
        if (cam.IsOrthographic)
        {
            float orthoH = MathF.Max(0.01f, cam.OrthoSize > 0f ? cam.OrthoSize : 5f);
            float orthoW = orthoH * ((float)width / height);
            proj = Matrix4x4.CreateOrthographicOffCenter(-orthoW, orthoW, -orthoH, orthoH, cam.NearZ, cam.FarZ);
        }
        else
        {
            float fov = MathF.Max(1f, cam.Fov) * MathF.PI / 180f;
            proj = Matrix4x4.CreatePerspectiveFieldOfView(fov, (float)width / height, cam.NearZ, cam.FarZ);
        }
        viewProj = view * proj;
        return true;
    }

    /// <summary>Finds the entity with IsMain camera and builds its view/proj matrices.</summary>
    private static bool TryGetMainCameraMatrices(int width, int height,
        out Matrix4x4 viewProj, out Vector3 eyePos)
    {
        viewProj = Matrix4x4.Identity;
        eyePos   = Vector3.Zero;
        if (width <= 0 || height <= 0) return false;

        var world = ECSWorld.Instance;
        foreach (int id in world.Entities)
        {
            if (!world.TryGetComponent<CameraComponent>(id, out var cam) || !cam.IsMain) continue;
            if (cam.NearZ <= 0f || cam.FarZ <= cam.NearZ) continue; // skip degenerate projection
            if (!world.TryGetComponent<Transform>(id, out var tr)) continue;

            Matrix4x4 rotMat = Matrix4x4.CreateFromYawPitchRoll(
                tr.EulerAngles.Y * MathF.PI / 180f,
                tr.EulerAngles.X * MathF.PI / 180f,
                tr.EulerAngles.Z * MathF.PI / 180f);

            // Use the rotation matrix's +Z column as forward (matches gizmo convention)
            Vector3 forward = new Vector3(rotMat.M31, rotMat.M32, rotMat.M33);
            Vector3 up      = new Vector3(rotMat.M21, rotMat.M22, rotMat.M23);

            eyePos = tr.Position;
            Matrix4x4 view = Matrix4x4.CreateLookAt(eyePos, eyePos + forward, up);
            Matrix4x4 proj;
            if (cam.IsOrthographic)
            {
                float orthoH = MathF.Max(0.01f, cam.OrthoSize > 0f ? cam.OrthoSize : 5f);
                float orthoW = orthoH * ((float)width / height);
                proj = Matrix4x4.CreateOrthographicOffCenter(-orthoW, orthoW, -orthoH, orthoH, cam.NearZ, cam.FarZ);
            }
            else
            {
                float fov = MathF.Max(1f, cam.Fov) * MathF.PI / 180f;
                proj = Matrix4x4.CreatePerspectiveFieldOfView(fov, (float)width / height, cam.NearZ, cam.FarZ);
            }
            viewProj = view * proj;
            return true;
        }
        return false;
    }

    [UnmanagedCallersOnly]
    private static void Event(sapp_event* e)    {
        // Let imgui handle events first; if it consumes, don't forward to viewport
        bool imguiWants = simgui_handle_event(*e);

        // Undo/Redo shortcuts
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN)
        {
            bool ctrl = (e->modifiers & SAPP_MODIFIER_CTRL) != 0;
            bool shift = (e->modifiers & SAPP_MODIFIER_SHIFT) != 0;
            bool super = (e->modifiers & SAPP_MODIFIER_SUPER) != 0;
            if ((ctrl || super) && e->key_code == sapp_keycode.SAPP_KEYCODE_S)
            {
                // Ctrl/Cmd+S — save the active scene (mirrors the File menu item)
                var activeScene = SceneManager.ActiveScene;
                if (activeScene != null)
                {
                    if (!string.IsNullOrWhiteSpace(activeScene.FilePath))
                        SceneManager.SaveScene(activeScene.FilePath!);
                    else
                        GameEditor.UI.Toolbar.TriggerSaveAs();
                }
            }
            else if (ctrl && e->key_code == sapp_keycode.SAPP_KEYCODE_Z)
            {
                if (shift) GameEditor.Framework.Core.UndoStack.Redo();
                else       GameEditor.Framework.Core.UndoStack.Undo();
            }
            else if (ctrl && e->key_code == sapp_keycode.SAPP_KEYCODE_Y)
            {
                GameEditor.Framework.Core.UndoStack.Redo();
            }
            else if ((ctrl || (e->modifiers & SAPP_MODIFIER_SUPER) != 0)
                     && e->key_code == sapp_keycode.SAPP_KEYCODE_D)
            {
                EditorState.DuplicateSelected();
            }
            // Gizmo operation shortcuts (no modifier) — skip when typing in any text field
            else if (!ctrl && !shift && igGetIO_Nil()->WantTextInput == 0)
            {
                if (e->key_code == sapp_keycode.SAPP_KEYCODE_DELETE
                         && EditorState.SelectionCount > 0
                         && SceneManager.ActiveScene != null)
                {
                    // Delete all selected entities
                    var toDelete = new System.Collections.Generic.List<int>(EditorState.SelectedEntities);
                    foreach (int did in toDelete)
                        SceneManager.ActiveScene.DestroyEntity(did);
                }
            }
        }

        if (!imguiWants)
            SceneWindow.HandleEvent(e);
        else
        {
            // Mouse and scroll events must reach the scene camera even when ImGui
            // "wants" them (the scene viewport is itself an ImGui image widget).
            // SceneWindow.HandleEvent checks viewport hover internally.
            var t = e->type;
            if (t == sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN  ||
                t == sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP    ||
                t == sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE  ||
                t == sapp_event_type.SAPP_EVENTTYPE_MOUSE_SCROLL)
            {
                SceneWindow.HandleEvent(e);
            }
        }
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        // Auto-save current scene on exit (Unity-like behavior).
        // If exiting while Playing, stop first so pre-play snapshot is restored.
        if (SceneManager.PlayMode != PlayModeState.Stopped)
            SceneManager.Stop();

        var active = SceneManager.ActiveScene;
        if (active != null && !string.IsNullOrWhiteSpace(active.FilePath))
        {
            try
            {
                SceneManager.SaveScene(active.FilePath!);
                Logger.Info($"[AutoSave] Scene saved on exit: {active.FilePath}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[AutoSave] Failed to save scene on exit: {ex.Message}");
            }
        }

        EditorPersistence.Save();
        SceneRenderer.Cleanup();
        GridRenderer.Cleanup();
        GameWindow.Cleanup();
        SceneWindow.Cleanup();
        simgui_shutdown();
        sg_shutdown();

        if (Debugger.IsAttached)
            Environment.Exit(0);
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc
        {
            init_cb     = &Init,
            frame_cb    = &Frame,
            event_cb    = &Event,
            cleanup_cb  = &Cleanup,
            width       = 1600,
            height      = 900,
            fullscreen   = false,
            sample_count = 1,
            window_title = "GameEditor",
            icon             = { sokol_default = true },
            logger           = { func = &slog_func },
            enable_clipboard = true,
            clipboard_size   = 1024 * 64
        };
    }
}
