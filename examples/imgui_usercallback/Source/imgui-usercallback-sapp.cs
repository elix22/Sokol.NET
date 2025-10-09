
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static System.Numerics.Matrix4x4;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SGL;
using static Sokol.SImgui;
using static Sokol.Utils;
using static Sokol.SApp;
using static Sokol.SShape;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_filter;
using static Sokol.SG.sg_wrap;
using static Sokol.SG.sg_load_action;
using static Sokol.SG.sg_vertex_step;
using System.Diagnostics;
using static imgui_usercallback_sapp_shader_cs.Shaders;

using static Imgui.ImguiNative;
using Imgui;

using static Sokol.SLog;

public static unsafe class ImguiUserCallbackApp
{

    static bool PauseUpdate = false;

    // global application state
    private static State state;

    private struct State
    {
        public sg_pass_action default_pass_action;
        public Scene1 scene1;
        public Scene2 scene2;
    }

    private struct Scene1
    {
        public float rx, ry;
        public sg_pass_action pass_action;
        public sg_pipeline pip;
        public sg_bindings bind;
    }

    private struct Scene2
    {
        public float r0, r1;
        public sgl_pipeline pip;
    }


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = {
                func = &slog_func,
            }
        });

        // vertices and indices for rendering a cube via sokol-gfx
        float[] cube_vertices = {
            -1.0F, -1.0F, -1.0F,   1.0F, 0.0F, 0.0F, 1.0F,
            1.0F, -1.0F, -1.0F,   1.0F, 0.0F, 0.0F, 1.0F,
            1.0F,  1.0F, -1.0F,   1.0F, 0.0F, 0.0F, 1.0F,
            -1.0F,  1.0F, -1.0F,   1.0F, 0.0F, 0.0F, 1.0F,

            -1.0F, -1.0F,  1.0F,   0.0F, 1.0F, 0.0F, 1.0F,
            1.0F, -1.0F,  1.0F,   0.0F, 1.0F, 0.0F, 1.0F,
            1.0F,  1.0F,  1.0F,   0.0F, 1.0F, 0.0F, 1.0F,
            -1.0F,  1.0F,  1.0F,   0.0F, 1.0F, 0.0F, 1.0F,

            -1.0F, -1.0F, -1.0F,   0.0F, 0.0F, 1.0F, 1.0F,
            -1.0F,  1.0F, -1.0F,   0.0F, 0.0F, 1.0F, 1.0F,
            -1.0F,  1.0F,  1.0F,   0.0F, 0.0F, 1.0F, 1.0F,
            -1.0F, -1.0F,  1.0F,   0.0F, 0.0F, 1.0F, 1.0F,

            1.0F, -1.0F, -1.0F,    1.0F, 0.5F, 0.0F, 1.0F,
            1.0F,  1.0F, -1.0F,    1.0F, 0.5F, 0.0F, 1.0F,
            1.0F,  1.0F,  1.0F,    1.0F, 0.5F, 0.0F, 1.0F,
            1.0F, -1.0F,  1.0F,    1.0F, 0.5F, 0.0F, 1.0F,

            -1.0F, -1.0F, -1.0F,   0.0F, 0.5F, 1.0F, 1.0F,
            -1.0F, -1.0F,  1.0F,   0.0F, 0.5F, 1.0F, 1.0F,
            1.0F, -1.0F,  1.0F,   0.0F, 0.5F, 1.0F, 1.0F,
            1.0F, -1.0F, -1.0F,   0.0F, 0.5F, 1.0F, 1.0F,

            -1.0F,  1.0F, -1.0F,   1.0F, 0.0F, 0.5F, 1.0F,
            -1.0F,  1.0F,  1.0F,   1.0F, 0.0F, 0.5F, 1.0F,
            1.0F,  1.0F,  1.0F,   1.0F, 0.0F, 0.5F, 1.0F,
            1.0F,  1.0F, -1.0F,   1.0F, 0.0F, 0.5F, 1.0F
        };

        ushort[] cube_indices = {
            0, 1, 2,  0, 2, 3,
            6, 5, 4,  7, 6, 4,
            8, 9, 10,  8, 10, 11,
            14, 13, 12,  15, 14, 12,
            16, 17, 18,  16, 18, 19,
            22, 21, 20,  23, 22, 20
        };

        simgui_setup(new simgui_desc_t
        {
           logger = {
                func = &slog_func,
            }
        });
        sgl_setup(new sgl_desc_t
        {
            logger = {
                func = &slog_func,
            }
        });

        state.default_pass_action = default;
        state.default_pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.default_pass_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.5f, b = 0.7f, a = 1.0f };

        // setup the sokol-gfx resources needed for the first user draw callback
        state.scene1.bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc
        {
            data = SG_RANGE(cube_vertices),
            label = "cube-vertices"
        });

        state.scene1.bind.index_buffer = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE(cube_indices),
            label = "cube-indices"
        });

        sg_pipeline_desc sg_Pipeline_Desc = default;
        sg_Pipeline_Desc.layout.attrs[0].format = SG_VERTEXFORMAT_FLOAT3;
        sg_Pipeline_Desc.layout.attrs[1].format = SG_VERTEXFORMAT_FLOAT4;
        sg_Pipeline_Desc.shader = sg_make_shader(scene_shader_desc(sg_query_backend()));
        sg_Pipeline_Desc.index_type = SG_INDEXTYPE_UINT16;
        sg_Pipeline_Desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        sg_Pipeline_Desc.depth.write_enabled = true;
        sg_Pipeline_Desc.cull_mode = SG_CULLMODE_BACK;
        sg_Pipeline_Desc.label = "cube-pipeline";

        state.scene1.pip = sg_make_pipeline(sg_Pipeline_Desc);

        // setup a sokol-gl pipeline needed for the second user draw callback
        sg_Pipeline_Desc = default;
        sg_Pipeline_Desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        sg_Pipeline_Desc.depth.write_enabled = true;
        sg_Pipeline_Desc.cull_mode = SG_CULLMODE_BACK;

        state.scene2.pip = sgl_make_pipeline(sg_Pipeline_Desc);


    }

    [UnmanagedCallersOnly(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    static unsafe void draw_scene_1(ImDrawList* dl, ImDrawCmd* cmd)
    {
        // first set the viewport rectangle to render in, same as
        // the ImGui draw command's clip rect
        int cx = (int)cmd->ClipRect.X;
        int cy = (int)cmd->ClipRect.Y;
        int cw = (int)(cmd->ClipRect.Z - cmd->ClipRect.X);
        int ch = (int)(cmd->ClipRect.W - cmd->ClipRect.Y);
        sg_apply_scissor_rect(cx, cy, cw, ch, true);
        sg_apply_viewport(cx, cy, 360, 360, true);

        // a model-view-proj matrix for the vertex shader
        float t = (float)(sapp_frame_duration());
        vs_params_t vs_params;



        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, 1.0f, 0.01f, 10.0f);
        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0.0f, 1.5f, 6.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f));
        Matrix4x4 view_proj = view * proj;
        state.scene1.rx += 1.0f * t; state.scene1.ry += 2.0f * t;
        Matrix4x4 rxm = Matrix4x4.CreateRotationX(state.scene1.rx);
        Matrix4x4 rym = Matrix4x4.CreateRotationY(state.scene1.ry);
        Matrix4x4 model = rxm * rym;
        vs_params.mvp = model * view_proj;

        /*
            NOTE: we cannot start a separate render pass here since passes cannot
            be nested, so if we'd need to clear the color- or z-buffer we'd need to
            render a quad instead

            Another option is to render into a texture render target outside the
            ImGui user callback, and render this texture as quad inside the
            callback (or as a standard Image widget). This allows to perform rendering
            inside an (offscreen) render pass, and clear the background as usual.
        */
        sg_apply_pipeline(state.scene1.pip);
        sg_apply_bindings(state.scene1.bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_draw(0, 36, 1);

    }

    // helper function to draw a cube via sokol-gl
    static void cube_sgl()
    {
        sgl_begin_quads();
        sgl_c3f(1.0f, 0.0f, 0.0f);
        sgl_v3f_t2f(-1.0f, 1.0f, -1.0f, -1.0f, 1.0f);
        sgl_v3f_t2f(1.0f, 1.0f, -1.0f, 1.0f, 1.0f);
        sgl_v3f_t2f(1.0f, -1.0f, -1.0f, 1.0f, -1.0f);
        sgl_v3f_t2f(-1.0f, -1.0f, -1.0f, -1.0f, -1.0f);
        sgl_c3f(0.0f, 1.0f, 0.0f);
        sgl_v3f_t2f(-1.0f, -1.0f, 1.0f, -1.0f, 1.0f);
        sgl_v3f_t2f(1.0f, -1.0f, 1.0f, 1.0f, 1.0f);
        sgl_v3f_t2f(1.0f, 1.0f, 1.0f, 1.0f, -1.0f);
        sgl_v3f_t2f(-1.0f, 1.0f, 1.0f, -1.0f, -1.0f);
        sgl_c3f(0.0f, 0.0f, 1.0f);
        sgl_v3f_t2f(-1.0f, -1.0f, 1.0f, -1.0f, 1.0f);
        sgl_v3f_t2f(-1.0f, 1.0f, 1.0f, 1.0f, 1.0f);
        sgl_v3f_t2f(-1.0f, 1.0f, -1.0f, 1.0f, -1.0f);
        sgl_v3f_t2f(-1.0f, -1.0f, -1.0f, -1.0f, -1.0f);
        sgl_c3f(1.0f, 0.5f, 0.0f);
        sgl_v3f_t2f(1.0f, -1.0f, 1.0f, -1.0f, 1.0f);
        sgl_v3f_t2f(1.0f, -1.0f, -1.0f, 1.0f, 1.0f);
        sgl_v3f_t2f(1.0f, 1.0f, -1.0f, 1.0f, -1.0f);
        sgl_v3f_t2f(1.0f, 1.0f, 1.0f, -1.0f, -1.0f);
        sgl_c3f(0.0f, 0.5f, 1.0f);
        sgl_v3f_t2f(1.0f, -1.0f, -1.0f, -1.0f, 1.0f);
        sgl_v3f_t2f(1.0f, -1.0f, 1.0f, 1.0f, 1.0f);
        sgl_v3f_t2f(-1.0f, -1.0f, 1.0f, 1.0f, -1.0f);
        sgl_v3f_t2f(-1.0f, -1.0f, -1.0f, -1.0f, -1.0f);
        sgl_c3f(1.0f, 0.0f, 0.5f);
        sgl_v3f_t2f(-1.0f, 1.0f, -1.0f, -1.0f, 1.0f);
        sgl_v3f_t2f(-1.0f, 1.0f, 1.0f, 1.0f, 1.0f);
        sgl_v3f_t2f(1.0f, 1.0f, 1.0f, 1.0f, -1.0f);
        sgl_v3f_t2f(1.0f, 1.0f, -1.0f, -1.0f, -1.0f);
        sgl_end();
    }

    // another ImGui draw callback to render via sokol-gl
    [UnmanagedCallersOnly(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    static unsafe void draw_scene_2(ImDrawList* dl, ImDrawCmd* cmd)
    {

        float t = (float)(sapp_frame_duration() * 60.0);

        int cx = (int)cmd->ClipRect.X;
        int cy = (int)cmd->ClipRect.Y;
        int cw = (int)(cmd->ClipRect.Z - cmd->ClipRect.X);
        int ch = (int)(cmd->ClipRect.W - cmd->ClipRect.Y);
        sgl_scissor_rect(cx, cy, cw, ch, true);
        sgl_viewport(cx, cy, 360, 360, true);

        state.scene2.r0 += 1.0f * t;
        state.scene2.r1 += 2.0f * t;

        sgl_defaults();
        sgl_load_pipeline(state.scene2.pip);

        sgl_matrix_mode_projection();
        sgl_perspective(sgl_as_radians(45.0f), 1.0f, 0.1f, 100.0f);

        sgl_matrix_mode_modelview();
        sgl_translate(0.0f, 0.0f, -12.0f);
        sgl_rotate(sgl_as_radians(state.scene2.r0), 1.0f, 0.0f, 0.0f);
        sgl_rotate(sgl_as_radians(state.scene2.r1), 0.0f, 1.0f, 0.0f);
        cube_sgl();
        sgl_push_matrix();
        sgl_translate(0.0f, 0.0f, 3.0f);
        sgl_scale(0.5f, 0.5f, 0.5f);
        sgl_rotate(-2.0f * sgl_as_radians(state.scene2.r0), 1.0f, 0.0f, 0.0f);
        sgl_rotate(-2.0f * sgl_as_radians(state.scene2.r1), 0.0f, 1.0f, 0.0f);
        cube_sgl();
        sgl_push_matrix();
        sgl_translate(0.0f, 0.0f, 3.0f);
        sgl_scale(0.5f, 0.5f, 0.5f);
        sgl_rotate(-3.0f * sgl_as_radians(2 * state.scene2.r0), 1.0f, 0.0f, 0.0f);
        sgl_rotate(3.0f * sgl_as_radians(2 * state.scene2.r1), 0.0f, 0.0f, 1.0f);
        cube_sgl();
        sgl_pop_matrix();
        sgl_pop_matrix();

        /*
            render the sokol-gl command list, this is the only call
            which actually needs to happen here in the callback,
            current downside is that only one such call must happen
            per frame
        */
        sgl_draw();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        if (PauseUpdate) return;

        // create the ImGui UI, a single window with two child views, each
        // rendering its own custom 3D scene via a user draw callback
        int w = sapp_width();
        int h = sapp_height();
        simgui_new_frame(new simgui_frame_desc_t
        {
            width = w,
            height = h,
            delta_time = sapp_frame_duration(),
            dpi_scale = 1
        });

        igSetNextWindowPos(new Vector2(20, 20), ImGuiCond.Once, Vector2.Zero);
        igSetNextWindowSize(new Vector2(800, 400), ImGuiCond.Once);
        byte show_another_window = 0;
        if (igBegin("Dear ImGui", ref show_another_window, 0))
        {
            if (igBeginChild_Str("sokol-gfx", new Vector2(360, 360), ImGuiChildFlags.Borders, ImGuiWindowFlags.None))
            {
                delegate* unmanaged[Cdecl]<ImDrawList*, ImDrawCmd*, void> drawScenePtr = &draw_scene_1;
                ImDrawList_AddCallback(igGetWindowDrawList(), (IntPtr)drawScenePtr, null, 0);
            }
            igEndChild();
            igSameLine(0, 10);
            if (igBeginChild_Str("sokol-gl", new Vector2(360, 360), ImGuiChildFlags.Borders, ImGuiWindowFlags.None))
            {
                delegate* unmanaged[Cdecl]<ImDrawList*, ImDrawCmd*, void> drawScenePtr = &draw_scene_2;
                ImDrawList_AddCallback(igGetWindowDrawList(), (IntPtr)drawScenePtr, null, 0);
            }
            igEndChild();
        }

        igEnd();

        // actual UI rendering, the user draw callbacks are called from inside simgui_render()
        sg_begin_pass(new sg_pass
        {
            action = state.default_pass_action,
            swapchain = sglue_swapchain()
        });
        simgui_render();
        sg_end_pass();
        sg_commit();

    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        sgl_shutdown();
        simgui_shutdown();
        sg_shutdown();

        // Force a complete shutdown if debugging
        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(SApp.sapp_event* ev)
    {
        simgui_handle_event(*ev);
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 860,
            height = 600,
            sample_count = 1,
            window_title = "imgui-usercallback",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}