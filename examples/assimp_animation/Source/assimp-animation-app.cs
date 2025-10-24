using System;
using System.IO;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using Assimp.Configs;
using static Sokol.SFetch;
using static Sokol.SDebugText;
using static assimp_animation_app_shader_cs.Shaders;
using Assimp;
using static Sokol.SImgui;
using Imgui;
using static Imgui.ImguiNative;

public static unsafe class AssimpAnimationApp
{
    static string modelPath = "DancingGangster.glb";

    class _state
    {
        public sg_pass_action pass_action;

        public sg_pipeline pip;
        public Sokol.Camera camera = new Sokol.Camera();

        public Sokol.Model? model;
        public Sokol.AnimationManager? animationManager;
        public Sokol.Animator? animator;

    }

    static _state state = new _state();


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        Console.WriteLine("Assimp: Init()");
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = {
                func = &slog_func,
            }
        });

        // duck center = new Vector3(0.0f, 150, 0.0f);
        // Distance = 400.0f,
        state.camera.Init(new CameraDesc()
        {
            Aspect = 60.0f,
            NearZ = 0.01f,
            FarZ = 500.0f,
            Center = new Vector3(0.0f, 1.1f, 0.0f),
            Distance = 3.0f,
        });

        // Setup sokol-imgui
        simgui_setup(new simgui_desc_t
        {
            logger = { func = &slog_func }
        });

        // Initialize FileSystem for file loading
        FileSystem.Instance.Initialize();

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };

        sg_shader shd = sg_make_shader(assimp_shader_desc(sg_query_backend()));

        var pipeline_desc = default(sg_pipeline_desc);
        // pipeline_desc.layout.buffers[0].stride = (17 * sizeof(float)); // 3 pos + 4 color + 2 texcoord + 4 boneIds + 4 weights
        pipeline_desc.layout.attrs[ATTR_assimp_position].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_assimp_color0].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.layout.attrs[ATTR_assimp_texcoord0].format = SG_VERTEXFORMAT_FLOAT2;
        pipeline_desc.layout.attrs[ATTR_assimp_boneIds].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.layout.attrs[ATTR_assimp_weights].format = SG_VERTEXFORMAT_FLOAT4;

        pipeline_desc.shader = shd;
        pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        pipeline_desc.cull_mode = SG_CULLMODE_BACK;
        pipeline_desc.depth.write_enabled = true;
        pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pipeline_desc.label = "assimp-simple-pipeline";

        state.pip = sg_make_pipeline(pipeline_desc);

        // Use FileSystem to load the model file
        string filePath = util_get_file_path(modelPath);
        state.model = new Sokol.Model(filePath);

        // Wait for model to load, then initialize animation
        // Animation will be created in Frame() once model is loaded


        Console.WriteLine($"Assimp: Requested file load for: {filePath}");

    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // Update FileSystem to process pending requests
        FileSystem.Instance.Update();

        // Start new imgui frame
        simgui_new_frame(new simgui_frame_desc_t
        {
            width = sapp_width(),
            height = sapp_height(),
            delta_time = sapp_frame_duration(),
            dpi_scale = 1 // TBD ELI doesn't work on Android sapp_dpi_scale()
        });

        // Initialize animation manager once model is loaded
        if (state.model != null &&
            state.model.SimpleMeshes != null &&
            state.model.SimpleMeshes.Count > 0 &&
            state.animationManager == null)
        {
            state.animationManager = new Sokol.AnimationManager();
            string animPath = util_get_file_path(modelPath);
            state.animationManager.LoadAnimation(animPath, state.model);
            Console.WriteLine("Animation loading started...");
        }

        // Initialize animator once animation is loaded
        if (state.animationManager != null &&
            state.animationManager.GetAnimationCount() > 0 &&
            state.animator == null)
        {
            var animation = state.animationManager.GetFirstAnimation();
            if (animation != null)
            {
                state.animator = new Sokol.Animator(animation);
                Console.WriteLine("Animator initialized successfully");
            }
        }

        // Update animator
        if (state.animator != null)
        {
            state.animator.UpdateAnimation((float)sapp_frame_duration());
        }

        state.camera.Update(sapp_width(), sapp_height());

        // Prepare uniform data
        vs_params_t vs_params = new vs_params_t();
        vs_params.projection = state.camera.Proj;
        vs_params.view = state.camera.View;
        vs_params.model = Matrix4x4.Identity;

        // Get bone matrices from animator
        if (state.animator != null)
        {
            // Optimized bulk copy - both arrays are exactly AnimationConstants.MAX_BONES elements
            var boneMatrices = state.animator.GetFinalBoneMatrices();
            var destSpan = MemoryMarshal.CreateSpan(ref vs_params.finalBonesMatrices[0], AnimationConstants.MAX_BONES);
            boneMatrices.AsSpan().CopyTo(destSpan);
        }
        else
        {
            // Initialize with identity matrices - optimized bulk initialization
            var bonesSpan = MemoryMarshal.CreateSpan(ref vs_params.finalBonesMatrices[0], AnimationConstants.MAX_BONES);
            bonesSpan.Fill(Matrix4x4.Identity);
        }

        // Draw UI
        DrawUI();

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pip);


        // Pass the entire vs_params struct directly - no intermediate copies needed!
        // The finalBonesMatricesCollection is already part of the struct layout
        sg_apply_uniforms(UB_vs_params, SG_RANGE(ref vs_params));

        foreach (var simpleMesh in state.model.SimpleMeshes)
        {
            simpleMesh.Draw();
        }

        simgui_render();
        sg_end_pass();
        sg_commit();
    }

    static void DrawUI()
    {
        igSetNextWindowPos(new Vector2(30, 30), ImGuiCond.Once, Vector2.Zero);
        igSetNextWindowBgAlpha(0.85f);
        byte open = 1;
        if (igBegin("Animation Controls", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            igText("Assimp Animation App");
            igSeparator();

            if (state.animationManager != null && state.animationManager.GetAnimationCount() > 0)
            {
                int animCount = state.animationManager.GetAnimationCount();
                string? currentAnimName = state.animationManager.GetCurrentAnimationName();
                int currentIndex = state.animationManager.GetCurrentAnimationIndex();

                igText($"Current Animation: {currentAnimName ?? "None"} (Index: {currentIndex})");
                igText($"Total Animations: {animCount}");

                // Display animation timing info
                if (state.animator != null)
                {
                    var currentAnim = state.animator.GetCurrentAnimation();
                    if (currentAnim != null)
                    {
                        float duration = currentAnim.GetDuration();
                        float currentTime = state.animator.GetCurrentTime();
                        float ticksPerSecond = currentAnim.GetTicksPerSecond();

                        // Convert to seconds for display
                        float durationInSeconds = duration / ticksPerSecond;
                        float currentTimeInSeconds = currentTime / ticksPerSecond;

                        igText($"Duration: {durationInSeconds:F2}s");
                        igText($"Current Time: {currentTimeInSeconds:F2}s");
                        igText($"Progress: {(currentTime / duration * 100):F1}%%");
                    }
                }

                igSeparator();

                if (animCount > 1)
                {
                    if (igButton("<- Previous Animation", Vector2.Zero))
                    {
                        var prevAnimation = state.animationManager.GetPreviousAnimation();
                        if (prevAnimation != null && state.animator != null)
                        {
                            state.animator.SetAnimation(prevAnimation);
                            string? animName = state.animationManager.GetCurrentAnimationName();
                            Console.WriteLine($"Switched to animation: {animName}");
                        }
                    }

                    igSameLine(0, 10);

                    if (igButton("Next Animation ->", Vector2.Zero))
                    {
                        var nextAnimation = state.animationManager.GetNextAnimation();
                        if (nextAnimation != null && state.animator != null)
                        {
                            state.animator.SetAnimation(nextAnimation);
                            string? animName = state.animationManager.GetCurrentAnimationName();
                            Console.WriteLine($"Switched to animation: {animName}");
                        }
                    }
                }
                else
                {
                    igText("Only one animation available");
                }
            }
            else
            {
                igText("Loading animations...");
            }

            igSeparator();
            igText($"Camera Distance: {state.camera.Distance:F2}");
            igText($"Camera Latitude: {state.camera.Latitude:F2}");
            igText($"Camera Longitude: {state.camera.Longitude:F2}");
        }
        igEnd();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        if (simgui_handle_event(in *e))
            return;
        state.camera.HandleEvent(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        FileSystem.Instance.Shutdown();
        simgui_shutdown();
        sg_shutdown();

        // Force a complete shutdown if debugging
        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 0,
            height = 0,
            sample_count = 4,
            window_title = "Assimp Animation (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
