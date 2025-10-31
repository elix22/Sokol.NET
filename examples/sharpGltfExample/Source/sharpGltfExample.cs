using System; 
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using static Sokol.SImgui;
using Imgui;
using static Imgui.ImguiNative;
using SharpGLTF.Schema2;
using static cgltf_sapp_shader_cs_cgltf.Shaders;
using static cgltf_sapp_shader_skinning_cs_skinning.Shaders;

public static unsafe class SharpGLTFApp
{
    // const string filename = "DamagedHelmet.glb";
    //   const string filename = "assimpScene.glb";
    // const string filename = "gltf/DamagedHelmet/DamagedHelmet.gltf";

    // const string filename = "DancingGangster.glb";
    // const string filename = "Gangster.glb";

    //race_track
    // const string filename = "race_track.glb";
    // const string filename = "mainsponza/NewSponza_Main_glTF_003.gltf";

    // const string filename = "glb/2CylinderEngine.glb";

    // const string filename = "ABeautifulGame/glTF/ABeautifulGame.gltf";
    // const string filename = "glb/AlphaBlendModeTest.glb";
    // const string filename = "glb/AntiqueCamera.glb";

    // const string filename = "AttenuationTest/glTF-Binary/AttenuationTest.glb";

    //BoomBox.glb
    // const string filename = "glb/BoomBox.glb";

    // const string filename = "glb/ClearCoatCarPaint.glb";

     const string filename = "ClearcoatRing/glTF/ClearcoatRing.gltf";

    class _state
    {
        public sg_pass_action pass_action;
        public Sokol.Camera camera = new Sokol.Camera();
        public SharpGltfModel? model;
        public SharpGltfAnimator? animator;
        public bool modelLoaded = false;
        public bool cameraInitialized = false;  // Track if camera has been auto-positioned
        public bool isMixamoModel = false;      // Track if this is a Mixamo model needing special transforms
        public Vector3 modelBoundsMin;
        public Vector3 modelBoundsMax;
        
        // Model rotation (middle mouse button)
        public float modelRotationX = 0.0f;     // Rotation around X-axis (vertical mouse movement)
        public float modelRotationY = 0.0f;     // Rotation around Y-axis (horizontal mouse movement)
        public bool middleMouseDown = false;    // Track middle mouse button state
        
        // Culling statistics
        public int totalMeshes = 0;
        public int visibleMeshes = 0;
        public int culledMeshes = 0;
        public bool enableFrustumCulling = true;
        
        // Lighting system
        public List<Light> lights = new List<Light>();
        public float ambientStrength = 0.03f;   // Ambient light strength (0.0 to 1.0)
    }

    static _state state = new _state();
    static bool _loggedMeshInfoOnce = false;  // Debug flag for mesh info
    static int _frameCount = 0;  // Frame counter for debugging

