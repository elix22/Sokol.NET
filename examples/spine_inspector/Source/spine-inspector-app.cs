using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_load_action;
using static Sokol.SSpine;
using static Sokol.SFetch;
using static Sokol.SGL;
using static Sokol.Utils;
using static Sokol.SLog;
using static Sokol.SImgui;
using static Sokol.SGImgui;
using Imgui;
using static Imgui.ImguiNative;
using static Sokol.StbImage;
using System.Diagnostics;

public static unsafe class SpineInspectorApp
{
    const int MAX_SPINE_SCENES = 5;
    const int MAX_QUEUE_ANIMS = 4;
    const int MAX_TRIGGERED_EVENTS = 16;

    struct AnimQueueItem
    {
        public string name;
        public bool looping;
        public float delay;
    }

    struct SpineScene
    {
        public string ui_name;
        public string atlas_file;
        public string skel_file_json;
        public string skel_file_binary;
        public string skin;
        public float prescale;
        public sspine_atlas_overrides atlas_overrides;
        public AnimQueueItem[] anim_queue;
    }

    struct LoadStatusItem
    {
        public bool loaded;
        public sspine_range data;
    }

    struct LoadStatus
    {
        public int scene_index;
        public int pending_count;
        public bool failed;
        public LoadStatusItem atlas;
        public LoadStatusItem skeleton;
        public bool skel_data_is_binary;
    }

    struct UISelected
    {
        public sspine_bone bone;
        public sspine_slot slot;
        public sspine_anim anim;
        public sspine_event evt;
        public sspine_skin skin;
        public sspine_iktarget iktarget;
    }

    struct UIState
    {
        public sgimgui_t sgimgui;
        public bool draw_bones_enabled;
        public bool atlas_open;
        public bool bones_open;
        public bool slots_open;
        public bool anims_open;
        public bool events_open;
        public bool skins_open;
        public bool iktargets_open;
        public UISelected selected;
        public double cur_time;
        public double last_triggered_event_time;
        public sspine_event last_triggered_event;
        public int theme;
    }

    class Buffers
    {
        public SharedBuffer atlas = SharedBuffer.Create(16 * 1024);
        public SharedBuffer skeleton = SharedBuffer.Create(512 * 1024);
        public SharedBuffer image = SharedBuffer.Create(512 * 1024);
    }

    class State
    {
        public sspine_atlas atlas;
        public sspine_skeleton skeleton;
        public sspine_instance instance;
        public sg_pass_action pass_action;
        public sspine_layer_transform layer_transform;
        public sspine_vec2 iktarget_pos;
        public LoadStatus load_status;
        public UIState ui;
        public Buffers buffers = new Buffers();
    }

    static State state = new State();
    static SpineScene[] spine_scenes;

