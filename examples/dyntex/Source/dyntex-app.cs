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
using static dyntex_sapp_shader_cs.Shaders;
using System.Diagnostics;
public static unsafe class DynTextApp
{
    const int IMAGE_WIDTH = (64);
    const int IMAGE_HEIGHT = (64);
    const uint LIVING = 0xFFFFFFFF;
    const uint DEAD = 0xFF000000;
    unsafe struct _state
    {
        public sg_pass_action pass_action;
        public sg_pipeline pip;
        public sg_image img;
        public sg_bindings bind;
        public float rx, ry;
        public int update_count;

        public fixed uint _pixels[IMAGE_WIDTH * IMAGE_HEIGHT];
        public ref uint pixels(int row, int column)
        {
            if (row < 0 || row >= IMAGE_HEIGHT || column < 0 || column >= IMAGE_WIDTH)
                throw new ArgumentOutOfRangeException();

            fixed (uint* ptr = _pixels)
            {
                uint* pixel = ptr + (row * IMAGE_WIDTH + column);
                return ref *pixel;
            }
        }
    };

    static _state state = new _state();


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = new sg_logger()
        });

        // a 128x128 image with streaming update strategy
        state.img = sg_make_image(new sg_image_desc()
        {
            width = IMAGE_WIDTH,
            height = IMAGE_HEIGHT,
            pixel_format = SG_PIXELFORMAT_RGBA8,
            usage = new sg_image_usage { stream_update = true },
            label = "dynamic-texture"
        }
        );

        sg_view tex_view = sg_make_view(new sg_view_desc()
        {
            texture = new sg_texture_view_desc { image = state.img },
            label = "dynamic-texture-view",
        });

        // a sampler object
        sg_sampler smp = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_LINEAR,
            mag_filter = SG_FILTER_LINEAR,
            wrap_u = SG_WRAP_CLAMP_TO_EDGE,
            wrap_v = SG_WRAP_CLAMP_TO_EDGE,
        });

        // cube vertex buffer
        float[] vertices = {
        // pos                  color                       uvs
        -1.0f, -1.0f, -1.0f,    1.0f, 0.0f, 0.0f, 1.0f,     0.0f, 0.0f,
         1.0f, -1.0f, -1.0f,    1.0f, 0.0f, 0.0f, 1.0f,     1.0f, 0.0f,
         1.0f,  1.0f, -1.0f,    1.0f, 0.0f, 0.0f, 1.0f,     1.0f, 1.0f,
        -1.0f,  1.0f, -1.0f,    1.0f, 0.0f, 0.0f, 1.0f,     0.0f, 1.0f,

        -1.0f, -1.0f,  1.0f,    0.0f, 1.0f, 0.0f, 1.0f,     0.0f, 0.0f,
         1.0f, -1.0f,  1.0f,    0.0f, 1.0f, 0.0f, 1.0f,     1.0f, 0.0f,
         1.0f,  1.0f,  1.0f,    0.0f, 1.0f, 0.0f, 1.0f,     1.0f, 1.0f,
        -1.0f,  1.0f,  1.0f,    0.0f, 1.0f, 0.0f, 1.0f,     0.0f, 1.0f,

        -1.0f, -1.0f, -1.0f,    0.0f, 0.0f, 1.0f, 1.0f,     0.0f, 0.0f,
        -1.0f,  1.0f, -1.0f,    0.0f, 0.0f, 1.0f, 1.0f,     1.0f, 0.0f,
        -1.0f,  1.0f,  1.0f,    0.0f, 0.0f, 1.0f, 1.0f,     1.0f, 1.0f,
        -1.0f, -1.0f,  1.0f,    0.0f, 0.0f, 1.0f, 1.0f,     0.0f, 1.0f,

         1.0f, -1.0f, -1.0f,    1.0f, 0.5f, 0.0f, 1.0f,     0.0f, 0.0f,
         1.0f,  1.0f, -1.0f,    1.0f, 0.5f, 0.0f, 1.0f,     1.0f, 0.0f,
         1.0f,  1.0f,  1.0f,    1.0f, 0.5f, 0.0f, 1.0f,     1.0f, 1.0f,
         1.0f, -1.0f,  1.0f,    1.0f, 0.5f, 0.0f, 1.0f,     0.0f, 1.0f,

        -1.0f, -1.0f, -1.0f,    0.0f, 0.5f, 1.0f, 1.0f,     0.0f, 0.0f,
        -1.0f, -1.0f,  1.0f,    0.0f, 0.5f, 1.0f, 1.0f,     1.0f, 0.0f,
         1.0f, -1.0f,  1.0f,    0.0f, 0.5f, 1.0f, 1.0f,     1.0f, 1.0f,
         1.0f, -1.0f, -1.0f,    0.0f, 0.5f, 1.0f, 1.0f,     0.0f, 1.0f,

        -1.0f,  1.0f, -1.0f,    1.0f, 0.0f, 0.5f, 1.0f,     0.0f, 0.0f,
        -1.0f,  1.0f,  1.0f,    1.0f, 0.0f, 0.5f, 1.0f,     1.0f, 0.0f,
         1.0f,  1.0f,  1.0f,    1.0f, 0.0f, 0.5f, 1.0f,     1.0f, 1.0f,
         1.0f,  1.0f, -1.0f,    1.0f, 0.0f, 0.5f, 1.0f,     0.0f, 1.0f
    };


        UInt16[] indices = {
        0, 1, 2,  0, 2, 3,
        6, 5, 4,  7, 6, 4,
        8, 9, 10,  8, 10, 11,
        14, 13, 12,  15, 14, 12,
        16, 17, 18,  16, 18, 19,
        22, 21, 20,  23, 22, 20
    };

        sg_buffer vbuf = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(vertices),
            label = "cube-vertices"
        });

        sg_buffer ibuf = sg_make_buffer(new sg_buffer_desc()
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE(indices),
            label = "cube-indices"
        });

        // a shader to render a textured cube
        sg_shader shd = sg_make_shader(dyntex_shader_desc(sg_query_backend()));

        var pipeline_desc = default(sg_pipeline_desc);
        pipeline_desc.layout.attrs[ATTR_dyntex_position].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_dyntex_color0].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.layout.attrs[ATTR_dyntex_texcoord0].format = SG_VERTEXFORMAT_FLOAT2;

        pipeline_desc.shader = shd;
        pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        pipeline_desc.cull_mode = SG_CULLMODE_BACK;
        pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pipeline_desc.depth.write_enabled = true;
        pipeline_desc.label = "cube-pipeline";
        state.pip = sg_make_pipeline(pipeline_desc);

        state.bind = new sg_bindings();
        state.bind.vertex_buffers[0] = vbuf;
        state.bind.index_buffer = ibuf;
        state.bind.views[VIEW_tex] = tex_view;
        state.bind.samplers[SMP_smp] = smp;

        game_of_life_init();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        vs_params_t vs_params = default;
        float t = (float)(sapp_frame_duration());

        var proj = CreatePerspectiveFieldOfView(
                        (float)(60.0f * Math.PI / 180),
                        sapp_widthf() / sapp_heightf(),
                        0.01f,
                        10.0f);

        var view = CreateLookAt(new Vector3(0.0f, 1.5f, 4.0f), Vector3.Zero, Vector3.UnitY);

        state.rx += 0.1f;
        state.ry += 0.2f;
        var rxm = CreateFromAxisAngle(Vector3.UnitX, state.rx * t);
        var rym = CreateFromAxisAngle(Vector3.UnitY, state.ry * t);
        var model = rxm * rym;

        vs_params.mvp = model * view * proj;

        // update game-of-life state
        game_of_life_update();

        // update the texture
        fixed (uint* pixels = state._pixels)
        {
            var img_data = default(sg_image_data);
            img_data.mip_levels[0] = SG_RANGE(pixels, IMAGE_WIDTH * IMAGE_HEIGHT);
            sg_update_image(state.img, img_data);
        }

        sg_begin_pass(new sg_pass() { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pip);
        sg_apply_bindings(state.bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_draw(0, 36, 1);

        sg_end_pass();
        sg_commit();

    }


    static void game_of_life_init()
    {
        for (int y = 0; y < IMAGE_HEIGHT; y++)
        {
            for (int x = 0; x < IMAGE_WIDTH; x++)
            {
                if ((random() & 255) > 230)
                {
                    state.pixels(y, x) = LIVING;
                }
                else
                {
                    state.pixels(y, x) = DEAD;
                }
            }
        }
    }


    static void game_of_life_update()
    {
        for (int y = 0; y < IMAGE_HEIGHT; y++)
        {
            for (int x = 0; x < IMAGE_WIDTH; x++)
            {
                int num_living_neighbours = 0;
                for (int ny = -1; ny < 2; ny++)
                {
                    for (int nx = -1; nx < 2; nx++)
                    {
                        if ((nx == 0) && (ny == 0))
                        {
                            continue;
                        }
                        if (state.pixels((y + ny) & (IMAGE_HEIGHT - 1), (x + nx) & (IMAGE_WIDTH - 1)) == LIVING)
                        {
                            num_living_neighbours++;
                        }
                    }
                }
                // any live cell...
                if (state.pixels(y, x) == LIVING)
                {
                    if (num_living_neighbours < 2)
                    {
                        /* ... with fewer than 2 living neighbours dies, as if caused by underpopulation */
                        state.pixels(y, x) = DEAD;
                    }
                    else if (num_living_neighbours > 3)
                    {
                        /* ... with more than 3 living neighbours dies, as if caused by overpopulation */
                        state.pixels(y, x) = DEAD;
                    }
                }
                else if (num_living_neighbours == 3)
                {
                    // any dead cell with exactly 3 living neighbours becomes a live cell, as if by reproduction
                    state.pixels(y, x) = LIVING;
                }
            }
        }
        if (state.update_count++ > 240)
        {
            game_of_life_init();
            state.update_count = 0;
        }
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
    private static unsafe void Event(sapp_event* e)
    {
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_KEY_UP)
        {
            // state.PauseUpdate = !state.PauseUpdate;
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
            window_title = "Dynamic Texture (sokol-app)",
            icon = { sokol_default = true },
        };
    }


}

