
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static System.Numerics.Matrix4x4;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SFetch;
using static Sokol.Utils;
using static Sokol.SApp;
using static Sokol.SAudio;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_filter;
using static Sokol.SG.sg_wrap;
using static Sokol.SG.sg_load_action;
using static plmpeg_sapp_shader_cs.Shaders;
using System.Diagnostics;
using static Sokol.SLog;
public static unsafe partial class PlMpegApp
{
    // application state
    class _state
    {
        public plm_t* plm;
        public plm_buffer_t* plm_buffer;
        public sg_pipeline pip;
        public sg_bindings bind;
        public sg_pass_action pass_action;
        public images_t[] images = new images_t[3];
        public ring_t free_buffers = new ring_t();
        public ring_t full_buffers = new ring_t();
        public int cur_download_buffer;
        public int cur_read_buffer;
        public uint cur_read_pos;
        public float ry;
        public UInt64 cur_frame;
    };

    static _state state = new _state();

    static bool PauseUpdate = false;


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        for (int i = 0; i < NUM_BUFFERS; i++)
        {
            sharedBuffer[i] = SharedBuffer.Create(BUFFER_SIZE);
        }

        // setup circular queues of "free" and "full" buffers
        for (int i = 0; i < NUM_BUFFERS; i++)
        {
            ring_enqueue(state.free_buffers, i);
        }
        state.cur_download_buffer = ring_dequeue(state.free_buffers);
        state.cur_read_buffer = -1;


        sfetch_setup(new sfetch_desc_t()
        {
            max_requests = 1,
            num_channels = 1,
            num_lanes = 1,
            logger = {
                func = &slog_func,
            }
        });

        sfetch_request_t request = default;
        request.path = util_get_file_path(filename);
        request.callback = &fetch_callback;
        var buf = sharedBuffer[state.cur_download_buffer];
        request.buffer = SFETCH_RANGE(sharedBuffer[state.cur_download_buffer]);
        request.chunk_size = CHUNK_SIZE;

