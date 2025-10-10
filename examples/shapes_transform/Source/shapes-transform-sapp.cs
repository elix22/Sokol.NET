
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static System.Numerics.Matrix4x4;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using static Sokol.SApp;
using static Sokol.SShape;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_filter;
using static Sokol.SG.sg_wrap;
using static Sokol.SG.sg_load_action;
using static Sokol.SG.sg_vertex_step;
using static Sokol.SApp.sapp_event_type;
using static Sokol.SApp.sapp_keycode;
using static Sokol.SDebugText;
using System.Diagnostics;
using static shapes_transform_sapp_shader_cs.Shaders;
using static Sokol.SLog;
using static Sokol.SDebugUI;

public static unsafe class ShapesTransformApp
{

    static bool PauseUpdate = false;
    struct _state
    {
        public sg_pass_action pass_action;
        public sg_pipeline pip;
        public sg_bindings bind;
        public sshape_element_range_t elms;
        public vs_params_t vs_params;
        public float rx, ry;
        public bool is_mobile ;
    };

    static _state state = new _state();


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

        sdtx_desc_t desc = new sdtx_desc_t();
        desc.fonts[0] = sdtx_font_oric();
        desc.logger.func =  &slog_func;
        sdtx_setup(desc);

        // clear to black
        state.pass_action = new sg_pass_action();
        state.pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new float[] { 0.0f, 0.0f, 0.0f, 1.0f };

        // shader and pipeline object
        sg_pipeline_desc pip_desc = new sg_pipeline_desc();
        pip_desc.shader = sg_make_shader(shapes_shader_desc(sg_query_backend()));
        pip_desc.layout.buffers[0] = sshape_vertex_buffer_layout_state();
        pip_desc.layout.attrs[0] = sshape_position_vertex_attr_state();
        pip_desc.layout.attrs[1] = sshape_normal_vertex_attr_state();
        pip_desc.layout.attrs[2] = sshape_texcoord_vertex_attr_state();
        pip_desc.layout.attrs[3] = sshape_color_vertex_attr_state();
        pip_desc.index_type = SG_INDEXTYPE_UINT16;
        pip_desc.cull_mode = SG_CULLMODE_NONE;
        pip_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pip_desc.depth.write_enabled = true;
        state.pip = sg_make_pipeline(pip_desc);

        // generate merged shape geometries
        sshape_vertex_t[] vertices = new sshape_vertex_t[6 * 1024];
        ushort[] indices = new ushort[16 * 1024];
        sshape_buffer_t buf = new sshape_buffer_t();
        buf.vertices.buffer = SSHAPE_RANGE(vertices);
        buf.indices.buffer = SSHAPE_RANGE(indices);

        // transform matrices for the shapes
        Matrix4x4 box_transform = Matrix4x4.CreateTranslation(new Vector3(-1.0f, 0.0f, +1.0f));
        Matrix4x4 sphere_transform = Matrix4x4.CreateTranslation(new Vector3(+1.0f, 0.0f, +1.0f));
        Matrix4x4 cylinder_transform = Matrix4x4.CreateTranslation(new Vector3(-1.0f, 0.0f, -1.0f));
        Matrix4x4 torus_transform = Matrix4x4.CreateTranslation(new Vector3(+1.0f, 0.0f, -1.0f));

        // // build the shapes...
        buf = sshape_build_box(in buf, new sshape_box_t()
        {
            width = 1.0f,
            height = 1.0f,
            depth = 1.0f,
            tiles = 10,
            random_colors = true,
            transform = sshape_make_mat4(in box_transform.M11) // using box_transform.AsFloat() won't work on Web
        });

