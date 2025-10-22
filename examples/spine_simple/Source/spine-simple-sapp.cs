
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SLog;
using static Sokol.Utils;
using static Sokol.SApp;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_load_action;
using static Sokol.SSpine;
using static Sokol.SFetch;
using static Sokol.StbImage;
using System.Diagnostics;


public static unsafe class SpineSimpleApp
{

    static bool PauseUpdate = false;

    struct load_status_t
    {
        public bool loaded;
        public sspine_range data;
    };

    struct _load_status
    {
        public load_status_t atlas;
        public load_status_t skeleton;
        public bool failed;
    };

    class _buffers
    {
        public SharedBuffer atlas = SharedBuffer.Create(4 * 1024);
        public SharedBuffer skeleton = SharedBuffer.Create(128 * 1024);
        public SharedBuffer image = SharedBuffer.Create(512 * 1024);
    };

    class _state
    {
        public sspine_atlas atlas;
        public sspine_skeleton skeleton;
        public sspine_instance instance;
        public sg_pass_action pass_action;
        public _load_status load_status;
        public _buffers buffers = new _buffers();
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

        // Setup sokol_spine.h, if desired, memory usage can be tuned by
        // setting the max number of vertices, draw commands and pool sizes

        sspine_setup(new sspine_desc()
        {
            max_vertices = 6 * 1024,
            max_commands = 16,
            atlas_pool_size = 1,
            skeleton_pool_size = 1,
            skinset_pool_size = 1,
            instance_pool_size = 1,
            logger = {
            // func =  &slog_func,
             },
        });

        // We'll use sokol_fetch.h for data loading because this gives us
        // asynchronous file loading which also works on the web.
        // The only downside is that spine initialization is spread
        // over a couple of callbacks and frames.
        // Configure sokol-fetch so that atlas and skeleton file
        // data are loaded in parallel across 2 channels.
        sfetch_setup(new sfetch_desc_t()
        {
            max_requests = 3,
            num_channels = 2,
            num_lanes = 1,
            // logger.func = slog_func,
        });

        // Setup a sokol-gfx pass action to clear the default framebuffer to black
        // (used in sg_begin_pass() down in the frame callback)
        sg_pass_action pass_action = new sg_pass_action();
        pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        pass_action.colors[0].clear_value = new sg_color() { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };
        state.pass_action = pass_action;


        // Start loading the spine atlas and skeleton file. This happens asynchronously
        // and in undefined finish-order. The 'fetch callbacks' will be called when the data
        // has finished loading (or an error occurs).
        // sokol_spine.h itself doesn't care about how the data is loaded, it expects
        // all data in memory chunks.

        sfetch_request_t req = default;
        req.path = util_get_file_path("spine/raptor-pma.atlas");
        req.channel = 0;
        req.buffer = SFETCH_RANGE(state.buffers.atlas);
        req.callback = &atlas_data_loaded;
        sfetch_send(req);

        req = default;
        req.path = util_get_file_path("spine/raptor-pro.skel");
        req.channel = 1;
        req.buffer = SFETCH_RANGE(state.buffers.skeleton);
        req.callback = &skeleton_data_loaded;
        sfetch_send(req);


    }