        sfetch_send(request);

        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = {
                func = &slog_func,
            }
        });

        Span<vertex_t> vertex_buffer = Utils.CreateSpan<vertex_t>(16);
        fixed (vertex_t* vertices = &vertex_buffer[0])
        {
            vertices[0] = new vertex_t() { x = -1, y = -1, z = -1, nx = 0, ny = 0, nz = -1, u = 1, v = 1 };
            vertices[1] = new vertex_t() { x = 1, y = -1, z = -1, nx = 0, ny = 0, nz = -1, u = 0, v = 1 };
            vertices[2] = new vertex_t() { x = 1, y = 1, z = -1, nx = 0, ny = 0, nz = -1, u = 0, v = 0 };
            vertices[3] = new vertex_t() { x = -1, y = 1, z = -1, nx = 0, ny = 0, nz = -1, u = 1, v = 0 };

            vertices[4] = new vertex_t() { x = -1, y = -1, z = 1, nx = 0, ny = 0, nz = 1, u = 0, v = 1 };
            vertices[5] = new vertex_t() { x = 1, y = -1, z = 1, nx = 0, ny = 0, nz = 1, u = 1, v = 1 };
            vertices[6] = new vertex_t() { x = 1, y = 1, z = 1, nx = 0, ny = 0, nz = 1, u = 1, v = 0 };
            vertices[7] = new vertex_t() { x = -1, y = 1, z = 1, nx = 0, ny = 0, nz = 1, u = 0, v = 0 };

            vertices[8] = new vertex_t() { x = -1, y = -1, z = -1, nx = -1, ny = 0, nz = 0, u = 0, v = 1 };
            vertices[9] = new vertex_t() { x = -1, y = 1, z = -1, nx = -1, ny = 0, nz = 0, u = 0, v = 0 };
            vertices[10] = new vertex_t() { x = -1, y = 1, z = 1, nx = -1, ny = 0, nz = 0, u = 1, v = 0 };
            vertices[11] = new vertex_t() { x = -1, y = -1, z = 1, nx = -1, ny = 0, nz = 0, u = 1, v = 1 };

            vertices[12] = new vertex_t() { x = 1, y = -1, z = -1, nx = 1, ny = 0, nz = 0, u = 1, v = 1 };
            vertices[13] = new vertex_t() { x = 1, y = 1, z = -1, nx = 1, ny = 0, nz = 0, u = 1, v = 0 };
            vertices[14] = new vertex_t() { x = 1, y = 1, z = 1, nx = 1, ny = 0, nz = 0, u = 0, v = 0 };
            vertices[15] = new vertex_t() { x = 1, y = -1, z = 1, nx = 1, ny = 0, nz = 0, u = 0, v = 1 };
        }

        state.bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE<vertex_t>(vertex_buffer),
            label = "vertices"
        });

        UInt16[] indices = {
        0, 1, 2,  0, 2, 3,
        6, 5, 4,  7, 6, 4,
        8, 9, 10,  8, 10, 11,
        14, 13, 12,  15, 14, 12,
    };

        state.bind.index_buffer = sg_make_buffer(new sg_buffer_desc()
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE(indices),
            label = "indices"
        });

        sg_pipeline_desc pipeline_desc = default;
        pipeline_desc.layout.attrs[ATTR_plmpeg_pos].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_plmpeg_normal].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_plmpeg_texcoord].format = SG_VERTEXFORMAT_FLOAT2;
        pipeline_desc.shader = sg_make_shader(plmpeg_shader_desc(sg_query_backend()));
        pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        pipeline_desc.cull_mode = SG_CULLMODE_NONE;
        pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pipeline_desc.depth.write_enabled = true;
        state.pip = sg_make_pipeline(pipeline_desc);

        sg_sampler_desc sampler_desc = default;
        sampler_desc.min_filter = SG_FILTER_LINEAR;
        sampler_desc.mag_filter = SG_FILTER_LINEAR;
        sampler_desc.wrap_u = SG_WRAP_CLAMP_TO_EDGE;
        sampler_desc.wrap_v = SG_WRAP_CLAMP_TO_EDGE;
        state.bind.samplers[SMP_smp] = sg_make_sampler(sampler_desc);

        state.pass_action = default;
        state.pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color() { r = 0.0f, g = 0.569f, b = 0.918f, a = 1.0f };

        // NOTE: texture creation is deferred until first frame is decoded


    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        state.cur_frame++;

        // pump the sokol-fetch message queues
        sfetch_dowork();


        // stop decoding if there's not at least one buffer of downloaded
        // data ready, to allow slow downloads to catch up
        if (state.plm != null)
        {
            if (!ring_empty(state.full_buffers))
            {
                plm_decode(state.plm, sapp_frame_duration());
            }
        }
        // initialize plmpeg once two buffers are filled with data
        else if (ring_count(state.full_buffers) == 2)
        {
            state.plm_buffer = plm_buffer_create_with_capacity(BUFFER_SIZE);
            plm_buffer_set_load_callback(state.plm_buffer, (IntPtr)(delegate* unmanaged<plm_buffer_t*, void*, void>)&plmpeg_load_callback, null);
            state.plm = plm_create_with_buffer(state.plm_buffer, 1);
            // assert(state.plm);
            plm_set_video_decode_callback(state.plm, (IntPtr)(delegate* unmanaged<plm_t*, plm_frame_t*, void*, void>)&video_cb, null);
            plm_set_audio_decode_callback(state.plm, (IntPtr)(delegate* unmanaged<plm_t*, plm_samples_t*, void*, void>)&audio_cb, null);
            plm_set_loop(state.plm, 1);
            plm_set_audio_enabled(state.plm, 1, 0);
            plm_set_audio_lead_time(state.plm, 0.25);
            if (plm_get_num_audio_streams(state.plm) > 0)
            {
                saudio_setup(new saudio_desc()
                {
                    sample_rate = plm_get_samplerate(state.plm),
                    buffer_frames = 4096,
                    num_packets = 256,
                    num_channels = 2,
                    logger = {
                        func = &slog_func,
                    }
                });
            }
        }

        // compute model-view-projection matrix for vertex shader
        var proj = CreatePerspectiveFieldOfView((float)(60.0f * Math.PI / 180), sapp_widthf() / sapp_heightf(), 0.01f, 10.0f);
        var view = CreateLookAt(new Vector3(0.0f, 0f, 6.0f), Vector3.Zero, Vector3.UnitY);
        var view_proj = view * proj;
        vs_params_t vs_params = default;
        state.ry += -0.1f * (float)sapp_frame_duration();
        var model = CreateRotationY(state.ry);
        vs_params.mvp = model * view_proj;

        sg_begin_pass(new sg_pass() { action = state.pass_action, swapchain = sglue_swapchain() });
        if (state.bind.views[0].id != SG_INVALID_ID)
        {
            sg_apply_pipeline(state.pip);
            sg_apply_bindings(state.bind);
            sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
            sg_draw(0, 24, 1);
        }
        // __dbgui_draw();
        sg_end_pass();
        sg_commit();

    }


    // (re-)create a video plane texture on demand, and update it with decoded video-plane data
    static void validate_texture(int slot, plm_plane_t* plane)
    {

        if ((state.images[slot].width != (int)plane->width) ||
            (state.images[slot].height != (int)plane->height))
        {
            state.images[slot].width = (int)plane->width;
            state.images[slot].height = (int)plane->height;

            // NOTE: it's ok to call sg_destroy_image() with SG_INVALID_ID
            sg_destroy_image(state.images[slot].img);
            sg_image_desc desc = default;
            desc.width = (int)plane->width;
            desc.height = (int)plane->height;
            desc.pixel_format = SG_PIXELFORMAT_R8;
            desc.usage.stream_update = true;
            state.images[slot].img = sg_make_image(desc);

            // recreate associated view
            sg_destroy_view(state.bind.views[slot]);
            state.bind.views[slot] = sg_make_view(new sg_view_desc()
            {
                texture = { image = state.images[slot].img },
            });
        }

        // copy decoded plane pixels into texture, need to prevent that
        // sg_update_image() is called more than once per frame
        if (state.images[slot].last_upd_frame != state.cur_frame)
        {
            state.images[slot].last_upd_frame = state.cur_frame;
            sg_image_data image_data = default;
            image_data.mip_levels[0] = new sg_range() { ptr = plane->data, size = (uint)(plane->width * plane->height * sizeof(byte)) };
            sg_update_image(state.images[slot].img, image_data);
        }
    }

    [UnmanagedCallersOnly]
    static void video_cb(plm_t* mpeg, plm_frame_t* frame, void* user)
    {
        validate_texture(VIEW_tex_y, &frame->y);
        validate_texture(VIEW_tex_cb, &frame->cb);
        validate_texture(VIEW_tex_cr, &frame->cr);
    }

    [UnmanagedCallersOnly]
    static void audio_cb(plm_t* mpeg, plm_samples_t* samples, void* user)
    {
        saudio_push(samples->interleaved[0], (int)samples->count);
    }


    [UnmanagedCallersOnly]
    static void fetch_callback(sfetch_response_t* response)
    {

        if (response->fetched)
        {
            // put the download buffer into the "full_buffers" queue
            ring_enqueue(state.full_buffers, state.cur_download_buffer);
            if (ring_full(state.full_buffers) || ring_empty(state.free_buffers))
            {
                // all buffers in use, need to wait for the video decoding to catch up
                sfetch_pause(response->handle);
            }
            else
            {
                // ...otherwise start streaming into the next free buffer
                state.cur_download_buffer = ring_dequeue(state.free_buffers);
                sfetch_unbind_buffer(response->handle);
                sfetch_bind_buffer(response->handle, SFETCH_RANGE(sharedBuffer[state.cur_download_buffer]));
            }
        }
        else if (response->paused)
        {
            // this handles a paused download, and continues it once the video
            // decoding has caught up
            if (!ring_empty(state.free_buffers))
            {
                state.cur_download_buffer = ring_dequeue(state.free_buffers);
                sfetch_unbind_buffer(response->handle);
                sfetch_bind_buffer(response->handle, SFETCH_RANGE(sharedBuffer[state.cur_download_buffer]));
                sfetch_continue(response->handle);
            }
        }
        else
        {
            // error or aborted
            Console.WriteLine($"sfetch error or aborted (0x{(int)response->error_code:x})");
        }
    }


    // the plmpeg load callback, this is called when plmpeg needs new data,
    // this takes buffers loaded with video data from the "full-queue"
    // as needed
    [UnmanagedCallersOnly]
    static void plmpeg_load_callback(plm_buffer_t* self, void* user)
    {
        if (state.cur_read_buffer == -1)
        {
            state.cur_read_buffer = ring_dequeue(state.full_buffers);
            state.cur_read_pos = 0;
        }

        plm_buffer_discard_read_bytes(self);
        int length = plm_buffer_get_length(self);
        byte* buf = plm_buffer_get_bytes(self);
        int bytes_wanted = plm_buffer_get_capacity(self) - length;
        uint bytes_available = BUFFER_SIZE - state.cur_read_pos;
        uint bytes_to_copy = (uint)((bytes_wanted > bytes_available) ? bytes_available : (uint)bytes_wanted);
        byte* dst = buf + length;
        fixed (byte* src = &sharedBuffer[state.cur_read_buffer].Buffer[state.cur_read_pos])
        {
            Buffer.MemoryCopy(src, dst, bytes_to_copy, bytes_to_copy);
        }
        plm_buffer_set_length(self, (int)(length + bytes_to_copy));
        length = plm_buffer_get_length(self);
        state.cur_read_pos += bytes_to_copy;
        if (state.cur_read_pos == BUFFER_SIZE)
        {
            ring_enqueue(state.free_buffers, state.cur_read_buffer);
            state.cur_read_buffer = -1;
        }
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        SharedBuffer.DisposeAll();

        if (state.plm_buffer != null)
        {
            plm_buffer_destroy(state.plm_buffer);
        }

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
            width = 960,
            height = 540,
            sample_count = 1,
            window_title = "pl_mpeg demo",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
