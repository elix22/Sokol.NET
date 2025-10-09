using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static System.Numerics.Matrix4x4;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using static Sokol.SApp;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_filter;
using static Sokol.SG.sg_wrap;
using static Sokol.SG.sg_load_action;
using static Sokol.SShape;
using static offscreen_sapp_shader_cs.Shaders;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;


public static unsafe class OffscreenApp
{

    struct Offscreen
    {
        public sg_pass pass;
        public sg_pipeline pip;
        public sg_bindings bind;
    }

    struct Display
    {
        public sg_pass_action pass_action;
        public sg_pipeline pip;
        public sg_bindings bind;
    }

    struct _state
    {
        public Offscreen offscreen;
        public Display display;
        public sshape_element_range_t donut;
        public sshape_element_range_t sphere;
        public float rx, ry;
        public bool PauseUpdate;
    }

    static _state state = default;

     public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 0,  // Let iOS determine the size based on orientation
            height = 0, // Let iOS determine the size based on orientation
            sample_count = 4,
            window_title = "Offscreen (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = new sg_logger()
        });
        __dbgui_setup(sapp_sample_count());

        // default pass action: clear to blue-ish
        state.display.pass_action = default;
        state.display.pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.display.pass_action.colors[0].clear_value = new sg_color() { r = 0.25f, g = 0.45f, b = 0.65f, a = 1.0f };

        // setup a render pass struct with one color and one depth render attachment image
        // NOTE: we need to explicitly set the sample count in the attachment image objects,
        // because the offscreen pass uses a different sample count than the display render pass
        // (the display render pass is multi-sampled, the offscreen pass is not)
        sg_image_desc img_desc = new sg_image_desc()
        {
            usage = { color_attachment = true },
            width = 256,
            height = 256,
            pixel_format = SG_PIXELFORMAT_RGBA8,
            sample_count = 1,
            label = "color-image"
        };

        sg_image color_img = sg_make_image(img_desc);
        img_desc.pixel_format = SG_PIXELFORMAT_DEPTH;
        img_desc.usage = new sg_image_usage() { depth_stencil_attachment = true };
        img_desc.label = "depth-image";
        sg_image depth_img = sg_make_image(img_desc);
        
        // Create offscreen pass with color and depth attachments
        sg_pass pass = default;
        
        // Create color attachment view
        sg_view_desc color_view_desc = default;
        color_view_desc.color_attachment.image = color_img;
        color_view_desc.label = "color-attachment";
        
        // Create depth attachment view
        sg_view_desc depth_view_desc = default;
        depth_view_desc.depth_stencil_attachment.image = depth_img;
        depth_view_desc.label = "depth-attachment";
        
        // Create attachments and assign views directly
        sg_attachments attachments = default;
        attachments.colors[0] = sg_make_view(color_view_desc);
        attachments.depth_stencil = sg_make_view(depth_view_desc);
        pass.attachments = attachments;
        
        // Set up pass action (clear color)
        sg_pass_action action = default;
        action.colors[0].load_action = SG_LOADACTION_CLEAR;
        action.colors[0].clear_value = new sg_color() { r = 0.25f, g = 0.25f, b = 0.25f, a = 1.0f };
        pass.action = action;
        pass.label = "offscreen-pass";

        state.offscreen.pass = pass;

        // a donut shape which is rendered into the offscreen render target, and
        // a sphere shape which is rendered into the default framebuffer
        sshape_vertex_t[] vertices = new sshape_vertex_t[4000];
        UInt16[] indices = new UInt16[24000];

        sshape_buffer_t buf = default;
        buf.vertices.buffer = SSHAPE_RANGE(vertices);
        buf.indices.buffer = SSHAPE_RANGE(indices);
        buf = sshape_build_torus(buf, new sshape_torus_t()
        {
            radius = 0.5f,
            ring_radius = 0.3f,
            sides = 20,
            rings = 36,
        });

        state.donut = sshape_make_element_range(buf);

        buf = sshape_build_sphere(buf, new sshape_sphere_t()
        {
            radius = 0.5f,
            slices = 72,
            stacks = 40
        });

        state.sphere = sshape_make_element_range(buf);

        sg_buffer_desc vbuf_desc = sshape_vertex_buffer_desc(buf);
        sg_buffer_desc ibuf_desc = sshape_index_buffer_desc(buf);
        vbuf_desc.label = "shape-vbuf";
        ibuf_desc.label = "shape-ibuf";
        sg_buffer vbuf = sg_make_buffer(vbuf_desc);
        sg_buffer ibuf = sg_make_buffer(ibuf_desc);

        // pipeline-state-object for offscreen-rendered donut
        // NOTE: we need to explicitly set the sample_count here because
        // the offscreen pass uses a different sample count than the default
        // pass (the display pass is multi-sampled, but the offscreen pass isn't)

        sg_pipeline_desc offscreen_pipeline_desc = default;
        offscreen_pipeline_desc.layout = new sg_vertex_layout_state()
        {
            attrs = {
                [ATTR_offscreen_position] = sshape_position_vertex_attr_state(),
                [ATTR_offscreen_normal] = sshape_normal_vertex_attr_state()
            }
        };
        offscreen_pipeline_desc.layout.buffers[0] = sshape_vertex_buffer_layout_state();
        offscreen_pipeline_desc.shader = sg_make_shader(offscreen_shader_desc(sg_query_backend()));
        offscreen_pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        offscreen_pipeline_desc.cull_mode = SG_CULLMODE_BACK;
        offscreen_pipeline_desc.sample_count = 1;
        offscreen_pipeline_desc.depth = new sg_depth_state()
        {
            pixel_format = SG_PIXELFORMAT_DEPTH,
            compare = SG_COMPAREFUNC_LESS_EQUAL,
            write_enabled = true
        };
        offscreen_pipeline_desc.colors[0].pixel_format = SG_PIXELFORMAT_RGBA8;
        offscreen_pipeline_desc.label = "offscreen-pipeline";
        state.offscreen.pip = sg_make_pipeline(offscreen_pipeline_desc);

        // and another pipeline-state-object for the default pass
        sg_pipeline_desc pipeline_desc = default;
        pipeline_desc.layout = new sg_vertex_layout_state()
        {
            attrs = {
                [ATTR_default_position] = sshape_position_vertex_attr_state(),
                [ATTR_default_normal] = sshape_normal_vertex_attr_state(),
                [ATTR_default_texcoord0] = sshape_texcoord_vertex_attr_state()
            }
        };
        pipeline_desc.layout.buffers[0] = sshape_vertex_buffer_layout_state();
        pipeline_desc.shader = sg_make_shader(default_shader_desc(sg_query_backend()));
        pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        pipeline_desc.cull_mode = SG_CULLMODE_BACK;
        pipeline_desc.depth = new sg_depth_state()
        {
            compare = SG_COMPAREFUNC_LESS_EQUAL,
            write_enabled = true
        };
        pipeline_desc.label = "default-pipeline";
        state.display.pip = sg_make_pipeline(pipeline_desc);

        // a sampler object for sampling the render target texture
        sg_sampler smp = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_LINEAR,
            mag_filter = SG_FILTER_LINEAR,
            wrap_u = SG_WRAP_REPEAT,
            wrap_v = SG_WRAP_REPEAT
        });

        // the resource bindings for rendering a non-textured shape into offscreen render target
        sg_bindings bindings = default;
        bindings.vertex_buffers[0] = vbuf;
        bindings.index_buffer = ibuf;
        state.offscreen.bind = bindings;

        // resource bindings to render a textured shape, using the offscreen render target as texture
        bindings = default;
        bindings.vertex_buffers[0] = vbuf;
        bindings.index_buffer = ibuf;
        bindings.views[VIEW_tex] = sg_make_view(new sg_view_desc
        {
            texture = { image = color_img },
            label = "texture-view",
            });
        bindings.samplers[SMP_smp] = smp;
        state.display.bind = bindings;

    }



    // helper function to compute model-view-projection matrix
    static Matrix4x4 compute_mvp(float rx, float ry, float aspect, float eye_dist)
    {

        var proj = CreatePerspectiveFieldOfView(
             (float)(60.0f * Math.PI / 180),
             aspect,
             0.01f,
             10.0f);
        var view = CreateLookAt(
            new Vector3(0.0f, 0.0f, eye_dist),
            Vector3.Zero,
            Vector3.UnitY);
        var rxm = CreateRotationX(rx);
        var rym = CreateRotationY(ry);
        var model = rym * rxm;
        var mvp = model * view * proj;
        return mvp;
    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        float t = (float)(sapp_frame_duration() );
        state.rx += 1.0f * t;
        state.ry += 2.0f * t;
        vs_params_t vs_params;

        // the offscreen pass, rendering an rotating, untextured donut into a render target image
        vs_params = default;
        vs_params.mvp = compute_mvp(state.rx, state.ry, 1.0f, 2.5f);
        sg_begin_pass(state.offscreen.pass);
        sg_apply_pipeline(state.offscreen.pip);
        sg_apply_bindings(state.offscreen.bind);
        sg_apply_uniforms(UB_vs_params,SG_RANGE<vs_params_t>(ref vs_params));
        sg_draw(state.donut.base_element, state.donut.num_elements, 1);
        sg_end_pass();

        // and the display-pass, rendering a rotating textured sphere which uses the
        // previously rendered offscreen render-target as texture
        int w = sapp_width();
        int h = sapp_height();
        vs_params = default;
        vs_params.mvp = compute_mvp(-state.rx * 0.25f, state.ry * 0.25f, (float)w / (float)h, 1.5f);
        sg_begin_pass(new sg_pass()
        {
            action = state.display.pass_action,
            swapchain = sglue_swapchain(),
            label = "swapchain-pass",
        });
        sg_apply_pipeline(state.display.pip);
        sg_apply_bindings(state.display.bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_draw(state.sphere.base_element, state.sphere.num_elements, 1);
        __dbgui_draw();
        sg_end_pass();
        sg_commit();
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

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_KEY_UP)
        {
            state.PauseUpdate = !state.PauseUpdate;
        }

        __dbgui_event(e);
    }

}