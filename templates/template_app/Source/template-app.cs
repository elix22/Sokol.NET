using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using Sokol;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SLog;
using GameEditor.Framework.Core;
using GameEditor.Framework.Scripting;

// ── Sample GameBehaviour — replace / add your own scripts here ───────────────

/// <summary>
/// Example behaviour: rotates the entity around the Y axis.
/// Register it in TemplateApp.Init() with ScriptSystem.RegisterType&lt;RotateScript&gt;().
/// Then add a ScriptComponent(TypeName = "RotateScript") to any entity in the scene.
/// </summary>
public sealed class RotateScript : GameBehaviour
{
    public float Speed = 90f; // degrees per second

    public override void OnStart()
    {
        Logger.Info($"[RotateScript] Started on entity {EntityId}");
    }

    public override void OnUpdate(float deltaTime)
    {
        ref var tr = ref Transform;
        tr.EulerAngles = new System.Numerics.Vector3(
            tr.EulerAngles.X,
            tr.EulerAngles.Y + Speed * deltaTime,
            tr.EulerAngles.Z);
    }

    public override void OnDestroy()
    {
        Logger.Info($"[RotateScript] Destroyed on entity {EntityId}");
    }
}

// ── Main application ─────────────────────────────────────────────────────────

public static unsafe class TemplateApp
{
    struct _state
    {
        public sg_pass_action pass_action;
    }

    static _state state = new _state();

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger      = { func = &slog_func }
        });

        // Initialise the cross-platform async file system (sokol_fetch)
        GameFileSystem.Instance.Initialize();

        // ── Register your GameBehaviour types here ────────────────────────
        // Each type name must match the TypeName stored in the ScriptComponent.
        ScriptSystem.RegisterType<RotateScript>();
        // ScriptSystem.RegisterType<MyOtherScript>();

        // Load config.json + default scene, wire Logger to console output
        GameApplication.Init();

        // Start play mode: populates ScriptSystem from scene, calls OnStart()
        GameApplication.StartPlay();

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.1f, g = 0.12f, b = 0.15f, a = 1.0f };
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // Pump the async file system (required on all platforms, especially WebAssembly)
        GameFileSystem.Instance.Update();

        // Tick all running game scripts
        GameApplication.Update((float)sapp_frame_duration());

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });

        // TODO: add your rendering code here

        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        // Handle input
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        GameApplication.Cleanup();
        GameFileSystem.Instance.Shutdown();
        sg_shutdown();

        if (Debugger.IsAttached)
            Environment.Exit(0);
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb    = &Init,
            frame_cb   = &Frame,
            event_cb   = &Event,
            cleanup_cb = &Cleanup,
            width      = 0,
            height     = 0,
            sample_count = 4,
            window_title = "Game (Sokol.NET)",
            icon         = { sokol_default = true },
            logger       = { func = &slog_func }
        };
    }
}

