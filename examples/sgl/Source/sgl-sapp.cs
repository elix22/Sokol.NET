
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static System.Numerics.Matrix4x4;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SGL;
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
public static unsafe class SglApp
{

    static bool PauseUpdate = false;

    struct _state
    {
        public sg_pass_action pass_action;
        public sg_view tex_view;
        public sg_sampler smp;
        public sgl_pipeline pip_3d;
    };

    static _state state = new _state();


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = new sg_logger()
        });

        // setup sokol-gl
        sgl_setup(new sgl_desc_t()
        {
            logger = new sgl_logger_t()
        });

        // a checkerboard texture
        uint[,] pixels = new uint[8, 8];
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                pixels[y, x] = (((y ^ x) & 1) != 0) ? 0xFFFFFFFF : 0xFF000000;
            }
        }

        sg_image_desc image_Desc = new sg_image_desc();
        image_Desc.width = 8;
        image_Desc.height = 8;
        image_Desc.data.mip_levels[0]  = SG_RANGE(pixels);
        sg_make_image(image_Desc);


        // Create a texture view from the checkerboard texture
        var imgDesc = new sg_image_desc()
        {
            width = 8,
            height = 8
        };
        imgDesc.data.mip_levels[0] = SG_RANGE(pixels);

        var texViewDesc = new sg_view_desc()
        {
            texture = new sg_texture_view_desc()
            {
                image = sg_make_image(imgDesc)
            }
        };

        state.tex_view = sg_make_view(texViewDesc);


        state.smp = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_NEAREST,
            mag_filter = SG_FILTER_NEAREST,
        });

        state.pip_3d = sgl_make_pipeline(new sg_pipeline_desc()
        {
            cull_mode = SG_CULLMODE_BACK,
            depth = {
            write_enabled = true,
            compare = SG_COMPAREFUNC_LESS_EQUAL,
        },
        });



        state.pass_action = default;
        state.pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color() { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };


    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        float t = (float)(sapp_frame_duration() * 60);

        /* compute viewport rectangles so that the views are horizontally
           centered and keep a 1:1 aspect ratio
        */
        int dw = sapp_width();
        int dh = sapp_height();
        int ww = dh / 2; // not a bug
        int hh = dh / 2;
        int x0 = dw / 2 - hh;
        int x1 = dw / 2;
        int y0 = 0;
        int y1 = dh / 2;
        // all sokol-gl functions except sgl_draw() can be called anywhere in the frame
        sgl_viewport(x0, y0, ww, hh, true);
        draw_triangle();
        sgl_viewport(x1, y0, ww, hh, true);
        draw_quad(t);
        sgl_viewport(x0, y1, ww, hh, true);
        draw_cubes(t);
        sgl_viewport(x1, y1, ww, hh, true);
        draw_tex_cube(t);
        sgl_viewport(0, 0, dw, dh, true);

        /* Render the sokol-gfx default pass, all sokol-gl commands
           that happened so far are rendered inside sgl_draw(), and this
           is the only sokol-gl function that must be called inside
           a sokol-gfx begin/end pass pair.
           sgl_draw() also 'rewinds' sokol-gl for the next frame.
        */
        sg_begin_pass(new sg_pass() { action = state.pass_action, swapchain = sglue_swapchain() });
        sgl_draw();
        // __dbgui_draw();
        sg_end_pass();
        sg_commit();


    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        sgl_shutdown();
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

    static void draw_triangle()
    {
        sgl_defaults();
        sgl_begin_triangles();
        sgl_v2f_c3b(0.0f, 0.5f, 255, 0, 0);
        sgl_v2f_c3b(-0.5f, -0.5f, 0, 0, 255);
        sgl_v2f_c3b(0.5f, -0.5f, 0, 255, 0);
        sgl_end();
    }

    static float angle_deg = 0.0f;
    static void draw_quad(float t)
    {
        float scale = 1.0f + MathF.Sin(sgl_as_radians(angle_deg)) * 0.5f;
        angle_deg += 1.0f * t;
        sgl_defaults();
        sgl_rotate(sgl_as_radians(angle_deg), 0.0f, 0.0f, 1.0f);
        sgl_scale(scale, scale, 1.0f);
        sgl_begin_quads();
        sgl_v2f_c3b(-0.5f, -0.5f, 255, 255, 0);
        sgl_v2f_c3b(0.5f, -0.5f, 0, 255, 0);
        sgl_v2f_c3b(0.5f, 0.5f, 0, 0, 255);
        sgl_v2f_c3b(-0.5f, 0.5f, 255, 0, 0);
        sgl_end();
    }

    // // vertex specification for a cube with colored sides and texture coords
    static void cube()
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

    static float[] rot = new float[] { 0.0f, 0.0f };


    static void draw_cubes(float t)
    {

        rot[0] += 1.0f * t;
        rot[1] += 2.0f * t;

        sgl_defaults();
        sgl_load_pipeline(state.pip_3d);

        sgl_matrix_mode_projection();
        sgl_perspective(sgl_as_radians(45.0f), 1.0f, 0.1f, 100.0f);

        sgl_matrix_mode_modelview();
        sgl_translate(0.0f, 0.0f, -12.0f);
        sgl_rotate(sgl_as_radians(rot[0]), 1.0f, 0.0f, 0.0f);
        sgl_rotate(sgl_as_radians(rot[1]), 0.0f, 1.0f, 0.0f);
        cube();
        sgl_push_matrix();
        sgl_translate(0.0f, 0.0f, 3.0f);
        sgl_scale(0.5f, 0.5f, 0.5f);
        sgl_rotate(-2.0f * sgl_as_radians(rot[0]), 1.0f, 0.0f, 0.0f);
        sgl_rotate(-2.0f * sgl_as_radians(rot[1]), 0.0f, 1.0f, 0.0f);
        cube();
        sgl_push_matrix();
        sgl_translate(0.0f, 0.0f, 3.0f);
        sgl_scale(0.5f, 0.5f, 0.5f);
        sgl_rotate(-3.0f * sgl_as_radians(2 * rot[0]), 1.0f, 0.0f, 0.0f);
        sgl_rotate(3.0f * sgl_as_radians(2 * rot[1]), 0.0f, 0.0f, 1.0f);
        cube();
        sgl_pop_matrix();
        sgl_pop_matrix();
    }

    static float frame_count = 0.0f;
    static void draw_tex_cube(float t)
    {

        frame_count += 1.0f * t;
        float a = sgl_as_radians(frame_count);

        // texture matrix rotation and scale
        float tex_rot = 0.5f * a;
        float tex_scale = 1.0f + MathF.Sin(a) * 0.5f;

        // compute an orbiting eye-position for testing sgl_lookat()
        float eye_x = MathF.Sin(a) * 6.0f;
        float eye_z = MathF.Cos(a) * 6.0f;
        float eye_y = MathF.Sin(a) * 3.0f;

        sgl_defaults();
        sgl_load_pipeline(state.pip_3d);

        sgl_enable_texture();
        sgl_texture(state.tex_view, state.smp);

        sgl_matrix_mode_projection();
        sgl_perspective(sgl_as_radians(45.0f), 1.0f, 0.1f, 100.0f);
        sgl_matrix_mode_modelview();
        sgl_lookat(eye_x, eye_y, eye_z, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f);
        sgl_matrix_mode_texture();
        sgl_rotate(tex_rot, 0.0f, 0.0f, 1.0f);
        sgl_scale(tex_scale, tex_scale, 1.0f);
        cube();
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 800,
            height = 600,
            sample_count = 1,
            window_title = "demo-sapp",
            icon = { sokol_default = true },
        };
    }

}