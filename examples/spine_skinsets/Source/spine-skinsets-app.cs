
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using static Sokol.SApp;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_load_action;
using static Sokol.SSpine;
using static Sokol.SFetch;
using static Sokol.STM;
using static Sokol.SDebugText;
using static Sokol.StbImage;

using vec2 = Sokol.SSpine.sspine_vec2;
using System.Diagnostics;


public static unsafe class SpineSkinSetApp
{

    static bool PauseUpdate = false;

    const int NUM_INSTANCES_X = 16;
    const int NUM_INSTANCES_Y = 8;
    const int NUM_INSTANCES = NUM_INSTANCES_X * NUM_INSTANCES_Y;
    const int NUM_SKINS = 8;
    const float PRESCALE = 0.15f;
    const float GRID_DX = 64.0f;
    const float GRID_DY = 96.0f;

    struct load_status_t
    {
        public bool loaded;
        public sspine_range data;
    }

    struct grid_cell_t
    {
        public grid_cell_t()
        {

        }
        public vec2 pos = new vec2();
        public vec2 vec = new vec2();
    }

    class _state
    {
        public sspine_atlas atlas;
        public sspine_skeleton skeleton;
        public sspine_instance[] instances = new sspine_instance[NUM_INSTANCES];
        public sg_pass_action pass_action;
        public float t;       // time interval 0..1
        public uint t_count;   // bumped each time t goes over 1
        public grid_cell_t[] grid = new grid_cell_t[NUM_INSTANCES];
        public load_status_t atlas_load_status;
        public load_status_t skeleton_load_status;
        public bool load_status_failed;
        public SharedBuffer atlas_buffer = SharedBuffer.Create(16 * 1024);
        public SharedBuffer skeleton_buffer = SharedBuffer.Create(300 * 1024);
        public SharedBuffer image_buffer = SharedBuffer.Create(512 * 1024);
    };

    static _state state = new _state();

    //unique skins to be combined into skin sets
    static string[] accessories = new string[NUM_SKINS] {
        "accessories/backpack",
        "accessories/bag",
        "accessories/cape-blue",
        "accessories/cape-red",
        "accessories/hat-pointy-blue-yellow",
        "accessories/hat-red-yellow",
        "accessories/scarf",
        "accessories/backpack",
    };

    static string[] clothes = new string[NUM_SKINS] {
        "clothes/dress-blue",
        "clothes/dress-green",
        "clothes/hoodie-blue-and-scarf",
        "clothes/hoodie-orange",
        "clothes/dress-blue",
        "clothes/dress-green",
        "clothes/hoodie-blue-and-scarf",
        "clothes/hoodie-orange"
    };

    static string[] eyelids = new string[NUM_SKINS] {
        "eyelids/girly",
        "eyelids/semiclosed",
        "eyelids/girly",
        "eyelids/semiclosed",
        "eyelids/girly",
        "eyelids/semiclosed",
        "eyelids/girly",
        "eyelids/semiclosed",
    };


    static string[] eyes = new string[NUM_SKINS] {
        "eyes/eyes-blue",
        "eyes/green",
        "eyes/violet",
        "eyes/yellow",
        "eyes/eyes-blue",
        "eyes/green",
        "eyes/violet",
        "eyes/yellow",
    };

    static string[] hair = new string[NUM_SKINS] {
        "hair/blue",
        "hair/brown",
        "hair/long-blue-with-scarf",
        "hair/pink",
        "hair/short-red",
        "hair/blue",
        "hair/brown",
        "hair/long-blue-with-scarf",
    };

    static string[] legs = new string[NUM_SKINS] {
        "legs/boots-pink",
        "legs/boots-red",
        "legs/pants-green",
        "legs/pants-jeans",
        "legs/boots-pink",
        "legs/boots-red",
        "legs/pants-green",
        "legs/pants-jeans"
    };

