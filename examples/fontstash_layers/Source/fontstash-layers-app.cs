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
using static fontstash_layers_sapp_shader_cs.Shaders;

public static unsafe class FontStashLayersApp
{
    const int FONT_DATA_SIZE = 256 * 1024;

    struct State
    {
        public sg_pass_action pass_action;
        public sg_pipeline pip;
        public sg_bindings bind;
        public IntPtr fons;
        public int font;
        public SharedBuffer font_data;
    }

    static State state = new State();

    // Round to next power of 2 (see bit-twiddling-hacks)
    static int RoundPow2(float v)
    {
        uint vi = ((uint)v) - 1;
        for (uint i = 0; i < 5; i++)
        {
            vi |= (vi >> (1 << (int)i));
        }
        return (int)(vi + 1);
    }

    // sokol-fetch callback for TTF font data
    [UnmanagedCallersOnly]
    static void FontLoaded(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            fixed (byte* data = state.font_data.Buffer)
            {
                state.font = fonsAddFontMem(state.fons, "sans", data, (int)response->data.size, 0);
            }
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });

        sgl_setup(new sgl_desc_t()
        {
            logger = { func = &slog_func }
        });

        sfetch_setup(new sfetch_desc_t()
        {
            num_channels = 1,
            num_lanes = 1,
            logger = { func = &slog_func }
        });

        // Make sure fontstash atlas width/height is pow-2
        int atlas_dim = RoundPow2(512.0f * sapp_dpi_scale());
        state.fons = sfons_create(new sfons_desc_t()
        {
            width = atlas_dim,
            height = atlas_dim
        });
        state.font = -1;  // FONS_INVALID

        // Use sokol-fetch to load TTF font file
        state.font_data = SharedBuffer.Create(FONT_DATA_SIZE);
        sfetch_send(new sfetch_request_t()
        {
            path = util_get_file_path("DroidSerif-Regular.ttf"),
            callback = &FontLoaded,
            buffer = SFETCH_RANGE(state.font_data)
        });

        // Pass action to clear framebuffer to black
        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };

        // Vertex buffer, shader and pipeline (with alpha-blending) for a triangle
        float[] vertices = {
            // positions            // colors
             0.0f,  0.5f, 0.5f,     1.0f, 0.0f, 0.0f, 0.9f,
             0.5f, -0.5f, 0.5f,     0.0f, 1.0f, 0.0f, 0.9f,
            -0.5f, -0.5f, 0.5f,     0.0f, 0.0f, 1.0f, 0.9f
        };

        fixed (float* vptr = vertices)
        {
            state.bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc()
            {
                data = new sg_range { ptr = vptr, size = (uint)(vertices.Length * sizeof(float)) }
            });
        }

        sg_pipeline_desc pip_desc = default;
        pip_desc.shader = sg_make_shader(triangle_shader_desc(sg_query_backend()));
        pip_desc.layout.attrs[ATTR_triangle_position].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
        pip_desc.layout.attrs[ATTR_triangle_color0].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4;
        pip_desc.colors[0].blend.enabled = true;
        pip_desc.colors[0].blend.src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
        pip_desc.colors[0].blend.dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
        
        state.pip = sg_make_pipeline(pip_desc);
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // Pump the sokol-fetch message queues
        sfetch_dowork();

        float dpis = sapp_dpi_scale();
        float disp_w = sapp_widthf();
        float disp_h = sapp_heightf();

        // Prepare sokol-gl
        sgl_defaults();
        sgl_matrix_mode_projection();
        sgl_ortho(0.0f, disp_w, disp_h, 0.0f, -1.0f, +1.0f);

        // Only render text once font data has been loaded
        IntPtr fs = state.fons;
        fonsClearState(fs);
        
        if (state.font != -1)  // FONS_INVALID
        {
            fonsSetFont(fs, state.font);
            fonsSetSize(fs, 124.0f * dpis);
            fonsSetColor(fs, 0xFFFFFFFF);
            float lh = 0;
            fonsVertMetrics(fs, ref Unsafe.NullRef<float>(), ref Unsafe.NullRef<float>(), ref lh);

            // Background text to sokol-gl layer 0
            {
                string text = "Background";
                sgl_layer(0);
                float w = fonsTextBounds(fs, 0, 0, text, null, ref Unsafe.NullRef<float>());
                float x = (disp_w - w) * 0.5f;
                float y = (disp_h * 0.5f) - lh * 0.25f;
                fonsDrawText(fs, x, y, text, null);
            }

            // Foreground text to sokol-gl layer 1
            {
                string text = "Foreground";
                sgl_layer(1);
                float w = fonsTextBounds(fs, 0, 0, text, null, ref Unsafe.NullRef<float>());
                float x = (disp_w - w) * 0.5f;
                float y = (disp_h * 0.5f) + lh * 1.0f;
                fonsDrawText(fs, x, y, text, null);
            }
        }
        sfons_flush(fs);

        // sokol-gfx render pass
        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });

        // Draw background text layer via sokol-gl
        sgl_draw_layer(0);

        // Render a triangle inbetween text layers
        sg_apply_pipeline(state.pip);
        sg_apply_bindings(ref state.bind);
        sg_draw(0, 3, 1);

        // Draw foreground text layer via sokol-gl
        sgl_draw_layer(1);

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
        state.font_data.Dispose();
        sfetch_shutdown();
        sfons_destroy(state.fons);
        sgl_shutdown();
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
            width = 800,
            height = 600,
            high_dpi = true,
            window_title = "fontstash-layers-sapp.c",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }
}