    // sokol-fetch callback functions for loading the atlas and skeleton data.
    // These are called in undefined order, but the spine atlas must be created
    // before the skeleton (because the skeleton creation functions needs an
    // atlas handle), this ordering problem is solved by both functions checking
    // whether the other function has already finished, and if yes a common
    // function 'create_spine_objects()' is called
    [UnmanagedCallersOnly]
    static void atlas_data_loaded(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            // atlas data was successfully loaded
            state.load_status.atlas = new load_status_t()
            {
                loaded = true,
                data = new sspine_range() { ptr = response->data.ptr, size = response->data.size },
            };
            // when both atlas and skeleton files have finished loading, create spine objects
            if (state.load_status.atlas.loaded && state.load_status.skeleton.loaded)
            {
                create_spine_objects();
            }
        }
        else if (response->failed)
        {
            Console.WriteLine("atlas_data_loaded failed");
            // loading the atlas data failed
            state.load_status.failed = true;
        }
    }

    [UnmanagedCallersOnly]
    static void skeleton_data_loaded(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            state.load_status.skeleton = new load_status_t()
            {
                loaded = true,
                data = new sspine_range() { ptr = response->data.ptr, size = response->data.size }
            };
            if (state.load_status.atlas.loaded && state.load_status.skeleton.loaded)
            {
                create_spine_objects();
            }
        }
        else if (response->failed)
        {
            Console.WriteLine("skeleton_data_loaded failed");
            state.load_status.failed = true;
        }
    }

    // this function is called when both the spine atlas and skeleton file has been loaded,
    // first an atlas object is created from the loaded atlas data, and then a skeleton
    // object (which requires an atlas object as dependency), then a spine instance object.
    // Finally any images required by the atlas object are loaded
    static void create_spine_objects()
    {
        // Create spine atlas object from loaded atlas data.
        state.atlas = sspine_make_atlas(new sspine_atlas_desc()
        {
            data = state.load_status.atlas.data,
        });

        // Next create a spine skeleton object, skeleton data files can be either
        // text (JSON) or binary (in our case, 'raptor-pro.skel' is a binary skeleton file).
        // In case of JSON data, make sure that the data is 0-terminated!
        state.skeleton = sspine_make_skeleton(new sspine_skeleton_desc()
        {
            atlas = state.atlas,
            binary_data = state.load_status.skeleton.data,
            // we can pre-scale the skeleton...
            prescale = 0.5f,
            // and we can set the default animation mixing / cross-fading time
            anim_default_mix = 0.2f,
        });

        // create a spine instance object, that's the thing that's actually rendered
        state.instance = sspine_make_instance(new sspine_instance_desc()
        {
            skeleton = state.skeleton,
        });

        // Since the spine instance doesn't move, its position can be set once,
        // the coordinate units depends on the sspine_layer_transform struct
        // that's passed to the sspine_draw_layer() during rendering (in our
        // case it's simply framebuffer pixels, with the origin in the
        // center)
        sspine_set_position(state.instance, new sspine_vec2() { x = -100.0f, y = 200.0f });

        // var testname = sspine_anim_by_name(state.skeleton, "jump");
        // configure a simple animation sequence (jump => roar => walk)
        sspine_set_animation(state.instance, sspine_anim_by_name(state.skeleton, "jump"), 0, false);
        sspine_add_animation(state.instance, sspine_anim_by_name(state.skeleton, "roar"), 0, false, 0.0f);
        sspine_add_animation(state.instance, sspine_anim_by_name(state.skeleton, "walk"), 0, true, 0.0f);

        // Finally start loading any atlas image files, one image file seems to be
        // common, but apparently atlases can also reference multiple images.
        // Image loading also happens asynchronously via sokol-fetch, and the
        // actual sokol-gfx image creation happens in the fetch-callback.
        int num_images = sspine_num_images(state.atlas);
        for (int img_index = 0; img_index < num_images; img_index++)
        {
            sspine_image img = sspine_image_by_index(state.atlas, img_index);
            sspine_image_info img_info = sspine_get_image_info(img);

            // We'll store the sspine_image handle in the fetch request's user data
            // blob, because we need the image info again later in the fetch callback
            // in order to initialize the sokol-gfx image with the right parameters.
            //
            // Also important to note: all image fetch requests load their data into the same
            // buffer. This is fine because sokol-fetch has been configured
            // with num_lanes=1, this will cause all requests on the same
            // channel to be serialized (not run in parallel). That way
            // the same buffer can be reused even if there are multiple atlas images.
            // The downside is that loading multiple images would take longer.

            sfetch_request_t req = default;
            string result = img_info.filename.String();
            req.path = util_get_file_path(Path.Combine("spine",result));
            Console.WriteLine("Loading image data req.path: " + req.path);
            req.channel = 0;
            req.buffer = SFETCH_RANGE(state.buffers.image);
            req.callback = &image_data_loaded;
            req.user_data = new sfetch_range_t() { ptr = Unsafe.AsPointer(ref img), size = (uint)Marshal.SizeOf<sspine_image>() };
            sfetch_send(req);

        }

    }

    // This is the image-data fetch callback. The loaded image data will be decoded
    // via stb_image.h and a sokol-gfx image object will be created.
    //
    // What's interesting here is that we're using sokol-gfx's multi-step
    // image setup. sokol-spine has already allocated an image handle
    // for each atlas image in sspine_make_atlas() via sg_alloc_image().
    //
    // The fetch callback just needs to finish the image setup by
    // calling sg_init_image(), or if loading has failed, put the
    // image object into the 'failed' resource state.
    //
    [UnmanagedCallersOnly]
    static void image_data_loaded(sfetch_response_t* response)
    {
        sspine_image img = *(sspine_image*)response->user_data;
        sspine_image_info img_info = sspine_get_image_info(img);
        if (response->fetched)
        {
            // Decode image using native STB from the fetched data in the buffer
            int img_width = 0, img_height = 0, channels = 0;
            byte* pixels = stbi_load_csharp(
                in state.buffers.image.Buffer[0],
                (int)response->data.size,
                ref img_width,
                ref img_height,
                ref channels,
                4  // desired_channels: force RGBA
            );

            if (pixels != null)
            {
                // sokol-spine has already allocated an image and sampler handle,
                // just need to call sg_init_image() and sg_init_sampler() to complete setup
                int pixel_data_size = img_width * img_height * 4;
                ReadOnlySpan<byte> pixelSpan = new ReadOnlySpan<byte>(pixels, pixel_data_size);
                
                sg_image_desc image_Desc = default;
                image_Desc.width = img_width;
                image_Desc.height = img_height;
                image_Desc.pixel_format = SG_PIXELFORMAT_RGBA8;
                image_Desc.label = img_info.filename.String();
                image_Desc.data.mip_levels[0] = SG_RANGE(pixelSpan);
                sg_init_image(img_info.sgimage, image_Desc);

                // Free the native STB image data
                stbi_image_free_csharp(pixels);

                sg_init_view(img_info.sgview, new sg_view_desc(){
                    texture = { image = img_info.sgimage },
                });

                sg_sampler_desc sampler_Desc = default;
                sampler_Desc.min_filter = img_info.min_filter;
                sampler_Desc.mag_filter = img_info.mag_filter;
                sampler_Desc.mipmap_filter = img_info.mipmap_filter;
                sampler_Desc.wrap_u = img_info.wrap_u;
                sampler_Desc.wrap_v = img_info.wrap_v;
                sampler_Desc.label = img_info.filename.String();
                sg_init_sampler(img_info.sgsampler, sampler_Desc);
            }
            else
            {
                // decoding has failed
                state.load_status.failed = true;
                // image decoding has failed, it's not strictly necessary, but
                // it's better here to put the sokol-gfx image object into
                // the 'failed' resource state (otherwise it would be stuck
                // in the 'alloc' state)
                sg_fail_image(img_info.sgimage);
            }
        }
        else
        {
            Console.WriteLine("image_data_loaded failed");
            state.load_status.failed = true;
            sg_fail_image(img_info.sgimage);
        }
    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // if (PauseUpdate) return;
        // need to call sfetch_dowork() once per frame, otherwise data loading will appear to be stuck
        sfetch_dowork();

        // the frame duration in seconds is needed for advancing the spine animations
        float delta_time = (float)sapp_frame_duration();
        // use the window size for the spine canvas, this means that 'spine pixels'
        // will map 1:1 to framebuffer pixels, with [0,0] in the center
        float w = sapp_widthf();
        float h = sapp_heightf();
        sspine_layer_transform layer_transform = new sspine_layer_transform()
        {
            size = new sspine_vec2 { x = w, y = h },
            origin = new sspine_vec2 { x = w * 0.5f, y = h * 0.5f }
        };

        // Advance the instance animation and draw the instance.
        // Important to note here is that no actual sokol-gfx rendering happens yet,
        // instead sokol-spine will only record vertices, indices and draw commands.
        // Also, all sokol-spine functions can be called with invalid or 'incomplete'
        // handles, that way we don't need to care about whether the spine objects
        // have actually been created yet (because their data might still be loading)
        sspine_update_instance(state.instance, delta_time);
        sspine_draw_instance_in_layer(state.instance, 0);

        // The actual sokol-gfx render pass, here we also don't need to care about
        // if the atlas image have already been loaded yet, if the image handles
        // recorded by sokol-spine for rendering are not yet valid, rendering
        // operations will silently be skipped.
        sg_begin_pass(new sg_pass() { action = state.pass_action, swapchain = sglue_swapchain() });
        sspine_draw_layer(0, layer_transform);
        // __dbgui_draw();
        sg_end_pass();
        sg_commit();

    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        sfetch_shutdown();
        sspine_shutdown();
        sg_shutdown();
        
        // Dispose pinned buffers
        state.buffers.atlas?.Dispose();
        state.buffers.skeleton?.Dispose();
        state.buffers.image?.Dispose();

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
            width = 1024,
            height = 768,
            sample_count = 1,
            window_title = "spine-simple-app",
            icon = { sokol_default = true },
        };
    }


}