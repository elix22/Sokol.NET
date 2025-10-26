
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

public static unsafe class CGLTFSceneApp
{
    static CGltfParser? _parser;

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
    // Must match FileSystem configuration (2 channels, 2 lanes)
    const int SFETCH_NUM_CHANNELS = 2;
    const int SFETCH_NUM_LANES = 2;
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

    // Copy CGltfParser scene data to inline state structure
    static void CopyParserSceneToState(CGltfScene parserScene)
    {
        try
        {
            // Copy buffers
            state.scene.num_buffers = parserScene.NumBuffers;
            for (int i = 0; i < parserScene.NumBuffers; i++)
            {
                state.scene.buffers[i] = parserScene.Buffers[i];
            }
            
            // Copy images
            state.scene.num_images = parserScene.NumImages;
            for (int i = 0; i < parserScene.NumImages; i++)
            {
                state.scene.images[i].img = parserScene.Images[i].Image;
                state.scene.images[i].smp = parserScene.Images[i].Sampler;
                state.scene.images[i].tex_view = parserScene.Images[i].TexView;
            }
            
            // Copy materials
            state.scene.num_materials = parserScene.NumMaterials;
            for (int i = 0; i < parserScene.NumMaterials; i++)
            {
                var parserMat = parserScene.Materials[i];
                ref material_t stateMat = ref state.scene.materials[i];
                
                stateMat.is_metallic = parserMat.IsMetallic;
                if (parserMat.IsMetallic)
                {
                    stateMat.metallic.fs_params = parserMat.Metallic.FsParams;
                    stateMat.metallic.images.base_color = parserMat.Metallic.Images.BaseColor;
                    stateMat.metallic.images.metallic_roughness = parserMat.Metallic.Images.MetallicRoughness;
                    stateMat.metallic.images.normal = parserMat.Metallic.Images.Normal;
                    stateMat.metallic.images.occlusion = parserMat.Metallic.Images.Occlusion;
                    stateMat.metallic.images.emissive = parserMat.Metallic.Images.Emissive;
                }
            }
            
            // Copy pipelines
            state.scene.num_pipelines = parserScene.NumPipelines;
            for (int i = 0; i < parserScene.NumPipelines; i++)
            {
                state.scene.pipelines[i] = parserScene.Pipelines[i];
            }
            
            // Copy primitives
            state.scene.num_primitives = parserScene.NumPrimitives;
            for (int i = 0; i < parserScene.NumPrimitives; i++)
            {
                var parserPrim = parserScene.Primitives[i];
                ref primitive_t statePrim = ref state.scene.primitives[i];
                
                statePrim.pipeline = parserPrim.PipelineIndex;
                statePrim.material = parserPrim.MaterialIndex;
                statePrim.index_buffer = parserPrim.IndexBuffer;
                statePrim.base_element = parserPrim.BaseElement;
                statePrim.num_elements = parserPrim.NumElements;
                
                // Copy vertex buffer mapping
                if (statePrim.vertex_buffers.buffer == null)
                {
                    statePrim.vertex_buffers.buffer = new int[SG_MAX_VERTEXBUFFER_BINDSLOTS];
                }
                
                statePrim.vertex_buffers.num = parserPrim.VertexBuffers.Num;
                if (parserPrim.VertexBuffers.BufferIndices != null)
                {
                    for (int j = 0; j < parserPrim.VertexBuffers.Num; j++)
                    {
                        statePrim.vertex_buffers.buffer[j] = parserPrim.VertexBuffers.BufferIndices[j];
                    }
                }
            }
            
            // Copy meshes
            state.scene.num_meshes = parserScene.NumMeshes;
            for (int i = 0; i < parserScene.NumMeshes; i++)
            {
                var parserMesh = parserScene.Meshes[i];
                ref mesh_t stateMesh = ref state.scene.meshes[i];
                
                stateMesh.first_primitive = parserMesh.FirstPrimitive;
                stateMesh.num_primitives = parserMesh.NumPrimitives;
            }
            
            // Copy nodes
            state.scene.num_nodes = parserScene.NumNodes;
            for (int i = 0; i < parserScene.NumNodes; i++)
            {
                var parserNode = parserScene.Nodes[i];
                ref node_t stateNode = ref state.scene.nodes[i];
                
                stateNode.mesh = parserNode.MeshIndex;
                stateNode.transform = parserNode.Transform;
            }
        }
        catch (Exception ex)
        {
            Error($"Error copying parser scene to state: {ex.Message}");
            Error($"Stack trace: {ex.StackTrace}");
        }
    }

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

        // Initialize FileSystem (wraps sokol-fetch)
        // FileSystem will be used by CGltfParser for loading external textures
        FileSystem.Instance.Initialize();

        // normal background color, and a "load failed" background color
        state.pass_actions.ok = default;
        state.pass_actions.ok.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_actions.ok.colors[0].clear_value = new sg_color() { r = 0.0f, g = 0.569f, b = 0.918f, a = 1.0f };

        state.pass_actions.failed = default;
        state.pass_actions.failed.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_actions.failed.colors[0].clear_value = new sg_color() { r = 1.0f, g = 0.0f, b = 0.0f, a = 1.0f };

        // create shaders
        state.shaders.metallic = sg_make_shader(cgltf_metallic_shader_desc(sg_query_backend()));

        // Initialize CGltfParser (for future migration)
        _parser = new CGltfParser();
        _parser.Init(state.shaders.metallic, state.shaders.metallic); // Using metallic shader for both for now

        // setup the point light
        state.point_light = default;
        state.point_light.light_pos = new Vector3(10.0f, 10.0f, 10.0f);
        state.point_light.light_range = 200.0f;
        state.point_light.light_color = new Vector3(1.0f, 1.5f, 2.0f);
        state.point_light.light_intensity = 700.0f;

        // Load GLTF file using CGltfParser (async)
        string gltfFilePath = util_get_file_path(filename);
        _parser.LoadFromFileAsync(gltfFilePath, 
            onComplete: (scene) => {
                // Copy CGltfParser scene data to state.scene so existing rendering code works
                CopyParserSceneToState(scene);
            },
            onFailed: (error) => {
                Error($"Failed to load GLTF scene: {error}");
                state.failed = true;
            });

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
            
            
            for (int node_index = 0; node_index < state.scene.num_nodes; node_index++)
            {
                node_t* node = (node_t*)Unsafe.AsPointer(ref state.scene.nodes[node_index]);
                cgltf_vs_params_t vs_params = vs_params_for_node(node_index);
                mesh_t* mesh = (mesh_t*)Unsafe.AsPointer(ref state.scene.meshes[node->mesh]);
                
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


    // ==========================================
    // Old inline GLTF parsing functions removed
    // Now using CGltfParser.cs for all GLTF operations
    // ==========================================

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
            window_title = "cgltf scene sample",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}