    static SpineInspectorApp()
    {
        spine_scenes = new SpineScene[MAX_SPINE_SCENES];
        
        // Spine Boy
        spine_scenes[0] = new SpineScene
        {
            ui_name = "Spine Boy",
            atlas_file = "spineboy.atlas",
            skel_file_json = "spineboy-pro.json",
            prescale = 0.75f,
            atlas_overrides = new sspine_atlas_overrides
            {
                min_filter = sg_filter.SG_FILTER_NEAREST,
                mag_filter = sg_filter.SG_FILTER_NEAREST,
            },
            anim_queue = new AnimQueueItem[]
            {
                new AnimQueueItem { name = "portal" },
                new AnimQueueItem { name = "run", looping = true },
            }
        };

        // Raptor
        spine_scenes[1] = new SpineScene
        {
            ui_name = "Raptor",
            atlas_file = "raptor-pma.atlas",
            skel_file_binary = "raptor-pro.skel",
            prescale = 0.5f,
            anim_queue = new AnimQueueItem[]
            {
                new AnimQueueItem { name = "jump" },
                new AnimQueueItem { name = "roar" },
                new AnimQueueItem { name = "walk", looping = true },
            }
        };

        // Alien
        spine_scenes[2] = new SpineScene
        {
            ui_name = "Alien",
            atlas_file = "alien-pma.atlas",
            skel_file_binary = "alien-pro.skel",
            prescale = 0.5f,
            anim_queue = new AnimQueueItem[]
            {
                new AnimQueueItem { name = "run", looping = true },
                new AnimQueueItem { name = "death", looping = false, delay = 5.0f },
                new AnimQueueItem { name = "run", looping = true },
                new AnimQueueItem { name = "death", looping = true, delay = 5.0f },
            }
        };

        // Speedy
        spine_scenes[3] = new SpineScene
        {
            ui_name = "Speedy",
            atlas_file = "speedy-pma.atlas",
            skel_file_binary = "speedy-ess.skel",
            anim_queue = new AnimQueueItem[]
            {
                new AnimQueueItem { name = "run", looping = true }
            }
        };

        // Mix & Match
        spine_scenes[4] = new SpineScene
        {
            ui_name = "Mix & Match",
            atlas_file = "mix-and-match-pma.atlas",
            skel_file_binary = "mix-and-match-pro.skel",
            skin = "full-skins/girl",
            prescale = 0.5f,
            anim_queue = new AnimQueueItem[]
            {
                new AnimQueueItem { name = "walk", looping = true }
            }
        };
    }

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        // Setup sokol-gfx
        sg_setup(new sg_desc
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });

        // Setup sokol-gl
        sgl_setup(new sgl_desc_t
        {
            logger = { func = &slog_func }
        });

        // Setup sokol-fetch
        sfetch_setup(new sfetch_desc_t
        {
            max_requests = 3,
            num_channels = 2,
            num_lanes = 1,
            logger = { func = &slog_func }
        });

        // Setup sokol-spine
        sspine_setup(new sspine_desc
        {
            logger = { func = &slog_func }
        });

        // Setup UI
        simgui_setup(new simgui_desc_t
        {
            logger = { func = &slog_func }
        });
        state.ui.sgimgui = sgimgui_init();

        // Initialize pass action
        state.pass_action = default;
        state.pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.5f, b = 0.7f, a = 1.0f };

        // Start loading first scene
        LoadSpineScene(0);
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        double delta_time = sapp_frame_duration();
        state.ui.cur_time += delta_time;
        
        state.layer_transform = new sspine_layer_transform
        {
            size = new sspine_vec2 { x = sapp_widthf(), y = sapp_heightf() },
            origin = new sspine_vec2 { x = sapp_widthf() * 0.5f, y = sapp_heightf() * 0.8f }
        };

        sfetch_dowork();
        
        simgui_new_frame(new simgui_frame_desc_t
        {
            width = sapp_width(),
            height = sapp_height(),
            dpi_scale = 1,// TBD ELI , too small on Android sapp_dpi_scale(),
            delta_time = delta_time
        });

        // Link IK target to mouse
        sspine_set_iktarget_world_pos(state.instance, state.ui.selected.iktarget, state.iktarget_pos);

        // Update instance
        sspine_update_instance(state.instance, (float)delta_time);

        // Draw instance to layer 0
        sspine_draw_instance_in_layer(state.instance, 0);

        // Keep track of triggered events
        int num_triggered_events = sspine_num_triggered_events(state.instance);
        for (int i = 0; i < num_triggered_events; i++)
        {
            state.ui.last_triggered_event_time = state.ui.cur_time;
            var triggered_event_info = sspine_get_triggered_event_info(state.instance, i);
            state.ui.last_triggered_event = triggered_event_info._event;
        }

        // Draw bones
        if (state.ui.draw_bones_enabled)
        {
            DrawBones();
        }

        DrawUI();

        // Update pass action based on load status
        if (state.load_status.failed)
        {
            state.pass_action.colors[0].clear_value = new sg_color { r = 1.0f, g = 0.0f, b = 0.0f, a = 1.0f };
        }
        else
        {
            state.pass_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.5f, b = 0.7f, a = 1.0f };
        }

        // Render
        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        sspine_draw_layer(0, in state.layer_transform);
        sgl_draw();
        simgui_render();
        sg_end_pass();
        sg_commit();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        if (simgui_handle_event(in *e))
        {
            return;
        }

        if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE)
        {
            state.iktarget_pos.x = e->mouse_x - state.layer_transform.origin.x;
            state.iktarget_pos.y = e->mouse_y - state.layer_transform.origin.y;
        }
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        state.buffers.atlas.Dispose();
        state.buffers.skeleton.Dispose();
        state.buffers.image.Dispose();
        
        sgimgui_discard(state.ui.sgimgui);
        simgui_shutdown();
        sspine_shutdown();
        sfetch_shutdown();
        sgl_shutdown();
        sg_shutdown();

        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    static bool LoadSpineScene(int scene_index)
    {
        if (scene_index < 0 || scene_index >= MAX_SPINE_SCENES)
            return false;
        
        if (string.IsNullOrEmpty(spine_scenes[scene_index].atlas_file))
            return false;
        
        if (string.IsNullOrEmpty(spine_scenes[scene_index].skel_file_json) && 
            string.IsNullOrEmpty(spine_scenes[scene_index].skel_file_binary))
            return false;

        // Don't disturb in-progress loading
        if (state.load_status.pending_count > 0)
            return false;

        state.load_status.scene_index = scene_index;
        state.load_status.pending_count = 0;
        state.load_status.failed = false;
        state.load_status.atlas = default;
        state.load_status.skeleton = default;
        state.load_status.skel_data_is_binary = false;

        // Discard previous spine scene
        sspine_destroy_instance(state.instance);
        sspine_destroy_skeleton(state.skeleton);
        sspine_destroy_atlas(state.atlas);

        // Start loading atlas file
        sfetch_send(new sfetch_request_t
        {
            path = util_get_file_path(spine_scenes[scene_index].atlas_file),
            channel = 0,
            buffer = SFETCH_RANGE(state.buffers.atlas.Buffer),
            callback = &AtlasDataLoaded
        });
        state.load_status.pending_count++;

        // Start loading skeleton file
        string skel_file = spine_scenes[scene_index].skel_file_json;
        if (string.IsNullOrEmpty(skel_file))
        {
            skel_file = spine_scenes[scene_index].skel_file_binary;
            state.load_status.skel_data_is_binary = true;
        }

        sfetch_send(new sfetch_request_t
        {
            path = util_get_file_path(skel_file),
            channel = 1,
            buffer = SFETCH_RANGE(state.buffers.skeleton.Buffer),
            callback = &SkeletonDataLoaded
        });
        state.load_status.pending_count++;

        return true;
    }

    [UnmanagedCallersOnly]
    static void AtlasDataLoaded(sfetch_response_t* response)
    {
        if (response->fetched || response->failed)
        {
            Debug.Assert(state.load_status.pending_count > 0);
            state.load_status.pending_count--;
        }

        if (response->fetched)
        {
            state.load_status.atlas = new LoadStatusItem
            {
                loaded = true,
                data = new sspine_range { ptr = response->data.ptr, size = response->data.size }
            };

            if (state.load_status.pending_count == 0)
            {
                CreateSpineObjects();
            }
        }
        else if (response->failed)
        {
            state.load_status.failed = true;
        }
    }

    [UnmanagedCallersOnly]
    static void SkeletonDataLoaded(sfetch_response_t* response)
    {
        if (response->fetched || response->failed)
        {
            Debug.Assert(state.load_status.pending_count > 0);
            state.load_status.pending_count--;
        }

        if (response->fetched)
        {
            state.load_status.skeleton = new LoadStatusItem
            {
                loaded = true,
                data = new sspine_range { ptr = response->data.ptr, size = response->data.size }
            };

            // Ensure zero termination for JSON data
            if (!state.load_status.skel_data_is_binary)
            {
                state.buffers.skeleton.Buffer[response->data.size] = 0;
            }

            if (state.load_status.pending_count == 0)
            {
                CreateSpineObjects();
            }
        }
        else if (response->failed)
        {
            state.load_status.failed = true;
        }
    }

    static void CreateSpineObjects()
    {
        int scene_index = state.load_status.scene_index;

        // Create atlas
        state.atlas = sspine_make_atlas(new sspine_atlas_desc
        {
            data = state.load_status.atlas.data,
            _override = spine_scenes[scene_index].atlas_overrides
        });
        Debug.Assert(sspine_atlas_valid(state.atlas));

        // Create skeleton
        sspine_skeleton_desc skel_desc = default;
        skel_desc.atlas = state.atlas;
        skel_desc.prescale = spine_scenes[scene_index].prescale;
        skel_desc.anim_default_mix = 0.2f;

        if (state.load_status.skel_data_is_binary)
        {
            skel_desc.binary_data = state.load_status.skeleton.data;
        }
        else
        {
            // For JSON data, convert pointer to string
            skel_desc.json_data = Marshal.PtrToStringUTF8((IntPtr)state.load_status.skeleton.data.ptr) ?? "";
        }

        state.skeleton = sspine_make_skeleton(skel_desc);
        Debug.Assert(sspine_skeleton_valid(state.skeleton));

        // Create instance
        state.instance = sspine_make_instance(new sspine_instance_desc
        {
            skeleton = state.skeleton
        });
        Debug.Assert(sspine_instance_valid(state.instance));

        // Set initial skin
        if (!string.IsNullOrEmpty(spine_scenes[scene_index].skin))
        {
            sspine_set_skin(state.instance, sspine_skin_by_name(state.skeleton, spine_scenes[scene_index].skin));
        }

        // Populate animation queue
        if (spine_scenes[scene_index].anim_queue != null)
        {
            for (int i = 0; i < spine_scenes[scene_index].anim_queue.Length && i < MAX_QUEUE_ANIMS; i++)
            {
                var queue_anim = spine_scenes[scene_index].anim_queue[i];
                if (!string.IsNullOrEmpty(queue_anim.name))
                {
                    sspine_anim anim = sspine_anim_by_name(state.skeleton, queue_anim.name);
                    if (i == 0)
                    {
                        sspine_set_animation(state.instance, anim, 0, queue_anim.looping);
                    }
                    else
                    {
                        sspine_add_animation(state.instance, anim, 0, queue_anim.looping, queue_anim.delay);
                    }
                }
            }
        }

        // Load atlas images
        int num_images = sspine_num_images(state.atlas);
        for (int img_index = 0; img_index < num_images; img_index++)
        {
            sspine_image img = sspine_image_by_index(state.atlas, img_index);
            sspine_image_info img_info = sspine_get_image_info(img);
            Debug.Assert(img_info.valid);

            sfetch_request_t req = default;
            req.path = util_get_file_path(img_info.filename.String());
            req.channel = 0;
            req.buffer = SFETCH_RANGE(state.buffers.image.Buffer);
            req.callback = &ImageDataLoaded;
            req.user_data = new sfetch_range_t { ptr = Unsafe.AsPointer(ref img), size = (uint)sizeof(sspine_image) };
            sfetch_send(req);
            
            state.load_status.pending_count++;
        }
    }

    [UnmanagedCallersOnly]
    static void ImageDataLoaded(sfetch_response_t* response)
    {
        if (response->fetched || response->failed)
        {
            Debug.Assert(state.load_status.pending_count > 0);
            state.load_status.pending_count--;
        }

        sspine_image img = *(sspine_image*)response->user_data;
        
        sspine_image_info img_info = sspine_get_image_info(img);
        Debug.Assert(img_info.valid);

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
                int pixel_data_size = img_width * img_height * 4;
                ReadOnlySpan<byte> pixelSpan = new ReadOnlySpan<byte>(pixels, pixel_data_size);
                
                sg_image_desc img_desc = default;
                img_desc.width = img_width;
                img_desc.height = img_height;
                img_desc.pixel_format = SG_PIXELFORMAT_RGBA8;
                img_desc.label = img_info.filename.String();
                img_desc.data.mip_levels[0] = SG_RANGE(pixelSpan);
                sg_init_image(img_info.sgimage, img_desc);

                // Free the native STB image data
                stbi_image_free_csharp(pixels);

                sg_init_view(img_info.sgview, new sg_view_desc
                {
                    texture = { image = img_info.sgimage }
                });

                sg_sampler_desc smp_desc = default;
                smp_desc.min_filter = img_info.min_filter;
                smp_desc.mag_filter = img_info.mag_filter;
                smp_desc.mipmap_filter = img_info.mipmap_filter;
                smp_desc.wrap_u = img_info.wrap_u;
                smp_desc.wrap_v = img_info.wrap_v;
                smp_desc.label = img_info.filename.String();
                sg_init_sampler(img_info.sgsampler, smp_desc);
            }
            else
            {
                state.load_status.failed = true;
                sg_fail_image(img_info.sgimage);
            }
        }
        else if (response->failed)
        {
            state.load_status.failed = true;
            sg_fail_image(img_info.sgimage);
        }
    }

    static string SGFilterName(sg_filter f)
    {
        return f switch
        {
            sg_filter._SG_FILTER_DEFAULT => "DEFAULT",
            sg_filter.SG_FILTER_NEAREST => "NEAREST",
            sg_filter.SG_FILTER_LINEAR => "LINEAR",
            _ => "???"
        };
    }

    static string SGWrapName(sg_wrap w)
    {
        return w switch
        {
            sg_wrap._SG_WRAP_DEFAULT => "DEFAULT",
            sg_wrap.SG_WRAP_REPEAT => "REPEAT",
            sg_wrap.SG_WRAP_CLAMP_TO_EDGE => "CLAMP_TO_EDGE",
            sg_wrap.SG_WRAP_CLAMP_TO_BORDER => "CLAMP_TO_BORDER",
            sg_wrap.SG_WRAP_MIRRORED_REPEAT => "MIRRORED_REPEAT",
            _ => "???"
        };
    }

    static void DrawUI()
    {
        if (igBeginMainMenuBar())
        {
            if (igBeginMenu("sokol-spine", true))
            {
                if (igBeginMenu("Load", true))
                {
                    for (int i = 0; i < MAX_SPINE_SCENES; i++)
                    {
                        if (!string.IsNullOrEmpty(spine_scenes[i].ui_name))
                        {
                            if (igMenuItem_Bool(spine_scenes[i].ui_name, null, i == state.load_status.scene_index, true))
                            {
                                LoadSpineScene(i);
                            }
                        }
                    }
                    igEndMenu();
                }
                
                byte draw_bones = state.ui.draw_bones_enabled ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Draw Bones", null, ref draw_bones, true))
                {
                    state.ui.draw_bones_enabled = draw_bones != 0;
                }

                byte atlas_open = state.ui.atlas_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Atlas...", null, ref atlas_open, true))
                {
                    state.ui.atlas_open = atlas_open != 0;
                }

                byte bones_open = state.ui.bones_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Bones...", null, ref bones_open, true))
                {
                    state.ui.bones_open = bones_open != 0;
                }

                byte slots_open = state.ui.slots_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Slots...", null, ref slots_open, true))
                {
                    state.ui.slots_open = slots_open != 0;
                }

                byte anims_open = state.ui.anims_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Anims...", null, ref anims_open, true))
                {
                    state.ui.anims_open = anims_open != 0;
                }

                byte events_open = state.ui.events_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Events...", null, ref events_open, true))
                {
                    state.ui.events_open = events_open != 0;
                }

                byte skins_open = state.ui.skins_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Skins...", null, ref skins_open, true))
                {
                    state.ui.skins_open = skins_open != 0;
                }

                byte iktargets_open = state.ui.iktargets_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("IK Targets...", null, ref iktargets_open, true))
                {
                    state.ui.iktargets_open = iktargets_open != 0;
                }

                igEndMenu();
            }

            sgimgui_draw_menu(state.ui.sgimgui, "sokol-gfx");

            if (igBeginMenu("options", true))
            {
                if (igRadioButton_IntPtr("Dark Theme", ref state.ui.theme, 0))
                {
                    igStyleColorsDark(null);
                }
                if (igRadioButton_IntPtr("Light Theme", ref state.ui.theme, 1))
                {
                    igStyleColorsLight(null);
                }
                if (igRadioButton_IntPtr("Classic Theme", ref state.ui.theme, 2))
                {
                    igStyleColorsClassic(null);
                }
                igEndMenu();
            }

            igEndMainMenuBar();
        }

        Vector2 pos = new Vector2(30, 30);

        // Atlas window
        if (state.ui.atlas_open)
        {
            igSetNextWindowSize(new Vector2(300, 330), ImGuiCond.Once);
            igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
            byte open = 1;
            if (igBegin("Spine Atlas", ref open, ImGuiWindowFlags.None))
            {
                state.ui.atlas_open = open != 0;
                
                if (!sspine_atlas_valid(state.atlas))
                {
                    igText("No Spine data loaded.");
                }
                else
                {
                    int num_pages = sspine_num_atlas_pages(state.atlas);
                    igText($"Num Pages: {num_pages}");
                    
                    for (int i = 0; i < num_pages; i++)
                    {
                        sspine_atlas_page_info info = sspine_get_atlas_page_info(sspine_atlas_page_by_index(state.atlas, i));
                        Debug.Assert(info.valid);
                        
                        igSeparator();
                        igText($"Filename: {info.image.filename.String()}");
                        igText($"Width: {info.image.width}");
                        igText($"Height: {info.image.height}");
                        igText($"Premul Alpha: {(!info.image.premul_alpha ? "NO" : "YES")}");
                        igText("Original Spine params:");
                        igText($"  Min Filter: {SGFilterName(info.image.min_filter)}");
                        igText($"  Mag Filter: {SGFilterName(info.image.mag_filter)}");
                        igText($"  Mipmap Filter: {SGFilterName(info.image.mipmap_filter)}");
                        igText($"  Wrap U: {SGWrapName(info.image.wrap_u)}");
                        igText($"  Wrap V: {SGWrapName(info.image.wrap_v)}");
                        igText("Overrides:");
                        igText($"  Min Filter: {SGFilterName(info.overrides.min_filter)}");
                        igText($"  Mag Filter: {SGFilterName(info.overrides.mag_filter)}");
                        igText($"  Mipmap Filter: {SGFilterName(info.overrides.mipmap_filter)}");
                        igText($"  Wrap U: {SGWrapName(info.overrides.wrap_u)}");
                        igText($"  Wrap V: {SGWrapName(info.overrides.wrap_v)}");
                        igText($"  Premul Alpha Enabled: {(info.overrides.premul_alpha_enabled ? "YES" : "NO")}");
                        igText($"  Premul Alpha Disabled: {(info.overrides.premul_alpha_disabled ? "YES" : "NO")}");
                    }
                }
            }
            igEnd();
        }

        pos.X += 20; pos.Y += 20;

        // Bones window
        if (state.ui.bones_open)
        {
            DrawBonesWindow(ref pos);
        }

        pos.X += 20; pos.Y += 20;

        // Slots window
        if (state.ui.slots_open)
        {
            DrawSlotsWindow(ref pos);
        }

        pos.X += 20; pos.Y += 20;

        // Anims window
        if (state.ui.anims_open)
        {
            DrawAnimsWindow(ref pos);
        }

        pos.X += 20; pos.Y += 20;

        // Events window
        if (state.ui.events_open)
        {
            DrawEventsWindow(ref pos);
        }

        pos.X += 20; pos.Y += 20;

        // Skins window
        if (state.ui.skins_open)
        {
            DrawSkinsWindow(ref pos);
        }

        pos.X += 20; pos.Y += 20;

        // IK Targets window
        if (state.ui.iktargets_open)
        {
            DrawIKTargetsWindow(ref pos);
        }

        // Display triggered events
        const double triggered_event_fade_time = 1.0;
        if (sspine_event_valid(state.ui.last_triggered_event) && 
            (state.ui.last_triggered_event_time + triggered_event_fade_time) > state.ui.cur_time)
        {
            sspine_event_info event_info = sspine_get_event_info(state.ui.last_triggered_event);
            if (event_info.valid)
            {
                float alpha = (float)(1.0 - ((state.ui.cur_time - state.ui.last_triggered_event_time) / triggered_event_fade_time));
                igSetNextWindowBgAlpha(alpha);
                igSetNextWindowPos(new Vector2(sapp_widthf() * 0.5f, sapp_heightf() - 50.0f), 
                    ImGuiCond.Always, new Vector2(0.5f, 0.5f));
                igPushStyleColor_U32(ImGuiCol.WindowBg, 0xFF0000FF);
                
                byte dummy = 0;
                if (igBegin("Triggered Events", ref dummy, 
                    ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration | 
                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav))
                {
                    igText($"{event_info.name.String()}: {state.ui.last_triggered_event_time:F3} (age: {state.ui.cur_time - state.ui.last_triggered_event_time:F3})");
                }
                igEnd();
                igPopStyleColor(1);
            }
        }

        sgimgui_draw(state.ui.sgimgui);
    }

    static void DrawBonesWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(300, 300), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Bones", ref open, ImGuiWindowFlags.None))
        {
            state.ui.bones_open = open != 0;
            
            if (!sspine_instance_valid(state.instance))
            {
                igText("No Spine data loaded.");
            }
            else
            {
                int num_bones = sspine_num_bones(state.skeleton);
                igText($"Num Bones: {num_bones}");
                
                igBeginChild_Str("bones_list", new Vector2(128, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);
                for (int i = 0; i < num_bones; i++)
                {
                    sspine_bone bone = sspine_bone_by_index(state.skeleton, i);
                    sspine_bone_info info = sspine_get_bone_info(bone);
                    Debug.Assert(info.valid);
                    
                    igPushID_Int(bone.index);
                    if (igSelectable_Bool(info.name.String(), sspine_bone_equal(state.ui.selected.bone, bone), 
                        ImGuiSelectableFlags.None, Vector2.Zero))
                    {
                        state.ui.selected.bone = bone;
                    }
                    igPopID();
                }
                igEndChild();
                
                igSameLine(0, -1);
                if (sspine_bone_valid(state.ui.selected.bone))
                {
                    sspine_bone_info info = sspine_get_bone_info(state.ui.selected.bone);
                    Debug.Assert(info.valid);
                    
                    igBeginChild_Str("bone_info", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.None);
                    igText($"Index: {info.index}");
                    igText($"Parent Bone: {(sspine_bone_valid(info.parent_bone) ? sspine_get_bone_info(info.parent_bone).name.String() : "---")}");
                    igText($"Name: {info.name.String()}");
                    igText($"Length: {info.length:F3}");
                    igText("Pose Transform:");
                    igText($"  Position: {info.pose.position.x:F3},{info.pose.position.y:F3}");
                    igText($"  Rotation: {info.pose.rotation:F3}");
                    igText($"  Scale: {info.pose.scale.x:F3},{info.pose.scale.y:F3}");
                    igText($"  Shear: {info.pose.shear.x:F3},{info.pose.shear.y:F3}");
                    igText($"Color: {info.color.r:F2},{info.color.g:F2},{info.color.b:F2},{info.color.a:F2}");
                    igText("Current Transform:");
                    sspine_bone_transform cur_tform = sspine_get_bone_transform(state.instance, state.ui.selected.bone);
                    igText($"  Position: {cur_tform.position.x:F3},{cur_tform.position.y:F3}");
                    igText($"  Rotation: {cur_tform.rotation:F3}");
                    igText($"  Scale: {cur_tform.scale.x:F3},{cur_tform.scale.y:F3}");
                    igText($"  Shear: {cur_tform.shear.x:F3},{cur_tform.shear.y:F3}");
                    igEndChild();
                }
            }
        }
        igEnd();
    }

    static void DrawSlotsWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(300, 300), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Slots", ref open, ImGuiWindowFlags.None))
        {
            state.ui.slots_open = open != 0;
            
            if (!sspine_instance_valid(state.instance))
            {
                igText("No Spine data loaded.");
            }
            else
            {
                int num_slots = sspine_num_slots(state.skeleton);
                igText($"Num Slots: {num_slots}");
                
                igBeginChild_Str("slot_list", new Vector2(128, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);
                for (int i = 0; i < num_slots; i++)
                {
                    sspine_slot slot = sspine_slot_by_index(state.skeleton, i);
                    sspine_slot_info info = sspine_get_slot_info(slot);
                    Debug.Assert(info.valid);
                    
                    igPushID_Int(slot.index);
                    if (igSelectable_Bool(info.name.String(), sspine_slot_equal(state.ui.selected.slot, slot), 
                        ImGuiSelectableFlags.None, Vector2.Zero))
                    {
                        state.ui.selected.slot = slot;
                    }
                    igPopID();
                }
                igEndChild();
                
                igSameLine(0, -1);
                if (sspine_slot_valid(state.ui.selected.slot))
                {
                    sspine_slot_info slot_info = sspine_get_slot_info(state.ui.selected.slot);
                    Debug.Assert(slot_info.valid);
                    sspine_bone_info bone_info = sspine_get_bone_info(slot_info.bone);
                    Debug.Assert(bone_info.valid);
                    
                    igBeginChild_Str("slot_info", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.None);
                    igText($"Index: {slot_info.index}");
                    igText($"Name: {slot_info.name.String()}");
                    igText($"Attachment: {(slot_info.attachment_name.valid ? slot_info.attachment_name.String() : "-")}");
                    igText($"Bone Name: {bone_info.name.String()}");
                    igText($"Color: {slot_info.color.r:F2},{slot_info.color.g:F2},{slot_info.color.b:F2},{slot_info.color.a:F2}");
                    igEndChild();
                }
            }
        }
        igEnd();
    }

    static void DrawAnimsWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(300, 300), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Anims", ref open, ImGuiWindowFlags.None))
        {
            state.ui.anims_open = open != 0;
            
            if (!sspine_instance_valid(state.instance))
            {
                igText("No Spine data loaded.");
            }
            else
            {
                int num_anims = sspine_num_anims(state.skeleton);
                igText($"Num Anims: {num_anims}");
                
                igBeginChild_Str("anim_list", new Vector2(128, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);
                for (int i = 0; i < num_anims; i++)
                {
                    sspine_anim anim = sspine_anim_by_index(state.skeleton, i);
                    sspine_anim_info info = sspine_get_anim_info(anim);
                    Debug.Assert(info.valid);
                    
                    igPushID_Int(anim.index);
                    if (igSelectable_Bool(info.name.String(), sspine_anim_equal(state.ui.selected.anim, anim), 
                        ImGuiSelectableFlags.None, Vector2.Zero))
                    {
                        state.ui.selected.anim = anim;
                        sspine_set_animation(state.instance, anim, 0, true);
                    }
                    igPopID();
                }
                igEndChild();
                
                igSameLine(0, -1);
                if (sspine_anim_valid(state.ui.selected.anim))
                {
                    sspine_anim_info info = sspine_get_anim_info(state.ui.selected.anim);
                    Debug.Assert(info.valid);
                    
                    igBeginChild_Str("anim_info", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.None);
                    igText($"Index: {info.index}");
                    igText($"Name: {info.name.String()}");
                    igText($"Duration: {info.duration:F3}");
                    igEndChild();
                }
            }
        }
        igEnd();
    }

    static void DrawEventsWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(300, 300), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Events", ref open, ImGuiWindowFlags.None))
        {
            state.ui.events_open = open != 0;
            
            if (!sspine_skeleton_valid(state.skeleton))
            {
                igText("No Spine data loaded");
            }
            else
            {
                int num_events = sspine_num_events(state.skeleton);
                igText($"Num Events: {num_events}");
                
                igBeginChild_Str("event_list", new Vector2(128, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);
                for (int i = 0; i < num_events; i++)
                {
                    sspine_event evt = sspine_event_by_index(state.skeleton, i);
                    sspine_event_info info = sspine_get_event_info(evt);
                    Debug.Assert(info.valid);
                    
                    igPushID_Int(evt.index);
                    if (igSelectable_Bool(info.name.String(), sspine_event_equal(state.ui.selected.evt, evt), 
                        ImGuiSelectableFlags.None, Vector2.Zero))
                    {
                        state.ui.selected.evt = evt;
                    }
                    igPopID();
                }
                igEndChild();
                
                igSameLine(0, -1);
                if (sspine_event_valid(state.ui.selected.evt))
                {
                    sspine_event_info info = sspine_get_event_info(state.ui.selected.evt);
                    Debug.Assert(info.valid);
                    
                    igBeginChild_Str("event_info", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.None);
                    igText($"Index: {info.index}");
                    igText($"Name: {info.name.String()}");
                    igText($"Int Value: {info.int_value}");
                    igText($"Float Value: {info.float_value:F3}");
                    igText($"String Value: {(info.string_value.valid ? info.string_value.String() : "NONE")}");
                    igText($"Audio Path: {(info.audio_path.valid ? info.audio_path.String() : "NONE")}");
                    igText($"Volume: {info.volume:F3}");
                    igText($"Balance: {info.balance:F3}");
                    igEndChild();
                }
            }
        }
        igEnd();
    }

    static void DrawSkinsWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(300, 300), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Skins", ref open, ImGuiWindowFlags.None))
        {
            state.ui.skins_open = open != 0;
            
            if (!sspine_skeleton_valid(state.skeleton))
            {
                igText("No Spine data loaded");
            }
            else
            {
                int num_skins = sspine_num_skins(state.skeleton);
                igText($"Num Skins: {num_skins}");
                
                igBeginChild_Str("skin_list", new Vector2(128, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);
                for (int i = 0; i < num_skins; i++)
                {
                    sspine_skin skin = sspine_skin_by_index(state.skeleton, i);
                    sspine_skin_info info = sspine_get_skin_info(skin);
                    Debug.Assert(info.valid);
                    
                    igPushID_Int(skin.index);
                    if (igSelectable_Bool(info.name.String(), sspine_skin_equal(state.ui.selected.skin, skin), 
                        ImGuiSelectableFlags.None, Vector2.Zero))
                    {
                        state.ui.selected.skin = skin;
                        sspine_set_skin(state.instance, skin);
                    }
                    igPopID();
                }
                igEndChild();
                
                igSameLine(0, -1);
                if (sspine_skin_valid(state.ui.selected.skin))
                {
                    sspine_skin_info info = sspine_get_skin_info(state.ui.selected.skin);
                    Debug.Assert(info.valid);
                    
                    igBeginChild_Str("skin_info", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.None);
                    igText($"Index: {info.index}");
                    igText($"Name: {info.name.String()}");
                    igEndChild();
                }
            }
        }
        igEnd();
    }

    static void DrawIKTargetsWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(300, 300), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("IK Targets", ref open, ImGuiWindowFlags.None))
        {
            state.ui.iktargets_open = open != 0;
            
            if (!sspine_skeleton_valid(state.skeleton))
            {
                igText("No Spine data loaded");
            }
            else
            {
                int num_iktargets = sspine_num_iktargets(state.skeleton);
                igText($"Num IK Targets: {num_iktargets}");
                
                igBeginChild_Str("iktarget_list", new Vector2(128, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);
                for (int i = 0; i < num_iktargets; i++)
                {
                    sspine_iktarget iktarget = sspine_iktarget_by_index(state.skeleton, i);
                    sspine_iktarget_info info = sspine_get_iktarget_info(iktarget);
                    Debug.Assert(info.valid);
                    
                    igPushID_Int(iktarget.index);
                    if (igSelectable_Bool(info.name.String(), sspine_iktarget_equal(state.ui.selected.iktarget, iktarget), 
                        ImGuiSelectableFlags.None, Vector2.Zero))
                    {
                        state.ui.selected.iktarget = iktarget;
                    }
                    igPopID();
                }
                igEndChild();
                
                igSameLine(0, -1);
                if (sspine_iktarget_valid(state.ui.selected.iktarget))
                {
                    sspine_iktarget_info info = sspine_get_iktarget_info(state.ui.selected.iktarget);
                    Debug.Assert(info.valid);
                    
                    igBeginChild_Str("iktarget_info", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.None);
                    igText($"Index: {info.index}");
                    igText($"Name: {info.name.String()}");
                    igText($"Target Bone: {sspine_get_bone_info(info.target_bone).name.String()}");
                    igEndChild();
                }
            }
        }
        igEnd();
    }

    static void DrawBones()
    {
        if (!sspine_instance_valid(state.instance))
            return;

        sspine_mat4 proj = sspine_layer_transform_to_mat4(in state.layer_transform);
        sgl_defaults();
        sgl_matrix_mode_projection();
        sgl_load_matrix(proj.m[0]);
        sgl_c3f(0.0f, 1.0f, 0.0f);
        sgl_begin_lines();
        
        int num_bones = sspine_num_bones(state.skeleton);
        for (int i = 0; i < num_bones; i++)
        {
            sspine_bone bone = sspine_bone_by_index(state.skeleton, i);
            sspine_bone parent_bone = sspine_get_bone_info(bone).parent_bone;
            if (sspine_bone_valid(parent_bone))
            {
                sspine_vec2 p0 = sspine_get_bone_world_position(state.instance, parent_bone);
                sspine_vec2 p1 = sspine_get_bone_world_position(state.instance, bone);
                sgl_v2f(p0.x, p0.y);
                sgl_v2f(p1.x, p1.y);
            }
        }
        
        sgl_end();
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 0,
            height = 0,
            sample_count = 4,
            window_title = "Spine Inspector (sokol-charp)",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }
}
