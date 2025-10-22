using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SFetch;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_filter;
using static Sokol.SG.sg_load_action;
using static Sokol.Utils;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using static Sokol.SDebugText;
using static Sokol.StbImage;
using static cubemap_jpeg_sapp_shader_cs.Shaders;

public static unsafe class CubemapJpegApp
{
    const int NUM_FACES = 6;
    const int FACE_WIDTH = 2048;
    const int FACE_HEIGHT = 2048;
    const int FACE_NUM_BYTES = FACE_WIDTH * FACE_HEIGHT * 4;

    struct _state
    {
        public sg_pass_action pass_action;
        public sg_pipeline pip;
        public sg_bindings bind;
        public Camera camera;
        public int load_count;
        public bool load_failed;
        public byte[] pixels;
        public SharedBuffer[] fetch_buffers; // One buffer per face
    }

    static _state state = default;

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

        sdtx_setup(new sdtx_desc_t()
        {
            fonts = { [0] = sdtx_font_oric() },
            logger = {
                func = &slog_func,
            }
        });

        // setup sokol-fetch with enough capacity for 6 concurrent requests
        sfetch_setup(new sfetch_desc_t()
        {
            max_requests = 6,
            num_channels = 1,
            num_lanes = 6,
        });

        // setup camera helper
        state.camera = new Camera();
        state.camera.Init(new CameraDesc
        {
            Latitude = 0.0f,
            Longitude = 0.0f,
            Distance = 0.1f,
            MinDist = 0.1f,
            MaxDist = 0.1f,
        });

        // allocate memory for pixel data
        state.pixels = new byte[NUM_FACES * FACE_NUM_BYTES];

        // allocate fetch buffers (each JPEG file is around 1-2MB compressed)
        state.fetch_buffers = new SharedBuffer[NUM_FACES];
        for (int i = 0; i < NUM_FACES; i++)
        {
            state.fetch_buffers[i] = SharedBuffer.Create(4 * 1024 * 1024); // 4MB per buffer
        }

