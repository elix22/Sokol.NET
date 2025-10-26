
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static System.Numerics.Matrix4x4;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SBasisu;
using static Sokol.SFetch;
using static Sokol.Utils;
using static Sokol.SApp;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_filter;
using static Sokol.SG.sg_wrap;
using static Sokol.SG.sg_primitive_type;
using static Sokol.SG.sg_face_winding;

using static Sokol.SDebugText;
using static Sokol.CGltf;
using static Sokol.STM;
using static cgltf_sapp_shader_cs_cgltf.Shaders;

using static Sokol.SLog;
using static Sokol.SDebugUI;

using cgltf_size = uint;
using System.Diagnostics;

public static unsafe class CGltfApp
{

    static bool PauseUpdate = false;

    const string filename = "gltf/DamagedHelmet/DamagedHelmet.gltf";

    const int SCENE_INVALID_INDEX = -1;
    const int SCENE_MAX_BUFFERS = 16;
    const int SCENE_MAX_IMAGES = 16;
    const int SCENE_MAX_MATERIALS = 16;
    const int SCENE_MAX_PIPELINES = 16;
    const int SCENE_MAX_PRIMITIVES = 16;   // aka submesh
    const int SCENE_MAX_MESHES = 16;
    const int SCENE_MAX_NODES = 16;

    // statically allocated buffers for file downloads
    const int SFETCH_NUM_CHANNELS = 1;
    const int SFETCH_NUM_LANES = 4;
    const int MAX_FILE_SIZE = 1024 * 1024;
    static SharedBuffer[,] sfetch_buffers = new SharedBuffer[SFETCH_NUM_CHANNELS, SFETCH_NUM_LANES];

    // per-material texture indices into scene.images for metallic material
    public struct metallic_images_t
    {
        public int base_color;
        public int metallic_roughness;
        public int normal;
        public int occlusion;
        public int emissive;
    }

    // per-material texture indices into scene.images for specular material
    public struct specular_images_t
    {
        public readonly int diffuse;
        public readonly int specular_glossiness;
        public readonly int normal;
        public readonly int occlusion;
        public readonly int emissive;
    }

    // fragment-shader-params and textures for metallic material
    public struct metallic_material_t
    {
        public metallic_material_t()
        {

        }
        public cgltf_metallic_params_t fs_params = new cgltf_metallic_params_t();
        public metallic_images_t images = new metallic_images_t();
    }




