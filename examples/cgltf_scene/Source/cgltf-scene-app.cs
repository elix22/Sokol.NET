
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
using static cgltf_sapp_shader_skinning_cs_skinning.Shaders;

using static Sokol.SLog;
using static Sokol.SDebugUI;

using cgltf_size = uint;
using System.Diagnostics;

public static unsafe class CGLTFSceneApp
{
    static CGltfParser? _parser;

    static bool PauseUpdate = false;

    //  const string filename = "glb/DamagedHelmet.glb";
    const string filename = "glb/assimpScene.glb";
    // const string filename = "gltf/DamagedHelmet/DamagedHelmet.gltf";

    // const string filename = "glb/DancingGangster.glb";
    // const string filename = "glb/Gangster.glb";
     
    //



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
        public cgltf_light_params_t light_params;  // Changed from point_light to light_params
        public List<Light> lights = new List<Light>();  // Active lights
        public Matrix4x4 root_transform;
        public float rx;
        public float ry;
        public Placeholders placeholders = new Placeholders();
        public bool cameraInitialized = false;  // Track if camera has been auto-positioned
        
        // Animation support
        public CGltfAnimator? animator;
        public bool animationsLoaded = false;
    }


    public static _state state = new _state();

    static uint frames = 0;
    static double frameRate = 30;
    static double averageFrameTimeMilliseconds = 33.333;
    static ulong startTime = 0;
    static bool debugPrinted = true;  // Temporarily false to see debug output  // Debug flag

    // Helper function to update light uniforms from Light objects
    static void UpdateLightUniforms()
    {
        state.light_params = default;
        state.light_params.num_lights = Math.Min(state.lights.Count, 4);

        for (int i = 0; i < state.light_params.num_lights; i++)
        {
            Light light = state.lights[i];
            if (!light.Enabled) continue;

            // All lights: Color + intensity in w component  
            state.light_params.light_colors[i] = new Vector4(
                light.Color.X, light.Color.Y, light.Color.Z, light.Intensity);

            switch (light.Type)
            {
                case LightType.Directional:
                    // Direction only (type will be in directions.w)
                    state.light_params.light_directions[i] = new Vector4(
                        light.Direction.X, light.Direction.Y, light.Direction.Z, (float)light.Type);
                    break;

                case LightType.Point:
                    // Position + type
                    state.light_params.light_positions[i] = new Vector4(
                        light.Position.X, light.Position.Y, light.Position.Z, (float)light.Type);
                    // Range in params
                    state.light_params.light_params_data[i] = new Vector4(light.Range, 0, 0, 0);
                    break;

                case LightType.Spot:
                    // Position + type
                    state.light_params.light_positions[i] = new Vector4(
                        light.Position.X, light.Position.Y, light.Position.Z, (float)light.Type);
                    // Direction + inner cutoff
                    float innerCutoff = MathF.Cos(light.SpotInnerAngle * MathF.PI / 180.0f);
                    state.light_params.light_directions[i] = new Vector4(
                        light.Direction.X, light.Direction.Y, light.Direction.Z, innerCutoff);
                    // Range + outer cutoff
                    float outerCutoff = MathF.Cos(light.SpotOuterAngle * MathF.PI / 180.0f);
                    state.light_params.light_params_data[i] = new Vector4(
                        light.Range, outerCutoff, 0, 0);
                    break;
            }
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            // Increase buffer pool for complex models with many meshes
            buffer_pool_size = 4096,  // Default is 128, DancingGangster needs ~2900
            image_pool_size = 256,    // Default is 128
            sampler_pool_size = 128,  // Default is 64
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
            FarZ = 1000.0f,  // Model is small, don't need huge far plane
            Center = new Vector3(0.0f, 0.0f, 0.0f),  // Model center at Y=1.0 (bounds: -0.002 to 1.993)
            Distance = 3.0f,  // Closer - model size is ~2 units tall
            Latitude = 10.0f,  // Look down slightly 
            Longitude = 0.0f,  // Front view
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

        // create shaders - regular and skinning variants
        sg_shader metallicShader = sg_make_shader(cgltf_sapp_shader_cs_cgltf.Shaders.cgltf_metallic_shader_desc(sg_query_backend()));
        sg_shader metallicShaderSkinning = sg_make_shader(cgltf_sapp_shader_skinning_cs_skinning.Shaders.skinning_metallic_shader_desc(sg_query_backend()));
        state.shaders.metallic = metallicShader;

        // Initialize CGltfParser with both regular and skinning shader variants
        _parser = new CGltfParser();
        _parser.Init(metallicShader, metallicShader, metallicShaderSkinning, metallicShaderSkinning);

        // Setup lights - mix of directional and point lights for best PBR results
        // Main directional light (like sun) - provides consistent lighting from one direction
        state.lights.Add(Light.CreateDirectionalLight(
            new Vector3(-0.3f, -0.8f, -0.5f),  // Direction (from upper-right)
            new Vector3(1.0f, 0.98f, 0.95f),   // Warm white light
            2.5f                                // Intensity
        ));

        // Key point light - creates primary specular highlights
        state.lights.Add(Light.CreatePointLight(
            new Vector3(5.0f, 8.0f, 5.0f),
            new Vector3(1.0f, 1.0f, 1.0f),     // White light
            500.0f,                             // Range
            25.0f                               // Intensity
        ));

        // Fill light - softer, from opposite side
        state.lights.Add(Light.CreatePointLight(
            new Vector3(-5.0f, 3.0f, -3.0f),
            new Vector3(0.7f, 0.8f, 1.0f),     // Cool blue-white light
            500.0f,
            10.0f
        ));

        // Load GLTF file using CGltfParser (async)
        string gltfFilePath = util_get_file_path(filename);

        // Set model scale (adjust this value as needed for different models)
        float modelScale = 1f; // Scale down for this model - model is ~200 units tall originally
        _parser.SetModelScale(modelScale);

        _parser.LoadFromFileAsync(gltfFilePath, state.scene,
    onComplete: () =>
    {
        // Print scene bounds for debugging
        Vector3 sceneMin = state.scene.SceneBoundsMin;
        Vector3 sceneMax = state.scene.SceneBoundsMax;
        Vector3 sceneSize = sceneMax - sceneMin;
        Vector3 sceneCenter = (sceneMax + sceneMin) * 0.5f;
        
        Console.WriteLine($"=== SCENE INFO (after scale {modelScale}) ===");
        Console.WriteLine($"Scene Bounds Min: ({sceneMin.X:F3}, {sceneMin.Y:F3}, {sceneMin.Z:F3})");
        Console.WriteLine($"Scene Bounds Max: ({sceneMax.X:F3}, {sceneMax.Y:F3}, {sceneMax.Z:F3})");
        Console.WriteLine($"Scene Size: ({sceneSize.X:F3}, {sceneSize.Y:F3}, {sceneSize.Z:F3})");
        Console.WriteLine($"Scene Center: ({sceneCenter.X:F3}, {sceneCenter.Y:F3}, {sceneCenter.Z:F3})");
        Console.WriteLine($"Number of nodes: {state.scene.NumNodes}");
        
        // Print first few node positions
        for (int i = 0; i < Math.Min(5, state.scene.NumNodes); i++)
        {
            Matrix4x4 transform = state.scene.Nodes[i].Transform;
            Vector3 pos = new Vector3(transform.M41, transform.M42, transform.M43);
            Console.WriteLine($"  Node {i} position: ({pos.X:F3}, {pos.Y:F3}, {pos.Z:F3})");
        }
        
        Console.WriteLine($"=======================================");
        
        // Initialize animation if available
        if (_parser.Animations.Count > 0)
        {
            Console.WriteLine($"Found {_parser.Animations.Count} animation(s)");
            var firstAnimation = _parser.Animations[0];
            if (firstAnimation.IsLoaded)
            {
                state.animator = new CGltfAnimator(firstAnimation);
                state.animationsLoaded = true;
                Console.WriteLine($"Animation '{firstAnimation.Name}' initialized successfully");
            }
        }
        else
        {
            Console.WriteLine("No animations found in GLTF file");
        }
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
        Matrix4x4 modelMatrix = state.root_transform * state.scene.Nodes[node_index].Transform;
        
        return new cgltf_vs_params_t
        {
            model = modelMatrix,
            view_proj = state.camera.ViewProj,
            eye_pos = state.camera.EyePos,
        };
    }

    /// <summary>
    /// Calculates a bounding sphere that contains the entire axis-aligned bounding box.
    /// </summary>
    /// <param name="min">Minimum corner of the bounding box</param>
    /// <param name="max">Maximum corner of the bounding box</param>
    /// <returns>Tuple of (center, radius) for the bounding sphere</returns>
    static (Vector3 center, float radius) CalculateBoundingSphere(Vector3 min, Vector3 max)
    {
        // Center of the bounding box
        Vector3 center = (min + max) * 0.5f;
        
        // Radius is the distance from center to any corner (they're all equidistant)
        // Using the maximum corner for convenience
        float radius = Vector3.Distance(center, max);
        
        return (center, radius);
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // if (PauseUpdate) return;
        // pump the sokol-fetch message queue
        FileSystem.Instance.Update();

        startTime = (startTime == 0) ? stm_now() : startTime;

        var begin_frame = stm_now();

        // Get framebuffer dimensions early for auto-positioning
        int fb_width = sapp_width();
        int fb_height = sapp_height();

        // Auto-position camera using REAL scene bounds from GLTF accessor data
        if (!state.cameraInitialized && state.scene.NumNodes > 0)
        {
            // Use the actual scene bounds calculated from GLTF accessor min/max
            Vector3 sceneMin = state.scene.SceneBoundsMin;
            Vector3 sceneMax = state.scene.SceneBoundsMax;
            Vector3 sceneSize = sceneMax - sceneMin;

            var (sphereCenter, sphereRadius) = CalculateBoundingSphere(sceneMin, sceneMax);
            Vector3 sceneCenter = sphereCenter + Vector3.UnitY * (sphereRadius / 2.0f);
            
            // Get all 8 corners of the bounding box
            Vector3[] corners = new Vector3[8]
            {
                new Vector3(sceneMin.X, sceneMin.Y, sceneMin.Z),
                new Vector3(sceneMin.X, sceneMin.Y, sceneMax.Z),
                new Vector3(sceneMin.X, sceneMax.Y, sceneMin.Z),
                new Vector3(sceneMin.X, sceneMax.Y, sceneMax.Z),
                new Vector3(sceneMax.X, sceneMin.Y, sceneMin.Z),
                new Vector3(sceneMax.X, sceneMin.Y, sceneMax.Z),
                new Vector3(sceneMax.X, sceneMax.Y, sceneMin.Z),
                new Vector3(sceneMax.X, sceneMax.Y, sceneMax.Z)
            };
            
            // Binary search for the minimum distance where all corners fit in view
            float fovRadians = state.camera.Aspect * (float)Math.PI / 180.0f;
            float aspectRatio = (float)fb_width / (float)fb_height;
            
            // Start with a reasonable distance estimate
            float minDistance = 0.01f;
            float maxDistance = Math.Max(sceneSize.X, Math.Max(sceneSize.Y, sceneSize.Z)) * 10000.0f;
            float bestDistance = maxDistance;
            
            // Binary search for optimal distance
            for (int iteration = 0; iteration < 40; iteration++)
            {
                float testDistance = (minDistance + maxDistance) * 0.5f;
                
                // Position camera at test distance looking at center
                Vector3 cameraPos = sceneCenter + new Vector3(0, 0, testDistance);
                
                // Create view and projection matrices for this camera position
                Matrix4x4 view = Matrix4x4.CreateLookAt(cameraPos, sceneCenter, Vector3.UnitY);
                Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspectRatio, 0.01f, 10000.0f);
                Matrix4x4 viewProj = view * proj;

                // Project all corners and check if they fit in NDC space [-1, 1]
                bool allFit = true;
                float testMarging = 0.95f; // Margin to ensure some padding
                foreach (var corner in corners)
                {
                    Vector4 clipSpace = Vector4.Transform(corner, viewProj);
                    if (clipSpace.W > 0)  // In front of camera
                    {
                        Vector3 ndc = new Vector3(clipSpace.X / clipSpace.W, clipSpace.Y / clipSpace.W, clipSpace.Z / clipSpace.W);
                        // Check if within NDC bounds with some margin
                        if (Math.Abs(ndc.X) > testMarging || Math.Abs(ndc.Y) > testMarging/1.2f)
                        {
                            allFit = false;
                            break;
                        }
                    }
                    else
                    {
                        allFit = false;
                        break;
                    }
                }
                
                if (allFit)
                {
                    // All corners fit, try closer
                    bestDistance = testDistance;
                    maxDistance = testDistance;
                }
                else
                {
                    // Doesn't fit, need more distance
                    minDistance = testDistance;
                }
            }
            
            Console.WriteLine($"=== AUTO-POSITIONING CAMERA ===");
            Console.WriteLine($"Scene bounds: Min={sceneMin}, Max={sceneMax}");
            Console.WriteLine($"Scene size: {sceneSize}");
            Console.WriteLine($"Scene center: {sceneCenter}");
            Console.WriteLine($"Final distance: {bestDistance:F3}");
            
            state.camera.Center = sceneCenter;
            state.camera.Distance = bestDistance;
            
            // Set camera angles for a straight-on view
            // Binary search ensures entire bounding box fits
            state.camera.Latitude = 0.0f;   // Look straight ahead (0 = level)
            state.camera.Longitude = 0.0f;  // Front view
            
            // Force camera update immediately to apply new position
            state.camera.Update(fb_width, fb_height);
            
            Console.WriteLine($"Camera EyePos after update: {state.camera.EyePos}");
            Console.WriteLine($"===============================");
            
            state.cameraInitialized = true;
        }


        // print help text
        sdtx_canvas(sapp_width() * 0.5f, sapp_height() * 0.5f);
        sdtx_color1i(0xFFFFFFFF);
        sdtx_origin(1.0f, 2.0f);
        sdtx_puts("LMB + drag:  rotate\n");
        sdtx_puts("mouse wheel: zoom\n");
        sdtx_print("FPS: {0} \n", frameRate);
        sdtx_print("Avg. Frame Time: {0:F4} ms\n", averageFrameTimeMilliseconds);

        state.root_transform = Matrix4x4.CreateRotationY(state.rx);

        // Update camera (already updated in auto-positioning if it was just initialized)
        if (state.cameraInitialized)
        {
            state.camera.Update(fb_width, fb_height);
        }

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

            // Debug: Print draw info for first frame
            if (!debugPrinted && state.scene.NumNodes > 0)
            {
                Console.WriteLine($"=== DRAW DEBUG (First Frame) ===");
                Console.WriteLine($"NumNodes: {state.scene.NumNodes}");
            }

            for (int node_index = 0; node_index < state.scene.NumNodes; node_index++)
            {
                ref CGltfNode node = ref state.scene.Nodes[node_index];
                cgltf_vs_params_t vs_params = vs_params_for_node(node_index);
                ref CGltfMesh mesh = ref state.scene.Meshes[node.MeshIndex];

                for (int i = 0; i < mesh.NumPrimitives; i++)
                {
                    ref CGltfPrimitive prim = ref state.scene.Primitives[i + mesh.FirstPrimitive];
                    ref CGltfMaterial mat = ref state.scene.Materials[prim.MaterialIndex];

                    // Debug for first frame
                    if (!debugPrinted)
                    {
                        Console.WriteLine($"  Node {node_index}, Mesh {node.MeshIndex}, Prim {i}: BaseElement={prim.BaseElement}, NumElements={prim.NumElements}, Pipeline={prim.PipelineIndex}, VBufs={prim.VertexBuffers.Num}, IndexBuf={prim.IndexBuffer}");
                        Console.WriteLine($"    Buffer indices: [{string.Join(", ", prim.VertexBuffers.BufferIndices.Take(prim.VertexBuffers.Num))}]");
                    }

                    sg_apply_pipeline(state.scene.Pipelines[prim.PipelineIndex]);
                    sg_bindings bind = default;
                    
                    if (!debugPrinted)
                        Console.WriteLine($"    Starting vertex buffer binding loop, Num={prim.VertexBuffers.Num}");
                    
                    // Bind vertex buffers
                    for (int vb_slot = 0; vb_slot < prim.VertexBuffers.Num; vb_slot++)
                    {
                        int bufferIndex = prim.VertexBuffers.BufferIndices[vb_slot];
                        sg_buffer buf = state.scene.Buffers[bufferIndex];
                        bind.vertex_buffers[vb_slot] = buf;
                        if (!debugPrinted)
                            Console.WriteLine($"    VB slot {vb_slot}: Using buffer index {bufferIndex} (id={buf.id})");
                    }
                    
                    if (prim.IndexBuffer != SCENE_INVALID_INDEX)
                    {
                        bind.index_buffer = state.scene.Buffers[prim.IndexBuffer];
                    }

                    // Lights are static (no animation) - directional light + fixed point lights
                    // This provides consistent, professional studio-style lighting

                    // Update light uniforms
                    UpdateLightUniforms();

                    // Apply uniforms based on whether this primitive has skinning
                    if (prim.HasSkinning)
                    {
                        // For skinned meshes, use the skinning shader's uniform structure
                        skinning_vs_params_t skinning_vs_params = new skinning_vs_params_t
                        {
                            model = vs_params.model,
                            view_proj = vs_params.view_proj,
                            eye_pos = vs_params.eye_pos
                        };
                        // TODO: Fill finalBonesMatrices array with animation data
                        
                        sg_apply_uniforms(UB_skinning_vs_params, new sg_range { ptr = Unsafe.AsPointer(ref skinning_vs_params), size = (uint)Marshal.SizeOf<skinning_vs_params_t>() });
                        sg_apply_uniforms(UB_skinning_light_params, new sg_range { ptr = Unsafe.AsPointer(ref state.light_params), size = (uint)Marshal.SizeOf<cgltf_light_params_t>() });
                    }
                    else
                    {
                        // For non-skinned meshes, use the regular shader
                        sg_apply_uniforms(UB_cgltf_vs_params, new sg_range { ptr = Unsafe.AsPointer(ref vs_params), size = (uint)Marshal.SizeOf<cgltf_vs_params_t>() });
                        sg_apply_uniforms(UB_cgltf_light_params, new sg_range { ptr = Unsafe.AsPointer(ref state.light_params), size = (uint)Marshal.SizeOf<cgltf_light_params_t>() });
                    }

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

                        // Bind all textures using the correct shader constants
                        if (prim.HasSkinning)
                        {
                            bind.views[VIEW_skinning_base_color_tex] = base_color_tex;
                            bind.views[VIEW_skinning_metallic_roughness_tex] = metallic_roughness_tex;
                            bind.views[VIEW_skinning_normal_tex] = normal_tex;
                            bind.views[VIEW_skinning_occlusion_tex] = occlusion_tex;
                            bind.views[VIEW_skinning_emissive_tex] = emissive_tex;

                            bind.samplers[SMP_skinning_base_color_smp] = base_color_smp;
                            bind.samplers[SMP_skinning_metallic_roughness_smp] = metallic_roughness_smp;
                            bind.samplers[SMP_skinning_normal_smp] = normal_smp;
                            bind.samplers[SMP_skinning_occlusion_smp] = occlusion_smp;
                            bind.samplers[SMP_skinning_emissive_smp] = emissive_smp;
                            sg_apply_uniforms(UB_skinning_metallic_params, new sg_range { ptr = Unsafe.AsPointer(ref mat.Metallic.FsParams), size = (uint)Marshal.SizeOf<cgltf_metallic_params_t>() });
                        }
                        else
                        {
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