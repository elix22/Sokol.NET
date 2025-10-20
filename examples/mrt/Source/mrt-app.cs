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
using static Sokol.SG.sg_filter;
using static Sokol.SG.sg_wrap;
using static Sokol.SG.sg_load_action;
using static Sokol.SG.sg_store_action;
using static Sokol.SG.sg_primitive_type;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using static mrt_sapp_shader_cs.Shaders;

public static unsafe class MRTApp
{
    const int OFFSCREEN_SAMPLE_COUNT = 4;
    const int NUM_MRTS = 3;

    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    {
        public float x, y, z, b;
    }

    class State
    {
        public class OffscreenState
        {
            public sg_pipeline pip;
            public sg_bindings bind;
            public sg_pass pass;
        }

        public class DisplayState
        {
            public sg_pipeline pip;
            public sg_bindings bind;
            public sg_pass_action pass_action;
        }

        public class DebugState
        {
            public sg_pipeline pip;
            public sg_bindings bind;
        }

        public class ImagesState
        {
            public sg_image[] color = new sg_image[NUM_MRTS];
            public sg_image[] resolve = new sg_image[NUM_MRTS];
            public sg_image depth;
        }

        public OffscreenState offscreen = new OffscreenState();
        public DisplayState display = new DisplayState();
        public DebugState dbg = new DebugState();
        public ImagesState images = new ImagesState();
        public float rx, ry;
    }

    static State state = new State();

