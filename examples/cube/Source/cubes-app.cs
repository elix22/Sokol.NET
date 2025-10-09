using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static cube_app_shader_cs.Shaders;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;

public static unsafe class CubeSapp
{
    struct _state
    {
        public float rx, ry;
        public sg_pipeline pip;
        public sg_bindings bind;
        public bool PauseUpdate;
    }

    static _state state = new _state();


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        Console.WriteLine("Initialize() Enter");

        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = {
                func = &slog_func,
            }
        });
        __dbgui_setup(sapp_sample_count());

        /* cube vertex buffer */
        float[] vertices =  {
            -1.0f, -1.0f, -1.0f,   1.0f, 0.0f, 0.0f, 1.0f,
            1.0f, -1.0f, -1.0f,   1.0f, 0.0f, 0.0f, 1.0f,
            1.0f,  1.0f, -1.0f,   1.0f, 0.0f, 0.0f, 1.0f,
            -1.0f,  1.0f, -1.0f,   1.0f, 0.0f, 0.0f, 1.0f,

            -1.0f, -1.0f,  1.0f,   0.0f, 1.0f, 0.0f, 1.0f,
            1.0f, -1.0f,  1.0f,   0.0f, 1.0f, 0.0f, 1.0f,
            1.0f,  1.0f,  1.0f,   0.0f, 1.0f, 0.0f, 1.0f,
            -1.0f,  1.0f,  1.0f,   0.0f, 1.0f, 0.0f, 1.0f,

            -1.0f, -1.0f, -1.0f,   0.0f, 0.0f, 1.0f, 1.0f,
            -1.0f,  1.0f, -1.0f,   0.0f, 0.0f, 1.0f, 1.0f,
            -1.0f,  1.0f,  1.0f,   0.0f, 0.0f, 1.0f, 1.0f,
            -1.0f, -1.0f,  1.0f,   0.0f, 0.0f, 1.0f, 1.0f,

            1.0f, -1.0f, -1.0f,   1.0f, 0.5f, 0.0f, 1.0f,
            1.0f,  1.0f, -1.0f,   1.0f, 0.5f, 0.0f, 1.0f,
            1.0f,  1.0f,  1.0f,   1.0f, 0.5f, 0.0f, 1.0f,
            1.0f, -1.0f,  1.0f,   1.0f, 0.5f, 0.0f, 1.0f,

            -1.0f, -1.0f, -1.0f,   0.0f, 0.5f, 1.0f, 1.0f,
            -1.0f, -1.0f,  1.0f,   0.0f, 0.5f, 1.0f, 1.0f,
            1.0f, -1.0f,  1.0f,   0.0f, 0.5f, 1.0f, 1.0f,
            1.0f, -1.0f, -1.0f,   0.0f, 0.5f, 1.0f, 1.0f,

            -1.0f,  1.0f, -1.0f,   1.0f, 0.0f, 0.5f, 1.0f,
            -1.0f,  1.0f,  1.0f,   1.0f, 0.0f, 0.5f, 1.0f,
            1.0f,  1.0f,  1.0f,   1.0f, 0.0f, 0.5f, 1.0f,
            1.0f,  1.0f, -1.0f,   1.0f, 0.0f, 0.5f, 1.0f
        };

        sg_buffer vbuf;
        fixed (float* ptr_vertices = vertices)
        {
            vbuf = sg_make_buffer(new sg_buffer_desc()
            {
                data = SG_RANGE(vertices),
                label = "cube-vertices"
            }
            );
        }

        UInt16[] indices = {
                0, 1, 2,  0, 2, 3,
                6, 5, 4,  7, 6, 4,
                8, 9, 10,  8, 10, 11,
                14, 13, 12,  15, 14, 12,
                16, 17, 18,  16, 18, 19,
                22, 21, 20,  23, 22, 20
            };

        sg_buffer ibuf;
        fixed (UInt16* ptr_indices = indices)
        {
            ibuf = sg_make_buffer(new sg_buffer_desc()
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(indices),
                label = "cube-indices"
            }
            );
        }


        sg_shader shd = sg_make_shader(cube_app_shader_cs.Shaders.cube_shader_desc(sg_query_backend()));

        var pipeline_desc = default(sg_pipeline_desc);
        pipeline_desc.layout.buffers[0].stride = 28;
        pipeline_desc.layout.attrs[ATTR_cube_position].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_cube_color0].format = SG_VERTEXFORMAT_FLOAT4;

        pipeline_desc.shader = shd;
        pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        pipeline_desc.cull_mode = SG_CULLMODE_BACK;
        pipeline_desc.depth.write_enabled = true;
        pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pipeline_desc.label = "cube-pipeline";

        state.pip = sg_make_pipeline(pipeline_desc);

        state.bind = new sg_bindings();
        state.bind.vertex_buffers[0] = vbuf;
        state.bind.index_buffer = ibuf;

    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        if (state.PauseUpdate)
        {
            return;
        }

        vs_params_t vs_params = default;

        float deltaSeconds = (float)(Sokol.SApp.sapp_frame_duration());

        state.rx += 1.0f * deltaSeconds;
        state.ry += 2.0f * deltaSeconds;
        var rotationMatrixX = Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, state.rx);
        var rotationMatrixY = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, state.ry);
        var modelMatrix = rotationMatrixX * rotationMatrixY;

        
        var width = SApp.sapp_widthf();
        var height = SApp.sapp_heightf();

        var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
            (float)(60.0f * Math.PI / 180),
            width / height,
            0.01f,
            10.0f);
        var viewMatrix = Matrix4x4.CreateLookAt(
            new Vector3(0.0f, 1.5f, 6.0f),
            Vector3.Zero,
            Vector3.UnitY);

        vs_params.mvp = modelMatrix * viewMatrix * projectionMatrix;

        sg_pass pass = default;
        pass.action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        pass.action.colors[0].clear_value =new  float[4] {  0.25f, 0.5f, 0.75f,  1.0f };
        pass.swapchain = sglue_swapchain();
        sg_begin_pass(pass);

        sg_apply_pipeline(state.pip);
        sg_apply_bindings(state.bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_draw(0, 36, 1);
        __dbgui_draw();
        sg_end_pass();
        sg_commit();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_KEY_UP)
        {
            state.PauseUpdate = !state.PauseUpdate;
        }

        __dbgui_event(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        __dbgui_shutdown();
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
            width = 0,  // Let iOS determine the size based on orientation
            height = 0, // Let iOS determine the size based on orientation
            sample_count = 4,
            window_title = "Cube (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