    [StructLayout(LayoutKind.Sequential)]
    public struct material_t
    {
        public material_t()
        {

        }
        public bool is_metallic;
        // In C this was a union; here we select the metallic material.
        public metallic_material_t metallic = new metallic_material_t();
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct vertex_buffer_mapping_t
    {
        public vertex_buffer_mapping_t()
        {
            buffer = new int[SG_MAX_VERTEXBUFFER_BINDSLOTS];
        }
        public int num;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SG_MAX_VERTEXBUFFER_BINDSLOTS)]
        public int[] buffer = new int[SG_MAX_VERTEXBUFFER_BINDSLOTS];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct primitive_t
    {
        public int pipeline;           // index into scene.pipelines array
        public int material;           // index into scene.materials array
        public vertex_buffer_mapping_t vertex_buffers; // indices into bufferview array by vbuf bind slot
        public int index_buffer;       // index into bufferview array for index buffer, or SCENE_INVALID_INDEX
        public int base_element;       // index of first index or vertex to draw
        public int num_elements;       // number of vertices or indices to draw
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct mesh_t
    {
        public int first_primitive;    // index into scene.primitives
        public int num_primitives;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct node_t
    {
        public int mesh;           // index into scene.meshes
        public Matrix4x4 transform;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct image_t
    {
        public sg_image img;
        public sg_view tex_view;
        public sg_sampler smp;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct scene_t
    {
        public scene_t()
        {
            buffers = new sg_buffer[SCENE_MAX_BUFFERS];
            images = new image_t[SCENE_MAX_IMAGES];
            pipelines = new sg_pipeline[SCENE_MAX_PIPELINES];
            materials = new material_t[SCENE_MAX_MATERIALS];
            primitives = new primitive_t[SCENE_MAX_PRIMITIVES];
            meshes = new mesh_t[SCENE_MAX_MESHES];
            nodes = new node_t[SCENE_MAX_NODES];

            for (int i = 0; i < SCENE_MAX_BUFFERS; i++)
            {
                buffers[i] = default;
            }

            for (int i = 0; i < SCENE_MAX_IMAGES; i++)
            {
                images[i] = default;
            }

            for (int i = 0; i < SCENE_MAX_PIPELINES; i++)
            {
                pipelines[i] = default;
            }

            for (int i = 0; i < SCENE_MAX_MATERIALS; i++)
            {
                materials[i] = default;
            }

            for (int i = 0; i < SCENE_MAX_PRIMITIVES; i++)
            {
                primitives[i] = default;
            }

            for (int i = 0; i < SCENE_MAX_MESHES; i++)
            {
                meshes[i] = default;
            }

            for (int i = 0; i < SCENE_MAX_NODES; i++)
            {
                nodes[i] = default;
            }
        }
        public int num_buffers;
        public int num_images;
        public int num_pipelines;
        public int num_materials;
        public int num_primitives; // aka 'submeshes'
        public int num_meshes;
        public int num_nodes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SCENE_MAX_BUFFERS)]
        public sg_buffer[] buffers;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SCENE_MAX_IMAGES)]
        public image_t[] images;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SCENE_MAX_PIPELINES)]
        public sg_pipeline[] pipelines;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SCENE_MAX_MATERIALS)]
        public material_t[] materials;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SCENE_MAX_PRIMITIVES)]
        public primitive_t[] primitives;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SCENE_MAX_MESHES)]
        public mesh_t[] meshes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SCENE_MAX_NODES)]
        public node_t[] nodes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct buffer_creation_params_t
    {
        public sg_buffer_usage usage;
        public int offset;
        public int size;
        public int gltf_buffer_index;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct image_sampler_creation_params_t
    {
        public sg_filter min_filter;
        public sg_filter mag_filter;
        public sg_filter mipmap_filter;
        public sg_wrap wrap_s;
        public sg_wrap wrap_t;
        public int gltf_image_index;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct pipeline_cache_params_t
    {
        public sg_vertex_layout_state layout;
        public sg_primitive_type prim_type;
        public sg_index_type index_type;
        public bool alpha;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PassActions
    {
        public sg_pass_action ok;
        public sg_pass_action failed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Shaders
    {
        public sg_shader metallic;
        public sg_shader specular;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class CreationParams
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SCENE_MAX_BUFFERS)]
        public buffer_creation_params_t[] buffers;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SCENE_MAX_IMAGES)]
        public image_sampler_creation_params_t[] images;
        public CreationParams()
        {
            buffers = new buffer_creation_params_t[SCENE_MAX_BUFFERS];
            for (int i = 0; i < SCENE_MAX_BUFFERS; i++)
            {
                buffers[i] = new buffer_creation_params_t();
            }

            images = new image_sampler_creation_params_t[SCENE_MAX_IMAGES];
            for (int i = 0; i < SCENE_MAX_IMAGES; i++)
            {
                images[i] = new image_sampler_creation_params_t();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PipCache
    {
        public PipCache()
        {

        }
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SCENE_MAX_PIPELINES)]
        public pipeline_cache_params_t[] items = new pipeline_cache_params_t[SCENE_MAX_PIPELINES];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Placeholders
    {
        public sg_view white;
        public sg_view normal;
        public sg_view black;
        public sg_sampler smp;
    }

    struct gltf_image_fetch_userdata_t
    {
        public cgltf_size image_index;
    };


    struct gltf_buffer_fetch_userdata_t
    {
        public uint buffer_index;
    };

    [StructLayout(LayoutKind.Sequential)]
    public class _state
    {
        public bool failed;
        public PassActions pass_actions;
        public Shaders shaders;
        public sg_sampler smp;
        public scene_t scene = new scene_t();
        public Camera camera = new Camera();
        public cgltf_light_params_t point_light;     // code-generated from shader
        public Matrix4x4 root_transform;
        public float rx;
        public float ry;
        public CreationParams creation_params = new CreationParams();
        public PipCache pip_cache = new PipCache();
        public Placeholders placeholders = new Placeholders();
    }


    public static _state state = new _state();

    static uint frames = 0;
    static double frameRate = 30;
    static double averageFrameTimeMilliseconds = 33.333;
    static ulong startTime = 0;
    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        for (int i = 0; i < SFETCH_NUM_CHANNELS; i++)
        {
            for (int j = 0; j < SFETCH_NUM_LANES; j++)
            {
                sfetch_buffers[i, j] = SharedBuffer.Create(MAX_FILE_SIZE);
            }
        }
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
              logger = {
                func = &slog_func,
            }
        });

        stm_setup();
         var start_time = stm_now();

        state.camera.Init(new CameraDesc()
        {
            Aspect = 60.0f,
            NearZ = 0.01f,
            FarZ = 100.0f,
            Center = Vector3.Zero,
            Distance = 2.5f,
        });

        // initialize Basis Universal
        sbasisu_setup();

        sdtx_desc_t desc = default;
        desc.fonts[0] = sdtx_font_oric();
        sdtx_setup(desc);

        

        // setup sokol-fetch with 2 channels and 6 lanes per channel,
        // we'll use one channel for mesh data and the other for textures
        sfetch_setup(new sfetch_desc_t()
        {
            max_requests = 64,
            num_channels = SFETCH_NUM_CHANNELS,
            num_lanes = SFETCH_NUM_LANES,
                       logger = {
                func = &slog_func,
            }
        });

        // normal background color, and a "load failed" background color
        state.pass_actions.ok = default;
        state.pass_actions.ok.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_actions.ok.colors[0].clear_value = new sg_color() { r = 0.0f, g = 0.569f, b = 0.918f, a = 1.0f };

        state.pass_actions.failed = default;
        state.pass_actions.failed.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_actions.failed.colors[0].clear_value = new sg_color() { r = 1.0f, g = 0.0f, b = 0.0f, a = 1.0f };

        // create shaders
        state.shaders.metallic = sg_make_shader(cgltf_metallic_shader_desc(sg_query_backend()));

        // setup the point light
        state.point_light = default;
        state.point_light.light_pos = new Vector3(10.0f, 10.0f, 10.0f);
        state.point_light.light_range = 200.0f;
        state.point_light.light_color = new Vector3(1.0f, 1.5f, 2.0f);
        state.point_light.light_intensity = 700.0f;

        // start loading the base gltf file...
        sfetch_request_t request = new sfetch_request_t();
        request.path = util_get_file_path(filename);
        request.callback = &gltf_fetch_callback;
        sfetch_send(request);

        // create placeholder textures and sampler
        uint[] pixels = new uint[64];
        for (int i = 0; i < 64; i++)
        {
            pixels[i] = 0xFFFFFFFF;
        }

        sg_image_desc img_desc = default;
        img_desc.width = 8;
        img_desc.height = 8;
        img_desc.pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8;
        img_desc.data.mip_levels[0] = SG_RANGE(pixels);
       
        state.placeholders.white = sg_make_view(new sg_view_desc()
        {
            texture = 
            {
                image = sg_make_image(img_desc)
            }
        });

        for (int i = 0; i < 64; i++)
        {
            pixels[i] = 0xFF000000;
        }
        state.placeholders.black = sg_make_view(new sg_view_desc()
        {
            texture = 
            {
                image = sg_make_image(img_desc)
            }
        });

        for (int i = 0; i < 64; i++)
        {
            pixels[i] = 0xFF8080FF;
        }

        state.placeholders.normal = sg_make_view(new sg_view_desc()
        {
            texture = 
            {
                image = sg_make_image(img_desc)
            }
        });

        state.placeholders.smp = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = sg_filter.SG_FILTER_NEAREST,
            mag_filter = sg_filter.SG_FILTER_NEAREST
        });

    }

    static void update_scene()
    {

        state.root_transform = Matrix4x4.CreateRotationY(state.rx);
    }

    static cgltf_vs_params_t vs_params_for_node(int node_index)
    {
        return new cgltf_vs_params_t
        {
            model = state.root_transform * state.scene.nodes[node_index].transform,
            view_proj = state.camera.ViewProj,
            eye_pos = state.camera.EyePos
        };
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // if (PauseUpdate) return;
        // pump the sokol-fetch message queue
        sfetch_dowork();

        startTime = (startTime == 0)?stm_now():startTime;
        
        var begin_frame = stm_now();

        // print help text
        sdtx_canvas(sapp_width() * 0.5f, sapp_height() * 0.5f);
        sdtx_color1i(0xFFFFFFFF);
        sdtx_origin(1.0f, 2.0f);
        sdtx_puts("LMB + drag:  rotate\n");
        sdtx_puts("mouse wheel: zoom\n");
        sdtx_print("FPS: {0} \n", frameRate);
        sdtx_print("Avg. Frame Time: {0:F4} ms\n", averageFrameTimeMilliseconds);

        update_scene();
        int fb_width = sapp_width();
        int fb_height = sapp_height();
        state.camera.Update(fb_width, fb_height);

        // render the scene
        if (state.failed)
        {
            // if something went wrong during loading, just render a red screen
            sg_begin_pass(new sg_pass { action = state.pass_actions.failed, swapchain = sglue_swapchain() });
            // __dbgui_draw();
            sg_end_pass();
        }
        else
        {
            sg_begin_pass(new sg_pass { action = state.pass_actions.ok, swapchain = sglue_swapchain() });
            
            bool logThisFrame = sapp_frame_count() % 120 == 0;
            if (logThisFrame)
            {
                Info($"[CGLTF WORKING] Camera: EyePos={state.camera.EyePos}, ViewProj rows:");
                Info($"  Row1: {state.camera.ViewProj.M11}, {state.camera.ViewProj.M12}, {state.camera.ViewProj.M13}, {state.camera.ViewProj.M14}");
                Info($"  Row2: {state.camera.ViewProj.M21}, {state.camera.ViewProj.M22}, {state.camera.ViewProj.M23}, {state.camera.ViewProj.M24}");
                Info($"  Row3: {state.camera.ViewProj.M31}, {state.camera.ViewProj.M32}, {state.camera.ViewProj.M33}, {state.camera.ViewProj.M34}");
                Info($"  Row4: {state.camera.ViewProj.M41}, {state.camera.ViewProj.M42}, {state.camera.ViewProj.M43}, {state.camera.ViewProj.M44}");
                Info($"[CGLTF WORKING] Light: pos={state.point_light.light_pos}, intensity={state.point_light.light_intensity}, range={state.point_light.light_range}");
            }
            
            for (int node_index = 0; node_index < state.scene.num_nodes; node_index++)
            {
                node_t* node = (node_t*)Unsafe.AsPointer(ref state.scene.nodes[node_index]);
                cgltf_vs_params_t vs_params = vs_params_for_node(node_index);
                mesh_t* mesh = (mesh_t*)Unsafe.AsPointer(ref state.scene.meshes[node->mesh]);
                
                if (logThisFrame)
                {
                    Info($"[CGLTF WORKING] Node {node_index}: model matrix rows:");
                    Info($"  Row1: {vs_params.model.M11}, {vs_params.model.M12}, {vs_params.model.M13}, {vs_params.model.M14}");
                    Info($"  Row2: {vs_params.model.M21}, {vs_params.model.M22}, {vs_params.model.M23}, {vs_params.model.M24}");
                    Info($"  Row3: {vs_params.model.M31}, {vs_params.model.M32}, {vs_params.model.M33}, {vs_params.model.M34}");
                    Info($"  Row4: {vs_params.model.M41}, {vs_params.model.M42}, {vs_params.model.M43}, {vs_params.model.M44}");
                }
                
                for (int i = 0; i < mesh->num_primitives; i++)
                {
                    primitive_t* prim = (primitive_t*)Unsafe.AsPointer(ref state.scene.primitives[i + mesh->first_primitive]);
                    material_t* mat = (material_t*)Unsafe.AsPointer(ref state.scene.materials[prim->material]);
                    sg_apply_pipeline(state.scene.pipelines[prim->pipeline]);
                    sg_bindings bind = default;
                    for (int vb_slot = 0; vb_slot < prim->vertex_buffers.num; vb_slot++)
                    {
                        bind.vertex_buffers[vb_slot] = state.scene.buffers[prim->vertex_buffers.buffer[vb_slot]];
                    }
                    if (prim->index_buffer != SCENE_INVALID_INDEX)
                    {
                        bind.index_buffer = state.scene.buffers[prim->index_buffer];
                    }
                    sg_apply_uniforms(UB_cgltf_vs_params, new sg_range { ptr = Unsafe.AsPointer(ref vs_params), size = (uint)Marshal.SizeOf<cgltf_vs_params_t>() });
                    sg_apply_uniforms(UB_cgltf_light_params, new sg_range { ptr = Unsafe.AsPointer(ref state.point_light), size = (uint)Marshal.SizeOf<cgltf_light_params_t>() });
                    if (mat->is_metallic)
                    {
                        sg_view base_color_tex = state.scene.images[mat->metallic.images.base_color].tex_view;
                        sg_view metallic_roughness_tex = state.scene.images[mat->metallic.images.metallic_roughness].tex_view;
                        sg_view normal_tex = state.scene.images[mat->metallic.images.normal].tex_view;
                        sg_view occlusion_tex = state.scene.images[mat->metallic.images.occlusion].tex_view;
                        sg_view emissive_tex = state.scene.images[mat->metallic.images.emissive].tex_view;
                        sg_sampler base_color_smp = state.scene.images[mat->metallic.images.base_color].smp;
                        sg_sampler metallic_roughness_smp = state.scene.images[mat->metallic.images.metallic_roughness].smp;
                        sg_sampler normal_smp = state.scene.images[mat->metallic.images.normal].smp;
                        sg_sampler occlusion_smp = state.scene.images[mat->metallic.images.occlusion].smp;
                        sg_sampler emissive_smp = state.scene.images[mat->metallic.images.emissive].smp;

                        if (base_color_tex.id == 0)
                        {
                            base_color_tex = state.placeholders.white;
                            base_color_smp = state.placeholders.smp;
                        }
                        if (metallic_roughness_tex.id == 0)
                        {
                            metallic_roughness_tex = state.placeholders.white;
                            metallic_roughness_smp = state.placeholders.smp;
                        }
                        if (normal_tex.id == 0)
                        {
                            normal_tex = state.placeholders.normal;
                            normal_smp = state.placeholders.smp;
                        }
                        if (occlusion_tex.id == 0)
                        {
                            occlusion_tex = state.placeholders.white;
                            occlusion_smp = state.placeholders.smp;
                        }
                        if (emissive_tex.id == 0)
                        {
                            emissive_tex = state.placeholders.black;
                            emissive_smp = state.placeholders.smp;
                        }
                        bind.views[VIEW_cgltf_base_color_tex] = base_color_tex;
                        bind.views[VIEW_cgltf_metallic_roughness_tex] = metallic_roughness_tex;
                        bind.views[VIEW_cgltf_normal_tex] = normal_tex;
                        bind.views[VIEW_cgltf_occlusion_tex] = occlusion_tex;
                        bind.views[VIEW_cgltf_emissive_tex] = emissive_tex;
                        bind.samplers[SMP_cgltf_base_color_smp] = base_color_smp;
                        bind.samplers[SMP_cgltf_metallic_roughness_smp] = metallic_roughness_smp;
                        bind.samplers[SMP_cgltf_normal_smp] = normal_smp;
                        bind.samplers[SMP_cgltf_occlusion_smp] = occlusion_smp;
                        bind.samplers[SMP_cgltf_emissive_smp] = emissive_smp;
                        sg_apply_uniforms(UB_cgltf_metallic_params, new sg_range { ptr = Unsafe.AsPointer(ref mat->metallic.fs_params), size = (uint)Marshal.SizeOf<cgltf_metallic_params_t>() });
                    }
                    else
                    {
                        /*
                            sg_apply_uniforms(SG_SHADERSTAGE_VS,
                                SLOT_specular_params,
                                &mat->specular.fs_params,
                                sizeof(specular_params_t));
                        */
                    }
                    sg_apply_bindings(bind);
                    sg_draw((uint)prim->base_element, (uint)prim->num_elements, 1);
                }
            }
            sdtx_draw();
            // __dbgui_draw();
            sg_end_pass();
        }
        sg_commit();

        var deltaTime =stm_ms(stm_now() - startTime);
        frames++;
        if (deltaTime >= 1000)
        {
            frameRate = frames;
            averageFrameTimeMilliseconds = deltaTime/ frameRate;
            frameRate = (int)(1000 / averageFrameTimeMilliseconds);

            frames = 0;
            startTime = 0;
        }
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        SharedBuffer.DisposeAll();
        sfetch_shutdown();
        // __dbgui_shutdown();
        sbasisu_shutdown();

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

        state.camera.HandleEvent(e);
    }

    // load-callback for the GLTF base file
    [UnmanagedCallersOnly]
    static void gltf_fetch_callback(sfetch_response_t* response)
    {
        if (response->dispatched)
        {

            var buf = Unsafe.AsPointer(ref sfetch_buffers[response->channel, response->lane].Buffer[0]);
            // bind buffer to load file into
            sfetch_bind_buffer(response->handle, new sfetch_range_t() { ptr = buf, size = MAX_FILE_SIZE });
        }
        else if (response->fetched)
        {
            // file has been loaded, parse as GLTF
            gltf_parse(response->data);
        }
        if (response->finished)
        {
            if (response->failed)
            {
                state.failed = true;
            }
        }
    }

    // load GLTF data from memory, build scene and issue resource fetch requests
    static unsafe void gltf_parse(sfetch_range_t file_data)
    {
        cgltf_options options = default;
        cgltf_data* out_data = null;

        cgltf_result result = cgltf_parse(in options, file_data.ptr, file_data.size, out out_data);
        if (result == cgltf_result.cgltf_result_success)
        {
            gltf_parse_buffers(out_data);
            gltf_parse_images(out_data);
            gltf_parse_materials(out_data);
            gltf_parse_meshes(out_data);
            gltf_parse_nodes(out_data);
            cgltf_free(out_data);
        }
    }


    [UnmanagedCallersOnly]
    static void gltf_buffer_fetch_callback(sfetch_response_t* response)
    {
        if (response->dispatched)
        {
            sfetch_bind_buffer(response->handle, SFETCH_RANGE(sfetch_buffers[response->channel, response->lane].Buffer));
        }
        else if (response->fetched)
        {
            gltf_buffer_fetch_userdata_t* user_data = (gltf_buffer_fetch_userdata_t*)response->user_data;
            int gltf_buffer_index = (int)user_data->buffer_index;
            var range = new sg_range();
            range.ptr = response->data.ptr;
            range.size = response->data.size;
            create_sg_buffers_for_gltf_buffer(gltf_buffer_index, range);
        }
        if (response->finished)
        {
            if (response->failed)
            {
                state.failed = true;
            }
        }
    }


    // parse GLTF nodes into our own node definition
    static void gltf_parse_nodes(cgltf_data* gltf)
    {
        if (gltf->nodes_count > SCENE_MAX_NODES)
        {
            state.failed = true;
            return;
        }
        for (cgltf_size node_index = 0; node_index < gltf->nodes_count; node_index++)
        {
            cgltf_node* gltf_node = (cgltf_node*)Unsafe.AsPointer(ref gltf->nodes[node_index]);
            // ignore nodes without mesh, those are not relevant since we
            // bake the transform hierarchy into per-node world space transforms
            if (gltf_node->mesh != null)
            {
                node_t* node = (node_t*)Unsafe.AsPointer(ref state.scene.nodes[state.scene.num_nodes++]);
                node->mesh = gltf_mesh_index(gltf, gltf_node->mesh);
                node->transform = build_transform_for_gltf_node(gltf, gltf_node);
            }
        }
    }


    static Matrix4x4 FromGltfMatrix(cgltf_node.matrixCollection coll)
    {
        return new Matrix4x4(
            coll[0], coll[1], coll[2], coll[3],
            coll[4], coll[5], coll[6], coll[7],
            coll[8], coll[9], coll[10], coll[11],
            coll[12], coll[13], coll[14], coll[15]
        );
    }


    static Matrix4x4 build_transform_for_gltf_node(cgltf_data* gltf, cgltf_node* node)
    {
        Matrix4x4 parent_tform = Identity;
        if (node->parent != null)
        {
            parent_tform = build_transform_for_gltf_node(gltf, node->parent);
        }
        if (node->has_matrix != 0)
        {
            return  FromGltfMatrix(node->matrix);
        }
        else
        {
            Matrix4x4 translate = Identity;
            Matrix4x4 rotate = Identity;
            Matrix4x4 scale = Identity;
            if (node->has_translation != 0)
            {
                translate = Matrix4x4.CreateTranslation(new Vector3(node->translation[0], node->translation[1], node->translation[2]));
            }
            if (node->has_rotation != 0)
            {
                rotate = Matrix4x4.CreateFromQuaternion(new Quaternion(node->rotation[0], node->rotation[1], node->rotation[2], node->rotation[3]));
            }
            if (node->has_scale != 0)
            {
                scale = Matrix4x4.CreateScale(new Vector3(node->scale[0], node->scale[1], node->scale[2]));
            }

            // TBD elix22
            // NOTE: not sure if the multiplication order is correct
            return parent_tform * scale * rotate * translate;
        }
    }


    // parse the GLTF buffer definitions and start loading buffer blobs
    static void gltf_parse_buffers(cgltf_data* gltf)
    {
        if (gltf->buffer_views_count > SCENE_MAX_BUFFERS)
        {
            state.failed = true;
            return;
        }
        // load-callback for GLTF buffer files


        // parse the buffer-view attributes
        state.scene.num_buffers = (int)gltf->buffer_views_count;
        for (int i = 0; i < state.scene.num_buffers; i++)
        {
            cgltf_buffer_view* gltf_buf_view = &gltf->buffer_views[i];
            buffer_creation_params_t* p = (buffer_creation_params_t*)Unsafe.AsPointer(ref state.creation_params.buffers[i]);
            p->gltf_buffer_index = gltf_buffer_index(gltf, gltf_buf_view->buffer);
            p->offset = (int)gltf_buf_view->offset;
            p->size = (int)gltf_buf_view->size;
            if (gltf_buf_view->type == cgltf_buffer_view_type.cgltf_buffer_view_type_indices)
            {
                p->usage.index_buffer = true;
            }
            else
            {
                p->usage.vertex_buffer = true;
            }
            // allocate a sokol-gfx buffer handle
            state.scene.buffers[i] = sg_alloc_buffer();
        }

        // start loading all buffers
        for (uint i = 0; i < gltf->buffers_count; i++)
        {
            cgltf_buffer* gltf_buf = &gltf->buffers[i];
            gltf_buffer_fetch_userdata_t user_data = new gltf_buffer_fetch_userdata_t
            {
                buffer_index = i
            };

            sfetch_request_t request = new sfetch_request_t();
            request.path = util_get_file_path(Path.Combine("gltf", "DamagedHelmet", gltf_buf->uri.String()));
            request.callback = &gltf_buffer_fetch_callback;
            request.user_data.ptr = Unsafe.AsPointer(ref user_data);
            request.user_data.size = (uint)Marshal.SizeOf<gltf_buffer_fetch_userdata_t>();

            sfetch_send(request);

        }
    }

    // parse all the image-related stuff in the GLTF data

    // https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#samplerminfilter
    static sg_filter gltf_to_sg_min_filter(cgltf_filter_type gltf_filter)
    {
        switch (gltf_filter)
        {
            case cgltf_filter_type.cgltf_filter_type_nearest: return SG_FILTER_NEAREST;
            case cgltf_filter_type.cgltf_filter_type_linear: return SG_FILTER_LINEAR;
            default: return SG_FILTER_LINEAR;
        }
    }

    static sg_filter gltf_to_sg_mag_filter(cgltf_filter_type gltf_filter)
    {
        switch (gltf_filter)
        {
            case cgltf_filter_type.cgltf_filter_type_nearest: return SG_FILTER_NEAREST;
            case cgltf_filter_type.cgltf_filter_type_linear: return SG_FILTER_LINEAR;
            default: return SG_FILTER_LINEAR;
        }
    }

    static sg_filter gltf_to_sg_mipmap_filter(cgltf_filter_type gltf_filter)
    {
        switch (gltf_filter)
        {
            case cgltf_filter_type.cgltf_filter_type_nearest:
            case cgltf_filter_type.cgltf_filter_type_linear:
            case cgltf_filter_type.cgltf_filter_type_nearest_mipmap_nearest:
            case cgltf_filter_type.cgltf_filter_type_linear_mipmap_nearest:
                return SG_FILTER_NEAREST;
            case cgltf_filter_type.cgltf_filter_type_nearest_mipmap_linear:
            case cgltf_filter_type.cgltf_filter_type_linear_mipmap_linear:
                return SG_FILTER_LINEAR;
            default: return SG_FILTER_LINEAR;
        }
    }

    // https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#samplerwraps
    static sg_wrap gltf_to_sg_wrap(cgltf_wrap_mode gltf_wrap)
    {
        switch (gltf_wrap)
        {
            case cgltf_wrap_mode.cgltf_wrap_mode_clamp_to_edge: return SG_WRAP_CLAMP_TO_EDGE;
            case cgltf_wrap_mode.cgltf_wrap_mode_mirrored_repeat: return SG_WRAP_MIRRORED_REPEAT;
            case cgltf_wrap_mode.cgltf_wrap_mode_repeat: return SG_WRAP_REPEAT;
            default: return SG_WRAP_REPEAT;
        }
    }

    static void gltf_parse_images(cgltf_data* gltf)
    {
        if (gltf->textures_count > SCENE_MAX_IMAGES)
        {
            state.failed = true;
            return;
        }

        static image_sampler_creation_params_t* AsPointer(ref image_sampler_creation_params_t p)
        {
            return (image_sampler_creation_params_t*)Unsafe.AsPointer(ref p);
        }

        // parse the texture and sampler attributes
        state.scene.num_images = (int)gltf->textures_count;
        for (int i = 0; i < state.scene.num_images; i++)
        {
            cgltf_texture* gltf_tex = &gltf->textures[i];
            image_sampler_creation_params_t* p = AsPointer(ref state.creation_params.images[i]);
            p->gltf_image_index = gltf_image_index(gltf, gltf_tex->image);
            p->min_filter = gltf_to_sg_min_filter(gltf_tex->sampler->min_filter);
            p->mag_filter = gltf_to_sg_mag_filter(gltf_tex->sampler->mag_filter);
            p->mipmap_filter = gltf_to_sg_mipmap_filter(gltf_tex->sampler->min_filter);
            p->wrap_s = gltf_to_sg_wrap(gltf_tex->sampler->wrap_s);
            p->wrap_t = gltf_to_sg_wrap(gltf_tex->sampler->wrap_t);
            state.scene.images[i].img.id = SG_INVALID_ID;
            state.scene.images[i].smp.id = SG_INVALID_ID;
            state.scene.images[i].tex_view.id = SG_INVALID_ID;
        }

        // start loading all images
        for (cgltf_size i = 0; i < gltf->images_count; i++)
        {
            cgltf_image* gltf_img = &gltf->images[i];
            gltf_image_fetch_userdata_t user_data = new gltf_image_fetch_userdata_t
            {
                image_index = i
            };

            sfetch_request_t request = new sfetch_request_t();
            request.path = util_get_file_path(Path.Combine("gltf", "DamagedHelmet", gltf_img->uri.String()));
            request.callback = &gltf_image_fetch_callback;
            request.user_data.ptr = Unsafe.AsPointer(ref user_data);
            request.user_data.size = (uint)Marshal.SizeOf<gltf_buffer_fetch_userdata_t>();

            sfetch_send(request);
        }
    }

    // parse GLTF materials into our own material definition
    static void gltf_parse_materials(cgltf_data* gltf)
    {
        if (gltf->materials_count > SCENE_MAX_MATERIALS)
        {
            state.failed = true;
            return;
        }
        state.scene.num_materials = (int)gltf->materials_count;
        for (int i = 0; i < state.scene.num_materials; i++)
        {
            cgltf_material* gltf_mat = &gltf->materials[i];
            material_t* scene_mat = (material_t*)Unsafe.AsPointer(ref state.scene.materials[i]);
            scene_mat->is_metallic = gltf_mat->has_pbr_metallic_roughness  != 0;
            if (scene_mat->is_metallic)
            {
                cgltf_pbr_metallic_roughness* src = &gltf_mat->pbr_metallic_roughness;
                metallic_material_t* dst = &scene_mat->metallic;
                for (int d = 0; d < 4; d++)
                {
                    dst->fs_params.base_color_factor[d] = src->base_color_factor[d];
                }
                for (int d = 0; d < 3; d++)
                {
                    dst->fs_params.emissive_factor[d] = gltf_mat->emissive_factor[d];
                }
                dst->fs_params.metallic_factor = src->metallic_factor;
                dst->fs_params.roughness_factor = src->roughness_factor;
                dst->images = new metallic_images_t()
                {
                    base_color = gltf_texture_index(gltf, src->base_color_texture.texture),
                    metallic_roughness = gltf_texture_index(gltf, src->metallic_roughness_texture.texture),
                    normal = gltf_texture_index(gltf, gltf_mat->normal_texture.texture),
                    occlusion = gltf_texture_index(gltf, gltf_mat->occlusion_texture.texture),
                    emissive = gltf_texture_index(gltf, gltf_mat->emissive_texture.texture)
                };
            }
        }
    }

    // parse GLTF meshes into our own mesh and submesh definition
    static void gltf_parse_meshes(cgltf_data* gltf)
    {
        if (gltf->meshes_count > SCENE_MAX_MESHES)
        {
            state.failed = true;
            return;
        }
        state.scene.num_meshes = (int)gltf->meshes_count;
        for (cgltf_size mesh_index = 0; mesh_index < gltf->meshes_count; mesh_index++)
        {
            cgltf_mesh* gltf_mesh = &gltf->meshes[mesh_index];
            if (((int)gltf_mesh->primitives_count + state.scene.num_primitives) > SCENE_MAX_PRIMITIVES)
            {
                state.failed = true;
                return;
            }
            mesh_t* mesh = (mesh_t*)Unsafe.AsPointer(ref state.scene.meshes[mesh_index]);
            mesh->first_primitive = state.scene.num_primitives;
            mesh->num_primitives = (int)gltf_mesh->primitives_count;
            for (cgltf_size prim_index = 0; prim_index < gltf_mesh->primitives_count; prim_index++)
            {
                cgltf_primitive* gltf_prim = &gltf_mesh->primitives[prim_index];
                primitive_t* prim = (primitive_t*)Unsafe.AsPointer(ref state.scene.primitives[state.scene.num_primitives++]);

                // // a mapping from sokol-gfx vertex buffer bind slots into the scene.buffers array
                prim->vertex_buffers = create_vertex_buffer_mapping_for_gltf_primitive(gltf, gltf_prim);
                // create or reuse a matching pipeline state object
                prim->pipeline = create_sg_pipeline_for_gltf_primitive(gltf, gltf_prim, &prim->vertex_buffers);
                // the material parameters
                prim->material = gltf_material_index(gltf, gltf_prim->material);
                // index buffer, base element, num elements
                if (gltf_prim->indices != null)
                {
                    prim->index_buffer = gltf_bufferview_index(gltf, gltf_prim->indices->buffer_view);
                    //     assert(state.creation_params.buffers[prim->index_buffer].type == SG_BUFFERTYPE_INDEXBUFFER);
                    //     assert(gltf_prim->indices->stride != 0);
                    prim->base_element = 0;
                    prim->num_elements = (int)gltf_prim->indices->count;
                }
                else
                {
                    // hmm... looking up the number of elements to render from
                    // a random vertex component accessor looks a bit shady
                    prim->index_buffer = SCENE_INVALID_INDEX;
                    prim->base_element = 0;
                    prim->num_elements = (int)gltf_prim->attributes->data->count;
                }
            }
        }
    }

    // creates a vertex buffer bind slot mapping for a specific GLTF primitive
    static vertex_buffer_mapping_t create_vertex_buffer_mapping_for_gltf_primitive(cgltf_data* gltf, cgltf_primitive* prim)
    {
        vertex_buffer_mapping_t map = new vertex_buffer_mapping_t();
        for (int i = 0; i < SG_MAX_VERTEXBUFFER_BINDSLOTS; i++)
        {
            map.buffer[i] = SCENE_INVALID_INDEX;
        }
        for (cgltf_size attr_index = 0; attr_index < prim->attributes_count; attr_index++)
        {
            cgltf_attribute* attr = &prim->attributes[attr_index];
            cgltf_accessor* acc = attr->data;
            int buffer_view_index = gltf_bufferview_index(gltf, acc->buffer_view);
            int i = 0;
            for (; i < map.num; i++)
            {
                if (map.buffer[i] == buffer_view_index)
                {
                    break;
                }
            }
            if ((i == map.num) && (map.num < SG_MAX_VERTEXBUFFER_BINDSLOTS))
            {
                map.buffer[map.num++] = buffer_view_index;
            }
            // assert(map.num <= SG_MAX_VERTEXBUFFER_BINDSLOTS);
        }
        return map;
    }

    static int gltf_attr_type_to_vs_input_slot(cgltf_attribute_type attr_type)
    {
        switch (attr_type)
        {
            case cgltf_attribute_type.cgltf_attribute_type_position: return ATTR_cgltf_metallic_position;
            case cgltf_attribute_type.cgltf_attribute_type_normal: return ATTR_cgltf_metallic_normal;
            case cgltf_attribute_type.cgltf_attribute_type_texcoord: return ATTR_cgltf_metallic_texcoord;
            default: return SCENE_INVALID_INDEX;
        }
    }

    static sg_primitive_type gltf_to_prim_type(cgltf_primitive_type prim_type)
    {
        switch (prim_type)
        {
            case cgltf_primitive_type.cgltf_primitive_type_points: return SG_PRIMITIVETYPE_POINTS;
            case cgltf_primitive_type.cgltf_primitive_type_lines: return SG_PRIMITIVETYPE_LINES;
            case cgltf_primitive_type.cgltf_primitive_type_line_strip: return SG_PRIMITIVETYPE_LINE_STRIP;
            case cgltf_primitive_type.cgltf_primitive_type_triangles: return SG_PRIMITIVETYPE_TRIANGLES;
            case cgltf_primitive_type.cgltf_primitive_type_triangle_strip: return SG_PRIMITIVETYPE_TRIANGLE_STRIP;
            default: return _SG_PRIMITIVETYPE_DEFAULT;
        }
    }

    static sg_index_type gltf_to_index_type(cgltf_primitive* prim)
    {
        if (prim->indices != null)
        {
            if (prim->indices->component_type == cgltf_component_type.cgltf_component_type_r_16u)
            {
                return SG_INDEXTYPE_UINT16;
            }
            else
            {
                return SG_INDEXTYPE_UINT32;
            }
        }
        else
        {
            return SG_INDEXTYPE_NONE;
        }
    }

    static sg_vertex_format gltf_to_vertex_format(cgltf_accessor* acc)
    {
        switch (acc->component_type)
        {
            case cgltf_component_type.cgltf_component_type_r_8:
                if (acc->type == cgltf_type.cgltf_type_vec4)
                {
                    return acc->normalized != 0 ? SG_VERTEXFORMAT_BYTE4N : SG_VERTEXFORMAT_BYTE4;
                }
                break;
            case cgltf_component_type.cgltf_component_type_r_8u:
                if (acc->type == cgltf_type.cgltf_type_vec4)
                {
                    return acc->normalized != 0 ? SG_VERTEXFORMAT_UBYTE4N : SG_VERTEXFORMAT_UBYTE4;
                }
                break;
            case cgltf_component_type.cgltf_component_type_r_16:
                switch (acc->type)
                {
                    case cgltf_type.cgltf_type_vec2: return acc->normalized != 0 ? SG_VERTEXFORMAT_SHORT2N : SG_VERTEXFORMAT_SHORT2;
                    case cgltf_type.cgltf_type_vec4: return acc->normalized != 0 ? SG_VERTEXFORMAT_SHORT4N : SG_VERTEXFORMAT_SHORT4;
                    default: break;
                }
                break;
            case cgltf_component_type.cgltf_component_type_r_32f:
                switch (acc->type)
                {
                    case cgltf_type.cgltf_type_scalar: return SG_VERTEXFORMAT_FLOAT;
                    case cgltf_type.cgltf_type_vec2: return SG_VERTEXFORMAT_FLOAT2;
                    case cgltf_type.cgltf_type_vec3: return SG_VERTEXFORMAT_FLOAT3;
                    case cgltf_type.cgltf_type_vec4: return SG_VERTEXFORMAT_FLOAT4;
                    default: break;
                }
                break;
            default: break;
        }
        return SG_VERTEXFORMAT_INVALID;
    }

    // helper to compare to pipeline-cache items
    static bool pipelines_equal(pipeline_cache_params_t* p0, pipeline_cache_params_t* p1)
    {
        if (p0->prim_type != p1->prim_type)
        {
            return false;
        }
        if (p0->alpha != p1->alpha)
        {
            return false;
        }
        if (p0->index_type != p1->index_type)
        {
            return false;
        }
        for (int i = 0; i < SG_MAX_VERTEX_ATTRIBUTES; i++)
        {
            sg_vertex_attr_state* a0 = (sg_vertex_attr_state*)Unsafe.AsPointer(ref p0->layout.attrs[i]);
            sg_vertex_attr_state* a1 = (sg_vertex_attr_state*)Unsafe.AsPointer(ref p1->layout.attrs[i]);
            if ((a0->buffer_index != a1->buffer_index) ||
                (a0->offset != a1->offset) ||
                (a0->format != a1->format))
            {
                return false;
            }
        }
        return true;
    }


    static sg_vertex_layout_state create_sg_layout_for_gltf_primitive(cgltf_data* gltf, cgltf_primitive* prim, vertex_buffer_mapping_t* vbuf_map)
    {
        // assert(prim->attributes_count <= SG_MAX_VERTEX_ATTRIBUTES);
        sg_vertex_layout_state layout = default;
        for (cgltf_size attr_index = 0; attr_index < prim->attributes_count; attr_index++)
        {
            cgltf_attribute* attr = &prim->attributes[attr_index];
            int attr_slot = gltf_attr_type_to_vs_input_slot(attr->type);
            if (attr_slot != SCENE_INVALID_INDEX)
            {
                layout.attrs[attr_slot].format = gltf_to_vertex_format(attr->data);
            }
            int buffer_view_index = gltf_bufferview_index(gltf, attr->data->buffer_view);
            for (int vb_slot = 0; vb_slot < vbuf_map->num; vb_slot++)
            {
                if (vbuf_map->buffer[vb_slot] == buffer_view_index)
                {
                    layout.attrs[attr_slot].buffer_index = vb_slot;
                }
            }
        }
        return layout;
    }

    // Create a unique sokol-gfx pipeline object for GLTF primitive (aka submesh),
    // maintains a cache of shared, unique pipeline objects. Returns an index
    // into state.scene.pipelines
    static int create_sg_pipeline_for_gltf_primitive(cgltf_data* gltf, cgltf_primitive* prim, vertex_buffer_mapping_t* vbuf_map)
    {
        pipeline_cache_params_t pip_params = new pipeline_cache_params_t
        {
            layout = create_sg_layout_for_gltf_primitive(gltf, prim, vbuf_map),
            prim_type = gltf_to_prim_type(prim->type),
            index_type = gltf_to_index_type(prim),
            alpha = prim->material->alpha_mode != cgltf_alpha_mode.cgltf_alpha_mode_opaque
        };
        int i = 0;
        for (; i < state.scene.num_pipelines; i++)
        {
            if (pipelines_equal((pipeline_cache_params_t*)Unsafe.AsPointer(ref state.pip_cache.items[i]), &pip_params))
            {
                // an indentical pipeline already exists, reuse this
                // assert(state.scene.pipelines[i].id != SG_INVALID_ID);
                return i;
            }
        }
        if ((i == state.scene.num_pipelines) && (state.scene.num_pipelines < SCENE_MAX_PIPELINES))
        {
            state.pip_cache.items[i] = pip_params;
            bool is_metallic = prim->material->has_pbr_metallic_roughness  != 0;

            sg_pipeline_desc desc = new sg_pipeline_desc()
            {
                layout = pip_params.layout,
                shader = is_metallic ? state.shaders.metallic : state.shaders.specular,
                primitive_type = pip_params.prim_type,
                index_type = pip_params.index_type,
                cull_mode = SG_CULLMODE_BACK,
                face_winding = SG_FACEWINDING_CCW,
                depth = new sg_depth_state
                {
                    write_enabled = !pip_params.alpha,
                    compare = SG_COMPAREFUNC_LESS_EQUAL,
                }
            };
            desc.colors[0].write_mask = pip_params.alpha ? sg_color_mask.SG_COLORMASK_RGB : 0;
            desc.colors[0].blend.enabled = pip_params.alpha;
            desc.colors[0].blend.src_factor_rgb = pip_params.alpha ? sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA : 0;
            desc.colors[0].blend.dst_factor_rgb = pip_params.alpha ? sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA : 0;
            state.scene.pipelines[i] = sg_make_pipeline(desc);

            state.scene.num_pipelines++;
        }
        // assert(state.scene.num_pipelines <= SCENE_MAX_PIPELINES);
        return i;
    }

    static sg_range Range(void* ptr, nuint size)
    {
        sg_range result = new sg_range()
        {
            ptr = ptr,
            size = (uint)size
        };
        return result;
    }

    [UnmanagedCallersOnly]
    static void gltf_image_fetch_callback(sfetch_response_t* response)
    {
        if (response->dispatched)
        {
            sfetch_bind_buffer(response->handle, SFETCH_RANGE(sfetch_buffers[response->channel, response->lane].Buffer));
        }
        else if (response->fetched)
        {
            gltf_image_fetch_userdata_t* user_data = (gltf_image_fetch_userdata_t*)response->user_data;
            int gltf_image_index = (int)user_data->image_index;
            create_sg_image_samplers_for_gltf_image(gltf_image_index, Range(response->data.ptr, response->data.size));
        }
        if (response->finished)
        {
            if (response->failed)
            {
                state.failed = true;
            }
        }
    }

    // create the sokol-gfx image objects associated with a GLTF image
    static void create_sg_image_samplers_for_gltf_image(int gltf_image_index, sg_range data)
    {
        for (int i = 0; i < state.scene.num_images; i++)
        {
            image_sampler_creation_params_t* p = (image_sampler_creation_params_t*)Unsafe.AsPointer(ref state.creation_params.images[i]);
            if (p->gltf_image_index == gltf_image_index)
            {
                state.scene.images[i].img = sbasisu_make_image(data);
                state.scene.images[i].tex_view = sg_make_view(new sg_view_desc()
                {
                    texture = { image = state.scene.images[i].img }
                });
                state.scene.images[i].smp = sg_make_sampler(new sg_sampler_desc()
                {
                    min_filter = p->min_filter,
                    mag_filter = p->mag_filter,
                    mipmap_filter = p->mipmap_filter,
                });
            }
        }
    }


    // compute indices from cgltf element pointers
    static int gltf_buffer_index(cgltf_data* gltf, cgltf_buffer* buf)
    {

        return (int)(buf - gltf->buffers);
    }


    static int gltf_bufferview_index(cgltf_data* gltf, cgltf_buffer_view* buf_view)
    {

        return (int)(buf_view - gltf->buffer_views);
    }

    static int gltf_image_index(cgltf_data* gltf, cgltf_image* img)
    {

        return (int)(img - gltf->images);
    }

    static int gltf_texture_index(cgltf_data* gltf, cgltf_texture* tex)
    {

        return (int)(tex - gltf->textures);
    }

    static int gltf_material_index(cgltf_data* gltf, cgltf_material* mat)
    {

        return (int)(mat - gltf->materials);
    }

    static int gltf_mesh_index(cgltf_data* gltf, cgltf_mesh* mesh)
    {

        return (int)(mesh - gltf->meshes);
    }

    // create the sokol-gfx buffer objects associated with a GLTF buffer view
    static void create_sg_buffers_for_gltf_buffer(int gltf_buffer_index, sg_range data)
    {
        for (int i = 0; i < state.scene.num_buffers; i++)
        {
            buffer_creation_params_t* p = (buffer_creation_params_t*)Unsafe.AsPointer(ref state.creation_params.buffers[i]);
            if (p->gltf_buffer_index == gltf_buffer_index)
            {
                //     assert((size_t)(p->offset + p->size) <= data.size);
                sg_init_buffer(state.scene.buffers[i], new sg_buffer_desc()
                {
                    usage = p->usage,
                    data = {
                    ptr = (byte*)data.ptr + p->offset,
                    size = (nuint)p->size,
                }
                });
            }
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
            width = 0,
            height = 0,
            sample_count = 1,
            window_title = "cgltf sample",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}