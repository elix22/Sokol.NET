using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Diagnostics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SImgui;
using static Sokol.SGImgui;
using static drawcallperf_sapp_shader_cs.Shaders;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using static Imgui.ImguiNative;
using static Sokol.STM;
using Imgui;

public static unsafe class DrawcallPerf
{
    const int NUM_IMAGES = 3;
    const int IMG_WIDTH = 8;
    const int IMG_HEIGHT = 8;
    const int MAX_INSTANCES = 100000;
    const int MAX_BIND_FREQUENCY = 1000;

    struct _state
    {
        public sg_pass_action pass_action;
        public sg_image[] img;
        public sg_view[] view;
        public sg_pipeline pip;
        public sg_bindings bind;
        public int num_instances;
        public int bind_frequency;
        public float angle;
        public ulong last_time;
        public struct _stats
        {
            public int num_uniform_updates;
            public int num_binding_updates;
            public int num_draw_calls;
        }
        public _stats stats;
        public string backend;
         public sgimgui_t sgimgui;
    }

    static _state state = new _state();

    static sg_logger logger = new sg_logger();

    [UnmanagedCallersOnly]
    static void slog_func_wrapper(byte* tag, uint log_level, uint log_item, byte* message, uint line_nr, byte* filename, void* user_data)
    {
        logger.func(tag, log_level, log_item, message, line_nr, filename, user_data);
    }


    [StructLayout(LayoutKind.Sequential)]
    struct vs_per_instance_t
    {
        public Vector4 world_pos;
    }

    static vs_per_instance_t[] positions = new vs_per_instance_t[MAX_INSTANCES];

    static uint xorshift32_state = 0x12345678;

    static uint xorshift32()
    {
        xorshift32_state ^= xorshift32_state << 13;
        xorshift32_state ^= xorshift32_state >> 17;
        xorshift32_state ^= xorshift32_state << 5;
        return xorshift32_state;
    }

    static Vector4 rand_pos()
    {
        float x = ((float)(xorshift32() & 0xFFFF)) / 0x10000 - 0.5f;
        float y = ((float)(xorshift32() & 0xFFFF)) / 0x10000 - 0.5f;
        float z = ((float)(xorshift32() & 0xFFFF)) / 0x10000 - 0.5f;
        return Vector4.Normalize(new Vector4(x, y, z, 0.0f));
    }

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        Console.WriteLine("Initialize() Enter");

