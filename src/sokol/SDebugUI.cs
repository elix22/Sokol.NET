using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SImgui;
using static Sokol.SGImgui;
using static Sokol.SLog;
using static Sokol.SApp;
using static Imgui.ImguiNative;
namespace Sokol
{
    public static unsafe partial class SDebugUI
    {
        private static sgimgui_t sgimgui = default;

        public static void __dbgui_setup(int sample_count)
        {
#if DEBUG
            sgimgui = sgimgui_init();

            simgui_setup(new simgui_desc_t
            {
                sample_count = sample_count,
                logger = {
                func = &slog_func,
            }
            });
#endif
        }

        public static void __dbgui_shutdown()
        {
#if DEBUG
            sgimgui_discard(sgimgui);
            simgui_shutdown();
#endif
        }

        public static void __dbgui_draw()
        {
#if DEBUG
            simgui_new_frame(new simgui_frame_desc_t
            {
                width = sapp_width(),
                height = sapp_height(),
                delta_time = sapp_frame_duration(),
                dpi_scale = 1//sapp_dpi_scale(), // Doesn't show well on Android
            });

            if (igBeginMainMenuBar())
            {
                sgimgui_draw_menu(sgimgui, "sokol-gfx");
                igEndMainMenuBar();
            }
            sgimgui_draw(sgimgui);
            simgui_render();
#endif
        }

        public static void __dbgui_event(sapp_event* e)
        {
#if DEBUG
            simgui_handle_event(*e);
#endif
        }

        public static bool __dbgui_event_with_retval(sapp_event* e)
        {
#if DEBUG
            return simgui_handle_event(*e);
#else
            return false;
#endif
        }
    }

}