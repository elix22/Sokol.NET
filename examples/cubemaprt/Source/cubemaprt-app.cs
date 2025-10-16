using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using static cubemaprt_sapp_shader_cs.Shaders;

public static unsafe class CubemaprtApp
{
    static readonly Random random = new Random();
    public static float NextRandom(float min, float max) { return (float)((random.NextDouble() * (max - min)) + min); }
    // NOTE: cubemaps can't be multisampled
    const int OFFSCREEN_SAMPLE_COUNT = 1;
    const int DISPLAY_SAMPLE_COUNT = 4;
    const int NUM_SHAPES = 32;
    const int NUM_FACES = 6;

    // State struct for the little cubes rotating around the big cube
    struct Shape
    {
        public Matrix4x4 model;
        public Vector4 color;
        public Vector3 axis;
        public float radius;
        public float angle;
        public float angular_velocity;
    }

    // Vertex (normals for simple point lighting)
    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    {
        public Vector3 pos;
        public Vector3 norm;
    }

    // A mesh consists of a vertex- and index-buffer
    struct Mesh
    {
        public sg_buffer vbuf;
        public sg_buffer ibuf;
        public int num_elements;
    }

    // The entire application state
    struct State
    {
        public sg_image cubemap;
        public sg_view cubemap_texview;
        public sg_sampler smp;
        public unsafe fixed uint offscreen_color_views[NUM_FACES]; // sg_view.id array
        public sg_view offscreen_depth_view;
        public sg_pass_action offscreen_pass_action;
        public sg_pass_action display_pass_action;
        public Mesh cube;
        public sg_pipeline offscreen_shapes_pip;
        public sg_pipeline display_shapes_pip;
        public sg_pipeline display_cube_pip;
        public Matrix4x4 offscreen_proj;
        public Vector4 light_dir;
        public float rx, ry;
        public unsafe fixed byte shapes_data[NUM_SHAPES * sizeof(float) * 32]; // Enough for Shape struct
    }

    static State state;
    static Shape[] shapes = new Shape[NUM_SHAPES];

