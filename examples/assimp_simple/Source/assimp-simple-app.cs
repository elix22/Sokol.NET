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
using static assimp_simple_app_shader_cs.Shaders;
using Assimp;

public static unsafe class AssimpSimpleApp
{
    private const int NUM_FONTS = 3;
    private const int FONT_KC854 = 0;
    private const int FONT_C64 = 1;
    private const int FONT_ORIC = 2;
    struct color_t
    {
        public byte r, g, b;
    };

    static readonly Random random = new Random();
    public static float NextRandom(float min, float max) { return (float)((random.NextDouble() * (max - min)) + min); }


    class _state
    {
        public sg_pass_action pass_action;
        public color_t[] palette = new color_t[NUM_FONTS];

        public sg_pipeline pip;
        public Sokol.Camera camera = new Sokol.Camera();

        public SimpleModel? m_simpleModel;
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
            Distance = 5.0f,
        });

        state.palette[FONT_KC854] = new color_t { r = 0xf4, g = 0x43, b = 0x36 };
        state.palette[FONT_C64] = new color_t { r = 0x21, g = 0x96, b = 0xf3 };
        state.palette[FONT_ORIC] = new color_t { r = 0x4c, g = 0xaf, b = 0x50 };

        sdtx_desc_t desc = default;
        desc.fonts[FONT_KC854] = sdtx_font_kc854();
        desc.fonts[FONT_C64] = sdtx_font_c64();
        desc.fonts[FONT_ORIC] = sdtx_font_oric();
        sdtx_setup(desc);

        // Initialize FileSystem for file loading
        FileSystem.Instance.Initialize();

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };

        sg_shader shd = sg_make_shader(assimp_shader_desc(sg_query_backend()));

        var pipeline_desc = default(sg_pipeline_desc);
        pipeline_desc.layout.attrs[ATTR_assimp_position].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_assimp_color0].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.layout.attrs[ATTR_assimp_texcoord0].format = SG_VERTEXFORMAT_FLOAT2;

        pipeline_desc.shader = shd;
        pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        pipeline_desc.cull_mode = SG_CULLMODE_BACK;
        pipeline_desc.depth.write_enabled = true;
        pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pipeline_desc.label = "assimp-simple-pipeline";

        state.pip = sg_make_pipeline(pipeline_desc);

        // Use FileSystem to load the model file
        string filePath = util_get_file_path("vampire/dancing_vampire.glb");
        state.m_simpleModel = new SimpleModel(filePath);


        Console.WriteLine($"Assimp: Requested file load for: {filePath}");

    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // Update FileSystem to process pending requests
        FileSystem.Instance.Update();

        state.camera.Update(sapp_width(), sapp_height()); vs_params_t vs_params = default;
        vs_params.mvp = Matrix4x4.Identity * state.camera.ViewProj;

        sdtx_canvas(sapp_width() * 0.5f, sapp_height() * 0.5f);
        sdtx_origin(3.0f, 3.0f);
        color_t color = state.palette[0];
        sdtx_color3b(color.r, color.g, color.b);
        sdtx_font((uint)0);
        sdtx_print("Assimp Simple App\n");
        sdtx_print($"Camera Position: {state.camera.Center}\n");
        sdtx_print($"Camera Distance: {state.camera.Distance}\n");
        sdtx_print($"Camera Orientation: {state.camera.Latitude}, {state.camera.Longitude}\n");

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pip);


        foreach (var simpleMesh in state.m_simpleModel.SimpleMeshes)
        {
            sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
            simpleMesh.Draw();
        }


        sdtx_draw();
        sg_end_pass();
        sg_commit();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        state.camera.HandleEvent(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        FileSystem.Instance.Shutdown();
        sdtx_shutdown();
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
            window_title = "Assimp Simple (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
