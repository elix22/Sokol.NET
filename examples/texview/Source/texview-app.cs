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
using static Sokol.SLog;
using static Sokol.SImgui;
using static Sokol.SBasisu;
using static Sokol.SGImgui;
using Imgui;
using static Imgui.ImguiNative;
using static texview_sapp_shader_cs.Shaders;
using System.Diagnostics;

public static unsafe class TexViewApp
{
    const int MAX_FILE_SIZE = 128 * 1024;
    const int NUM_FILES = 5;
    static readonly string[] files = new string[]
    {
        "kodim05.basis",
        "kodim07.basis",
        "kodim17.basis",
        "kodim20.basis",
        "kodim23.basis",
    };

    struct State
    {
        public sg_pass_action pass_action;
        public sg_image img;
        public sg_view tex_view;
        public sg_pipeline pip;
        public sg_sampler smp_linear;
        public sg_sampler smp_nearest;
        
        // Image info
        public int img_width;
        public int img_height;
        public int img_num_mipmaps;
        
        // Load state
        public bool load_pending;
        public bool load_failed;
        
        // UI state
        public int ui_selected;
        public int ui_min_mip;
        public int ui_max_mip;
        public float ui_mip_lod;
        public bool ui_use_linear_sampler;
        public sgimgui_t sgimgui;
        
        // Fetch buffer
        public SharedBuffer fetch_buffer;
    }

    static State state = new State();

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        // Setup basis universal texture support
        sbasisu_setup();
        
