using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static System.Numerics.Matrix4x4;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_load_action;
using static Sokol.SG.sg_filter;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using static Sokol.SDebugText;
using static debugtext_context_sapp_shader_cs.Shaders;

public static unsafe class DebugtextContextApp
{
    const int FONT_KC853 = 0;
    const int FONT_KC854 = 1;
    const int FONT_Z1013 = 2;
    const int FONT_CPC = 3;
    const int FONT_C64 = 4;
    const int FONT_ORIC = 5;

    const int NUM_FACES = 6;
    const sg_pixel_format OFFSCREEN_PIXELFORMAT = SG_PIXELFORMAT_RGBA8;
    const int OFFSCREEN_SAMPLE_COUNT = 1;
    const int OFFSCREEN_WIDTH = 32;
    const int OFFSCREEN_HEIGHT = 32;
    const int DISPLAY_SAMPLE_COUNT = 4;

    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    {
        public float x, y, z;
        public ushort u, v;
    }

    // Face background colors
    static readonly sg_color[] bg = new sg_color[]
    {
        new sg_color { r = 0.0f, g = 0.0f, b = 0.5f, a = 1.0f },
        new sg_color { r = 0.0f, g = 0.5f, b = 0.0f, a = 1.0f },
        new sg_color { r = 0.5f, g = 0.0f, b = 0.0f, a = 1.0f },
        new sg_color { r = 0.5f, g = 0.0f, b = 0.25f, a = 1.0f },
        new sg_color { r = 0.5f, g = 0.25f, b = 0.0f, a = 1.0f },
        new sg_color { r = 0.0f, g = 0.25f, b = 0.5f, a = 1.0f }
    };

    class PassState
    {
        public sdtx_context text_context;
        public sg_view tex_view;
        public sg_pass pass;
    }

    class State
    {
        public float rx, ry;
        public sg_buffer vbuf;
        public sg_buffer ibuf;
        public sg_pipeline pip;
        public sg_sampler smp;
        public sg_pass_action pass_action;
        public PassState[] passes = new PassState[NUM_FACES];

        public State()
        {
            for (int i = 0; i < NUM_FACES; i++)
            {
                passes[i] = new PassState();
            }
        }
    }

