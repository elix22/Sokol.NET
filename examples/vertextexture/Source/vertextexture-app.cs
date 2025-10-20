using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static System.Numerics.Matrix4x4;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_load_action;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_filter;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static vertextexture_sapp_shader_cs.Shaders;

public static unsafe class VertexTextureSapp
{
    // Plane number of tiles along edge (don't change this since the value is hardcoded in shader)
    const int NUM_TILES_ALONG_EDGE = 255;

    class State
    {
        public double time;
        public float ry;
        
        // Offscreen pass state
        public class OffscreenState
        {
            public sg_image img;
            public sg_pipeline pip;
            public sg_pass pass;
            public plasma_params_t plasma_params;
        }
        
        // Display pass state
        public class DisplayState
        {
            public sg_buffer ibuf;
            public sg_pipeline pip;
            public sg_pass_action pass_action;
            public sg_bindings bind;
        }
        
        public OffscreenState offscreen = new OffscreenState();
        public DisplayState display = new DisplayState();
    }

    static State state = new State();

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });

        // Render target texture for GPU-rendered plasma
        state.offscreen.img = sg_make_image(new sg_image_desc()
        {
            usage = { color_attachment = true },
            width = 256,
            height = 256,
            pixel_format = SG_PIXELFORMAT_RGBA8,
            sample_count = 1,
            label = "plasma-texture"
        });

        // Render-pass attachments and pass action for the offscreen render pass
        sg_pass pass = default;
        
        // Create pass action
        sg_pass_action action = default;
        action.colors[0].load_action = SG_LOADACTION_DONTCARE;
        pass.action = action;
        
        // Create attachments
        sg_attachments attachments = default;
        attachments.colors[0] = sg_make_view(new sg_view_desc()
        {
            color_attachment = { image = state.offscreen.img },
            label = "plasma-texture-attachment"
        });
        pass.attachments = attachments;
        pass.label = "offscreen-pass";
        
        state.offscreen.pass = pass;

        // Pipeline object for offscreen rendering
        // Vertices will be synthesized in the vertex shader, so no vertex buffer needed
        sg_pipeline_desc pip_desc = default;
        pip_desc.shader = sg_make_shader(plasma_shader_desc(sg_query_backend()));
        pip_desc.colors[0].pixel_format = SG_PIXELFORMAT_RGBA8;
        pip_desc.depth.pixel_format = SG_PIXELFORMAT_NONE;
        pip_desc.sample_count = 1;
        pip_desc.label = "plasma-pipeline";
        state.offscreen.pip = sg_make_pipeline(pip_desc);

        // An index buffer with triangle indices for a 256x256 plane
        {
            int tiles = NUM_TILES_ALONG_EDGE;
            int ibuf_size = tiles * tiles * 6 * sizeof(ushort);
            ushort[] indices = new ushort[tiles * tiles * 6];
            int idx = 0;
            
            for (int y = 0; y < tiles; y++)
            {
                for (int x = 0; x < tiles; x++)
                {
                    ushort v00 = (ushort)(y * (tiles + 1) + x);
                    ushort v10 = (ushort)(v00 + 1);
                    ushort v01 = (ushort)(v00 + tiles + 1);
                    ushort v11 = (ushort)(v01 + 1);
                    
                    indices[idx++] = v00;
                    indices[idx++] = v10;
                    indices[idx++] = v01;
                    indices[idx++] = v10;
                    indices[idx++] = v11;
                    indices[idx++] = v01;
                }
            }
            
            state.display.ibuf = sg_make_buffer(new sg_buffer_desc()
            {
                usage = { index_buffer = true },
                data = SG_RANGE(indices),
                label = "plane-indices"
            });
        }

        // A pipeline object for rendering the vertex-displaced plane
        // Again, vertices will be synthesized in the shader so no vertex buffer needed
        state.display.pip = sg_make_pipeline(new sg_pipeline_desc()
        {
            shader = sg_make_shader(display_shader_desc(sg_query_backend())),
            index_type = SG_INDEXTYPE_UINT16,
            cull_mode = SG_CULLMODE_NONE,
            depth = new sg_depth_state()
            {
                compare = SG_COMPAREFUNC_LESS_EQUAL,
                write_enabled = true
            },
            label = "render-pipeline"
        });

        // Display pass action (clear to black)
        state.display.pass_action = default;
        state.display.pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.display.pass_action.colors[0].clear_value = new sg_color { r = 0, g = 0, b = 0, a = 1 };

        // A sampler for accessing the render target as texture
        sg_sampler smp = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_NEAREST,
            mag_filter = SG_FILTER_NEAREST,
            label = "plasma-sampler"
        });

        // Bindings for the display-pass
        state.display.bind = default;
        state.display.bind.index_buffer = state.display.ibuf;
        state.display.bind.views[VIEW_tex] = sg_make_view(new sg_view_desc()
        {
            texture = { image = state.offscreen.img },
            label = "plasma-texture-view"
        });
        state.display.bind.samplers[SMP_smp] = smp;
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        state.offscreen.plasma_params.time += (float)sapp_frame_duration();

        // Offscreen pass to render plasma
        // This renders a fullscreen triangle with vertices synthesized in the vertex shader
        sg_begin_pass(state.offscreen.pass);
        sg_apply_pipeline(state.offscreen.pip);
        sg_apply_uniforms(UB_plasma_params, SG_RANGE(ref state.offscreen.plasma_params));
        sg_draw(0, 3, 1);
        sg_end_pass();

        // Display pass to render vertex-displaced plane
        uint num_elements = (uint)(NUM_TILES_ALONG_EDGE * NUM_TILES_ALONG_EDGE * 6);
        vs_params_t vs_params = ComputeVsParams();
        
        sg_pass display_pass = default;
        display_pass.action = state.display.pass_action;
        display_pass.swapchain = sglue_swapchain();
        
        sg_begin_pass(display_pass);
        sg_apply_pipeline(state.display.pip);
        sg_apply_bindings(state.display.bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE(ref vs_params));
        sg_draw(0, num_elements, 1);
        sg_end_pass();
        sg_commit();
    }

    // Compute the model-view-projection matrix used in the display pass
    static vs_params_t ComputeVsParams()
    {
        float w = sapp_widthf();
        float h = sapp_heightf();
        float t = (float)(sapp_frame_duration() * 60.0);
        
        Matrix4x4 proj = CreatePerspectiveFieldOfView((float)(60.0f * Math.PI / 180.0f), w / h, 0.01f, 10.0f);
        Matrix4x4 view = CreateLookAt(new Vector3(0.0f, 1.0f, 2.5f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f));
        Matrix4x4 view_proj = view * proj;
        
        state.ry += 0.5f * t;
        Matrix4x4 model = CreateRotationY(state.ry * (float)Math.PI / 180.0f);
        
        return new vs_params_t { mvp = model * view_proj };
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        // No event handling needed
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
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
            window_title = "Vertex Texture (sokol-app)",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }
}