    static void ReinitAttachments(int width, int height)
    {
        // Uninitialize the render target images and associated views
        for (int i = 0; i < NUM_MRTS; i++)
        {
            sg_uninit_image(state.images.color[i]);
            sg_uninit_image(state.images.resolve[i]);
            sg_uninit_view(state.offscreen.pass.attachments.colors[i]);
            sg_uninit_view(state.offscreen.pass.attachments.resolves[i]);
            sg_uninit_view(state.display.bind.views[VIEW_tex0 + i]);
        }
        sg_uninit_image(state.images.depth);
        sg_uninit_view(state.offscreen.pass.attachments.depth_stencil);

        // Initialize images with the new size and re-init their associated handles
        string[] msaa_image_labels = { "msaa-image-red", "msaa-image-green", "msaa-image-blue" };
        string[] resolve_image_labels = { "resolve-image-red", "resolve-image-green", "resolve-image-blue" };
        string[] color_attachment_labels = { "color-attachment-red", "color-attachment-green", "color-attachment-blue" };
        string[] resolve_attachment_labels = { "resolve-attachment-red", "resolve-attachment-green", "resolve-attachment-blue" };
        string[] tex_view_labels = { "texture-view-red", "texture-view-green", "texture-view-blue" };

        for (int i = 0; i < NUM_MRTS; i++)
        {
            sg_init_image(state.images.color[i], new sg_image_desc()
            {
                usage = { color_attachment = true },
                width = width,
                height = height,
                pixel_format = sglue_environment().defaults.color_format,
                sample_count = OFFSCREEN_SAMPLE_COUNT,
                label = msaa_image_labels[i]
            });

            sg_init_image(state.images.resolve[i], new sg_image_desc()
            {
                usage = { resolve_attachment = true },
                width = width,
                height = height,
                pixel_format = sglue_environment().defaults.color_format,
                sample_count = 1,
                label = resolve_image_labels[i]
            });

            sg_init_view(state.offscreen.pass.attachments.colors[i], new sg_view_desc()
            {
                color_attachment = { image = state.images.color[i] },
                label = color_attachment_labels[i]
            });

            sg_init_view(state.offscreen.pass.attachments.resolves[i], new sg_view_desc()
            {
                resolve_attachment = { image = state.images.resolve[i] },
                label = resolve_attachment_labels[i]
            });

            sg_init_view(state.display.bind.views[VIEW_tex0 + i], new sg_view_desc()
            {
                texture = { image = state.images.resolve[i] },
                label = tex_view_labels[i]
            });
        }

        sg_init_image(state.images.depth, new sg_image_desc()
        {
            usage = { depth_stencil_attachment = true },
            width = width,
            height = height,
            pixel_format = SG_PIXELFORMAT_DEPTH,
            sample_count = OFFSCREEN_SAMPLE_COUNT,
            label = "depth-image"
        });

        sg_init_view(state.offscreen.pass.attachments.depth_stencil, new sg_view_desc()
        {
            depth_stencil_attachment = { image = state.images.depth },
            label = "depth-attachment"
        });
    }

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });
        __dbgui_setup(sapp_sample_count());


        // A pass action for the default render pass
        state.display.pass_action = default;
        state.display.pass_action.colors[0].load_action = SG_LOADACTION_DONTCARE;

        // Pre-allocate all image and view handles upfront
        for (int i = 0; i < NUM_MRTS; i++)
        {
            state.images.color[i] = sg_alloc_image();
            state.images.resolve[i] = sg_alloc_image();
            state.offscreen.pass.attachments.colors[i] = sg_alloc_view();
            state.offscreen.pass.attachments.resolves[i] = sg_alloc_view();
            state.display.bind.views[VIEW_tex0 + i] = sg_alloc_view();
        }
        state.images.depth = sg_alloc_image();
        state.offscreen.pass.attachments.depth_stencil = sg_alloc_view();

        // Initialize pass attachment images and views
        ReinitAttachments(sapp_width(), sapp_height());

        // Create a vertex buffer for the cube
        Vertex[] cube_vertices = {
            // pos + brightness
            new Vertex { x = -1.0f, y = -1.0f, z = -1.0f, b = 1.0f },
            new Vertex { x =  1.0f, y = -1.0f, z = -1.0f, b = 1.0f },
            new Vertex { x =  1.0f, y =  1.0f, z = -1.0f, b = 1.0f },
            new Vertex { x = -1.0f, y =  1.0f, z = -1.0f, b = 1.0f },

            new Vertex { x = -1.0f, y = -1.0f, z =  1.0f, b = 0.8f },
            new Vertex { x =  1.0f, y = -1.0f, z =  1.0f, b = 0.8f },
            new Vertex { x =  1.0f, y =  1.0f, z =  1.0f, b = 0.8f },
            new Vertex { x = -1.0f, y =  1.0f, z =  1.0f, b = 0.8f },

            new Vertex { x = -1.0f, y = -1.0f, z = -1.0f, b = 0.6f },
            new Vertex { x = -1.0f, y =  1.0f, z = -1.0f, b = 0.6f },
            new Vertex { x = -1.0f, y =  1.0f, z =  1.0f, b = 0.6f },
            new Vertex { x = -1.0f, y = -1.0f, z =  1.0f, b = 0.6f },

            new Vertex { x =  1.0f, y = -1.0f, z = -1.0f, b = 0.4f },
            new Vertex { x =  1.0f, y =  1.0f, z = -1.0f, b = 0.4f },
            new Vertex { x =  1.0f, y =  1.0f, z =  1.0f, b = 0.4f },
            new Vertex { x =  1.0f, y = -1.0f, z =  1.0f, b = 0.4f },

            new Vertex { x = -1.0f, y = -1.0f, z = -1.0f, b = 0.5f },
            new Vertex { x = -1.0f, y = -1.0f, z =  1.0f, b = 0.5f },
            new Vertex { x =  1.0f, y = -1.0f, z =  1.0f, b = 0.5f },
            new Vertex { x =  1.0f, y = -1.0f, z = -1.0f, b = 0.5f },

            new Vertex { x = -1.0f, y =  1.0f, z = -1.0f, b = 0.7f },
            new Vertex { x = -1.0f, y =  1.0f, z =  1.0f, b = 0.7f },
            new Vertex { x =  1.0f, y =  1.0f, z =  1.0f, b = 0.7f },
            new Vertex { x =  1.0f, y =  1.0f, z = -1.0f, b = 0.7f }
        };

        state.offscreen.bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(cube_vertices),
            label = "cube vertices"
        });

        // And an index buffer for the cube
        ushort[] cube_indices = {
            0, 1, 2,  0, 2, 3,
            6, 5, 4,  7, 6, 4,
            8, 9, 10,  8, 10, 11,
            14, 13, 12,  15, 14, 12,
            16, 17, 18,  16, 18, 19,
            22, 21, 20,  23, 22, 20
        };

        state.offscreen.bind.index_buffer = sg_make_buffer(new sg_buffer_desc()
        {
            usage = { index_buffer = true },
            data = SG_RANGE(cube_indices),
            label = "cube indices"
        });

        // Pipeline and shader object for the offscreen-rendered cube
        sg_pipeline_desc pip_desc = default;
        pip_desc.shader = sg_make_shader(offscreen_shader_desc(sg_query_backend()));
        pip_desc.layout.buffers[0].stride = sizeof(Vertex);
        pip_desc.layout.attrs[ATTR_offscreen_pos].offset = 0;  // offsetof(Vertex, x)
        pip_desc.layout.attrs[ATTR_offscreen_pos].format = SG_VERTEXFORMAT_FLOAT3;
        pip_desc.layout.attrs[ATTR_offscreen_bright0].offset = sizeof(float) * 3;  // offsetof(Vertex, b)
        pip_desc.layout.attrs[ATTR_offscreen_bright0].format = SG_VERTEXFORMAT_FLOAT;
        pip_desc.index_type = SG_INDEXTYPE_UINT16;
        pip_desc.cull_mode = SG_CULLMODE_BACK;
        pip_desc.sample_count = OFFSCREEN_SAMPLE_COUNT;
        pip_desc.depth.pixel_format = SG_PIXELFORMAT_DEPTH;
        pip_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pip_desc.depth.write_enabled = true;
        pip_desc.color_count = NUM_MRTS;
        for (int i = 0; i < NUM_MRTS; i++)
        {
            pip_desc.colors[i].pixel_format = sglue_environment().defaults.color_format;
        }
        pip_desc.label = "offscreen pipeline";
        state.offscreen.pip = sg_make_pipeline(pip_desc);

        // A pass action for the offscreen pass
        sg_pass_action offscreen_action = default;
        offscreen_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        offscreen_action.colors[0].store_action = SG_STOREACTION_DONTCARE;
        offscreen_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.0f, b = 0.0f, a = 1.0f };

        offscreen_action.colors[1].load_action = SG_LOADACTION_CLEAR;
        offscreen_action.colors[1].store_action = SG_STOREACTION_DONTCARE;
        offscreen_action.colors[1].clear_value = new sg_color { r = 0.0f, g = 0.25f, b = 0.0f, a = 1.0f };

        offscreen_action.colors[2].load_action = SG_LOADACTION_CLEAR;
        offscreen_action.colors[2].store_action = SG_STOREACTION_DONTCARE;
        offscreen_action.colors[2].clear_value = new sg_color { r = 0.0f, g = 0.0f, b = 0.25f, a = 1.0f };

        state.offscreen.pass.action = offscreen_action;
        state.offscreen.pass.label = "offscreen-pass";

        // A vertex buffer to render a fullscreen rectangle
        float[] quad_vertices = { 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 1.0f, 1.0f };
        sg_buffer quad_vbuf = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(quad_vertices),
            label = "quad vertices"
        });

        // A pipeline and shader object to render the fullscreen quad
        sg_pipeline_desc display_pip_desc = default;
        display_pip_desc.shader = sg_make_shader(fsq_shader_desc(sg_query_backend()));
        display_pip_desc.layout = new sg_vertex_layout_state();
        display_pip_desc.layout.attrs[ATTR_fsq_pos].format = SG_VERTEXFORMAT_FLOAT2;
        display_pip_desc.primitive_type = SG_PRIMITIVETYPE_TRIANGLE_STRIP;
        display_pip_desc.label = "fullscreen quad pipeline";
        state.display.pip = sg_make_pipeline(display_pip_desc);

        // A sampler object to sample the offscreen render targets as textures
        sg_sampler smp = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_LINEAR,
            mag_filter = SG_FILTER_LINEAR,
            wrap_u = SG_WRAP_CLAMP_TO_EDGE,
            wrap_v = SG_WRAP_CLAMP_TO_EDGE,
            label = "sampler"
        });

        // Complete the resource bindings for the fullscreen quad
        state.display.bind.vertex_buffers[0] = quad_vbuf;
        state.display.bind.samplers[SMP_smp] = smp;
        // Note: texture views are set in ReinitAttachments() after image initialization

        // Pipeline and resource bindings to render debug-visualization quads
        sg_pipeline_desc dbg_pip_desc = default;
        dbg_pip_desc.shader = sg_make_shader(dbg_shader_desc(sg_query_backend()));
        dbg_pip_desc.layout = new sg_vertex_layout_state();
        dbg_pip_desc.layout.attrs[ATTR_dbg_pos].format = SG_VERTEXFORMAT_FLOAT2;
        dbg_pip_desc.primitive_type = SG_PRIMITIVETYPE_TRIANGLE_STRIP;
        dbg_pip_desc.label = "dbgvis quad pipeline";
        state.dbg.pip = sg_make_pipeline(dbg_pip_desc);

        state.dbg.bind.vertex_buffers[0] = quad_vbuf;
        state.dbg.bind.samplers[SMP_smp] = smp;
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // View-projection matrix
        float aspect = sapp_widthf() / sapp_heightf();
        Matrix4x4 proj = CreatePerspectiveFieldOfView((float)(60.0f * Math.PI / 180.0f), aspect, 0.01f, 10.0f);
        Matrix4x4 view = CreateLookAt(new Vector3(0.0f, 1.5f, 4.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f));
        Matrix4x4 view_proj = view * proj;

        // Shader parameters
        float t = (float)(sapp_frame_duration() * 60.0);
        state.rx += 1.0f * t;
        state.ry += 2.0f * t;
        Matrix4x4 rxm = CreateRotationX(state.rx * (float)Math.PI / 180.0f);
        Matrix4x4 rym = CreateRotationY(state.ry * (float)Math.PI / 180.0f);
        Matrix4x4 model = rym * rxm;
        offscreen_params_t offscreen_params = new offscreen_params_t { mvp = model * view_proj };
        fsq_params_t fsq_params = new fsq_params_t { 
            offset = new Vector2(
                (float)Math.Sin(state.rx * 0.01f) * 0.1f,
                (float)Math.Sin(state.ry * 0.01f) * 0.1f
            )
        };

        // Render cube into MRT offscreen render targets
        sg_begin_pass(state.offscreen.pass);
        sg_apply_pipeline(state.offscreen.pip);
        sg_apply_bindings(state.offscreen.bind);
        sg_apply_uniforms(UB_offscreen_params, SG_RANGE(ref offscreen_params));
        sg_draw(0, 36, 1);
        sg_end_pass();

        // Render fullscreen quad with the 'composed image', plus 3 small debug-view quads
        sg_pass display_pass = default;
        display_pass.action = state.display.pass_action;
        display_pass.swapchain = sglue_swapchain();

        sg_begin_pass(display_pass);
        sg_apply_pipeline(state.display.pip);
        sg_apply_bindings(state.display.bind);
        sg_apply_uniforms(UB_fsq_params, SG_RANGE(ref fsq_params));
        sg_draw(0, 4, 1);

        // Render debug visualization quads
        sg_apply_pipeline(state.dbg.pip);
        for (int i = 0; i < NUM_MRTS; i++)
        {
            sg_apply_viewport(i * 100, 0, 100, 100, false);
            state.dbg.bind.views[VIEW_tex] = state.display.bind.views[VIEW_tex0 + i];
            sg_apply_bindings(state.dbg.bind);
            sg_draw(0, 4, 1);
        }

        sg_apply_viewport(0, 0, sapp_width(), sapp_height(), false);
        __dbgui_draw();
        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_RESIZED)
        {
            ReinitAttachments(sapp_width(), sapp_height());
        }
        __dbgui_event(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
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
            sample_count = 4,
            window_title = "MRT (sokol-app)",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }
}
