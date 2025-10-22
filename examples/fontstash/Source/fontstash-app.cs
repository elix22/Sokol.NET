using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SFetch;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SFontstash;
using static Sokol.Fontstash;
using static Sokol.SGL;

public static unsafe class FontStashApp
{
    const int FONT_NORMAL_SIZE = 256 * 1024;
    const int FONT_ITALIC_SIZE = 256 * 1024;
    const int FONT_BOLD_SIZE = 256 * 1024;
    const int FONT_JAPANESE_SIZE = 2 * 1024 * 1024;

    struct State
    {
        public IntPtr fons;
        public float dpi_scale;
        public int font_normal;
        public int font_italic;
        public int font_bold;
        public int font_japanese;
        public SharedBuffer font_normal_data;
        public SharedBuffer font_italic_data;
        public SharedBuffer font_bold_data;
        public SharedBuffer font_japanese_data;
    }

    static State state = new State();

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        state.dpi_scale = sapp_dpi_scale();
        
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });

        sgl_setup(new sgl_desc_t()
        {
            logger = { func = &slog_func }
        });

        // Make sure the fontstash atlas width/height is pow-2
        int atlas_dim = RoundPow2(512.0f * state.dpi_scale);
        state.fons = sfons_create(new sfons_desc_t()
        {
            width = atlas_dim,
            height = atlas_dim
        });

        state.font_normal = -1;  // FONS_INVALID
        state.font_italic = -1;
        state.font_bold = -1;
        state.font_japanese = -1;

        // Use sokol_fetch for loading the TTF font files
        sfetch_setup(new sfetch_desc_t()
        {
            max_requests = 4,
            num_channels = 1,
            num_lanes = 4,
            logger = { func = &slog_func }
        });

        // Allocate font data buffers
        state.font_normal_data = SharedBuffer.Create(FONT_NORMAL_SIZE);
        state.font_italic_data = SharedBuffer.Create(FONT_ITALIC_SIZE);
        state.font_bold_data = SharedBuffer.Create(FONT_BOLD_SIZE);
        state.font_japanese_data = SharedBuffer.Create(FONT_JAPANESE_SIZE);

        // Start loading fonts
        sfetch_send(new sfetch_request_t()
        {
            path = util_get_file_path("DroidSerif-Regular.ttf"),
            callback = &FontNormalLoaded,
            buffer = SFETCH_RANGE(state.font_normal_data)
        });

        sfetch_send(new sfetch_request_t()
        {
            path = util_get_file_path("DroidSerif-Italic.ttf"),
            callback = &FontItalicLoaded,
            buffer = SFETCH_RANGE(state.font_italic_data)
        });

        sfetch_send(new sfetch_request_t()
        {
            path = util_get_file_path("DroidSerif-Bold.ttf"),
            callback = &FontBoldLoaded,
            buffer = SFETCH_RANGE(state.font_bold_data)
        });

        sfetch_send(new sfetch_request_t()
        {
            path = util_get_file_path("DroidSansJapanese.ttf"),
            callback = &FontJapaneseLoaded,
            buffer = SFETCH_RANGE(state.font_japanese_data)
        });
    }

    [UnmanagedCallersOnly]
    static void FontNormalLoaded(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            fixed (byte* data = state.font_normal_data.Buffer)
            {
                state.font_normal = fonsAddFontMem(state.fons, "sans", data, (int)response->data.size, 0);
            }
        }
    }

    [UnmanagedCallersOnly]
    static void FontItalicLoaded(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            fixed (byte* data = state.font_italic_data.Buffer)
            {
                state.font_italic = fonsAddFontMem(state.fons, "sans-italic", data, (int)response->data.size, 0);
            }
        }
    }

    [UnmanagedCallersOnly]
    static void FontBoldLoaded(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            fixed (byte* data = state.font_bold_data.Buffer)
            {
                state.font_bold = fonsAddFontMem(state.fons, "sans-bold", data, (int)response->data.size, 0);
            }
        }
    }

    [UnmanagedCallersOnly]
    static void FontJapaneseLoaded(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            fixed (byte* data = state.font_japanese_data.Buffer)
            {
                state.font_japanese = fonsAddFontMem(state.fons, "sans-japanese", data, (int)response->data.size, 0);
            }
        }
    }

    static int RoundPow2(float v)
    {
        uint vi = ((uint)v) - 1;
        for (uint i = 0; i < 5; i++)
        {
            vi |= (vi >> (1 << (int)i));
        }
        return (int)(vi + 1);
    }

    static void Line(float sx, float sy, float ex, float ey)
    {
        sgl_begin_lines();
        sgl_c4b(255, 255, 0, 128);
        sgl_v2f(sx, sy);
        sgl_v2f(ex, ey);
        sgl_end();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        float dpis = state.dpi_scale;

        // Pump sokol_fetch message queues
        sfetch_dowork();

        // Text rendering via fontstash.h
        float sx, sy, dx, dy, lh = 0.0f;
        uint white = sfons_rgba(255, 255, 255, 255);
        uint black = sfons_rgba(0, 0, 0, 255);
        uint brown = sfons_rgba(192, 128, 0, 128);
        uint blue = sfons_rgba(0, 192, 255, 255);
        
        fonsClearState(state.fons);

        sgl_defaults();
        sgl_matrix_mode_projection();
        sgl_ortho(0.0f, sapp_widthf(), sapp_heightf(), 0.0f, -1.0f, +1.0f);

        sx = 50 * dpis;
        sy = 50 * dpis;
        dx = sx;
        dy = sy;

        IntPtr fs = state.fons;
        
        if (state.font_normal != -1)
        {
            fonsSetFont(fs, state.font_normal);
            fonsSetSize(fs, 124.0f * dpis);
            fonsVertMetrics(fs, ref Unsafe.NullRef<float>(), ref Unsafe.NullRef<float>(), ref lh);
            dx = sx;
            dy += lh;
            fonsSetColor(fs, white);
            dx = fonsDrawText(fs, dx, dy, "The quick ", null);
        }

        if (state.font_italic != -1)
        {
            fonsSetFont(fs, state.font_italic);
            fonsSetSize(fs, 48.0f * dpis);
            fonsSetColor(fs, brown);
            dx = fonsDrawText(fs, dx, dy, "brown ", null);
        }

        if (state.font_normal != -1)
        {
            fonsSetFont(fs, state.font_normal);
            fonsSetSize(fs, 24.0f * dpis);
            fonsSetColor(fs, white);
            dx = fonsDrawText(fs, dx, dy, "fox ", null);
        }

        if (state.font_normal != -1 && state.font_italic != -1 && state.font_bold != -1)
        {
            fonsVertMetrics(fs, ref Unsafe.NullRef<float>(), ref Unsafe.NullRef<float>(), ref lh);
            dx = sx;
            dy += lh * 1.2f;
            fonsSetFont(fs, state.font_italic);
            dx = fonsDrawText(fs, dx, dy, "jumps over ", null);
            fonsSetFont(fs, state.font_bold);
            dx = fonsDrawText(fs, dx, dy, "the lazy ", null);
            fonsSetFont(fs, state.font_normal);
            dx = fonsDrawText(fs, dx, dy, "dog.", null);
        }

        if (state.font_normal != -1)
        {
            dx = sx;
            dy += lh * 1.2f;
            fonsSetSize(fs, 12.0f * dpis);
            fonsSetFont(fs, state.font_normal);
            fonsSetColor(fs, blue);
            fonsDrawText(fs, dx, dy, "Now is the time for all good men to come to the aid of the party.", null);
        }

        if (state.font_italic != -1)
        {
            fonsVertMetrics(fs, ref Unsafe.NullRef<float>(), ref Unsafe.NullRef<float>(), ref lh);
            dx = sx;
            dy += lh * 1.2f * 2;
            fonsSetSize(fs, 18.0f * dpis);
            fonsSetFont(fs, state.font_italic);
            fonsSetColor(fs, white);
            fonsDrawText(fs, dx, dy, "Ég get etið gler án þess að meiða mig.", null);
        }

        if (state.font_japanese != -1)
        {
            fonsVertMetrics(fs, ref Unsafe.NullRef<float>(), ref Unsafe.NullRef<float>(), ref lh);
            dx = sx;
            dy += lh * 1.2f;
            fonsSetFont(fs, state.font_japanese);
            fonsDrawText(fs, dx, dy, "私はガラスを食べられます。それは私を傷つけません。", null);
        }

        // Font alignment
        if (state.font_normal != -1)
        {
            fonsSetSize(fs, 18.0f * dpis);
            fonsSetFont(fs, state.font_normal);
            fonsSetColor(fs, white);
            dx = 50 * dpis;
            dy = 350 * dpis;
            Line(dx - 10 * dpis, dy, dx + 250 * dpis, dy);
            fonsSetAlign(fs, 1 | 8);  // FONS_ALIGN_LEFT | FONS_ALIGN_TOP
            dx = fonsDrawText(fs, dx, dy, "Top", null);
            dx += 10 * dpis;
            fonsSetAlign(fs, 1 | 16);  // FONS_ALIGN_LEFT | FONS_ALIGN_MIDDLE
            dx = fonsDrawText(fs, dx, dy, "Middle", null);
            dx += 10 * dpis;
            fonsSetAlign(fs, 1 | 32);  // FONS_ALIGN_LEFT | FONS_ALIGN_BASELINE
            dx = fonsDrawText(fs, dx, dy, "Baseline", null);
            dx += 10 * dpis;
            fonsSetAlign(fs, 1 | 64);  // FONS_ALIGN_LEFT | FONS_ALIGN_BOTTOM
            fonsDrawText(fs, dx, dy, "Bottom", null);

            dx = 150 * dpis;
            dy = 400 * dpis;
            Line(dx, dy - 30 * dpis, dx, dy + 80.0f * dpis);
            fonsSetAlign(fs, 1 | 32);  // FONS_ALIGN_LEFT | FONS_ALIGN_BASELINE
            fonsDrawText(fs, dx, dy, "Left", null);
            dy += 30 * dpis;
            fonsSetAlign(fs, 2 | 32);  // FONS_ALIGN_CENTER | FONS_ALIGN_BASELINE
            fonsDrawText(fs, dx, dy, "Center", null);
            dy += 30 * dpis;
            fonsSetAlign(fs, 4 | 32);  // FONS_ALIGN_RIGHT | FONS_ALIGN_BASELINE
            fonsDrawText(fs, dx, dy, "Right", null);
        }

        // Blur
        if (state.font_italic != -1)
        {
            dx = 500 * dpis;
            dy = 350 * dpis;
            fonsSetAlign(fs, 1 | 32);  // FONS_ALIGN_LEFT | FONS_ALIGN_BASELINE
            fonsSetSize(fs, 60.0f * dpis);
            fonsSetFont(fs, state.font_italic);
            fonsSetColor(fs, white);
            fonsSetSpacing(fs, 5.0f * dpis);
            fonsSetBlur(fs, 10.0f);
            fonsDrawText(fs, dx, dy, "Blurry...", null);
        }

        if (state.font_bold != -1)
        {
            dy += 50.0f * dpis;
            fonsSetSize(fs, 18.0f * dpis);
            fonsSetFont(fs, state.font_bold);
            fonsSetColor(fs, black);
            fonsSetSpacing(fs, 0.0f);
            fonsSetBlur(fs, 3.0f);
            fonsDrawText(fs, dx, dy + 2, "DROP THAT SHADOW", null);

            fonsSetColor(fs, white);
            fonsSetBlur(fs, 0.0f);
            fonsDrawText(fs, dx, dy, "DROP THAT SHADOW", null);
        }

        // Flush fontstash's font atlas to sokol-gfx texture
        sfons_flush(fs);

        // Render pass
        sg_pass_action pass_action = default;
        pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        pass_action.colors[0].clear_value = new sg_color { r = 0.3f, g = 0.3f, b = 0.32f, a = 1.0f };
        
        sg_begin_pass(new sg_pass
        {
            action = pass_action,
            swapchain = sglue_swapchain()
        });
        
        sgl_draw();
        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        state.font_normal_data.Dispose();
        state.font_italic_data.Dispose();
        state.font_bold_data.Dispose();
        state.font_japanese_data.Dispose();
        
        sfetch_shutdown();
        sfons_destroy(state.fons);
        sgl_shutdown();
        sg_shutdown();

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
            width = 800,
            height = 600,
            sample_count = 4,
            window_title = "Fontstash (sokol-app)",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }
}