        // Setup sokol-gfx
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });
        
        // Setup sokol-gfx-imgui
        state.sgimgui = sgimgui_init();
        
        // Setup sokol-imgui
        simgui_setup(new simgui_desc_t
        {
            logger = { func = &slog_func }
        });
        
        // Setup sokol-fetch
        sfetch_setup(new sfetch_desc_t()
        {
            max_requests = 1,
            num_channels = 1,
            num_lanes = 1,
            logger = { func = &slog_func }
        });
        
        // Initialize pass action
        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };
        
        // Pre-allocate handles
        state.img = sg_alloc_image();
        state.tex_view = sg_alloc_view();
        
        // Create render pipeline for bufferless 2D rendering
        state.pip = sg_make_pipeline(new sg_pipeline_desc
        {
            shader = sg_make_shader(texview_shader_desc(sg_query_backend())),
            primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLE_STRIP,
            label = "pipeline"
        });
        
        // Create samplers
        state.smp_linear = sg_make_sampler(new sg_sampler_desc
        {
            min_filter = sg_filter.SG_FILTER_LINEAR,
            mag_filter = sg_filter.SG_FILTER_LINEAR,
            mipmap_filter = sg_filter.SG_FILTER_LINEAR,
            label = "linear-sampler"
        });
        
        state.smp_nearest = sg_make_sampler(new sg_sampler_desc
        {
            min_filter = sg_filter.SG_FILTER_NEAREST,
            mag_filter = sg_filter.SG_FILTER_NEAREST,
            mipmap_filter = sg_filter.SG_FILTER_LINEAR,
            label = "nearest-sampler"
        });
        
        // Allocate fetch buffer
        state.fetch_buffer = SharedBuffer.Create(MAX_FILE_SIZE);
        
        // Start loading the first image
        FetchAsync(files[0]);
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // Pump sokol-fetch message queues
        sfetch_dowork();
        
        // Start new imgui frame
        simgui_new_frame(new simgui_frame_desc_t
        {
            width = sapp_width(),
            height = sapp_height(),
            delta_time = sapp_frame_duration(),
            dpi_scale = 1// TBD ELI , too small on Android sapp_dpi_scale()
        });
        
        // Draw UI
        DrawUI();
        
        // Render
        fs_params_t fs_params = new fs_params_t { mip_lod = state.ui_mip_lod };
        
        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        ApplyViewport();
        sg_apply_pipeline(state.pip);
        sg_bindings bind = default;
        bind.views[VIEW_tex] = state.tex_view;
        bind.samplers[SMP_smp] = state.ui_use_linear_sampler ? state.smp_linear : state.smp_nearest;
        sg_apply_bindings(bind);
        sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fs_params));
        sg_draw(0, 4, 1);
        
        sgimgui_draw(state.sgimgui);
        simgui_render();
        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        simgui_handle_event(in *e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        state.fetch_buffer.Dispose();
        sfetch_shutdown();
        sgimgui_discard(state.sgimgui);
        simgui_shutdown();
        sg_shutdown();
        sbasisu_shutdown();

        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    static void DrawUI()
    {
        if (igBeginMainMenuBar())
        {
            sgimgui_draw_menu(state.sgimgui, "sokol-gfx");
            igEndMainMenuBar();
        }
        
        igSetNextWindowPos(new Vector2(30, 50), ImGuiCond.Once, Vector2.Zero);
        igSetNextWindowBgAlpha(0.75f);
        byte open = 1;
        if (igBegin("Controls", ref open, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (state.load_pending)
            {
                igText("Loading ...");
            }
            else
            {
                if (state.load_failed)
                {
                    igText("Loading failed!");
                }
                if (!HasTextureViews())
                {
                    igText("NOTE: WebGL2/GLES3/GL4.1 have no texture views!");
                }
                
                // Simplified combo for now - using buttons instead
                if (igButton($"<- Previous Image", Vector2.Zero))
                {
                    state.ui_selected = (state.ui_selected - 1 + NUM_FILES) % NUM_FILES;
                    FetchAsync(files[state.ui_selected]);
                }
                igSameLine(0, 10);
                igText($"Image: {files[state.ui_selected]}");
                igSameLine(0, 10);
                if (igButton($"Next Image ->", Vector2.Zero))
                {
                    state.ui_selected = (state.ui_selected + 1) % NUM_FILES;
                    FetchAsync(files[state.ui_selected]);
                }
                
                igText($"Width:   {state.img_width}");
                igText($"Height:  {state.img_height}");
                igText($"Mipmaps: {state.img_num_mipmaps}");
                igSeparator();
                
                byte use_linear = state.ui_use_linear_sampler ? (byte)1 : (byte)0;
                if (igCheckbox("Use Linear Sampler", ref use_linear))
                {
                    state.ui_use_linear_sampler = use_linear != 0;
                }
                
                float max_mip_lod = (float)(state.ui_max_mip - state.ui_min_mip);
                if (state.ui_mip_lod > max_mip_lod)
                {
                    state.ui_mip_lod = max_mip_lod;
                }
                igSliderFloat("Mip LOD", ref state.ui_mip_lod, 0.0f, max_mip_lod, "%.1f", 0);
                
                if (igSliderInt("Min Mip", ref state.ui_min_mip, 0, state.img_num_mipmaps - 1, "%d", 0))
                {
                    if (state.ui_max_mip < state.ui_min_mip)
                    {
                        state.ui_max_mip = state.ui_min_mip;
                    }
                    ReinitTexView();
                }
                
                if (igSliderInt("Max Mip", ref state.ui_max_mip, 0, state.img_num_mipmaps - 1, "%d", 0))
                {
                    if (state.ui_min_mip > state.ui_max_mip)
                    {
                        state.ui_min_mip = state.ui_max_mip;
                    }
                    ReinitTexView();
                }
            }
        }
        igEnd();
    }

    static void FetchAsync(string filename)
    {
        state.load_pending = true;
        state.load_failed = false;
        
        sfetch_request_t request = new sfetch_request_t
        {
            path = util_get_file_path(filename),
            callback = &FetchCallback,
            buffer = SFETCH_RANGE(state.fetch_buffer)
        };
        sfetch_send(request);
    }

    [UnmanagedCallersOnly]
    static void FetchCallback(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            state.load_pending = false;
            sg_uninit_image(state.img);
            
            sg_image_desc img_desc = sbasisu_transcode(new sg_range
            {
                ptr = response->data.ptr,
                size = response->data.size
            });
            
            Debug.Assert(img_desc.num_mipmaps > 0);
            
            state.img_width = img_desc.width;
            state.img_height = img_desc.height;
            state.img_num_mipmaps = img_desc.num_mipmaps;
            state.ui_min_mip = 0;
            state.ui_max_mip = img_desc.num_mipmaps - 1;
            state.ui_mip_lod = 0.0f;
            
            sg_init_image(state.img, img_desc);
            sbasisu_free(img_desc);
            ReinitTexView();
        }
        else if (response->failed)
        {
            state.load_failed = true;
            state.load_pending = false;
        }
    }

    static void ReinitTexView()
    {
        sg_uninit_view(state.tex_view);
        sg_init_view(state.tex_view, new sg_view_desc
        {
            texture = 
            {
                image = state.img,
                mip_levels =
                {
                    _base = state.ui_min_mip,
                    count = (state.ui_max_mip - state.ui_min_mip) + 1
                }
            }
        });
    }

    static bool HasTextureViews()
    {
        sg_backend backend = sg_query_backend();
        return !((backend == sg_backend.SG_BACKEND_GLCORE || backend == sg_backend.SG_BACKEND_GLES3) 
                 && !sg_query_features().gl_texture_views);
    }

    static void ApplyViewport()
    {
        if (state.img_width == 0 || state.img_height == 0)
        {
            return;
        }
        
        float border = 5.0f;
        float canvas_width = sapp_widthf() - 2.0f * border;
        float canvas_height = sapp_heightf() - 2.0f * border;
        
        if (canvas_width < 1.0f) canvas_width = 1.0f;
        if (canvas_height < 1.0f) canvas_height = 1.0f;
        
        float canvas_aspect = canvas_width / canvas_height;
        float img_aspect = (float)state.img_width / (float)state.img_height;
        
        float vp_x, vp_y, vp_w, vp_h;
        
        if (img_aspect < canvas_aspect)
        {
            vp_y = border;
            vp_h = canvas_height;
            vp_w = canvas_height * img_aspect;
            vp_x = border + (canvas_width - vp_w) * 0.5f;
        }
        else
        {
            vp_x = border;
            vp_w = canvas_width;
            vp_h = canvas_width / img_aspect;
            vp_y = border + (canvas_height - vp_h) * 0.5f;
        }
        
        sg_apply_viewportf(vp_x, vp_y, vp_w, vp_h, true);
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
            window_title = "Texture View (sokol-app)",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }
}
