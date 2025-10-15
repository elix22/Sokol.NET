using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SGL;
using static Sokol.SDebugText;
using static Sokol.SBasisu;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;

public static unsafe class BasisUApp
{
    class State
    {
        public sg_pass_action pass_action;
        public sgl_pipeline alpha_pip;
        public sg_view opaque_view;
        public sg_view alpha_view;
        public sg_sampler smp;
        public double angle_deg;
    }

    static State state = new State();


    // Use embedded texture data from basisu-assets.cs
    static readonly byte[] embed_testcard_basis = BasisUAssets.embed_testcard_basis;
    static readonly byte[] embed_testcard_rgba_basis = BasisUAssets.embed_testcard_rgba_basis;

    static string PixelFormatToString(sg_pixel_format fmt)
    {
        return fmt switch
        {
            sg_pixel_format.SG_PIXELFORMAT_BC3_RGBA => "BC3 RGBA",
            sg_pixel_format.SG_PIXELFORMAT_BC1_RGBA => "BC1 RGBA",
            sg_pixel_format.SG_PIXELFORMAT_ETC2_RGBA8 => "ETC2 RGBA8",
            sg_pixel_format.SG_PIXELFORMAT_ETC2_RGB8 => "ETC2 RGB8",
            _ => "???"
        };
    }

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc
        {
            environment = sglue_environment(),
            logger = new sg_logger { func = &slog_func }
        });

        __dbgui_setup(sapp_sample_count());

        sdtx_desc_t desc = default;
        desc.fonts[0] = sdtx_font_oric();

        sdtx_setup(desc);
    
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.25f, b = 1.0f, a = 1.0f };


        sgl_setup(new sgl_desc_t
        {
            logger = new sgl_logger_t { func = &slog_func }
        });

        // Setup Basis Universal
        sbasisu_setup();

        // Create sokol-gfx textures from embedded Basis Universal textures
        // NOTE: This will fail with empty placeholder data
        if (embed_testcard_basis.Length > 0)
        {
            fixed (byte* ptr_opaque = embed_testcard_basis)
            {
                state.opaque_view = sg_make_view(new sg_view_desc
                {
                    texture = 
                    {
                        image = sbasisu_make_image(new sg_range
                        {
                            ptr = ptr_opaque,
                            size = (nuint)embed_testcard_basis.Length
                        })
                    }
                });
            }
        }

        if (embed_testcard_rgba_basis.Length > 0)
        {
            fixed (byte* ptr_alpha = embed_testcard_rgba_basis)
            {
                state.alpha_view = sg_make_view(new sg_view_desc
                {
                    texture =
                    {
                        image = sbasisu_make_image(new sg_range
                        {
                            ptr = ptr_alpha,
                            size = (nuint)embed_testcard_rgba_basis.Length
                        })
                    }
                });
            }
        }

        // Create sampler object
        state.smp = sg_make_sampler(new sg_sampler_desc
        {
            min_filter = sg_filter.SG_FILTER_LINEAR,
            mag_filter = sg_filter.SG_FILTER_LINEAR,
            mipmap_filter = sg_filter.SG_FILTER_LINEAR,
            max_anisotropy = 8
        });

        // sokol-gl pipeline for alpha-blended rendering
        sg_pipeline_desc alpha__pipeline_desc = default;
        alpha__pipeline_desc.colors[0].write_mask = sg_color_mask.SG_COLORMASK_RGB;
        alpha__pipeline_desc.colors[0].blend.enabled = true;
        alpha__pipeline_desc.colors[0].blend.src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
        alpha__pipeline_desc.colors[0].blend.dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
        state.alpha_pip = sgl_make_pipeline(alpha__pipeline_desc);

    }

    struct QuadParams
    {
        public float pos_x, pos_y;
        public float scale_x, scale_y;
        public float rot;
        public sg_view view;
        public sgl_pipeline pip;
    }

    static void DrawQuad(QuadParams p)
    {
        sgl_texture(p.view, state.smp);
        
        if (p.pip.id != 0)
        {
            sgl_load_pipeline(p.pip);
        }
        else
        {
            sgl_load_default_pipeline();
        }

        sgl_push_matrix();
        sgl_translate(p.pos_x, p.pos_y, 0.0f);
        sgl_scale(p.scale_x, p.scale_y, 1.0f);
        sgl_rotate(p.rot, 0.0f, 0.0f, 1.0f);
        
        sgl_begin_quads();
        sgl_v2f_t2f(-1.0f, -1.0f, 0.0f, 0.0f);
        sgl_v2f_t2f(+1.0f, -1.0f, 1.0f, 0.0f);
        sgl_v2f_t2f(+1.0f, +1.0f, 1.0f, 1.0f);
        sgl_v2f_t2f(-1.0f, +1.0f, 0.0f, 1.0f);
        sgl_end();
        
        sgl_pop_matrix();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // Info text
        sdtx_canvas(sapp_widthf() * 0.5f, sapp_heightf() * 0.5f);
        sdtx_origin(0.5f, 2.0f);
        
        if (embed_testcard_basis.Length > 0)
        {
            string opaque_fmt = PixelFormatToString(sbasisu_pixelformat(false));
            sdtx_puts("Opaque format: ");
            sdtx_puts(opaque_fmt);
            sdtx_puts("\n\n");
            
            string alpha_fmt = PixelFormatToString(sbasisu_pixelformat(true));
            sdtx_puts("Alpha format: ");
            sdtx_puts(alpha_fmt);
        }
        else
        {
            sdtx_puts("Basis Universal Example\n\n");
            sdtx_puts("TODO: Add basis texture data\n");
            sdtx_puts("See c_reference/basisu-assets.h");
        }

        // Draw textured quads via sokol-gl
        sgl_defaults();
        sgl_enable_texture();
        sgl_matrix_mode_projection();
        
        float aspect = sapp_heightf() / sapp_widthf();
        sgl_ortho(-1.0f, +1.0f, aspect, -aspect, -1.0f, +1.0f);
        sgl_matrix_mode_modelview();

        state.angle_deg += sapp_frame_duration() * 60.0;
        float angle_rad = (float)(state.angle_deg * Math.PI / 180.0);

        if (embed_testcard_basis.Length > 0)
        {
            // Draw opaque quad
            DrawQuad(new QuadParams
            {
                pos_x = -0.425f,
                pos_y = 0.0f,
                scale_x = 0.4f,
                scale_y = 0.4f,
                rot = angle_rad,
                view = state.opaque_view,
                pip = default
            });

            // Draw alpha blended quad
            DrawQuad(new QuadParams
            {
                pos_x = +0.425f,
                pos_y = 0.0f,
                scale_x = 0.4f,
                scale_y = 0.4f,
                rot = -angle_rad,
                view = state.alpha_view,
                pip = state.alpha_pip
            });
        }

        // Render
        sg_begin_pass(new sg_pass
        {
            action = state.pass_action,
            swapchain = sglue_swapchain()
        });
        
        sgl_draw();
        sdtx_draw();
        __dbgui_draw();
        
        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        __dbgui_event(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        sbasisu_shutdown();
        sgl_shutdown();
        sdtx_shutdown();
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
        return new SApp.sapp_desc
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 800,
            height = 600,
            sample_count = 1,
            window_title = "basisu-sapp.c#",
            icon = new sapp_icon_desc { sokol_default = true },
            logger = new sapp_logger { func = &slog_func }
        };
    }
}
