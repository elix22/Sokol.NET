
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

public static unsafe class SglLinesApp
{

    static bool PauseUpdate = false;

    struct _state
    {
        public sg_pass_action pass_action;
        public sgl_pipeline depth_test_pip;
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

        sgl_setup(new sgl_desc_t()
        {
            logger = new sgl_logger_t()
        });

        // a pipeline object with less-equal depth-testing
        state.depth_test_pip = sgl_make_pipeline(new sg_pipeline_desc()
        {
            depth = {
                write_enabled = true,
                compare = SG_COMPAREFUNC_LESS_EQUAL
            }
        });

        sg_pass_action pass_Action = new sg_pass_action();
        pass_Action.colors[0].load_action = SG_LOADACTION_CLEAR;
        pass_Action.colors[0].clear_value = new sg_color() { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };
        state.pass_action = pass_Action;



    }

    static void grid(float y, uint frame_count)
    {
        const int num = 64;
        const float dist = 4.0f;
        float z_offset = (dist / 8) * (frame_count & 7);
        sgl_begin_lines();
        for (int i = 0; i < num; i++)
        {
            float x = i * dist - num * dist * 0.5f;
            sgl_v3f(x, y, -num * dist);
            sgl_v3f(x, y, 0.0f);
        }
        for (int i = 0; i < num; i++)
        {
            float z = z_offset + i * dist - num * dist;
            sgl_v3f(-num * dist * 0.5f, y, z);
            sgl_v3f(num * dist * 0.5f, y, z);
        }
        sgl_end();
    }

    static void floaty_thingy(uint frame_count)
    {
        const uint num_segs = 32;
        uint start = frame_count % (num_segs * 2);
        if (start < num_segs)
        {
            start = 0;
        }
        else
        {
            start -= num_segs;
        }
        uint end = frame_count % (num_segs * 2);
        if (end > num_segs)
        {
            end = num_segs;
        }
        const float dx = 0.25f;
        const float dy = 0.25f;
        const float x0 = -(num_segs * dx * 0.5f);
        const float x1 = -x0;
        const float y0 = -(num_segs * dy * 0.5f);
        const float y1 = -y0;
        sgl_begin_lines();
        for (uint i = start; i < end; i++)
        {
            float x = i * dx;
            float y = i * dy;
            sgl_v2f(x0 + x, y0); sgl_v2f(x1, y0 + y);
            sgl_v2f(x1 - x, y1); sgl_v2f(x0, y1 - y);
            sgl_v2f(x0 + x, y1); sgl_v2f(x1, y1 - y);
            sgl_v2f(x1 - x, y0); sgl_v2f(x0, y0 + y);
        }
        sgl_end();
    }

    static uint x = 0x12345678;
    static uint xorshift32()
    {
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return x;
    }

    static float rnd()
    {
        return (((float)(xorshift32() & 0xFFFF)) / 0x10000) * 2.0f - 1.0f;
    }


    const int RING_NUM = (1024);
    const int RING_MASK = (RING_NUM - 1);

    static float[,] ring = new float[RING_NUM, 6];
    static uint head = 0;
    static void hairball()
    {


        float vx = rnd();
        float vy = rnd();
        float vz = rnd();
        float r = (rnd() + 1.0f) * 0.5f;
        float g = (rnd() + 1.0f) * 0.5f;
        float b = (rnd() + 1.0f) * 0.5f;
        float x = ring[head, 0];
        float y = ring[head, 1];
        float z = ring[head, 2];
        head = (head + 1) & RING_MASK;
        ring[head, 0] = x * 0.9f + vx;
        ring[head, 1] = y * 0.9f + vy;
        ring[head, 2] = z * 0.9f + vz;
        ring[head, 3] = r;
        ring[head, 4] = g;
        ring[head, 5] = b;

        sgl_begin_line_strip();
        for (uint i = (head + 1) & RING_MASK; i != head; i = (i + 1) & RING_MASK)
        {
            sgl_c3f(ring[i, 3], ring[i, 4], ring[i, 5]);
            sgl_v3f(ring[i, 0], ring[i, 1], ring[i, 2]);
        }
        sgl_end();
    }



    static uint frame_count = 0;
    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        if (PauseUpdate) return;

        float aspect = sapp_widthf() / sapp_heightf();

        frame_count++;

        sgl_defaults();
        sgl_push_pipeline();
        sgl_load_pipeline(state.depth_test_pip);
        sgl_matrix_mode_projection();
        sgl_perspective(sgl_as_radians(45.0f), aspect, 0.1f, 1000.0f);
        sgl_matrix_mode_modelview();
        sgl_translate(MathF.Sin(frame_count * 0.02f) * 16.0f, MathF.Sin(frame_count * 0.01f) * 4.0f, 0.0f);
        sgl_c3f(1.0f, 0.0f, 1.0f);
        grid(-7.0f, frame_count);
        grid(+7.0f, frame_count);
        sgl_push_matrix();
        sgl_translate(0.0f, 0.0f, -30.0f);
        sgl_rotate(frame_count * 0.05f, 0.0f, 1.0f, 1.0f);
        sgl_c3f(1.0f, 1.0f, 0.0f);
        floaty_thingy(frame_count);
        sgl_pop_matrix();
        sgl_push_matrix();
        sgl_translate(-MathF.Sin(frame_count * 0.02f) * 32.0f, 0.0f, -70.0f + MathF.Cos(frame_count * 0.01f) * 50.0f);
        sgl_rotate(frame_count * 0.05f, 0.0f, -1.0f, 1.0f);
        sgl_c3f(0.0f, 1.0f, 0.0f);
        floaty_thingy(frame_count + 32);
        sgl_pop_matrix();
        sgl_push_matrix();
        sgl_translate(-MathF.Sin(frame_count * 0.02f) * 16.0f, 0.0f, -30.0f);
        sgl_rotate(frame_count * 0.01f, MathF.Sin(frame_count * 0.005f), 0.0f, 1.0f);
        sgl_c3f(0.5f, 1.0f, 0.0f);
        hairball();
        sgl_pop_matrix();
        sgl_pop_pipeline();

        // sokol-gfx default pass with the actual sokol-gl drawing
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
            window_title = "sgl-lines-sapp",
            icon = { sokol_default = true },
        };
    }

}