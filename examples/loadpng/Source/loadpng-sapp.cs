
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static System.Numerics.Matrix4x4;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using static Sokol.SApp;
using static Sokol.SFetch;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_filter;
using static Sokol.SG.sg_wrap;
using static Sokol.SG.sg_load_action;
using static Sokol.SG.sg_vertex_step;
using static loadpng_sapp_shader_cs.Shaders;
using static Sokol.StbImage;

using System.Diagnostics;

public static unsafe class LoadPngSApp
{

    static bool PauseUpdate = false;

    private class State
    {
        public float rx, ry;
        public sg_pass_action pass_action;
        public sg_pipeline pip;
        public sg_bindings bind;

        //Dispose the shared buffer at the exit
        public SharedBuffer fetch_buffer = SharedBuffer.Create(256 * 1024);
    }

    private static State state = new State();


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct vertex_t
    {
        public float x, y, z;
        public short u, v;
    }

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {

        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = new sg_logger()
        });

        // setup sokol-fetch with the minimal "resource limits"
        sfetch_setup(new sfetch_desc_t()
        {
            max_requests = 1,
            num_channels = 1,
            num_lanes = 1,
            // logger = new sfetch_logger_t()
        });

        state.pass_action = default;
        state.pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color() { r = 0.125f, g = 0.25f, b = 0.35f, a = 1.0f };

        /*  Allocate an image handle, but don't actually initialize the image yet,
            this happens later when the asynchronous file load has finished.
            Any draw calls containing such an "incomplete" image handle
            will be silently dropped.
        */
        state.bind.views[VIEW_tex] = sg_alloc_view();

        // a sampler object
        state.bind.samplers[SMP_smp] = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_LINEAR,
            mag_filter = SG_FILTER_LINEAR,
        });

        
        // make continguous vertex buffer for the cube vertices
        byte[] byte_buf = new byte[Marshal.SizeOf<vertex_t>() * 24];
        // we need to use MemoryMarshal.Cast to convert the byte array to a Span of vertex_t
        ReadOnlySpan<vertex_t> vertex_buffer = MemoryMarshal.Cast<byte, vertex_t>(byte_buf);
        // fill the vertex buffer , it needs to be fixed in order to avoid GC moving the memory
        fixed (byte* buf = byte_buf)
        {
            vertex_t* vert = (vertex_t*)buf;
            vert[0] = new vertex_t { x = -1.0f, y = -1.0f, z = -1.0f, u = 0, v = 0 };
            vert[1] = new vertex_t { x = 1.0f, y = -1.0f, z = -1.0f, u = 32767, v = 0 };
            vert[2] = new vertex_t { x = 1.0f, y = 1.0f, z = -1.0f, u = 32767, v = 32767 };
            vert[3] = new vertex_t { x = -1.0f, y = 1.0f, z = -1.0f, u = 0, v = 32767 };

            vert[4] = new vertex_t { x = -1.0f, y = -1.0f, z = 1.0f, u = 0, v = 0 };
            vert[5] = new vertex_t { x = 1.0f, y = -1.0f, z = 1.0f, u = 32767, v = 0 };
            vert[6] = new vertex_t { x = 1.0f, y = 1.0f, z = 1.0f, u = 32767, v = 32767 };
            vert[7] = new vertex_t { x = -1.0f, y = 1.0f, z = 1.0f, u = 0, v = 32767 };

            vert[8] = new vertex_t { x = -1.0f, y = -1.0f, z = -1.0f, u = 0, v = 0 };
            vert[9] = new vertex_t { x = -1.0f, y = 1.0f, z = -1.0f, u = 32767, v = 0 };
            vert[10] = new vertex_t { x = -1.0f, y = 1.0f, z = 1.0f, u = 32767, v = 32767 };
            vert[11] = new vertex_t { x = -1.0f, y = -1.0f, z = 1.0f, u = 0, v = 32767 };

            vert[12] = new vertex_t { x = 1.0f, y = -1.0f, z = -1.0f, u = 0, v = 0 };
            vert[13] = new vertex_t { x = 1.0f, y = 1.0f, z = -1.0f, u = 32767, v = 0 };
            vert[14] = new vertex_t { x = 1.0f, y = 1.0f, z = 1.0f, u = 32767, v = 32767 };
            vert[15] = new vertex_t { x = 1.0f, y = -1.0f, z = 1.0f, u = 0, v = 32767 };

            vert[16] = new vertex_t { x = -1.0f, y = -1.0f, z = -1.0f, u = 0, v = 0 };
            vert[17] = new vertex_t { x = -1.0f, y = -1.0f, z = 1.0f, u = 32767, v = 0 };
            vert[18] = new vertex_t { x = 1.0f, y = -1.0f, z = 1.0f, u = 32767, v = 32767 };
            vert[19] = new vertex_t { x = 1.0f, y = -1.0f, z = -1.0f, u = 0, v = 32767 };

            vert[20] = new vertex_t { x = -1.0f, y = 1.0f, z = -1.0f, u = 0, v = 0 };
            vert[21] = new vertex_t { x = -1.0f, y = 1.0f, z = 1.0f, u = 32767, v = 0 };
            vert[22] = new vertex_t { x = 1.0f, y = 1.0f, z = 1.0f, u = 32767, v = 32767 };
            vert[23] = new vertex_t { x = 1.0f, y = 1.0f, z = -1.0f, u = 0, v = 32767 };

        }

        state.bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE<vertex_t>(vertex_buffer),
            label = "cube-vertices"
        });

        // create an index buffer for the cube
        UInt16[] indices = {
        0, 1, 2,  0, 2, 3,
        6, 5, 4,  7, 6, 4,
        8, 9, 10,  8, 10, 11,
        14, 13, 12,  15, 14, 12,
        16, 17, 18,  16, 18, 19,
        22, 21, 20,  23, 22, 20
        };

        state.bind.index_buffer = sg_make_buffer(new sg_buffer_desc()
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE(indices),
            label = "cube-indices"
        });

        // create a shader object

        sg_pipeline_desc desc = new sg_pipeline_desc();
        desc.shader = sg_make_shader(loadpng_shader_desc(sg_query_backend()));
        desc.layout.attrs[ATTR_loadpng_pos].format = SG_VERTEXFORMAT_FLOAT3;
        desc.layout.attrs[ATTR_loadpng_texcoord0].format = SG_VERTEXFORMAT_SHORT2N;
        desc.index_type = SG_INDEXTYPE_UINT16;
        desc.cull_mode = SG_CULLMODE_BACK;
        desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        desc.depth.write_enabled = true;
        desc.label = "cube-pipeline";
        state.pip = sg_make_pipeline(desc);

        /* start loading the PNG file, we don't need the returned handle since
            we can also get that inside the fetch-callback from the response
            structure.
            - NOTE that we're not using the user_data member, since all required
                state is in a global variable anyway.
        */

        sfetch_request_t request = default;
        request.path = util_get_file_path("baboon.png");
        request.callback = &fetch_callback;
        request.buffer = SFETCH_RANGE(state.fetch_buffer);
        sfetch_send(request);

    }

    [UnmanagedCallersOnly]
    static void fetch_callback(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            /* the file data has been fetched, since we provided a big-enough
                buffer we can be sure that all data has been loaded here
            */
            
            // Decode PNG using native STB from the fetched data in the buffer
            int png_width = 0, png_height = 0, channels = 0;
            byte* pixels = stbi_load_csharp(
                in state.fetch_buffer.Buffer[0],
                (int)response->data.size,
                ref png_width,
                ref png_height,
                ref channels,
                4  // desired_channels: force RGBA
            );

            if (pixels == null)
            {
                state.pass_action.colors[0].clear_value = new sg_color() { r = 1f, g = 0f, b = 0f, a = 1.0f };
                return;
            }

            // Create the image descriptor with the decoded pixel data
            sg_image_desc image_desc = new sg_image_desc();
            image_desc.width = png_width;
            image_desc.height = png_height;
            image_desc.pixel_format = SG_PIXELFORMAT_RGBA8;
            
            // Calculate the size of the pixel data
            int pixel_data_size = png_width * png_height * 4;
            
            // Create a span from the native pointer for the image data
            ReadOnlySpan<byte> pixelSpan = new ReadOnlySpan<byte>(pixels, pixel_data_size);
            image_desc.data.mip_levels[0] = SG_RANGE(pixelSpan);
            
            sg_image img = sg_make_image(image_desc);

            // Free the native STB image data
            stbi_image_free_csharp(pixels);

            // ...and initialize the pre-allocated texture view handle with that image
            sg_init_view(state.bind.views[VIEW_tex], new sg_view_desc()
            {
                texture = new sg_texture_view_desc(){ image = img },
                label = "png-texture-view",
            });

        }
        else if (response->failed)
        {
            state.pass_action.colors[0].clear_value = new sg_color() { r = 1f, g = 0f, b = 0f, a = 1.0f };
        }

    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        if (PauseUpdate) return;

        // pump the sokol-fetch message queues, and invoke response callbacks
        sfetch_dowork();

        // compute model-view-projection matrix for vertex shader
        float t = (float)(sapp_frame_duration());

        var proj = CreatePerspectiveFieldOfView((float)(60.0f * Math.PI / 180), sapp_widthf() / sapp_heightf(), 0.01f, 10.0f);
        Matrix4x4 view = CreateLookAt(new Vector3(0.0f, 1.5f, 6.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f));
        vs_params_t vs_params = default;
        state.rx += 1.0f * t; state.ry += 2.0f * t;
        Matrix4x4 rxm = CreateRotationX(state.rx);
        Matrix4x4 rym = CreateRotationY(state.ry);
        Matrix4x4 model = rxm * rym;
        vs_params.mvp = model * view * proj;

        sg_begin_pass(new sg_pass() { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pip);
        sg_apply_bindings(state.bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_draw(0, 36, 1);
        sg_end_pass();
        sg_commit();

    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        //Dispose the shared buffer
        state.fetch_buffer.Dispose();

        if (state.bind.vertex_buffers[0].id != 0)
        {
            sg_destroy_buffer(state.bind.vertex_buffers[0]);
        }
        if (state.bind.views[VIEW_tex].id != 0)
        {
            sg_destroy_view(state.bind.views[VIEW_tex]);
        }

        sfetch_shutdown();
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
            PauseUpdate = !PauseUpdate;
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
            window_title = "Async PNG Loading (sokol-app)",
            icon = { sokol_default = true },
        };
    }

}