        stm_setup();

        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            uniform_buffer_size = MAX_INSTANCES * 256 + 1024,
            logger = { func = &slog_func_wrapper, }
        });

        simgui_setup(new simgui_desc_t
        {
            logger = { func = &slog_func_wrapper }
        });

        state.sgimgui = sgimgui_init();

        state.pass_action = new sg_pass_action();
        state.pass_action.colors[0] = new sg_color_attachment_action
        {
            load_action = sg_load_action.SG_LOADACTION_CLEAR,
            clear_value = new sg_color { r = 0.0f, g = 0.5f, b = 0.75f, a = 1.0f }
        };

        state.num_instances = 100;
        state.bind_frequency = MAX_BIND_FREQUENCY;

        switch (sg_query_backend())
        {
            case sg_backend.SG_BACKEND_GLCORE: state.backend = "GLCORE"; break;
            case sg_backend.SG_BACKEND_GLES3: state.backend = "GLES3"; break;
            case sg_backend.SG_BACKEND_D3D11: state.backend = "D3D11"; break;
            case sg_backend.SG_BACKEND_METAL_IOS: state.backend = "METAL_IOS"; break;
            case sg_backend.SG_BACKEND_METAL_MACOS: state.backend = "METAL_MACOS"; break;
            case sg_backend.SG_BACKEND_METAL_SIMULATOR: state.backend = "METAL_SIMULATOR"; break;
            case sg_backend.SG_BACKEND_WGPU: state.backend = "WGPU"; break;
            case sg_backend.SG_BACKEND_DUMMY: state.backend = "DUMMY"; break;
            default: state.backend = "???"; break;
        }

        // vertices and indices for a 2d quad
        float[] vertices = {
            -1.0f, -1.0f, -1.0f,   0.0f, 0.0f,  1.0f,
             1.0f, -1.0f, -1.0f,   1.0f, 0.0f,  1.0f,
             1.0f,  1.0f, -1.0f,   1.0f, 1.0f,  1.0f,
            -1.0f,  1.0f, -1.0f,   0.0f, 1.0f,  1.0f,

            -1.0f, -1.0f,  1.0f,   0.0f, 0.0f,  0.9f,
             1.0f, -1.0f,  1.0f,   1.0f, 0.0f,  0.9f,
             1.0f,  1.0f,  1.0f,   1.0f, 1.0f,  0.9f,
            -1.0f,  1.0f,  1.0f,   0.0f, 1.0f,  0.9f,

            -1.0f, -1.0f, -1.0f,   0.0f, 0.0f,  0.8f,
            -1.0f,  1.0f, -1.0f,   1.0f, 0.0f,  0.8f,
            -1.0f,  1.0f,  1.0f,   1.0f, 1.0f,  0.8f,
            -1.0f, -1.0f,  1.0f,   0.0f, 1.0f,  0.8f,

            1.0f, -1.0f, -1.0f,    0.0f, 0.0f,  0.7f,
            1.0f,  1.0f, -1.0f,    1.0f, 0.0f,  0.7f,
            1.0f,  1.0f,  1.0f,    1.0f, 1.0f,  0.7f,
            1.0f, -1.0f,  1.0f,    0.0f, 1.0f,  0.7f,

            -1.0f, -1.0f, -1.0f,   0.0f, 0.0f,  0.6f,
            -1.0f, -1.0f,  1.0f,   1.0f, 0.0f,  0.6f,
             1.0f, -1.0f,  1.0f,   1.0f, 1.0f,  0.6f,
             1.0f, -1.0f, -1.0f,   0.0f, 1.0f,  0.6f,

            -1.0f,  1.0f, -1.0f,   0.0f, 0.0f,  0.5f,
            -1.0f,  1.0f,  1.0f,   1.0f, 0.0f,  0.5f,
             1.0f,  1.0f,  1.0f,   1.0f, 1.0f,  0.5f,
             1.0f,  1.0f, -1.0f,   0.0f, 1.0f,  0.5f,
        };
        ushort[] indices = {
            0, 1, 2,  0, 2, 3,
            6, 5, 4,  7, 6, 4,
            8, 9, 10,  8, 10, 11,
            14, 13, 12,  15, 14, 12,
            16, 17, 18,  16, 18, 19,
            22, 21, 20,  23, 22, 20
        };

        state.bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc
        {
            data = SG_RANGE(vertices)
        });
        state.bind.index_buffer = sg_make_buffer(new sg_buffer_desc
        {
            usage = { index_buffer = true },
            data = SG_RANGE(indices)
        });

        // three textures and a sampler
        uint[] pixels = new uint[IMG_HEIGHT * IMG_WIDTH];
        state.img = new sg_image[NUM_IMAGES];
        state.view = new sg_view[NUM_IMAGES];
        for (int i = 0; i < NUM_IMAGES; i++)
        {
            uint color;
            switch (i)
            {
                case 0: color = 0xFF0000FF; break;
                case 1: color = 0xFF00FF00; break;
                default: color = 0xFFFF0000; break;
            }
            for (int y = 0; y < IMG_HEIGHT; y++)
            {
                for (int x = 0; x < IMG_WIDTH; x++)
                {
                    pixels[y * IMG_WIDTH + x] = color;
                }
            }

            sg_image_data pixels_data = default;
            pixels_data.mip_levels[0] = SG_RANGE(pixels);

            state.img[i] = sg_make_image(new sg_image_desc
            {
                width = IMG_WIDTH,
                height = IMG_HEIGHT,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                data = pixels_data
            });

            state.view[i] = sg_make_view(new sg_view_desc
            {
                texture = { image = state.img[i] }
            });
        }
        state.bind.samplers[SMP_smp] = sg_make_sampler(new sg_sampler_desc
        {
            min_filter = sg_filter.SG_FILTER_NEAREST,
            mag_filter = sg_filter.SG_FILTER_NEAREST,
        });

        var pipeline_desc = default(sg_pipeline_desc);
        pipeline_desc.layout.attrs[ATTR_drawcallperf_in_pos].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_drawcallperf_in_uv].format = SG_VERTEXFORMAT_FLOAT2;
        pipeline_desc.layout.attrs[ATTR_drawcallperf_in_bright].format = SG_VERTEXFORMAT_FLOAT;

        pipeline_desc.shader = sg_make_shader(drawcallperf_shader_desc(sg_query_backend()));
        pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        pipeline_desc.cull_mode = SG_CULLMODE_BACK;
        pipeline_desc.depth = new sg_depth_state
        {
            write_enabled = true,
            compare = SG_COMPAREFUNC_LESS_EQUAL,
        };

        state.pip = sg_make_pipeline(pipeline_desc);

        // initialize a fixed array of random positions
        for (int i = 0; i < MAX_INSTANCES; i++)
        {
            positions[i].world_pos = rand_pos();
        }
    }

    static Matrix4x4 compute_viewproj()
    {
        float w = sapp_widthf();
        float h = sapp_heightf();
        state.angle = (float)(state.angle + 0.01) % 360.0f;
        float dist = 4.5f;
        Vector3 eye = new Vector3((float)Math.Sin(state.angle) * dist, 1.5f, (float)Math.Cos(state.angle) * dist);
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(60.0 * Math.PI / 180), w / h, 0.01f, 10.0f);
        Matrix4x4 view = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY);
        return view * proj;
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        double frame_measured_time = stm_sec(stm_laptime(ref state.last_time));

        simgui_new_frame(new simgui_frame_desc_t
        {
            width = sapp_width(),
            height = sapp_height(),
            delta_time = sapp_frame_duration(),
            dpi_scale = 1//sapp_dpi_scale(), // Doesn't show well on Android
        });

        // sokol-gfx debug ui
        if (igBeginMainMenuBar())
        {
            sgimgui_draw_menu( state.sgimgui, "sokol-gfx");
            sgimgui_draw(state.sgimgui);
            igEndMainMenuBar();
        }

        // control ui
        igSetNextWindowPos(new Vector2(20, 20), ImGuiCond.Once,Vector2.Zero);
        igSetNextWindowSize(new Vector2(600, 200), ImGuiCond.Once);
        byte open = 1;
        if (igBegin("Controls", ref open, ImGuiWindowFlags.NoResize))
        {
            igText("Each cube/instance is 1 16-byte uniform update and 1 draw call\n");
            igText("DC/texture is the number of adjacent draw calls with the same texture binding\n");
            igSliderInt("Num Instances", ref state.num_instances, 100, MAX_INSTANCES, "%d", ImGuiSliderFlags.Logarithmic);
            igSliderInt("DC/texture", ref state.bind_frequency, 1, MAX_BIND_FREQUENCY, "%d", ImGuiSliderFlags.Logarithmic);
            igText($"Backend: {state.backend}");
            igText($"Frame duration: {frame_measured_time * 1000.0:F4}ms");
            igText($"sg_apply_bindings(): {state.stats.num_binding_updates}");
            igText($"sg_apply_uniforms(): {state.stats.num_uniform_updates}");
            igText($"sg_draw(): {state.stats.num_draw_calls}");
        }

        igEnd();

        if (state.num_instances < 1)
        {
            state.num_instances = 1;
        }
        else if (state.num_instances > MAX_INSTANCES)
        {
            state.num_instances = MAX_INSTANCES;
        }

        // view-proj matrix for the frame
        vs_per_frame_t vs_per_frame = new vs_per_frame_t { viewproj = compute_viewproj() };

        state.stats.num_uniform_updates = 0;
        state.stats.num_binding_updates = 0;
        state.stats.num_draw_calls = 0;

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pip);
        sg_apply_uniforms(UB_vs_per_frame, SG_RANGE(ref vs_per_frame));
        state.stats.num_uniform_updates++;

        state.bind.views[VIEW_tex] = state.view[0];
        sg_apply_bindings( state.bind);
        state.stats.num_binding_updates++;
        int cur_bind_count = 0;
        int cur_img = 0;
        for (int i = 0; i < state.num_instances; i++)
        {
            if (++cur_bind_count == state.bind_frequency)
            {
                cur_bind_count = 0;
                if (cur_img == NUM_IMAGES)
                {
                    cur_img = 0;
                }
                state.bind.views[VIEW_tex] = state.view[cur_img++];
                sg_apply_bindings(state.bind);
                state.stats.num_binding_updates++;
            }
            sg_apply_uniforms(UB_vs_per_instance, SG_RANGE(ref positions[i]));
            state.stats.num_uniform_updates++;
            sg_draw(0, 36, 1);
            state.stats.num_draw_calls++;
        }
        simgui_render();
        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* ev)
    {
        simgui_handle_event(*ev);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        sgimgui_discard(state.sgimgui);
        simgui_shutdown();
        sg_shutdown();

        // Force a complete shutdown if debugging
        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    public static sapp_desc sokol_main()
    {
        return new sapp_desc
        {
            init_cb = &Init,
            frame_cb = &Frame,
            cleanup_cb = &Cleanup,
            event_cb = &Event,
            width = 1024,
            height = 768,
            sample_count = 4,
            window_title = "drawcallperf-sapp",
            icon = { sokol_default = true },
            // logger = { func = &Sokol.SLog.slog_func },
        };
    }

}
