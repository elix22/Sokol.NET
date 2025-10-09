
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static System.Numerics.Matrix4x4;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using static Sokol.SApp;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_filter;
using static Sokol.SG.sg_wrap;
using static Sokol.SG.sg_load_action;
using static Sokol.SG.sg_primitive_type;
using static shadows_sapp_shader_cs.Shaders;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;

public static unsafe class ShadowsApp
{
    struct shadow
    {
        public sg_pass pass;
        public sg_pipeline pip;
        public sg_bindings bind;
    }

    struct display
    {
        public sg_pass_action pass_action;
        public sg_pipeline pip;
        public sg_bindings bind;
    }

    struct dbg
    {
        public sg_pipeline pip;
        public sg_bindings bind;
    }

    static class state
    {
        public static sg_buffer vbuf;
        public static sg_buffer ibuf;
        public static float ry;
        public static shadow shadow;
        public static display display;
        public static dbg dbg;
    }

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
            window_title = "Shadows (sokol-app)",
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

        // vertex buffer for a cube and plane
        float[] scene_vertices = {
            // pos                  normals
            -1.0f, -1.0f, -1.0f,    0.0f, 0.0f, -1.0f,  //CUBE BACK FACE
            1.0f, -1.0f, -1.0f,    0.0f, 0.0f, -1.0f,
            1.0f,  1.0f, -1.0f,    0.0f, 0.0f, -1.0f,
            -1.0f,  1.0f, -1.0f,    0.0f, 0.0f, -1.0f,

            -1.0f, -1.0f,  1.0f,    0.0f, 0.0f, 1.0f,   //CUBE FRONT FACE
            1.0f, -1.0f,  1.0f,    0.0f, 0.0f, 1.0f,
            1.0f,  1.0f,  1.0f,    0.0f, 0.0f, 1.0f,
            -1.0f,  1.0f,  1.0f,    0.0f, 0.0f, 1.0f,

            -1.0f, -1.0f, -1.0f,    -1.0f, 0.0f, 0.0f,  //CUBE LEFT FACE
            -1.0f,  1.0f, -1.0f,    -1.0f, 0.0f, 0.0f,
            -1.0f,  1.0f,  1.0f,    -1.0f, 0.0f, 0.0f,
            -1.0f, -1.0f,  1.0f,    -1.0f, 0.0f, 0.0f,

            1.0f, -1.0f, -1.0f,    1.0f, 0.0f, 0.0f,   //CUBE RIGHT FACE
            1.0f,  1.0f, -1.0f,    1.0f, 0.0f, 0.0f,
            1.0f,  1.0f,  1.0f,    1.0f, 0.0f, 0.0f,
            1.0f, -1.0f,  1.0f,    1.0f, 0.0f, 0.0f,

            -1.0f, -1.0f, -1.0f,    0.0f, -1.0f, 0.0f,  //CUBE BOTTOM FACE
            -1.0f, -1.0f,  1.0f,    0.0f, -1.0f, 0.0f,
            1.0f, -1.0f,  1.0f,    0.0f, -1.0f, 0.0f,
            1.0f, -1.0f, -1.0f,    0.0f, -1.0f, 0.0f,

            -1.0f,  1.0f, -1.0f,    0.0f, 1.0f, 0.0f,   //CUBE TOP FACE
            -1.0f,  1.0f,  1.0f,    0.0f, 1.0f, 0.0f,
            1.0f,  1.0f,  1.0f,    0.0f, 1.0f, 0.0f,
            1.0f,  1.0f, -1.0f,    0.0f, 1.0f, 0.0f,

            -5.0f,  0.0f, -5.0f,    0.0f, 1.0f, 0.0f,   //PLANE GEOMETRY
            -5.0f,  0.0f,  5.0f,    0.0f, 1.0f, 0.0f,
            5.0f,  0.0f,  5.0f,    0.0f, 1.0f, 0.0f,
            5.0f,  0.0f, -5.0f,    0.0f, 1.0f, 0.0f,
        };