    static State state = new State();

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        // Setup sokol-gfx
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });
        __dbgui_setup(sapp_sample_count());

        // Setup sokol-debugtext using all builtin fonts
        sdtx_desc_t sdtx_desc = default;
        sdtx_desc.fonts[FONT_KC853] = sdtx_font_kc853();
        sdtx_desc.fonts[FONT_KC854] = sdtx_font_kc854();
        sdtx_desc.fonts[FONT_Z1013] = sdtx_font_z1013();
        sdtx_desc.fonts[FONT_CPC] = sdtx_font_cpc();
        sdtx_desc.fonts[FONT_C64] = sdtx_font_c64();
        sdtx_desc.fonts[FONT_ORIC] = sdtx_font_oric();
        sdtx_desc.logger.func = &slog_func;
        sdtx_setup(sdtx_desc);

        // Create resources to render a textured cube (vertex buffer, index buffer, shader and pipeline state object)
        Vertex[] vertices = new Vertex[]
        {
            // pos                  uvs
            new Vertex { x = -1.0f, y = -1.0f, z = -1.0f, u = 0, v = 0 },
            new Vertex { x = 1.0f, y = -1.0f, z = -1.0f, u = 32767, v = 0 },
            new Vertex { x = 1.0f, y = 1.0f, z = -1.0f, u = 32767, v = 32767 },
            new Vertex { x = -1.0f, y = 1.0f, z = -1.0f, u = 0, v = 32767 },
            new Vertex { x = -1.0f, y = -1.0f, z = 1.0f, u = 32767, v = 0 },
            new Vertex { x = 1.0f, y = -1.0f, z = 1.0f, u = 0, v = 0 },
            new Vertex { x = 1.0f, y = 1.0f, z = 1.0f, u = 0, v = 32767 },
            new Vertex { x = -1.0f, y = 1.0f, z = 1.0f, u = 32767, v = 32767 },
            new Vertex { x = -1.0f, y = -1.0f, z = -1.0f, u = 0, v = 0 },
            new Vertex { x = -1.0f, y = 1.0f, z = -1.0f, u = 32767, v = 0 },
            new Vertex { x = -1.0f, y = 1.0f, z = 1.0f, u = 32767, v = 32767 },
            new Vertex { x = -1.0f, y = -1.0f, z = 1.0f, u = 0, v = 32767 },
            new Vertex { x = 1.0f, y = -1.0f, z = -1.0f, u = 32767, v = 0 },
            new Vertex { x = 1.0f, y = 1.0f, z = -1.0f, u = 0, v = 0 },
            new Vertex { x = 1.0f, y = 1.0f, z = 1.0f, u = 0, v = 32767 },
            new Vertex { x = 1.0f, y = -1.0f, z = 1.0f, u = 32767, v = 32767 },
            new Vertex { x = -1.0f, y = -1.0f, z = -1.0f, u = 0, v = 0 },
            new Vertex { x = -1.0f, y = -1.0f, z = 1.0f, u = 32767, v = 0 },
            new Vertex { x = 1.0f, y = -1.0f, z = 1.0f, u = 32767, v = 32767 },
            new Vertex { x = 1.0f, y = -1.0f, z = -1.0f, u = 0, v = 32767 },
            new Vertex { x = -1.0f, y = 1.0f, z = -1.0f, u = 32767, v = 0 },
            new Vertex { x = -1.0f, y = 1.0f, z = 1.0f, u = 0, v = 0 },
            new Vertex { x = 1.0f, y = 1.0f, z = 1.0f, u = 0, v = 32767 },
            new Vertex { x = 1.0f, y = 1.0f, z = -1.0f, u = 32767, v = 32767 }
        };

        state.vbuf = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(vertices),
            label = "cube-vertices"
        });

        ushort[] indices = new ushort[]
        {
            0, 1, 2, 0, 2, 3,
            6, 5, 4, 7, 6, 4,
            8, 9, 10, 8, 10, 11,
            14, 13, 12, 15, 14, 12,
            16, 17, 18, 16, 18, 19,
            22, 21, 20, 23, 22, 20
        };

        state.ibuf = sg_make_buffer(new sg_buffer_desc()
        {
            usage = { index_buffer = true },
            data = SG_RANGE(indices),
            label = "cube-indices"
        });

        sg_pipeline_desc pip_desc = default;
        pip_desc.layout.attrs[ATTR_debugtext_context_pos].format = SG_VERTEXFORMAT_FLOAT3;
        pip_desc.layout.attrs[ATTR_debugtext_context_texcoord0].format = SG_VERTEXFORMAT_SHORT2N;
        pip_desc.shader = sg_make_shader(debugtext_context_shader_desc(sg_query_backend()));
        pip_desc.index_type = SG_INDEXTYPE_UINT16;
        pip_desc.cull_mode = SG_CULLMODE_BACK;
        pip_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pip_desc.depth.write_enabled = true;
        pip_desc.label = "cube-pipeline";
        state.pip = sg_make_pipeline(pip_desc);

        // Create resources for each offscreen-rendered cube face
        for (int i = 0; i < NUM_FACES; i++)
        {
            // Each face gets its separate text context
            state.passes[i].text_context = sdtx_make_context(new sdtx_context_desc_t()
            {
                char_buf_size = 64,
                canvas_width = OFFSCREEN_WIDTH,
                canvas_height = OFFSCREEN_HEIGHT / 2,
                color_format = OFFSCREEN_PIXELFORMAT,
                depth_format = SG_PIXELFORMAT_NONE,
                sample_count = OFFSCREEN_SAMPLE_COUNT
            });

            // The render target image, texture view and pass descriptor
            sg_image img = sg_make_image(new sg_image_desc()
            {
                usage = { color_attachment = true },
                width = OFFSCREEN_WIDTH,
                height = OFFSCREEN_HEIGHT,
                pixel_format = OFFSCREEN_PIXELFORMAT,
                sample_count = OFFSCREEN_SAMPLE_COUNT
            });

            state.passes[i].tex_view = sg_make_view(new sg_view_desc()
            {
                texture = { image = img }
            });

            sg_pass pass = default;
            pass.attachments.colors[0] = sg_make_view(new sg_view_desc()
            {
                color_attachment = { image = img }
            });
            // Each render target is cleared to a different background color
            pass.action.colors[0].load_action = SG_LOADACTION_CLEAR;
            pass.action.colors[0].clear_value = bg[i];
            state.passes[i].pass = pass;
        }

        // Create a sampler for sampling offscreen render targets as texture
        state.smp = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_NEAREST,
            mag_filter = SG_FILTER_NEAREST
        });

        // Default pass action (just keep this default-initialized, which clears to gray)
        state.pass_action = default;
    }

    // Compute the model-view-proj matrix for rendering the rotating cube
    static vs_params_t ComputeVsParams(int w, int h)
    {
        Matrix4x4 proj = CreatePerspectiveFieldOfView((float)(60.0f * Math.PI / 180.0f), (float)w / (float)h, 0.01f, 10.0f);
        Matrix4x4 view = CreateLookAt(new Vector3(0.0f, 1.5f, 4.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f));
        Matrix4x4 view_proj = view * proj;
        Matrix4x4 rxm = CreateRotationX(state.rx * (float)Math.PI / 180.0f);
        Matrix4x4 rym = CreateRotationY(state.ry * (float)Math.PI / 180.0f);
        Matrix4x4 model = rym * rxm;
        return new vs_params_t { mvp = model * view_proj };
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        int disp_width = sapp_width();
        int disp_height = sapp_height();
        float t = (float)(sapp_frame_duration() * 60.0);
        uint frame_count = (uint)sapp_frame_count();
        state.rx += 0.25f * t;
        state.ry += 0.5f * t;
        vs_params_t vs_params = ComputeVsParams(disp_width, disp_height);

        // Text in the main display
        sdtx_set_context(sdtx_default_context());
        sdtx_canvas(disp_width * 0.5f, disp_height * 0.5f);
        sdtx_origin(3, 3);
        sdtx_puts("Hello from main context!\n");
        sdtx_print("Frame count: {0}\n", frame_count);

        // Text in each offscreen render target
        for (int i = 0; i < NUM_FACES; i++)
        {
            sdtx_set_context(state.passes[i].text_context);
            sdtx_origin(1.0f, 0.5f);
            sdtx_font((uint)i);
            sdtx_print("{0:X2}", ((frame_count / 16) + (uint)i) & 0xFF);
        }

        // Rasterize text into offscreen render targets
        for (int i = 0; i < NUM_FACES; i++)
        {
            sg_begin_pass(state.passes[i].pass);
            sdtx_set_context(state.passes[i].text_context);
            sdtx_draw();
            sg_end_pass();
        }

        // Finally render to the default framebuffer
        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });

        // Draw the cube as 6 separate draw calls (because each has its own texture)
        sg_apply_pipeline(state.pip);
        sg_apply_uniforms(UB_vs_params, SG_RANGE(ref vs_params));
        for (int i = 0; i < NUM_FACES; i++)
        {
            sg_bindings bindings = default;
            bindings.vertex_buffers[0] = state.vbuf;
            bindings.index_buffer = state.ibuf;
            bindings.views[VIEW_tex] = state.passes[i].tex_view;
            bindings.samplers[SMP_smp] = state.smp;
            sg_apply_bindings(bindings);
            sg_draw((uint)(i * 6), 6, 1);
        }

        // Draw default-display text
        sdtx_set_context(sdtx_default_context());
        sdtx_draw();

        // Conclude the default pass and frame
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
        sdtx_shutdown();
        __dbgui_shutdown();
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
            sample_count = DISPLAY_SAMPLE_COUNT,
            window_title = "debugtext-context-sapp",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }
}
