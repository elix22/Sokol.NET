
using Sokol;
using System.Runtime.InteropServices;
using System.Numerics;
using static System.Numerics.Matrix4x4;
using static Sokol.SG;
using static Sokol.SGlue;
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
using static Sokol.SDebugText;
using System.Diagnostics;
public static unsafe class DebugTextPrintApp
{
    private static bool PauseUpdate = false;
    private const int NUM_FONTS = 3;
    private const int FONT_KC854 = 0;
    private const int FONT_C64 = 1;
    private const int FONT_ORIC = 2;

    struct color_t
    {
        public byte r, g, b;
    };

    class _state
    {
        public sg_pass_action pass_action;
        public color_t[] palette = new color_t[NUM_FONTS];
    }

    static _state state = new _state();

    [UnmanagedCallersOnly]
    private static void Init()
    {
        sg_setup(new sg_desc
        {
            environment = sglue_environment(),
            logger = new sg_logger()
        });

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color() { r = 0.0f, g = 0.125f, b = 0.25f, a = 1.0f };

        state.palette[FONT_KC854] = new color_t { r = 0xf4, g = 0x43, b = 0x36 };
        state.palette[FONT_C64] = new color_t { r = 0x21, g = 0x96, b = 0xf3 };
        state.palette[FONT_ORIC] = new color_t { r = 0x4c, g = 0xaf, b = 0x50 };

        sdtx_desc_t desc = default;
        desc.fonts[FONT_KC854] = sdtx_font_kc854();
        desc.fonts[FONT_C64] = sdtx_font_c64();
        desc.fonts[FONT_ORIC] = sdtx_font_oric();
        sdtx_setup(desc);

    }

    [UnmanagedCallersOnly]
    private static void Frame()
    {
        if (PauseUpdate)
        {
            return;
        }

        uint frame_count = (uint)sapp_frame_count();
        double frame_time = sapp_frame_duration() * 1000.0;

        sdtx_canvas(sapp_width() * 0.5f, sapp_height() * 0.5f);
        sdtx_origin(3.0f, 3.0f);
        for (int i = 0; i < NUM_FONTS; i++)
        {
            color_t color = state.palette[i];
            sdtx_font((uint)i);
            sdtx_color3b(color.r, color.g, color.b);
            sdtx_print("Hello '{0}' !\n", (frame_count & (1 << 7)) != 0 ? "Welt" : "World");
            sdtx_print("\tFrame Time:\t\t{0:0.000}\n", frame_time);
            sdtx_print("\tFrame Count:\t{0}\t0x{1:X04}\n", frame_count, frame_count);
            sdtx_putr("Range Test 1(xyzbla)", 12);
            sdtx_putr("\nRange Test 2\n", 32);
            sdtx_move_y(2);
        }

        sg_begin_pass(new sg_pass() { action = state.pass_action, swapchain = sglue_swapchain() });

        sdtx_draw();

        sg_end_pass();
        sg_commit();

    }

    [UnmanagedCallersOnly]
    private static void Cleanup()
    {
        sdtx_shutdown();
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
            window_title = "debugtext-printf-sapp",
            icon = { sokol_default = true },
        };
    }




}