    /// <summary>
    /// Calculates a bounding sphere that contains the entire axis-aligned bounding box.
    /// </summary>
    static (Vector3 center, float radius) CalculateBoundingSphere(Vector3 min, Vector3 max)
    {
        Vector3 center = (min + max) * 0.5f;
        float radius = Vector3.Distance(center, max);
        return (center, radius);
    }


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            buffer_pool_size = 4096*2,//increased to handle very large scene graphs
            sampler_pool_size = 512, // Reduced from 2048 - texture cache prevents duplicate samplers
            uniform_buffer_size = 64 * 1024 * 1024, // 64 MB - increased to handle very large scene graphs (2500+ nodes)
            logger = {
                func = &slog_func,
            }
        });

        // Setup sokol-imgui
        simgui_setup(new simgui_desc_t
        {
            logger = { func = &slog_func }
        });

        // Setup camera
        state.camera.Init(new CameraDesc()
        {
            Aspect = 60.0f,
            NearZ = 0.01f,
            FarZ = 2000.0f,
            Center = new Vector3(0.0f, 1.0f, 0.0f),
            Distance = 3.0f,
            Latitude = 10.0f,
            Longitude = 0.0f,
        });

        // Initialize lighting system - Multi-light setup for both indoor and outdoor scenes
        // Light 1: Main directional light (sun) - provides broad coverage for outdoor/large indoor spaces
        state.lights.Add(Light.CreateDirectionalLight(
            new Vector3(-0.3f, -0.7f, -0.5f),   // Direction (from upper right, angled down)
            new Vector3(1.0f, 0.95f, 0.85f),    // Warm white (sun color)
            1.5f                                 // Intensity
        ));
        
        // Light 2: Fill directional light - softens shadows and provides ambient-like fill
        state.lights.Add(Light.CreateDirectionalLight(
            new Vector3(0.5f, -0.3f, 0.3f),     // Direction (from opposite side, shallower angle)
            new Vector3(0.6f, 0.7f, 0.9f),      // Cool blue-tinted (sky light)
            0.4f                                 // Lower intensity for fill
        ));
        
        // Light 3: Point light - for localized indoor lighting or accent
        state.lights.Add(Light.CreatePointLight(
            new Vector3(0.0f, 15.0f, 0.0f),     // Position - overhead
            new Vector3(1.0f, 0.9f, 0.8f),      // Warm white
            300.0f,                              // High intensity for large areas
            100.0f                               // Large range
        ));
        
        // Light 4: Back/rim light - adds depth and separation
        state.lights.Add(Light.CreateDirectionalLight(
            new Vector3(0.2f, 0.1f, 0.8f),      // Direction (from behind)
            new Vector3(0.8f, 0.85f, 1.0f),     // Slightly blue
            0.3f                                 // Subtle intensity
        ));

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };

        PipeLineManager.GetOrCreatePipeline(PipelineType.Standard);
        PipeLineManager.GetOrCreatePipeline(PipelineType.Skinned);

        // Initialize FileSystem
        FileSystem.Instance.Initialize();
        
        // Load model asynchronously
        FileSystem.Instance.LoadFile(filename, (path, buffer, status) => 
        {
            if (status == FileLoadStatus.Success && buffer != null)
            {
                try
                {
                    var memoryStream = new MemoryStream(buffer);
                    
                    // Get the directory of the main GLTF file for resolving relative paths
                    string? baseDirectory = Path.GetDirectoryName(path);
                    
                    // Create a FileReaderCallback that uses LoadFileSync for dependent files
                    SharpGLTF.Schema2.FileReaderCallback fileReader = (assetName) =>
                    {
                        // Construct full path by combining base directory with asset name
                        string fullAssetPath = string.IsNullOrEmpty(baseDirectory) 
                            ? assetName 
                            : Path.Combine(baseDirectory, assetName);
                        
                        Info($"[SharpGLTF] Loading dependent asset: {assetName} -> {fullAssetPath}");
                        var (data, loadStatus) = FileSystem.Instance.LoadFileSync(fullAssetPath);
                        
                        if (loadStatus == FileLoadStatus.Success && data != null)
                        {
                            Info($"[SharpGLTF] Successfully loaded {fullAssetPath} ({data.Length} bytes)");
                            return new ArraySegment<byte>(data);
                        }
                        else
                        {
                            Error($"[SharpGLTF] Failed to load {fullAssetPath}: {loadStatus}");
                            throw new FileNotFoundException($"Failed to load asset: {fullAssetPath}");
                        }
                    };
                    
                    var context = SharpGLTF.Schema2.ReadContext.Create(fileReader);
                        
                    ModelRoot modelRoot = context.ReadSchema2(memoryStream);
                    
                    // Calculate model bounds from GLTF accessors BEFORE creating the model
                    state.modelBoundsMin = new Vector3(float.MaxValue);
                    state.modelBoundsMax = new Vector3(float.MinValue);
                    
                    foreach (var mesh in modelRoot.LogicalMeshes)
                    {
                        foreach (var primitive in mesh.Primitives)
                        {
                            var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
                            if (positions != null)
                            {
                                foreach (var pos in positions)
                                {
                                    state.modelBoundsMin = Vector3.Min(state.modelBoundsMin, pos);
                                    state.modelBoundsMax = Vector3.Max(state.modelBoundsMax, pos);
                                }
                            }
                        }
                    }
                    
                    Vector3 size = state.modelBoundsMax - state.modelBoundsMin;
                    Vector3 center = (state.modelBoundsMin + state.modelBoundsMax) * 0.5f;
                    
                    Info($"[SharpGLTF] Model bounds: Min={state.modelBoundsMin}, Max={state.modelBoundsMax}");
                    Info($"[SharpGLTF] Model size: {size}, Center: {center}");
                    
                    // Safety check: if bounds are invalid or too small, use defaults
                    if (float.IsInfinity(size.X) || float.IsNaN(size.X) || size.Length() < 0.01f)
                    {
                        Info("[SharpGLTF] Warning: Invalid bounds detected, using defaults");
                        state.modelBoundsMin = new Vector3(-1, 0, -1);
                        state.modelBoundsMax = new Vector3(1, 2, 1);
                    }

                    // Create our model wrapper
                    state.model = new SharpGltfModel(modelRoot);
                    
                    // TBD ELI , this is an hack to detect Mixamo models
                    // Detect if this is a Mixamo model by checking node names
                    state.isMixamoModel = modelRoot.LogicalNodes.Any(n => 
                        n.Name != null && (n.Name.Contains("mixamorig", StringComparison.OrdinalIgnoreCase) ||
                        n.Name.Contains("Armature", StringComparison.OrdinalIgnoreCase)));
                    
                    if (state.isMixamoModel)
                    {
                        Info("[SharpGLTF] Detected Mixamo model - will apply scale/rotation correction");
                    }
                    
                    Info($"[SharpGLTF] Model has {state.model.Meshes.Count} meshes, {state.model.Nodes.Count} nodes");
                    Info($"[SharpGLTF] Model has {state.model.BoneCounter} bones");
                    
                    // Create animator if model has animations
                    if (state.model.HasAnimations)
                    {
                        state.animator = new SharpGltfAnimator(state.model.Animation);
                        Info("[SharpGLTF] Animator created for animated model");
                    }
                    else
                    {
                        Info("[SharpGLTF] No animations found in model");
                    }
                    
                    state.modelLoaded = true;
                    Info($"[SharpGLTF] Model loaded successfully: {path}");
                }
                catch (Exception ex)
                {
                    Error($"[SharpGLTF] Error processing model: {ex.Message}");
                    Info($"[SharpGLTF] Stack trace: {ex.StackTrace}");
                }
            }
            else
            {
                Error($"[SharpGLTF] Failed to load file '{path}': {status}");
            }
        }); // 3 GB max size

    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // Update FileSystem to process pending file loads
        FileSystem.Instance.Update();

        int fb_width = sapp_width();
        int fb_height = sapp_height();

        // Start new imgui frame
        simgui_new_frame(new simgui_frame_desc_t
        {
            width = fb_width,
            height = fb_height,
            delta_time = sapp_frame_duration(),
            dpi_scale = 1// TBD ELI , looks very samll on Android sapp_dpi_scale()
        });

        // Auto-position camera using scene bounds after model is loaded
        if (!state.cameraInitialized && state.modelLoaded && state.model != null)
        {
            Vector3 sceneMin = state.modelBoundsMin;
            Vector3 sceneMax = state.modelBoundsMax;
            
            // After rotation, min/max might be swapped, so recalculate
            Vector3 actualMin = Vector3.Min(sceneMin, sceneMax);
            Vector3 actualMax = Vector3.Max(sceneMin, sceneMax);
            sceneMin = actualMin;
            sceneMax = actualMax;
            
            Vector3 sceneSize = sceneMax - sceneMin;

            var (sphereCenter, sphereRadius) = CalculateBoundingSphere(sceneMin, sceneMax);
            Vector3 sceneCenter = sphereCenter;
            
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
            
            float minDistance = 0.02f;
            float maxDistance = Math.Max(sceneSize.X, Math.Max(sceneSize.Y, sceneSize.Z)) * 20000.0f;
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
                float margin = 0.95f;
                foreach (var corner in corners)
                {
                    Vector4 clipSpace = Vector4.Transform(corner, viewProj);
                    if (clipSpace.W > 0)
                    {
                        Vector3 ndc = new Vector3(clipSpace.X / clipSpace.W, clipSpace.Y / clipSpace.W, clipSpace.Z / clipSpace.W);
                        if (Math.Abs(ndc.X) > margin || Math.Abs(ndc.Y) > margin / 1.2f)
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
                    bestDistance = testDistance;
                    maxDistance = testDistance;
                }
                else
                {
                    minDistance = testDistance;
                }
            }
            
            Info($"=== AUTO-POSITIONING CAMERA ===");
            Info($"Scene bounds: Min={sceneMin}, Max={sceneMax}");
            Info($"Scene size: {sceneSize}");
            Info($"Scene center: {sceneCenter}");
            Info($"Final distance: {bestDistance:F3}");
            
            state.camera.Center = sceneCenter;
            state.camera.Distance = bestDistance;
            state.camera.Latitude = 0.0f;
            state.camera.Longitude = 0.0f;
            
            state.cameraInitialized = true;
        }

        // Update camera (handles WASD movement internally)
        float deltaTime = (float)sapp_frame_duration();
        state.camera.Update(fb_width, fb_height, state.cameraInitialized ? deltaTime : 0.0f);

        // Update animation if available
        if (state.animator != null)
        {
            state.animator.UpdateAnimation((float)sapp_frame_duration());
        }

        // Begin rendering
        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });

        // Render model if loaded
        if (state.modelLoaded && state.model != null)
        {
            
            // Prepare vertex shader uniforms (common for both pipelines)
            // Apply model rotation on X and Y axes (controlled by middle mouse button)
            // Order: Y rotation (horizontal mouse) then X rotation (vertical mouse)
            Matrix4x4 modelRotation = Matrix4x4.CreateRotationY(state.modelRotationY) * 
                                     Matrix4x4.CreateRotationX(state.modelRotationX);
            
            // Calculate the model center for rotation
            Vector3 modelCenter = (state.modelBoundsMin + state.modelBoundsMax) * 0.5f;
            
            // Create transform: translate to origin -> rotate -> translate back
            Matrix4x4 model = Matrix4x4.CreateTranslation(-modelCenter) * 
                             modelRotation * 
                             Matrix4x4.CreateTranslation(modelCenter);

            // Prepare fragment shader uniforms (lighting)
            // Build light parameters from the lights list
            cgltf_light_params_t lightParams = new cgltf_light_params_t();
            
            // Count enabled lights (max 4 supported by shader)
            int enabledLightCount = 0;
            foreach (var light in state.lights)
            {
                if (!light.Enabled || enabledLightCount >= 4)
                    continue;
                
                int idx = enabledLightCount;
                
                // Set light type in position.w
                lightParams.light_positions[idx] = new Vector4(light.Position, (float)light.Type);
                
                // Set direction (and spot inner cutoff in w for spot lights)
                float spotInnerCutoff = light.Type == LightType.Spot 
                    ? (float)Math.Cos(light.SpotInnerAngle * Math.PI / 180.0) 
                    : 0;
                lightParams.light_directions[idx] = new Vector4(light.Direction, spotInnerCutoff);
                
                // Set color and intensity
                lightParams.light_colors[idx] = new Vector4(light.Color, light.Intensity);
                
                // Set range and spot outer cutoff
                float spotOuterCutoff = light.Type == LightType.Spot 
                    ? (float)Math.Cos(light.SpotOuterAngle * Math.PI / 180.0) 
                    : 0;
                lightParams.light_params_data[idx] = new Vector4(light.Range, spotOuterCutoff, 0, 0);
                
                enabledLightCount++;
            }
            
            lightParams.num_lights = enabledLightCount;
            lightParams.ambient_strength = state.ambientStrength;


            // Debug output on first render when model exists
            bool shouldLogMeshInfo = !_loggedMeshInfoOnce;
            
            // Reset culling statistics
            state.totalMeshes = 0;
            state.visibleMeshes = 0;
            state.culledMeshes = 0;
            
            // Calculate view-projection matrix for frustum culling
            Matrix4x4 viewProjection = state.camera.ViewProj;
            
            // Separate nodes into opaque and transparent lists
            List<(SharpGltfNode node, float distance)> opaqueNodes = new List<(SharpGltfNode, float)>();
            List<(SharpGltfNode node, float distance)> transparentNodes = new List<(SharpGltfNode, float)>();
            
            // Collect and categorize all visible nodes
            foreach (var node in state.model.Nodes)
            {
                // Skip nodes without meshes (e.g., bone nodes, empty transforms)
                if (node.MeshIndex < 0 || node.MeshIndex >= state.model.Meshes.Count)
                    continue;
                
                var mesh = state.model.Meshes[node.MeshIndex];
                state.totalMeshes++;
                
                // Apply Mixamo-specific transforms if needed
                Matrix4x4 modelMatrix;
                if (state.isMixamoModel)
                {
                    // Mixamo models exported from Blender have 0.01 scale and need rotation correction
                    var scaleMatrix = Matrix4x4.CreateScale(100.0f);
                    var rotationMatrix = Matrix4x4.CreateRotationX(-MathF.PI / 2.0f);
                    modelMatrix = node.Transform * scaleMatrix * rotationMatrix * model;
                }
                else
                {
                    // Use the node's original transform from the GLTF file + global model rotation
                    modelMatrix = node.Transform * model;
                }
                
                // FRUSTUM CULLING: Check if mesh is visible
                if (state.enableFrustumCulling && !mesh.IsVisible(modelMatrix, viewProjection))
                {
                    state.culledMeshes++;
                    continue;  // Skip this mesh
                }
                
                state.visibleMeshes++;
                
                // Calculate distance to camera for sorting
                // Use the center of the mesh's bounding box
                BoundingBox worldBounds = mesh.Bounds.Transform(modelMatrix);
                Vector3 meshCenter = (worldBounds.Min + worldBounds.Max) * 0.5f;
                float distanceToCamera = Vector3.Distance(meshCenter, state.camera.EyePos);
                
                // Categorize as opaque or transparent
                if (mesh.AlphaMode == SharpGLTF.Schema2.AlphaMode.BLEND)
                {
                    transparentNodes.Add((node, distanceToCamera));
                }
                else
                {
                    opaqueNodes.Add((node, distanceToCamera));
                }
            }
            
            // Sort transparent nodes back-to-front (furthest first)
            transparentNodes.Sort((a, b) => b.distance.CompareTo(a.distance));
            
            // Helper function to render a node
            void RenderNode(SharpGltfNode node)
            {
                var mesh = state.model.Meshes[node.MeshIndex];
                
                // Apply Mixamo-specific transforms if needed
                Matrix4x4 modelMatrix;
                if (state.isMixamoModel)
                {
                    var scaleMatrix = Matrix4x4.CreateScale(100.0f);
                    var rotationMatrix = Matrix4x4.CreateRotationX(-MathF.PI / 2.0f);
                    modelMatrix = node.Transform * scaleMatrix * rotationMatrix * model;
                }
                else
                {
                    // Apply global model rotation to node transform
                    modelMatrix = node.Transform * model;
                }
                
                // Use skinning if mesh has it and animator exists
                bool useSkinning = mesh.HasSkinning && state.animator != null;
                
                // Choose pipeline based on alpha mode and skinning
                PipelineType pipelineType = PipeLineManager.GetPipelineTypeForMaterial(mesh.AlphaMode, useSkinning);
                sg_pipeline pipeline = PipeLineManager.GetOrCreatePipeline(pipelineType);

                if (useSkinning)
                {
                    // Use skinned pipeline with bone matrices
                    skinning_vs_params_t vsParams = new skinning_vs_params_t();
                    vsParams.model = modelMatrix;
                    vsParams.view_proj = state.camera.ViewProj;
                    vsParams.eye_pos = state.camera.EyePos;

                    // Copy bone matrices
                    var boneMatrices = state.animator.GetFinalBoneMatrices();
                    
                    
                    var destSpan = MemoryMarshal.CreateSpan(ref vsParams.finalBonesMatrices[0], AnimationConstants.MAX_BONES);
                    boneMatrices.AsSpan().CopyTo(destSpan);

                    sg_apply_pipeline(pipeline);
                    sg_apply_uniforms(UB_skinning_vs_params, SG_RANGE(ref vsParams));

                    // Material uniforms
                    skinning_metallic_params_t metallicParams = new skinning_metallic_params_t();
                    metallicParams.base_color_factor = mesh.BaseColorFactor;
                    metallicParams.metallic_factor = mesh.MetallicFactor;
                    metallicParams.roughness_factor = mesh.RoughnessFactor;
                    metallicParams.emissive_factor = mesh.EmissiveFactor;
                    
                    // Set texture availability flags
                    metallicParams.has_base_color_tex = mesh.Textures.Count > 0 && mesh.Textures[0] != null ? 1.0f : 0.0f;
                    metallicParams.has_metallic_roughness_tex = mesh.Textures.Count > 1 && mesh.Textures[1] != null ? 1.0f : 0.0f;
                    metallicParams.has_normal_tex = mesh.Textures.Count > 2 && mesh.Textures[2] != null ? 1.0f : 0.0f;
                    metallicParams.has_occlusion_tex = mesh.Textures.Count > 3 && mesh.Textures[3] != null ? 1.0f : 0.0f;
                    metallicParams.has_emissive_tex = mesh.Textures.Count > 4 && mesh.Textures[4] != null ? 1.0f : 0.0f;
                    
                    // Set alpha cutoff (0.0 for OPAQUE/BLEND, actual value for MASK)
                    metallicParams.alpha_cutoff = mesh.AlphaMode == SharpGLTF.Schema2.AlphaMode.MASK ? mesh.AlphaCutoff : 0.0f;

                    sg_apply_uniforms(UB_skinning_metallic_params, SG_RANGE(ref metallicParams));

                    // Light uniforms (cast to skinning version)
                    skinning_light_params_t skinningLightParams = new skinning_light_params_t();
                    skinningLightParams.num_lights = lightParams.num_lights;
                    skinningLightParams.ambient_strength = lightParams.ambient_strength;
                    for (int i = 0; i < 4; i++)
                    {
                        skinningLightParams.light_positions[i] = lightParams.light_positions[i];
                        skinningLightParams.light_directions[i] = lightParams.light_directions[i];
                        skinningLightParams.light_colors[i] = lightParams.light_colors[i];
                        skinningLightParams.light_params_data[i] = lightParams.light_params_data[i];
                    }
                    sg_apply_uniforms(UB_skinning_light_params, SG_RANGE(ref skinningLightParams));
                }
                else
                {
                    // Use static pipeline
                    cgltf_vs_params_t vsParams = new cgltf_vs_params_t();
                    vsParams.model = modelMatrix;
                    vsParams.view_proj = state.camera.ViewProj;
                    vsParams.eye_pos = state.camera.EyePos;

                    sg_apply_pipeline(pipeline);
                    sg_apply_uniforms(UB_cgltf_vs_params, SG_RANGE(ref vsParams));

                    // Material uniforms
                    cgltf_metallic_params_t metallicParams = new cgltf_metallic_params_t();
                    metallicParams.base_color_factor = mesh.BaseColorFactor;
                    metallicParams.metallic_factor = mesh.MetallicFactor;
                    metallicParams.roughness_factor = mesh.RoughnessFactor;
                    metallicParams.emissive_factor = mesh.EmissiveFactor;
                    
                    // Set texture availability flags
                    metallicParams.has_base_color_tex = mesh.Textures.Count > 0 && mesh.Textures[0] != null ? 1.0f : 0.0f;
                    metallicParams.has_metallic_roughness_tex = mesh.Textures.Count > 1 && mesh.Textures[1] != null ? 1.0f : 0.0f;
                    metallicParams.has_normal_tex = mesh.Textures.Count > 2 && mesh.Textures[2] != null ? 1.0f : 0.0f;
                    metallicParams.has_occlusion_tex = mesh.Textures.Count > 3 && mesh.Textures[3] != null ? 1.0f : 0.0f;
                    metallicParams.has_emissive_tex = mesh.Textures.Count > 4 && mesh.Textures[4] != null ? 1.0f : 0.0f;
                    
                    // Set alpha cutoff (0.0 for OPAQUE/BLEND, actual value for MASK)
                    metallicParams.alpha_cutoff = mesh.AlphaMode == SharpGLTF.Schema2.AlphaMode.MASK ? mesh.AlphaCutoff : 0.0f;

                    sg_apply_uniforms(UB_cgltf_metallic_params, SG_RANGE(ref metallicParams));

                    // Light uniforms
                    sg_apply_uniforms(UB_cgltf_light_params, SG_RANGE(ref lightParams));
                }

                // Draw the mesh
                mesh.Draw(pipeline);
            }
            
            // PASS 1: Render all opaque objects (no specific order needed)
            foreach (var (node, _) in opaqueNodes)
            {
                RenderNode(node);
            }
            
            // PASS 2: Render all transparent objects (back-to-front order)
            foreach (var (node, _) in transparentNodes)
            {
                RenderNode(node);
            }
            
            // Mark that we've logged mesh info
            if (shouldLogMeshInfo)
                _loggedMeshInfoOnce = true;
        }

        // Draw UI (builds ImGui commands)
        DrawUI();

        // Render ImGui (submits draw commands to sokol-gfx)
        simgui_render();

        sg_end_pass();
        sg_commit();
        
        _frameCount++;  // Increment frame counter
    }

    static void DrawUI()
    {
        // Window 1: Controls (Model Info, Animation)
        igSetNextWindowPos(new Vector2(30, 30), ImGuiCond.Once, Vector2.Zero);
        igSetNextWindowBgAlpha(0.85f);
        byte open1 = 1;
        if (igBegin("Controls", ref open1, ImGuiWindowFlags.AlwaysAutoResize))
        {
            igText("SharpGLTF Animation Viewer");
            igSeparator();
            
            if (state.model != null)
            {
                igText($"Model: {filename}");
                igText($"Meshes: {state.model.Meshes.Count}");
                igText($"Nodes: {state.model.Nodes.Count}");
                igText($"Bones: {state.model.BoneCounter}");
                
                if (state.animator != null && state.model.HasAnimations)
                {
                    igSeparator();
                    igText("=== Animation ===");
                    int animCount = state.model.GetAnimationCount();
                    string currentAnimName = state.model.GetCurrentAnimationName();
                    int currentAnimIndex = state.model.CurrentAnimationIndex;

                    igText($"Current: {currentAnimName}");
                    igText($"Total: {animCount}");

                    if (animCount > 1)
                    {
                        if (igButton("<- Previous", Vector2.Zero))
                        {
                            state.model.PreviousAnimation();
                            state.animator.SetAnimation(state.model.Animation);
                        }

                        igSameLine(0, 10);

                        if (igButton("Next ->", Vector2.Zero))
                        {
                            state.model.NextAnimation();
                            state.animator.SetAnimation(state.model.Animation);
                        }
                    }
                }
                
                igSeparator();
                igText("=== Model Rotation ===");
                igText("Middle Mouse: Rotate Model");
                float rotationYDegrees = state.modelRotationY * 180.0f / MathF.PI;
                float rotationXDegrees = state.modelRotationX * 180.0f / MathF.PI;
                igText($"Y-axis: {rotationYDegrees:F1}° (horizontal)");
                igText($"X-axis: {rotationXDegrees:F1}° (vertical)");
                
                // Reset button
                if (igButton("Reset Rotation", Vector2.Zero))
                {
                    state.modelRotationY = 0.0f;
                    state.modelRotationX = 0.0f;
                }
                
                igSeparator();
                igText("=== Frustum Culling ===");
                byte frustumEnabled = (byte)(state.enableFrustumCulling ? 1 : 0);
                if (igCheckbox("Enable Culling", ref frustumEnabled))
                {
                    state.enableFrustumCulling = frustumEnabled != 0;
                }
                
                igSeparator();
                igText("=== Lighting ===");
                
                // Ambient light slider
                igText("Ambient Light:");
                float ambientStrength = state.ambientStrength;
                if (igSliderFloat("##ambient", ref ambientStrength, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.None))
                {
                    state.ambientStrength = ambientStrength;
                }
                
                igSeparator();
                int activeCount = state.lights.Count(l => l.Enabled);
                igText($"Active Lights: {activeCount}/{state.lights.Count}");
                
                // Individual light controls
                igText("Individual Lights:");
                igIndent(20);
                for (int i = 0; i < state.lights.Count; i++)
                {
                    var light = state.lights[i];
                    
                    // Light enable/disable checkbox with unique ID
                    igPushID_Int(i);
                    byte lightEnabled = light.Enabled ? (byte)1 : (byte)0;
                    if (igCheckbox($"Light {i + 1}", ref lightEnabled))
                    {
                        light.Enabled = lightEnabled != 0;
                    }
                    
                    // Show light details
                    igSameLine(0, 10);
                    if (light.Enabled)
                    {
                        string lightTypeName = light.Type switch
                        {
                            LightType.Directional => "Directional",
                            LightType.Point => "Point",
                            LightType.Spot => "Spot",
                            _ => "Unknown"
                        };
                        igTextColored(new Vector4(0.7f, 0.9f, 1.0f, 1), $"({lightTypeName})");
                    }
                    else
                    {
                        igTextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "(disabled)");
                    }
                    
                    igPopID();
                }
                igUnindent(20);
            }
            else
            {
                igText("Loading model...");
            }
        }
        igEnd();
        
        // Window 2: Statistics (FPS, Animation Time, Culling Stats, Camera Info)
        int screenWidth = sapp_width();
        igSetNextWindowPos(new Vector2(screenWidth - 30, 30), ImGuiCond.Once, new Vector2(1.0f, 0.0f));  // Anchor to top-right
        igSetNextWindowBgAlpha(0.85f);
        byte open2 = 1;
        if (igBegin("Statistics", ref open2, ImGuiWindowFlags.AlwaysAutoResize))
        {
            // Display FPS (calculated from frame duration)
            double frameDuration = sapp_frame_duration();
            float fps = frameDuration > 0 ? (float)(1.0 / frameDuration) : 0.0f;
            igText($"FPS: {fps:F1}");
            igText($"Frame Time: {frameDuration * 1000.0:F2} ms");
            
            if (state.model != null)
            {
                igSeparator();
                
                // Animation timing info
                if (state.animator != null)
                {
                    var currentAnim = state.animator.GetCurrentAnimation();
                    if (currentAnim != null)
                    {
                        float duration = currentAnim.GetDuration();
                        float currentTime = state.animator.GetCurrentTime();
                        float ticksPerSecond = currentAnim.GetTicksPerSecond();

                        // Convert to seconds for display
                        float durationInSeconds = duration / ticksPerSecond;
                        float currentTimeInSeconds = currentTime / ticksPerSecond;

                        igText($"Anim Duration: {durationInSeconds:F2}s");
                        igText($"Anim Time: {currentTimeInSeconds:F2}s");
                        igText($"Progress: {(currentTime / duration * 100):F1}%%");
                        igSeparator();
                    }
                }
                
                // Display frustum culling statistics
                igText("=== Culling Statistics ===");
                igText($"Total Meshes: {state.totalMeshes}");
                igText($"Visible: {state.visibleMeshes}");
                igText($"Culled: {state.culledMeshes}");
                if (state.totalMeshes > 0)
                {
                    float cullPercent = state.totalMeshes > 0 ? (state.culledMeshes * 100.0f / state.totalMeshes) : 0;
                    igText($"Culled: {cullPercent:F1}%%");
                }
                
                igSeparator();
                
                // Texture cache statistics
                var (hits, misses, total) = TextureCache.Instance.GetStats();
                var hitRate = hits + misses > 0 ? (hits * 100.0 / (hits + misses)) : 0.0;
                igText("=== Texture Cache ===");
                igText($"Unique: {total}");
                igText($"Hits: {hits}, Misses: {misses}");
                igText($"Hit Rate: {hitRate:F1}%%");
                
                igSeparator();
                igText("=== Camera ===");
                igText($"Distance: {state.camera.Distance:F2}");
                igText($"Latitude: {state.camera.Latitude:F2}");
                igText($"Longitude: {state.camera.Longitude:F2}");
            }
        }
        igEnd();
        
        // Window 3: Mobile Camera Controls
        int screenHeight = sapp_height();
        igSetNextWindowPos(new Vector2(30, screenHeight - 30), ImGuiCond.Once, new Vector2(0.0f, 1.0f));  // Anchor to bottom-left
        igSetNextWindowBgAlpha(0.85f);
        byte open3 = 1;
        if (igBegin("Camera Controls", ref open3, ImGuiWindowFlags.AlwaysAutoResize))
        {
            // Calculate forward and right vectors for camera movement
            Vector3 forward = Vector3.Normalize(state.camera.Center - state.camera.EyePos);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            
            // Movement speed (scaled by frame time for smooth continuous movement)
            float moveSpeed = 50.0f * (float)sapp_frame_duration();
            
            // Forward button (centered)
            igIndent(50);
            igButton("Forward", new Vector2(80, 40));
            if (igIsItemActive())  // Check if button is being held down
            {
                state.camera.Center = state.camera.Center + forward * moveSpeed;
            }
            igUnindent(50);
            
            // Left and Right buttons (side by side)
            igButton("Left", new Vector2(80, 40));
            if (igIsItemActive())  // Check if button is being held down
            {
                state.camera.Center = state.camera.Center - right * moveSpeed;
            }
            igSameLine(0, 10);
            igButton("Right", new Vector2(80, 40));
            if (igIsItemActive())  // Check if button is being held down
            {
                state.camera.Center = state.camera.Center + right * moveSpeed;
            }
            
            // Backward button (centered)
            igIndent(50);
            igButton("Back", new Vector2(80, 40));
            if (igIsItemActive())  // Check if button is being held down
            {
                state.camera.Center = state.camera.Center - forward * moveSpeed;
            }
            igUnindent(50);
        }
        igEnd();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        // Handle ImGui events first
        if (simgui_handle_event(in *e))
        {
            return; // ImGui consumed the event
        }

        // Handle middle mouse button for model rotation
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN && 
            e->mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_MIDDLE)
        {
            state.middleMouseDown = true;
        }
        else if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP && 
                 e->mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_MIDDLE)
        {
            state.middleMouseDown = false;
        }
        else if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE && state.middleMouseDown)
        {
            // Rotate model: horizontal mouse movement rotates around Y-axis, vertical around X-axis
            state.modelRotationY += e->mouse_dx * 0.01f;  // Horizontal movement -> Y-axis rotation
            state.modelRotationX += e->mouse_dy * 0.01f;  // Vertical movement -> X-axis rotation
            return; // Don't pass to camera
        }

        // Camera handles all other input events including keyboard
        state.camera.HandleEvent(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        // Print texture cache statistics before cleanup
        Info("[SharpGLTF] Cleanup - Texture Cache Statistics:");
        TextureCache.Instance.PrintStats();
        
        state.model?.Dispose();
        
        // Clear texture cache (will dispose all cached textures)
        TextureCache.Instance.Clear();
        
        FileSystem.Instance.Shutdown();
        simgui_shutdown();
        sg_shutdown();

        // Force a complete shutdown if debugging
        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
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
            sample_count = 4,
            window_title = "SharpGLTF  (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
