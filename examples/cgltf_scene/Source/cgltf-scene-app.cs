
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

    // const string filename = "assimpScene.glb";
     const string filename = "gltf/DamagedHelmet/DamagedHelmet.gltf";

    const int SCENE_INVALID_INDEX = -1;

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
        public CGltfScene scene = new CGltfScene();
        public Camera camera = new Camera();
        public cgltf_light_params_t point_light;
        public Matrix4x4 root_transform;
        public float rx;
        public float ry;
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

        // Setup light with moderate intensity for shader with ambient
        state.point_light = default;
        state.point_light.light_pos = new Vector3(10.0f, 10.0f, 10.0f);
        state.point_light.light_range = 200.0f;
        state.point_light.light_color = new Vector3(1.0f, 1.5f, 2.0f);
        state.point_light.light_intensity = 10.0f;  // Lower due to 30% ambient in shader

        // Load GLTF file using CGltfParser (async)
        string gltfFilePath = util_get_file_path(filename);

        _parser.LoadFromFileAsync(gltfFilePath, state.scene,
    onComplete: () =>
    {

    },
    onFailed: (error) =>
    {
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

        // Normal map placeholder: need (0.5, 0.5) for tangent space flat normal
        // Shader reads texture(...).xw which maps to Red and Alpha channels
        // After *2.0-1.0 transform: (0.5*2-1, 0.5*2-1) = (0, 0)  
        // Format is RGBA8, stored as 0xAABBGGRR in little-endian
        // We need: R=128 (0.5), G=any, B=any, A=128 (0.5)
        for (int i = 0; i < 64; i++)
        {
            pixels[i] = 0x80FF8080;  // ABGR: A=128, B=255, G=128, R=128
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


    static cgltf_vs_params_t vs_params_for_node(int node_index)
    {
        return new cgltf_vs_params_t
        {
            model = state.root_transform * state.scene.Nodes[node_index].Transform,
            view_proj = state.camera.ViewProj,
            eye_pos = state.camera.EyePos
        };
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // if (PauseUpdate) return;
        // pump the sokol-fetch message queue
        FileSystem.Instance.Update();

        startTime = (startTime == 0) ? stm_now() : startTime;

        var begin_frame = stm_now();

        // print help text
        sdtx_canvas(sapp_width() * 0.5f, sapp_height() * 0.5f);
        sdtx_color1i(0xFFFFFFFF);
        sdtx_origin(1.0f, 2.0f);
        sdtx_puts("LMB + drag:  rotate\n");
        sdtx_puts("mouse wheel: zoom\n");
        sdtx_print("FPS: {0} \n", frameRate);
        sdtx_print("Avg. Frame Time: {0:F4} ms\n", averageFrameTimeMilliseconds);

        state.root_transform = Matrix4x4.CreateRotationY(state.rx);

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


            for (int node_index = 0; node_index < state.scene.NumNodes; node_index++)
            {
                ref CGltfNode node = ref state.scene.Nodes[node_index];
                cgltf_vs_params_t vs_params = vs_params_for_node(node_index);
                ref CGltfMesh mesh = ref state.scene.Meshes[node.MeshIndex];

                for (int i = 0; i < mesh.NumPrimitives; i++)
                {
                    ref CGltfPrimitive prim = ref state.scene.Primitives[i + mesh.FirstPrimitive];
                    ref CGltfMaterial mat = ref state.scene.Materials[prim.MaterialIndex];

                    sg_apply_pipeline(state.scene.Pipelines[prim.PipelineIndex]);
                    sg_bindings bind = default;
                    for (int vb_slot = 0; vb_slot < prim.VertexBuffers.Num; vb_slot++)
                    {
                        bind.vertex_buffers[vb_slot] = state.scene.Buffers[prim.VertexBuffers.BufferIndices[vb_slot]];
                    }
                    if (prim.IndexBuffer != SCENE_INVALID_INDEX)
                    {
                        bind.index_buffer = state.scene.Buffers[prim.IndexBuffer];
                    }
                    
                    // Update light to follow camera
                    state.point_light.light_pos = state.camera.EyePos;
                    
                    // Apply uniforms
                    sg_apply_uniforms(UB_cgltf_vs_params, new sg_range { ptr = Unsafe.AsPointer(ref vs_params), size = (uint)Marshal.SizeOf<cgltf_vs_params_t>() });
                    sg_apply_uniforms(UB_cgltf_light_params, new sg_range { ptr = Unsafe.AsPointer(ref state.point_light), size = (uint)Marshal.SizeOf<cgltf_light_params_t>() });
                    
                    if (mat.IsMetallic)
                    {
                        // Read textures from scene (match working cgltf sample pattern)
                        // But first check if indices are valid to avoid array access errors
                        sg_view base_color_tex = (mat.Metallic.Images.BaseColor != SCENE_INVALID_INDEX) 
                            ? state.scene.Images[mat.Metallic.Images.BaseColor].TexView 
                            : new sg_view();
                        sg_view metallic_roughness_tex = (mat.Metallic.Images.MetallicRoughness != SCENE_INVALID_INDEX)
                            ? state.scene.Images[mat.Metallic.Images.MetallicRoughness].TexView
                            : new sg_view();
                        sg_view normal_tex = (mat.Metallic.Images.Normal != SCENE_INVALID_INDEX)
                            ? state.scene.Images[mat.Metallic.Images.Normal].TexView
                            : new sg_view();
                        sg_view occlusion_tex = (mat.Metallic.Images.Occlusion != SCENE_INVALID_INDEX)
                            ? state.scene.Images[mat.Metallic.Images.Occlusion].TexView
                            : new sg_view();
                        sg_view emissive_tex = (mat.Metallic.Images.Emissive != SCENE_INVALID_INDEX)
                            ? state.scene.Images[mat.Metallic.Images.Emissive].TexView
                            : new sg_view();
                        
                        sg_sampler base_color_smp = (mat.Metallic.Images.BaseColor != SCENE_INVALID_INDEX)
                            ? state.scene.Images[mat.Metallic.Images.BaseColor].Sampler
                            : new sg_sampler();
                        sg_sampler metallic_roughness_smp = (mat.Metallic.Images.MetallicRoughness != SCENE_INVALID_INDEX)
                            ? state.scene.Images[mat.Metallic.Images.MetallicRoughness].Sampler
                            : new sg_sampler();
                        sg_sampler normal_smp = (mat.Metallic.Images.Normal != SCENE_INVALID_INDEX)
                            ? state.scene.Images[mat.Metallic.Images.Normal].Sampler
                            : new sg_sampler();
                        sg_sampler occlusion_smp = (mat.Metallic.Images.Occlusion != SCENE_INVALID_INDEX)
                            ? state.scene.Images[mat.Metallic.Images.Occlusion].Sampler
                            : new sg_sampler();
                        sg_sampler emissive_smp = (mat.Metallic.Images.Emissive != SCENE_INVALID_INDEX)
                            ? state.scene.Images[mat.Metallic.Images.Emissive].Sampler
                            : new sg_sampler();
                        
                        // Check if textures are valid and use placeholders if not
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

                        // Bind all textures
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
                        sg_apply_uniforms(UB_cgltf_metallic_params, new sg_range { ptr = Unsafe.AsPointer(ref mat.Metallic.FsParams), size = (uint)Marshal.SizeOf<cgltf_metallic_params_t>() });
                    }
                    
                    sg_apply_bindings(bind);
                    sg_draw((uint)prim.BaseElement, (uint)prim.NumElements, 1);
                }
            }
            sdtx_draw();
            // __dbgui_draw();
            sg_end_pass();
        }
        sg_commit();

        var deltaTime = stm_ms(stm_now() - startTime);
        frames++;
        if (deltaTime >= 1000)
        {
            frameRate = frames;
            averageFrameTimeMilliseconds = deltaTime / frameRate;
            frameRate = (int)(1000 / averageFrameTimeMilliseconds);

            frames = 0;
            startTime = 0;
        }
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        FileSystem.Instance.Shutdown();
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