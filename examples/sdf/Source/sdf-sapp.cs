
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using static Sokol.SApp;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_load_action;
using static sdf_sapp_shader_cs.Shaders;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;

public static unsafe class SdfApp
{

    static bool PauseUpdate = false;

    struct State
    {
        public sg_pipeline pip;
        public sg_bindings bind;
        public sg_pass_action pass_action;
        public vs_params_t vs_params;
    }

    static State state = default;

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 0,  // Let iOS determine the size based on orientation
            height = 0, // Let iOS determine the size based on orientation
            sample_count = 4,
            window_title = "Sdf (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = new sg_logger()
        });

        // a vertex buffer to render a 'fullscreen triangle'
        float[] fsq_verts = { -1.0f, -3.0f, 3.0f, 1.0f, -1.0f, 1.0f };
        state.bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(fsq_verts),
            label = "fsq vertices"
        });

        // shader and pipeline object for rendering a fullscreen quad

        sg_pipeline_desc desc = default;
        desc.layout.attrs[ATTR_sdf_position].format = SG_VERTEXFORMAT_FLOAT2;
        desc.shader = sg_make_shader(sdf_shader_desc(sg_query_backend()));
        state.pip = sg_make_pipeline(desc);

        state.pass_action = default;
        state.pass_action.colors[0].load_action = SG_LOADACTION_DONTCARE;

    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        if (PauseUpdate) return;

        int w = sapp_width();
        int h = sapp_height();
        state.vs_params.time += (float)sapp_frame_duration();
        state.vs_params.aspect = (float)w / (float)h;
        sg_begin_pass(new sg_pass() { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pip);
        sg_apply_bindings(state.bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref state.vs_params));
        sg_draw(0, 3, 1);
        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        sg_shutdown();

        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(SApp.sapp_event* e)
    {
        if (e->type == SApp.sapp_event_type.SAPP_EVENTTYPE_KEY_UP)
        {
            PauseUpdate = !PauseUpdate;
        }
    }

}