    static float Rnd(float min_val, float max_val)
    {
        return NextRandom(min_val, max_val);
    }

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });
        __dbgui_setup(DISPLAY_SAMPLE_COUNT);

        // Create a cubemap as render target
        state.cubemap = sg_make_image(new sg_image_desc()
        {
            type = sg_image_type.SG_IMAGETYPE_CUBE,
            usage = { color_attachment = true },
            width = 1024,
            height = 1024,
            sample_count = OFFSCREEN_SAMPLE_COUNT,
            label = "cubemap-color-rt"
        });

        state.cubemap_texview = sg_make_view(new sg_view_desc()
        {
            texture = { image = state.cubemap },
            label = "cubemap-texview"
        });

        // Create depth buffer
        sg_image depth_img = sg_make_image(new sg_image_desc()
        {
            type = sg_image_type.SG_IMAGETYPE_2D,
            usage = { depth_stencil_attachment = true },
            width = 1024,
            height = 1024,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
            sample_count = OFFSCREEN_SAMPLE_COUNT,
            label = "cubemap-depth-rt"
        });

        // Create 6 view objects, one for each cubemap face
        fixed (uint* views = state.offscreen_color_views)
        {
            for (int i = 0; i < NUM_FACES; i++)
            {
                views[i] = sg_make_view(new sg_view_desc()
                {
                    color_attachment = { image = state.cubemap, slice = i },
                    label = $"cubemap-texview-{i}"
                }).id;
            }
        }

        state.offscreen_depth_view = sg_make_view(new sg_view_desc()
        {
            depth_stencil_attachment = { image = depth_img },
            label = "depth-stencil-attachment"
        });

        // Pass actions
        state.offscreen_pass_action = default;
        state.offscreen_pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.offscreen_pass_action.colors[0].clear_value = new sg_color { r = 0.5f, g = 0.5f, b = 0.5f, a = 1.0f };

        state.display_pass_action = default;
        state.display_pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.display_pass_action.colors[0].clear_value = new sg_color { r = 0.75f, g = 0.75f, b = 0.75f, a = 1.0f };

        // Create cube mesh
        state.cube = MakeCubeMesh();

        // Shader and pipeline for offscreen rendering
        sg_pipeline_desc pip_desc = default;
        pip_desc.shader = sg_make_shader(shapes_shader_desc(sg_query_backend()));
        pip_desc.layout.attrs[ATTR_shapes_pos].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
        pip_desc.layout.attrs[ATTR_shapes_norm].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
        pip_desc.index_type = sg_index_type.SG_INDEXTYPE_UINT16;
        pip_desc.cull_mode = sg_cull_mode.SG_CULLMODE_BACK;
        pip_desc.sample_count = OFFSCREEN_SAMPLE_COUNT;
        pip_desc.depth.pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH;
        pip_desc.depth.compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL;
        pip_desc.depth.write_enabled = true;
        pip_desc.label = "offscreen-shapes-pipeline";
        state.offscreen_shapes_pip = sg_make_pipeline(pip_desc);

        pip_desc.sample_count = DISPLAY_SAMPLE_COUNT;
        pip_desc.depth.pixel_format = 0;
        pip_desc.label = "display-shapes-pipeline";
        state.display_shapes_pip = sg_make_pipeline(pip_desc);

        // Shader and pipeline for display rendering (cube with environment mapping)
        sg_pipeline_desc cube_pip_desc = default;
        cube_pip_desc.shader = sg_make_shader(cube_shader_desc(sg_query_backend()));
        cube_pip_desc.layout.attrs[ATTR_cube_pos].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
        cube_pip_desc.layout.attrs[ATTR_cube_norm].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
        cube_pip_desc.index_type = sg_index_type.SG_INDEXTYPE_UINT16;
        cube_pip_desc.cull_mode = sg_cull_mode.SG_CULLMODE_BACK;
        cube_pip_desc.sample_count = DISPLAY_SAMPLE_COUNT;
        cube_pip_desc.depth.compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL;
        cube_pip_desc.depth.write_enabled = true;
        cube_pip_desc.label = "display-cube-pipeline";
        state.display_cube_pip = sg_make_pipeline(cube_pip_desc);

        // Sampler
        state.smp = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = sg_filter.SG_FILTER_LINEAR,
            mag_filter = sg_filter.SG_FILTER_LINEAR
        });

        // 1:1 aspect ratio projection matrix for offscreen rendering
        state.offscreen_proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(90.0 * Math.PI / 180.0), 1.0f, 0.01f, 100.0f);
        state.light_dir = new Vector4(Vector3.Normalize(new Vector3(-0.75f, 1.0f, 0.0f)), 0.0f);

        // Setup initial state for the orbiting cubes
        for (int i = 0; i < NUM_SHAPES; i++)
        {
            shapes[i].color = new Vector4(Rnd(0.0f, 1.0f), Rnd(0.0f, 1.0f), Rnd(0.0f, 1.0f), 1.0f);
            shapes[i].axis = Vector3.Normalize(new Vector3(Rnd(-1.0f, 1.0f), Rnd(-1.0f, 1.0f), Rnd(-1.0f, 1.0f)));
            shapes[i].radius = Rnd(5.0f, 10.0f);
            shapes[i].angle = Rnd(0.0f, 360.0f);
            shapes[i].angular_velocity = Rnd(15.0f, 50.0f) * (Rnd(-1.0f, 1.0f) > 0.0f ? 1.0f : -1.0f);
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        float t = (float)sapp_frame_duration();

        // Update the little cubes that are reflected in the big cube
        for (int i = 0; i < NUM_SHAPES; i++)
        {
            shapes[i].angle += shapes[i].angular_velocity * t;
            Matrix4x4 scale = Matrix4x4.CreateScale(0.25f);
            Matrix4x4 rot = Matrix4x4.CreateFromAxisAngle(shapes[i].axis, (float)(shapes[i].angle * Math.PI / 180.0));
            Matrix4x4 trans = Matrix4x4.CreateTranslation(0.0f, 0.0f, shapes[i].radius);
            shapes[i].model = scale * trans * rot;
        }

        // Offscreen pass which renders the environment cubemap
        // NOTE: These values work for Metal and D3D11
        Vector3[][] center_and_up = new Vector3[NUM_FACES][];
        center_and_up[0] = new[] { new Vector3(+1.0f,  0.0f,  0.0f), new Vector3(0.0f, -1.0f,  0.0f) };
        center_and_up[1] = new[] { new Vector3(-1.0f,  0.0f,  0.0f), new Vector3(0.0f, -1.0f,  0.0f) };
        center_and_up[2] = new[] { new Vector3( 0.0f, -1.0f,  0.0f), new Vector3(0.0f,  0.0f, -1.0f) };
        center_and_up[3] = new[] { new Vector3( 0.0f, +1.0f,  0.0f), new Vector3(0.0f,  0.0f, +1.0f) };
        center_and_up[4] = new[] { new Vector3( 0.0f,  0.0f, +1.0f), new Vector3(0.0f, -1.0f,  0.0f) };
        center_and_up[5] = new[] { new Vector3( 0.0f,  0.0f, -1.0f), new Vector3(0.0f, -1.0f,  0.0f) };

        fixed (uint* views = state.offscreen_color_views)
        {
            for (int face = 0; face < NUM_FACES; face++)
            {
                sg_pass pass = default;
                pass.action = state.offscreen_pass_action;
                pass.attachments.colors[0] = new sg_view { id = views[face] };
                pass.attachments.depth_stencil = state.offscreen_depth_view;
                
                sg_begin_pass(pass);
                
                Matrix4x4 face_view = Matrix4x4.CreateLookAt(Vector3.Zero, center_and_up[face][0], center_and_up[face][1]);
                Matrix4x4 face_view_proj = face_view * state.offscreen_proj;
                DrawCubes(state.offscreen_shapes_pip, Vector3.Zero, face_view_proj);
                
                sg_end_pass();
            }
        }

        // Render the default pass
        int w = sapp_width();
        int h = sapp_height();
        sg_begin_pass(new sg_pass { action = state.display_pass_action, swapchain = sglue_swapchain() });

        Vector3 eye_pos = new Vector3(0.0f, 0.0f, 20.0f);
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(45.0 * Math.PI / 180.0), (float)w / (float)h, 0.01f, 100.0f);
        Matrix4x4 view = Matrix4x4.CreateLookAt(eye_pos, Vector3.Zero, Vector3.UnitY);
        Matrix4x4 view_proj = view * proj;

        // Render the orbiting cubes
        DrawCubes(state.display_shapes_pip, eye_pos, view_proj);

        // Render a big cube in the middle with environment mapping
        state.rx += 0.1f * 60.0f * t;
        state.ry += 0.2f * 60.0f * t;
        Matrix4x4 rxm = Matrix4x4.CreateRotationX((float)(state.rx * Math.PI / 180.0));
        Matrix4x4 rym = Matrix4x4.CreateRotationY((float)(state.ry * Math.PI / 180.0));
        Matrix4x4 model = Matrix4x4.CreateScale(2.0f) * rym * rxm;

        sg_apply_pipeline(state.display_cube_pip);
        
        sg_bindings bind = default;
        bind.vertex_buffers[0] = state.cube.vbuf;
        bind.index_buffer = state.cube.ibuf;
        bind.views[VIEW_tex] = state.cubemap_texview;
        bind.samplers[SMP_smp] = state.smp;
        sg_apply_bindings(ref bind);

        shape_uniforms_t uniforms = new shape_uniforms_t
        {
            mvp = model * view_proj,
            model = model,
            shape_color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
            light_dir = state.light_dir,
            eye_pos = new Vector4(eye_pos, 1.0f)
        };
        sg_apply_uniforms(UB_shape_uniforms, SG_RANGE(ref uniforms));
        sg_draw(0, (uint)state.cube.num_elements, 1);

        __dbgui_draw();
        sg_end_pass();
        sg_commit();
    }

    static void DrawCubes(sg_pipeline pip, Vector3 eye_pos, Matrix4x4 view_proj)
    {
        sg_apply_pipeline(pip);
        
        sg_bindings bind = default;
        bind.vertex_buffers[0] = state.cube.vbuf;
        bind.index_buffer = state.cube.ibuf;
        sg_apply_bindings(ref bind);

        for (int i = 0; i < NUM_SHAPES; i++)
        {
            ref Shape shape = ref shapes[i];
            shape_uniforms_t uniforms = new shape_uniforms_t
            {
                mvp = shape.model * view_proj,
                model = shape.model,
                shape_color = shape.color,
                light_dir = state.light_dir,
                eye_pos = new Vector4(eye_pos, 1.0f)
            };
            sg_apply_uniforms(UB_shape_uniforms, SG_RANGE(ref uniforms));
            sg_draw(0, (uint)state.cube.num_elements, 1);
        }
    }

    static unsafe Mesh MakeCubeMesh()
    {
        Vertex[] vertices = new Vertex[]
        {
            new() { pos = new(-1.0f, -1.0f, -1.0f), norm = new( 0.0f,  0.0f, -1.0f) },
            new() { pos = new( 1.0f, -1.0f, -1.0f), norm = new( 0.0f,  0.0f, -1.0f) },
            new() { pos = new( 1.0f,  1.0f, -1.0f), norm = new( 0.0f,  0.0f, -1.0f) },
            new() { pos = new(-1.0f,  1.0f, -1.0f), norm = new( 0.0f,  0.0f, -1.0f) },

            new() { pos = new(-1.0f, -1.0f,  1.0f), norm = new( 0.0f,  0.0f,  1.0f) },
            new() { pos = new( 1.0f, -1.0f,  1.0f), norm = new( 0.0f,  0.0f,  1.0f) },
            new() { pos = new( 1.0f,  1.0f,  1.0f), norm = new( 0.0f,  0.0f,  1.0f) },
            new() { pos = new(-1.0f,  1.0f,  1.0f), norm = new( 0.0f,  0.0f,  1.0f) },

            new() { pos = new(-1.0f, -1.0f, -1.0f), norm = new(-1.0f,  0.0f,  0.0f) },
            new() { pos = new(-1.0f,  1.0f, -1.0f), norm = new(-1.0f,  0.0f,  0.0f) },
            new() { pos = new(-1.0f,  1.0f,  1.0f), norm = new(-1.0f,  0.0f,  0.0f) },
            new() { pos = new(-1.0f, -1.0f,  1.0f), norm = new(-1.0f,  0.0f,  0.0f) },

            new() { pos = new( 1.0f, -1.0f, -1.0f), norm = new( 1.0f,  0.0f,  0.0f) },
            new() { pos = new( 1.0f,  1.0f, -1.0f), norm = new( 1.0f,  0.0f,  0.0f) },
            new() { pos = new( 1.0f,  1.0f,  1.0f), norm = new( 1.0f,  0.0f,  0.0f) },
            new() { pos = new( 1.0f, -1.0f,  1.0f), norm = new( 1.0f,  0.0f,  0.0f) },

            new() { pos = new(-1.0f, -1.0f, -1.0f), norm = new( 0.0f, -1.0f,  0.0f) },
            new() { pos = new(-1.0f, -1.0f,  1.0f), norm = new( 0.0f, -1.0f,  0.0f) },
            new() { pos = new( 1.0f, -1.0f,  1.0f), norm = new( 0.0f, -1.0f,  0.0f) },
            new() { pos = new( 1.0f, -1.0f, -1.0f), norm = new( 0.0f, -1.0f,  0.0f) },

            new() { pos = new(-1.0f,  1.0f, -1.0f), norm = new( 0.0f,  1.0f,  0.0f) },
            new() { pos = new(-1.0f,  1.0f,  1.0f), norm = new( 0.0f,  1.0f,  0.0f) },
            new() { pos = new( 1.0f,  1.0f,  1.0f), norm = new( 0.0f,  1.0f,  0.0f) },
            new() { pos = new( 1.0f,  1.0f, -1.0f), norm = new( 0.0f,  1.0f,  0.0f) }
        };

        ushort[] indices = {
            0, 1, 2,  0, 2, 3,
            6, 5, 4,  7, 6, 4,
            8, 9, 10,  8, 10, 11,
            14, 13, 12,  15, 14, 12,
            16, 17, 18,  16, 18, 19,
            22, 21, 20,  23, 22, 20
        };

        Mesh mesh;
        mesh.vbuf = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(vertices),
            label = "cube-vertices"
        });
        mesh.ibuf = sg_make_buffer(new sg_buffer_desc()
        {
            usage = { index_buffer = true },
            data = SG_RANGE(indices),
            label = "cube-indices"
        });
        mesh.num_elements = indices.Length;
        return mesh;
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
            window_title = "Cubemap Render Target (sokol-app)",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }
}