        buf = sshape_build_sphere(in buf, new sshape_sphere_t
        {
            merge = true,
            radius = 0.75f,
            slices = 36,
            stacks = 20,
            random_colors = true,
            transform = sshape_make_mat4(in sphere_transform.M11)
        });
        buf = sshape_build_cylinder(in buf, new sshape_cylinder_t
        {
            merge = true,
            radius = 0.5f,
            height = 1.0f,
            slices = 36,
            stacks = 10,
            random_colors = true,
            transform = sshape_make_mat4(in cylinder_transform.M11)
        });
        buf = sshape_build_torus(in buf, new sshape_torus_t
        {
            merge = true,
            radius = 0.5f,
            ring_radius = 0.3f,
            rings = 36,
            sides = 18,
            random_colors = true,
            transform = sshape_make_mat4(in torus_transform.M11)
        });

        state.elms = sshape_make_element_range(buf);

        // and finally create the vertex- and index-buffer
        sg_buffer_desc vbuf_desc = sshape_vertex_buffer_desc(buf);
        sg_buffer_desc ibuf_desc = sshape_index_buffer_desc(buf);
        state.bind.vertex_buffers[0] = sg_make_buffer(vbuf_desc);
        state.bind.index_buffer = sg_make_buffer(ibuf_desc);

         switch (sg_query_backend())
         {
              
                case sg_backend.SG_BACKEND_GLES3:
                     state.is_mobile = true;
                    break;
                case sg_backend.SG_BACKEND_METAL_IOS:
                    state.is_mobile = true;
                    break;
                default:
                    state.is_mobile = false;
                    break;
         }

    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        if (PauseUpdate) return;

        // help text
        sdtx_canvas(sapp_width() * 0.5f, sapp_height() * 0.5f);
        sdtx_pos(0.5f, 0.5f);
        
        if (state.is_mobile)
        {
            sdtx_puts("touch screen to switch draw modes\n\n");
        }
        else
        {
            sdtx_puts("press key to switch draw mode:\n\n" +
          "  1: vertex normals\n" +
          "  2: texture coords\n" +
          "  3: vertex color\n\n");
        }

        string draw_mode_str =string.Empty;
        switch(state.vs_params.draw_mode)
        {
            case 0.0f:
                draw_mode_str = "vertex normals";
            break;

            case 1.0f:
                draw_mode_str = "texture coords";
            break;         

            case 2.0f:
                draw_mode_str = "vertex color";
            break;     
        }

         sdtx_puts($"draw mode : {draw_mode_str}\n\n");



        // build model-view-projection matrix
        float t = (float)(sapp_frame_duration());
        state.rx += 1.0f * t;
        state.ry += 2.0f * t;
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(60.0f * Math.PI / 180), sapp_widthf() / sapp_heightf(), 0.01f, 10.0f);
        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0.0f, 1.5f, 6.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f));
        Matrix4x4 view_proj = view * proj;
        Matrix4x4 rxm = Matrix4x4.CreateRotationX(state.rx);
        Matrix4x4 rym = Matrix4x4.CreateRotationY(state.ry);
        Matrix4x4 model = rym * rxm;
        state.vs_params.mvp = model*view_proj ;

        // render the single shape
        sg_begin_pass(new sg_pass{ action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pip);
        sg_apply_bindings(state.bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref state.vs_params));
        sg_draw(state.elms.base_element, state.elms.num_elements, 1);

        // render help text and finish frame
        sdtx_draw();
       __dbgui_draw();
        sg_end_pass();
        sg_commit();

 
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

    [UnmanagedCallersOnly]
    private static unsafe void Event(SApp.sapp_event* ev)
    {


        if (ev->type == SAPP_EVENTTYPE_KEY_DOWN)
        {
            switch (ev->key_code)
            {
                case SAPP_KEYCODE_1: state.vs_params.draw_mode = 0.0f; break;
                case SAPP_KEYCODE_2: state.vs_params.draw_mode = 1.0f; break;
                case SAPP_KEYCODE_3: state.vs_params.draw_mode = 2.0f; break;
                default: break;
            }
        }
        else if (ev->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN)
        {
            state.vs_params.draw_mode = (state.vs_params.draw_mode + 1) % 3;
        }

        __dbgui_event(ev);
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 0,
            height = 0,
            sample_count = 1,
            window_title = "shapes-transform (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}