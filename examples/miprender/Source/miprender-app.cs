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
using static Sokol.Utils;
using static Sokol.SShape;
using static miprender_sapp_shader_cs.Shaders;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;

public static unsafe class MipRenderApp
{
    const int IMG_WIDTH = 512;
    const int IMG_HEIGHT = 512;
    const int IMG_NUM_MIPMAPS = 9;

    const int SHAPE_BOX = 0;
    const int SHAPE_DONUT = 1;
    const int SHAPE_SPHERE = 2;
    const int NUM_SHAPES = 3;

    struct AttachmentViews
    {
        public sg_view color;
        public sg_view depth;
    }

    struct _state
    {
        public float rx, ry;
        public double time;
        public sg_buffer vbuf;
        public sg_buffer ibuf;
        public sg_view tex_view;
        public sg_sampler smp;
        
        // Offscreen
        public sg_pipeline offscreen_pip;
        public sg_pass_action offscreen_pass_action;
        public sg_bindings offscreen_bindings;
        public AttachmentViews[] offscreen_att_views;
        public sshape_element_range_t[] offscreen_shapes;
        
        // Display
        public sg_pipeline display_pip;
        public sg_pass_action display_pass_action;
        public sg_bindings display_bindings;
        public sshape_element_range_t display_plane;
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
            width = 800,
            height = 600,
            sample_count = 1,
            window_title = "MipRender (sokol-app)",
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
            logger = {
                func = &slog_func,
            }
        });
        __dbgui_setup(sapp_sample_count());

        // Initialize arrays
        state.offscreen_att_views = new AttachmentViews[IMG_NUM_MIPMAPS];
        state.offscreen_shapes = new sshape_element_range_t[NUM_SHAPES];

        // setup a couple of shape geometries
        sshape_vertex_t[] vertices = new sshape_vertex_t[4 * 1024];
        ushort[] indices = new ushort[12 * 1024];
        sshape_buffer_t buf = default;
        buf.vertices.buffer = SSHAPE_RANGE(vertices);
        buf.indices.buffer = SSHAPE_RANGE(indices);

        buf = sshape_build_box(buf, new sshape_box_t() { width = 1.5f, height = 1.5f, depth = 1.5f });
        state.offscreen_shapes[SHAPE_BOX] = sshape_make_element_range(buf);

        buf = sshape_build_torus(buf, new sshape_torus_t() { radius = 1.0f, ring_radius = 0.3f, rings = 36, sides = 18 });
        state.offscreen_shapes[SHAPE_DONUT] = sshape_make_element_range(buf);

        buf = sshape_build_sphere(buf, new sshape_sphere_t() { radius = 1.0f, slices = 36, stacks = 20 });
        state.offscreen_shapes[SHAPE_SPHERE] = sshape_make_element_range(buf);

        buf = sshape_build_plane(buf, new sshape_plane_t() { width = 2.0f, depth = 2.0f });
        state.display_plane = sshape_make_element_range(buf);

        // create one vertex- and one index-buffer for all shapes
        sg_buffer_desc vbuf_desc = sshape_vertex_buffer_desc(buf);
        vbuf_desc.label = "shape-vertices";
        sg_buffer_desc ibuf_desc = sshape_index_buffer_desc(buf);
        ibuf_desc.label = "shape-indices";
        state.vbuf = sg_make_buffer(vbuf_desc);
        state.ibuf = sg_make_buffer(ibuf_desc);

        // create an offscreen render target with a complete mipmap chain
        sg_image color_img = sg_make_image(new sg_image_desc()
        {
            usage = { color_attachment = true },
            width = IMG_WIDTH,
            height = IMG_HEIGHT,
            num_mipmaps = IMG_NUM_MIPMAPS,
            pixel_format = SG_PIXELFORMAT_RGBA8,
            sample_count = 1,
            label = "color-image"
        });

        // we also need a matching depth buffer image
        sg_image depth_img = sg_make_image(new sg_image_desc()
        {
            usage = { depth_stencil_attachment = true },
            width = IMG_WIDTH,
            height = IMG_HEIGHT,
            num_mipmaps = IMG_NUM_MIPMAPS,
            pixel_format = SG_PIXELFORMAT_DEPTH,
            sample_count = 1,
            label = "depth-image"
        });

        // create a sampler which smoothly blends between mipmaps
        state.smp = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_LINEAR,
            mag_filter = SG_FILTER_LINEAR,
            mipmap_filter = SG_FILTER_LINEAR,
            wrap_u = SG_WRAP_CLAMP_TO_EDGE,
            wrap_v = SG_WRAP_CLAMP_TO_EDGE,
            label = "sampler"
        });

        // create a single texture view for the color attachment image
        state.tex_view = sg_make_view(new sg_view_desc()
        {
            texture = { image = color_img },
            label = "color-texture-view"
        });

        // create pass attachment views for each miplevel
        for (int mip_level = 0; mip_level < IMG_NUM_MIPMAPS; mip_level++)
        {
            state.offscreen_att_views[mip_level].color = sg_make_view(new sg_view_desc()
            {
                color_attachment = { image = color_img, mip_level = mip_level },
                label = $"color-attachment-mip-{mip_level}"
            });
            state.offscreen_att_views[mip_level].depth = sg_make_view(new sg_view_desc()
            {
                depth_stencil_attachment = { image = depth_img, mip_level = mip_level },
                label = $"depth-attachment-mip-{mip_level}"
            });
        }

        // a pipeline object for the offscreen passes
        sg_pipeline_desc offscreen_pip_desc = new sg_pipeline_desc()
        {
            layout =
            {
                buffers = { [0] = sshape_vertex_buffer_layout_state() },
                attrs = {
                    [ATTR_offscreen_in_pos] = sshape_position_vertex_attr_state(),
                    [ATTR_offscreen_in_nrm] = sshape_normal_vertex_attr_state()
                }
            },
            shader = sg_make_shader(offscreen_shader_desc(sg_query_backend())),
            index_type = SG_INDEXTYPE_UINT16,
            cull_mode = SG_CULLMODE_BACK,
            sample_count = 1,
            depth = new sg_depth_state()
            {
                write_enabled = true,
                compare = SG_COMPAREFUNC_LESS_EQUAL,
                pixel_format = SG_PIXELFORMAT_DEPTH
            },
            label = "offscreen-pipeline"
        };
        offscreen_pip_desc.colors[0].pixel_format = SG_PIXELFORMAT_RGBA8;
        state.offscreen_pip = sg_make_pipeline(offscreen_pip_desc);

        // ...and a pipeline object for the display pass
        sg_pipeline_desc display_pip_desc = new sg_pipeline_desc()
        {
            layout =
            {
                buffers = { [0] = sshape_vertex_buffer_layout_state() },
                attrs = {
                    [ATTR_display_in_pos] = sshape_position_vertex_attr_state(),
                    [ATTR_display_in_uv] = sshape_texcoord_vertex_attr_state()
                }
            },
            shader = sg_make_shader(display_shader_desc(sg_query_backend())),
            index_type = SG_INDEXTYPE_UINT16,
            cull_mode = SG_CULLMODE_NONE,
            depth = new sg_depth_state()
            {
                write_enabled = true,
                compare = SG_COMPAREFUNC_LESS_EQUAL
            },
            label = "display-pipeline"
        };

        state.display_pip = sg_make_pipeline(display_pip_desc);

        // initialize resource bindings
        state.offscreen_bindings = new sg_bindings()
        {
            vertex_buffers = { [0] = state.vbuf },
            index_buffer = state.ibuf
        };
        state.display_bindings = new sg_bindings()
        {
            vertex_buffers = { [0] = state.vbuf },
            index_buffer = state.ibuf,
            views = { [VIEW_tex] = state.tex_view },
            samplers = { [SMP_smp] = state.smp }
        };

        // initialize pass actions
        state.offscreen_pass_action = default;
        state.offscreen_pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.offscreen_pass_action.colors[0].clear_value = new sg_color { r = 0.5f, g = 0.5f, b = 0.5f, a = 1.0f };
        
        state.display_pass_action = default;
        state.display_pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.display_pass_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        double dt = sapp_frame_duration();
        state.time += dt;
        state.rx += (float)(dt * 20.0f);
        state.ry += (float)(dt * 40.0f);

        vs_params_t offscreen_vsparams = compute_offscreen_vsparams();
        vs_params_t display_vsparams = compute_display_vsparams();

        // render different shapes into each mipmap level
        for (int i = 0; i < IMG_NUM_MIPMAPS; i++)
        {
            sg_begin_pass(new sg_pass()
            {
                action = state.offscreen_pass_action,
                attachments = new sg_attachments()
                {
                    colors = { [0] = state.offscreen_att_views[i].color },
                    depth_stencil = state.offscreen_att_views[i].depth
                }
            });
            sg_apply_pipeline(state.offscreen_pip);
            sg_apply_bindings(state.offscreen_bindings);
            sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref offscreen_vsparams));
            
            int shape_idx = i % NUM_SHAPES;
            sshape_element_range_t shape = state.offscreen_shapes[shape_idx];
            sg_draw((uint)shape.base_element, (uint)shape.num_elements, 1);
            sg_end_pass();
        }

        // default pass: render a textured plane that moves back and forth to use different mipmap levels
        sg_begin_pass(new sg_pass() { action = state.display_pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.display_pip);
        sg_apply_bindings(state.display_bindings);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref display_vsparams));
        sg_draw((uint)state.display_plane.base_element, (uint)state.display_plane.num_elements, 1);
        __dbgui_draw();
        sg_end_pass();
        sg_commit();
    }

    // compute a model-view-projection matrix for offscreen rendering (aspect ratio 1:1)
    static vs_params_t compute_offscreen_vsparams()
    {
        var proj = CreatePerspectiveFieldOfView((float)(60.0f * Math.PI / 180.0f), 1.0f, 0.01f, 10.0f);
        var view = CreateLookAt(new Vector3(0.0f, 0.0f, 3.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f));
        var view_proj = view * proj;
        var rxm = CreateRotationX((float)(state.rx * Math.PI / 180.0f));
        var rym = CreateRotationZ((float)(state.ry * Math.PI / 180.0f));
        var model = rym * rxm;
        return new vs_params_t() { mvp = model * view_proj };
    }

    // compute a model-view-projection matrix with display aspect ratio
    static vs_params_t compute_display_vsparams()
    {
        float w = sapp_widthf();
        float h = sapp_heightf();
        float scale = ((float)Math.Sin(state.time) + 1.0f) * 0.5f;
        var proj = CreatePerspectiveFieldOfView((float)(40.0f * Math.PI / 180.0f), w / h, 0.01f, 10.0f);
        var view = CreateLookAt(new Vector3(0.0f, 0.0f, 2.5f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f));
        var view_proj = view * proj;
        var model = CreateRotationX((float)(90.0f * Math.PI / 180.0f)) * CreateScale(scale, scale, 1.0f);
        return new vs_params_t() { mvp = model * view_proj };
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        __dbgui_event(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        __dbgui_shutdown();
        sg_shutdown();

        // Force a complete shutdown if debugging
        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }
}