    static string[] nose = new string[NUM_SKINS] {
        "nose/long",
        "nose/short",
        "nose/long",
        "nose/short",
        "nose/long",
        "nose/short",
        "nose/long",
        "nose/short",
    };
    static sg_logger logger = new sg_logger();


    [UnmanagedCallersOnly]
    static void slog_func_wrapper(byte* tag, uint log_level, uint log_item, byte* message, uint line_nr, byte* filename, void* user_data)
    {
        logger.func(tag, log_level, log_item, message, line_nr, filename, user_data);
    }

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = new sg_logger()
        });

        stm_setup();

        sdtx_desc_t desc = default;
        desc.fonts[0] = sdtx_font_oric();
        desc.logger.func = &slog_func_wrapper;

        sdtx_setup(desc);

        // setup sokol-spine
        sspine_setup(new sspine_desc
        {
            skinset_pool_size = NUM_INSTANCES,
            instance_pool_size = NUM_INSTANCES,
            max_vertices = 256 * 1024,
            logger = {
            func = &slog_func_wrapper,
        },
        });

        sfetch_setup(new sfetch_desc_t()
        {
            max_requests = 3,
            num_channels = 2,
            num_lanes = 1,
            logger = {
            func = &slog_func_wrapper,
        },
        });

        sg_pass_action pass_action = default;
        pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        pass_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.5f, b = 0.7f, a = 1.0f };
        state.pass_action = pass_action;

        sfetch_send(new sfetch_request_t
        {
            path = util_get_file_path(Path.Combine("spine", "mix-and-match-pma.atlas")),
            channel = 0,
            buffer = SFETCH_RANGE(state.atlas_buffer),
            callback = &atlas_data_loaded,
        });

        sfetch_send(new sfetch_request_t
        {
            path = util_get_file_path(Path.Combine("spine", "mix-and-match-pro.skel")),
            channel = 1,
            buffer = SFETCH_RANGE(state.skeleton_buffer),
            callback = &skeleton_data_loaded,
        });

        const float dx = GRID_DX;
        const float dy = GRID_DY;
        float y = -dy * (NUM_INSTANCES_Y / 2) + dy;
        for (int iy = 0; iy < NUM_INSTANCES_Y; iy++)
        {
            float x = -dx * (NUM_INSTANCES_X / 2) + dx * 0.5f;
            for (int ix = 0; ix < NUM_INSTANCES_X; ix++)
            {
                grid_cell_t* cell = (grid_cell_t*)Unsafe.AsPointer(ref state.grid[iy * NUM_INSTANCES_X + ix]);
                if ((iy & 1) == 0)
                {
                    cell->pos = new vec2 { x = x + ix * dx, y = y + iy * dy };
                    if (ix == (NUM_INSTANCES_X - 1)) { cell->vec = new vec2 { x = 0.0f, y = 1.0f }; }
                    else { cell->vec = new vec2 { x = 1.0f, y = 0.0f }; }
                }
                else
                {
                    cell->pos = new vec2 { x = x + (NUM_INSTANCES_X - 1 - ix) * dx, y = y + iy * dy };
                    if (ix == (NUM_INSTANCES_X - 1)) { cell->vec = new vec2 { x = 0.0f, y = 1.0f }; }
                    else { cell->vec = new vec2 { x = -1.0f, y = 0.0f }; }
                }
            }
        }

    }


    [UnmanagedCallersOnly]
    static void atlas_data_loaded(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            state.atlas_load_status = new load_status_t
            {
                loaded = true,
                data = new sspine_range { ptr = response->data.ptr, size = response->data.size },
            };
            // when both atlas and skeleton file have finished loading, create spine objects
            if (state.atlas_load_status.loaded && state.skeleton_load_status.loaded)
            {
                create_spine_objects();
            }
        }
        else if (response->failed)
        {
            state.load_status_failed = true;
        }
    }

    [UnmanagedCallersOnly]
    static void skeleton_data_loaded(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            state.skeleton_load_status = new load_status_t
            {
                loaded = true,
                data = new sspine_range { ptr = response->data.ptr, size = response->data.size },
            };
            // when both atlas and skeleton file have finished loading, create spine objects
            if (state.atlas_load_status.loaded && state.skeleton_load_status.loaded)
            {
                create_spine_objects();
            }
        }
        else if (response->failed)
        {
            state.load_status_failed = true;
        }
    }

    // load spine atlas image data and create a sokol-gfx image object
    [UnmanagedCallersOnly]
    static void image_data_loaded(sfetch_response_t* response)
    {
        sspine_image img = *(sspine_image*)response->user_data;
        sspine_image_info img_info = sspine_get_image_info(img);
        // assert(img_info.valid);
        if (response->fetched)
        {
            // Decode image using native STB from the fetched data in the buffer
            int img_width = 0, img_height = 0, channels = 0;
            byte* pixels = stbi_load_csharp(
                in state.image_buffer.Buffer[0],
                (int)response->data.size,
                ref img_width,
                ref img_height,
                ref channels,
                4  // desired_channels: force RGBA
            );

            if (pixels != null)
            {
                // sokol-spine has already allocated a sokol-gfx image and sampler handle for use,
                // now "populate" the handles with an actual image and sampler
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

                sg_init_view(img_info.sgview, new sg_view_desc()
                {
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
                state.load_status_failed = true;
                sg_fail_image(img_info.sgimage);
            }
        }
        else if (response->failed)
        {
            state.load_status_failed = true;
            sg_fail_image(img_info.sgimage);
        }
    }

    // called when both the atlas and skeleton files have been loaded,
    // creates an sspine_atlas and sspine_skeleton object, starts loading
    // the atlas texture(s) and finally creates and sets up spine instances
    static void create_spine_objects()
    {

        // create spine atlas object
        state.atlas = sspine_make_atlas(new sspine_atlas_desc
        {
            data = state.atlas_load_status.data
        });


        // create spine skeleton object
        state.skeleton = sspine_make_skeleton(new sspine_skeleton_desc
        {
            atlas = state.atlas,
            prescale = PRESCALE,
            anim_default_mix = 0.2f,
            binary_data = state.skeleton_load_status.data,
        });

        // start loading atlas images
        int num_images = sspine_num_images(state.atlas);
        for (int img_index = 0; img_index < num_images; img_index++)
        {
            sspine_image img = sspine_image_by_index(state.atlas, img_index);
            sspine_image_info img_info = sspine_get_image_info(img);
            sfetch_send(new sfetch_request_t
            {
                path = util_get_file_path(Path.Combine("spine", img_info.filename.String())),
                channel = 0,
                buffer = SFETCH_RANGE(state.image_buffer),
                callback = &image_data_loaded,
                user_data = new sfetch_range_t { ptr = Unsafe.AsPointer(ref img), size = (uint)Marshal.SizeOf<sspine_image>() }
            });
        }

        // create many instances
        float initial_time = 0.0f;
        for (int i = 0; i < NUM_INSTANCES; i++)
        {
            state.instances[i] = sspine_make_instance(new sspine_instance_desc
            {
                skeleton = state.skeleton,
            });

            string anim_name = ((i & 1) != 0) ? "walk" : "dance";
            sspine_set_animation(state.instances[i], sspine_anim_by_name(state.skeleton, anim_name), 0, true);

            sspine_skinset_desc skinset_desc = new sspine_skinset_desc();
            skinset_desc.skeleton = state.skeleton;
            skinset_desc.skins[0] = sspine_skin_by_name(state.skeleton, "skin-base");
            skinset_desc.skins[1] = sspine_skin_by_name(state.skeleton, accessories[random_skin_index()]);
            skinset_desc.skins[2] = sspine_skin_by_name(state.skeleton, clothes[random_skin_index()]);
            skinset_desc.skins[3] = sspine_skin_by_name(state.skeleton, eyelids[random_skin_index()]);
            skinset_desc.skins[4] = sspine_skin_by_name(state.skeleton, eyes[random_skin_index()]);
            skinset_desc.skins[5] = sspine_skin_by_name(state.skeleton, hair[random_skin_index()]);
            skinset_desc.skins[6] = sspine_skin_by_name(state.skeleton, legs[random_skin_index()]);
            skinset_desc.skins[7] = sspine_skin_by_name(state.skeleton, nose[random_skin_index()]);

            sspine_skinset skinset = sspine_make_skinset(skinset_desc);

            sspine_set_skinset(state.instances[i], skinset);
            sspine_update_instance(state.instances[i], initial_time);
            initial_time += 0.1f;
        }
    }

    // returns a xorshift32 random number between 0..<NUM_SKINS
    static uint x = 0x87654321;
    static uint random_skin_index()
    {

        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return (x & (NUM_SKINS - 1));
    }




    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        double delta_time = sapp_frame_duration();
        float width = sapp_widthf();
        float height = sapp_heightf();
        float aspect = width / height;
        state.t += (float)delta_time;
        if (state.t > 1.0f)
        {
            state.t_count++;
            state.t -= 1.0f;
        }
        sfetch_dowork();

        // use a fixed 'virtual resolution' for the spine rendering, but keep the same
        // aspect as the window/display
        vec2 virt_size = new vec2 { x = 1024.0f * aspect, y = 1024.0f };
        sspine_layer_transform layer_transform = new sspine_layer_transform
        {
            size = virt_size,
            origin = { x = virt_size.x * 0.5f, y = virt_size.y * 0.5f }
        };

        // // update and draw Spine objects
        ulong start_time = stm_now();
        for (uint i = 0; i < NUM_INSTANCES; i++)
        {
            uint grid_index = (i + state.t_count) % NUM_INSTANCES;
            vec2 pos = state.grid[grid_index].pos;
            vec2 vec = state.grid[grid_index].vec;
            vec2 p = new vec2
            {
                x = pos.x + vec.x * GRID_DX * state.t,
                y = pos.y + vec.y * GRID_DY * state.t,
            };
            sspine_set_position(state.instances[i], p);
            sspine_update_instance(state.instances[i], (float)delta_time);
            sspine_draw_instance_in_layer(state.instances[i], 0);
        }

        double eval_time = stm_ms(stm_since(start_time));

        // debug text
        sspine_context_info ctx_info = sspine_get_context_info(sspine_default_context());
        sdtx_canvas(sapp_widthf() * 0.3f, sapp_height() * 0.3f);
        sdtx_origin(1.0f, 1.0f);
        sdtx_home();
        sdtx_color3b(0, 0, 0);
        sdtx_print("spine eval time: {0:F3} ms\n", eval_time);
        sdtx_move_y(0.5f);
        sdtx_print("vertices:{0} \nindices:{1} \ndraws:{2}", ctx_info.num_vertices, ctx_info.num_indices, ctx_info.num_commands);

        // // actual sokol-gfx render pass
        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        sspine_draw_layer(0, layer_transform);
        sdtx_draw();
        // __dbgui_draw();
        sg_end_pass();
        sg_commit();

    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {

        state.atlas_buffer?.Dispose();
        state.skeleton_buffer?.Dispose();
        state.image_buffer?.Dispose();

        // Shutdown order is important: high-level systems must shutdown before sokol_gfx
        sspine_shutdown();   // spine-c removes commit listeners from sokol_gfx
        sdtx_shutdown();     // debug text uses sokol_gfx resources
        // __dbgui_shutdown();
        sg_shutdown();       // sokol_gfx shutdown
        sfetch_shutdown();   // fetch can shutdown last

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
            window_title = "spine-skinsets-app",
            icon = { sokol_default = true },
        };
    }


}