        // pass action, clear to black
        state.pass_action = default;
        state.pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0, g = 0, b = 0, a = 1 };

        float[] vertices = new float[] {
            -1.0f, -1.0f, -1.0f,
             1.0f, -1.0f, -1.0f,
             1.0f,  1.0f, -1.0f,
            -1.0f,  1.0f, -1.0f,

            -1.0f, -1.0f,  1.0f,
             1.0f, -1.0f,  1.0f,
             1.0f,  1.0f,  1.0f,
            -1.0f,  1.0f,  1.0f,

            -1.0f, -1.0f, -1.0f,
            -1.0f,  1.0f, -1.0f,
            -1.0f,  1.0f,  1.0f,
            -1.0f, -1.0f,  1.0f,

             1.0f, -1.0f, -1.0f,
             1.0f,  1.0f, -1.0f,
             1.0f,  1.0f,  1.0f,
             1.0f, -1.0f,  1.0f,

            -1.0f, -1.0f, -1.0f,
            -1.0f, -1.0f,  1.0f,
             1.0f, -1.0f,  1.0f,
             1.0f, -1.0f, -1.0f,

            -1.0f,  1.0f, -1.0f,
            -1.0f,  1.0f,  1.0f,
             1.0f,  1.0f,  1.0f,
             1.0f,  1.0f, -1.0f,
        };
        state.bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(vertices),
            label = "cubemap-vertices"
        });

        ushort[] indices = new ushort[] {
            0, 1, 2,  0, 2, 3,
            6, 5, 4,  7, 6, 4,
            8, 9, 10,  8, 10, 11,
            14, 13, 12,  15, 14, 12,
            16, 17, 18,  16, 18, 19,
            22, 21, 20,  23, 22, 20
        };
        state.bind.index_buffer = sg_make_buffer(new sg_buffer_desc()
        {
            usage = { index_buffer = true },
            data = SG_RANGE(indices),
            label = "cubemap-indices"
        });

        // allocate a (texture) view handle, but only initialize it later once the
        // texture data has been asynchronously loaded
        state.bind.views[VIEW_tex] = sg_alloc_view();

        // a sampler object
        state.bind.samplers[SMP_smp] = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_LINEAR,
            mag_filter = SG_FILTER_LINEAR,
            label = "cubemap-sampler"
        });

        // a pipeline object
        state.pip = sg_make_pipeline(new sg_pipeline_desc()
        {
            layout = {
                attrs = {
                    [ATTR_cubemap_pos] = new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT3 }
                }
            },
            shader = sg_make_shader(cubemap_shader_desc(sg_query_backend())),
            index_type = SG_INDEXTYPE_UINT16,
            depth = new sg_depth_state()
            {
                compare = SG_COMPAREFUNC_LESS_EQUAL,
                write_enabled = true
            },
            label = "cubemap-pipeline"
        });

        // start loading JPEG files
        string[] filenames = [
            "nb2_posx.jpg", "nb2_negx.jpg",
            "nb2_posy.jpg", "nb2_negy.jpg",
            "nb2_posz.jpg", "nb2_negz.jpg"
        ];

        for (int i = 0; i < NUM_FACES; i++)
        {
            sfetch_send(new sfetch_request_t()
            {
                path = util_get_file_path(filenames[i]),
                callback = &fetch_callback,
                buffer = SFETCH_RANGE(state.fetch_buffers[i])
            });
        }
    }

    [UnmanagedCallersOnly]
    static void fetch_callback(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            // Identify which face this is by comparing buffer pointers
            int face_index = -1;
            for (int i = 0; i < NUM_FACES; i++)
            {
                fixed (byte* bufferPtr = &state.fetch_buffers[i].Buffer[0])
                {
                    if (response->buffer.ptr == bufferPtr)
                    {
                        face_index = i;
                        break;
                    }
                }
            }

            if (face_index < 0)
            {
                state.load_failed = true;
                return;
            }

            // Decode JPEG using native STB from the fetched data in the buffer
            int width = 0, height = 0, channels = 0;
            byte* pixels = stbi_load_csharp(
                in state.fetch_buffers[face_index].Buffer[0],
                (int)response->data.size,
                ref width,
                ref height,
                ref channels,
                4  // desired_channels: force RGBA
            );

            if (pixels == null)
            {
                state.load_failed = true;
                return;
            }

            if (width != FACE_WIDTH || height != FACE_HEIGHT)
            {
                stbi_image_free_csharp(pixels);
                state.load_failed = true;
                return;
            }

            // Copy decoded pixels to the appropriate face
            int offset = face_index * FACE_NUM_BYTES;
            fixed (byte* dest = &state.pixels[offset])
            {
                Buffer.MemoryCopy(pixels, dest, FACE_NUM_BYTES, FACE_NUM_BYTES);
            }

            // Free the native STB image data
            stbi_image_free_csharp(pixels);

            // Increment load count
            state.load_count++;

            // All 6 faces loaded?
            if (state.load_count == NUM_FACES)
            {
                sg_image img = sg_make_image(new sg_image_desc()
                {
                    type = sg_image_type.SG_IMAGETYPE_CUBE,
                    width = FACE_WIDTH,
                    height = FACE_HEIGHT,
                    pixel_format = SG_PIXELFORMAT_RGBA8,
                    data = {
                            mip_levels = {
                                [0] = SG_RANGE(state.pixels)
                            }
                        },
                    label = "cubemap-image"
                });

                // Initialize the pre-allocated view
                sg_init_view(state.bind.views[VIEW_tex], new sg_view_desc()
                {
                    texture = { image = img },
                    label = "cubemap-view"
                });
            }
        }
        else if (response->failed)
        {
            state.load_failed = true;
        }
    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // pump the sokol-fetch message queues
        sfetch_dowork();

        state.camera.Update(sapp_width(), sapp_height());

        vs_params_t vs_params = new vs_params_t()
        {
            mvp = state.camera.ViewProj
        };

        sdtx_canvas(sapp_widthf() * 0.5f, sapp_heightf() * 0.5f);
        sdtx_origin(1, 1);
        if (state.load_failed)
        {
            sdtx_puts("LOAD FAILED!");
        }
        else if (state.load_count < 6)
        {
            sdtx_puts("LOADING ...");
        }
        else
        {
            sdtx_puts("LMB + move mouse to look around");
        }

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pip);
        sg_apply_bindings(state.bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_draw(0, 36, 1);
        sdtx_draw();
        __dbgui_draw();
        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        if (__dbgui_event_with_retval(e))
        {
            return;
        }
        state.camera.HandleEvent(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        // Dispose the shared buffers
        if (state.fetch_buffers != null)
        {
            for (int i = 0; i < NUM_FACES; i++)
            {
                state.fetch_buffers[i]?.Dispose();
            }
        }

        sfetch_shutdown();
        __dbgui_shutdown();
        sdtx_shutdown();
        sg_shutdown();
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
            sample_count = 1,
            window_title = "Cubemap JPEG (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