        state.vbuf = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(scene_vertices),
            label = "cube-vertices"
        });

        // ...and a matching index buffer for the scene
        UInt16[] scene_indices = {
            0, 1, 2,  0, 2, 3,
            6, 5, 4,  7, 6, 4,
            8, 9, 10,  8, 10, 11,
            14, 13, 12,  15, 14, 12,
            16, 17, 18,  16, 18, 19,
            22, 21, 20,  23, 22, 20,
            26, 25, 24,  27, 26, 24
        };

        state.ibuf = sg_make_buffer(new sg_buffer_desc()
        {
            usage = { index_buffer = true },
            data = SG_RANGE(scene_indices),
            label = "cube-indices"
        });

        state.display.pass_action = new sg_pass_action();
        state.display.pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.display.pass_action.colors[0].clear_value = new sg_color() { r = 0.25f, g = 0.5f, b = 0.25f, a = 1.0f };

        // a regular RGBA8 render target image as shadow map
        sg_image shadow_map_img = sg_make_image(new sg_image_desc()
        {
            usage = { color_attachment = true },
            width = 2048,
            height = 2048,
            pixel_format = SG_PIXELFORMAT_RGBA8,
            sample_count = 1,
            label = "shadow-map",
        });

        // ...we also need a separate depth-buffer image for the shadow pass
        sg_image shadow_depth_img = sg_make_image(new sg_image_desc()
        {
            usage = { depth_stencil_attachment = true },
            width = 2048,
            height = 2048,
            pixel_format = SG_PIXELFORMAT_DEPTH,
            sample_count = 1,
            label = "shadow-depth-buffer",
        });


            // attachment and texture views
    sg_view shadow_map_att_view = sg_make_view(new sg_view_desc(){
        color_attachment = { image = shadow_map_img },
        label = "shadow-map-att-view",
    });

    sg_view shadow_map_tex_view = sg_make_view(new sg_view_desc(){
        texture = { image = shadow_map_img },
        label = "shadow-map-tex-view",
    });

    sg_view shadow_depth_att_view = sg_make_view(new sg_view_desc(){
        depth_stencil_attachment = { image = shadow_depth_img },
        label = "shadow-depth-attachment",
    });


    // shadow render pass descriptor
        var shadow_pass_action = new sg_pass_action();
        shadow_pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        shadow_pass_action.colors[0].clear_value = new sg_color() { r = 1.0f, g = 1.0f, b = 1.0f, a = 1.0f };

        sg_attachments shadow_attachments = default;
        shadow_attachments.colors[0] = shadow_map_att_view;
        shadow_attachments.depth_stencil = shadow_depth_att_view;
        
        state.shadow.pass = new sg_pass()
        {
            action = shadow_pass_action,
            attachments = shadow_attachments,
        };




        // a regular sampler with nearest filtering to sample the shadow map
        sg_sampler shadow_sampler= sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_NEAREST,
            mag_filter = SG_FILTER_NEAREST,
            wrap_u = SG_WRAP_CLAMP_TO_EDGE,
            wrap_v = SG_WRAP_CLAMP_TO_EDGE,
            label = "shadow-sampler",
        });
        

        var shadow_pipeline_desc = default(sg_pipeline_desc);
        shadow_pipeline_desc.layout.buffers[0].stride = 6 * sizeof(float);
        shadow_pipeline_desc.layout.attrs[ATTR_shadow_pos].format = SG_VERTEXFORMAT_FLOAT3;
        shadow_pipeline_desc.shader = sg_make_shader(shadow_shader_desc(sg_query_backend()));
        shadow_pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        shadow_pipeline_desc.cull_mode = SG_CULLMODE_FRONT;
        shadow_pipeline_desc.sample_count = 1;
        shadow_pipeline_desc.colors[0].pixel_format = SG_PIXELFORMAT_RGBA8;
        shadow_pipeline_desc.depth.pixel_format = SG_PIXELFORMAT_DEPTH;
        shadow_pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        shadow_pipeline_desc.depth.write_enabled = true;
        shadow_pipeline_desc.label = "shadow-pipeline";
        state.shadow.pip = sg_make_pipeline(shadow_pipeline_desc);



        // resource bindings to render shadow scene
        state.shadow.bind = new sg_bindings();
        state.shadow.bind.vertex_buffers[0] = state.vbuf;
        state.shadow.bind.index_buffer = state.ibuf;




        var display_pipeline_desc = default(sg_pipeline_desc);
        display_pipeline_desc.layout.attrs[ATTR_display_pos].format = SG_VERTEXFORMAT_FLOAT3;
        display_pipeline_desc.layout.attrs[ATTR_display_norm].format = SG_VERTEXFORMAT_FLOAT3;
        display_pipeline_desc.shader = sg_make_shader(display_shader_desc(sg_query_backend()));
        display_pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        display_pipeline_desc.cull_mode = SG_CULLMODE_BACK;
        display_pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        display_pipeline_desc.depth.write_enabled = true;
        display_pipeline_desc.label = "display-pipeline";
        state.display.pip = sg_make_pipeline(display_pipeline_desc);


        state.display.bind = new sg_bindings();
        state.display.bind.vertex_buffers[0] = state.vbuf;
        state.display.bind.index_buffer = state.ibuf;
        state.display.bind.views[VIEW_shadow_map] = shadow_map_tex_view;
        state.display.bind.samplers[SMP_shadow_sampler] = shadow_sampler;


        // a vertex buffer, pipeline and sampler to render a debug visualization of the shadow map
        float[] dbg_vertices = { 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 1.0f, 1.0f };
        sg_buffer dbg_vbuf = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(dbg_vertices),
            label = "debug-vertices"
        });

        var dbg_pipeline_desc = default(sg_pipeline_desc);
        dbg_pipeline_desc.layout.attrs[ATTR_dbg_pos].format = SG_VERTEXFORMAT_FLOAT2;
        dbg_pipeline_desc.shader = sg_make_shader(dbg_shader_desc(sg_query_backend()));
        dbg_pipeline_desc.primitive_type = SG_PRIMITIVETYPE_TRIANGLE_STRIP;
        dbg_pipeline_desc.label = "debug-pipeline";
        state.dbg.pip = sg_make_pipeline(dbg_pipeline_desc);

        var dbg_smp = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_NEAREST,
            mag_filter = SG_FILTER_NEAREST,
            wrap_u = SG_WRAP_CLAMP_TO_EDGE,
            wrap_v = SG_WRAP_CLAMP_TO_EDGE,
            label = "debug-sampler"
        });
        state.dbg.bind = new sg_bindings();
        state.dbg.bind.vertex_buffers[0] = dbg_vbuf;
        state.dbg.bind.views[VIEW_dbg_tex] = shadow_map_tex_view;
        state.dbg.bind.samplers[SMP_dbg_smp] = dbg_smp;
    }


    static Matrix4x4 CreateOrthographic(float Left, float Right, float Bottom, float Top, float Near, float Far)
    {
        Matrix4x4 Result = Matrix4x4.Identity;

        Result.M11 = 2.0f / (Right - Left);
        Result.M22 = 2.0f / (Top - Bottom);
        Result.M33 = 2.0f / (Near - Far);

        Result.M41 = (Left + Right) / (Left - Right);
        Result.M42 = (Bottom + Top) / (Bottom - Top);
        Result.M43 = (Far + Near) / (Near - Far);

        return (Result);
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {

        float t = (float)(sapp_frame_duration());
        state.ry += 0.2f * t;

        var eye_pos = new Vector3(5.0f, 5.0f, 5.0f);
        var plane_model = Identity;
        var cube_model = CreateTranslation(new Vector3(0.0f, 1.5f, 0.0f));
        var plane_color = new Vector3(1.0f, 0.5f, 0.0f);
        var cube_color = new Vector3(0.5f, 0.5f, 1.0f);

        // calculate matrices for shadow pass
        var rym = CreateFromAxisAngle(Vector3.UnitY, state.ry);
        var light_pos = Vector4.Transform(new Vector4(50.0f, 50.0f, -50.0f, 1.0f), rym);
        var light_view = CreateLookAt(light_pos.AsVector3(), new Vector3(0.0f, 1.5f, 0.0f), Vector3.UnitY);
        var light_proj = CreateOrthographic(-5.0f, 5.0f, -5.0f, 5.0f, 0, 100.0f);
        var light_view_proj = light_view * light_proj;

        var cube_vs_shadow_params = new vs_shadow_params_t()
        {
            mvp = cube_model * light_view_proj
        };

        // calculate matrices for display pass
        var proj = CreatePerspectiveFieldOfView((float)(60.0f * Math.PI / 180), sapp_widthf() / sapp_heightf(), 0.01f, 100.0f);
        var view = CreateLookAt(eye_pos, new Vector3(0.0f, 0.0f, 0.0f), Vector3.UnitY);
        var view_proj = view * proj;

        var fs_display_params = new fs_display_params_t()
        {
            light_dir = Vector3.Normalize(light_pos.AsVector3()),
            eye_pos = eye_pos
        };

        var plane_vs_display_params = new vs_display_params_t()
        {
            mvp = plane_model * view_proj,
            model = plane_model,
            light_mvp = plane_model * light_view_proj,
            diff_color = plane_color
        };

        var cube_vs_display_params = new vs_display_params_t()
        {
            mvp = cube_model * view_proj,
            model = cube_model,
            light_mvp = cube_model * light_view_proj,
            diff_color = cube_color
        };

        // the shadow map pass, render scene from light source into shadow map texture
        sg_begin_pass(state.shadow.pass);
        sg_apply_pipeline(state.shadow.pip);
        sg_apply_bindings(state.shadow.bind);
        sg_apply_uniforms(UB_vs_shadow_params, SG_RANGE<vs_shadow_params_t>(ref cube_vs_shadow_params));
        sg_draw(0, 36, 1);
        sg_end_pass();

        // the display pass, render scene from camera and sample the shadow map
        sg_begin_pass(new sg_pass(){ action = state.display.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.display.pip);
        sg_apply_bindings(state.display.bind);
        sg_apply_uniforms(UB_fs_display_params, SG_RANGE<fs_display_params_t>(ref fs_display_params));
        sg_apply_uniforms(UB_vs_display_params, SG_RANGE<vs_display_params_t>(ref plane_vs_display_params));
        sg_draw(36, 6, 1);
        sg_apply_uniforms(UB_vs_display_params, SG_RANGE<vs_display_params_t>(ref cube_vs_display_params));
        sg_draw(0, 36, 1);

        // render debug visualization of shadow-map
        sg_apply_pipeline(state.dbg.pip);
        sg_apply_bindings(state.dbg.bind);
        sg_apply_viewport(sapp_width() - 150, 0, 150, 150, false);
        sg_draw(0, 4, 1);

        sg_end_pass();
        sg_commit();

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

    [UnmanagedCallersOnly]
    private static unsafe void Event(SApp.sapp_event* e)
    {
        if (e->type == SApp.sapp_event_type.SAPP_EVENTTYPE_KEY_UP)
        {
            // state.PauseUpdate = !state.PauseUpdate;
        }
    }
}