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
    // const string filename = "assimpScene.glb";
    // const string filename = "gltf/DamagedHelmet/DamagedHelmet.gltf";

    // const string filename = "DancingGangster.glb";
    // const string filename = "Gangster.glb";

    //race_track
    const string filename = "race_track.glb";

    class _state
    {
        public sg_pass_action pass_action;
        public sg_pipeline pipeline_static;     // Pipeline without skinning
        public sg_pipeline pipeline_skinned;    // Pipeline with skinning
        public Sokol.Camera camera = new Sokol.Camera();
        public SharpGltfModel? model;
        public SharpGltfAnimator? animator;
        public bool modelLoaded = false;
        public bool cameraInitialized = false;  // Track if camera has been auto-positioned
        public bool isMixamoModel = false;      // Track if this is a Mixamo model needing special transforms
        public Vector3 modelBoundsMin;
        public Vector3 modelBoundsMax;
        
        // Keyboard state for WASD movement
        public bool keyW = false;
        public bool keyA = false;
        public bool keyS = false;
        public bool keyD = false;
        public bool keyQ = false;  // Up
        public bool keyE = false;  // Down
        public bool keyUp = false;    // Arrow up
        public bool keyDown = false;  // Arrow down
        
        // Culling statistics
        public int totalMeshes = 0;
        public int visibleMeshes = 0;
        public int culledMeshes = 0;
        public bool enableFrustumCulling = true;
    }

    static _state state = new _state();
    static bool _loggedPipelineOnce = false;  // Debug flag
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
            NearZ = 0.1f,
            FarZ = 2000.0f,
            Center = new Vector3(0.0f, 1.0f, 0.0f),
            Distance = 3.0f,
            Latitude = 10.0f,
            Longitude = 0.0f,
        });

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };

        // Create shader for static meshes (no skinning)
        sg_shader shader_static = sg_make_shader(cgltf_metallic_shader_desc(sg_query_backend()));

        // Create pipeline for static meshes
        var pipeline_desc = default(sg_pipeline_desc);
        pipeline_desc.layout.attrs[ATTR_cgltf_metallic_position].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_cgltf_metallic_normal].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_cgltf_metallic_color].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.layout.attrs[ATTR_cgltf_metallic_texcoord].format = SG_VERTEXFORMAT_FLOAT2;
        pipeline_desc.layout.attrs[ATTR_cgltf_metallic_boneIds].format = SG_VERTEXFORMAT_FLOAT4;  // Changed from UINT4 for WebGL compatibility
        pipeline_desc.layout.attrs[ATTR_cgltf_metallic_weights].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.shader = shader_static;
        pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        pipeline_desc.cull_mode = SG_CULLMODE_BACK;
        pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
        pipeline_desc.depth.write_enabled = true;
        pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pipeline_desc.label = "static-pipeline";
        state.pipeline_static = sg_make_pipeline(pipeline_desc);

        // Create shader for skinned meshes
        sg_shader shader_skinned = sg_make_shader(skinning_metallic_shader_desc(sg_query_backend()));

        // Create pipeline for skinned meshes
        pipeline_desc.shader = shader_skinned;
        pipeline_desc.label = "skinned-pipeline";
        state.pipeline_skinned = sg_make_pipeline(pipeline_desc);

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
                    var context = SharpGLTF.Schema2.ReadContext
                        .Create(f => throw new NotSupportedException());
                        
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
                    
                    Console.WriteLine($"[SharpGLTF] Model bounds: Min={state.modelBoundsMin}, Max={state.modelBoundsMax}");
                    Console.WriteLine($"[SharpGLTF] Model size: {size}, Center: {center}");
                    
                    // Safety check: if bounds are invalid or too small, use defaults
                    if (float.IsInfinity(size.X) || float.IsNaN(size.X) || size.Length() < 0.01f)
                    {
                        Console.WriteLine("[SharpGLTF] Warning: Invalid bounds detected, using defaults");
                        state.modelBoundsMin = new Vector3(-1, 0, -1);
                        state.modelBoundsMax = new Vector3(1, 2, 1);
                    }

                    // Create our model wrapper
                    state.model = new SharpGltfModel(modelRoot);
                    
                    // TBD ELI , this is an hack to detect Mixamo models
                    // Detect if this is a Mixamo model by checking node names
                    state.isMixamoModel = modelRoot.LogicalNodes.Any(n => 
                        n.Name.Contains("mixamorig", StringComparison.OrdinalIgnoreCase) ||
                        n.Name.Contains("Armature", StringComparison.OrdinalIgnoreCase));
                    
                    if (state.isMixamoModel)
                    {
                        Console.WriteLine("[SharpGLTF] Detected Mixamo model - will apply scale/rotation correction");
                    }
                    
                    Console.WriteLine($"[SharpGLTF] Model has {state.model.Meshes.Count} meshes, {state.model.Nodes.Count} nodes");
                    Console.WriteLine($"[SharpGLTF] Model has {state.model.BoneCounter} bones");
                    
                    // Create animator if model has animations
                    if (state.model.HasAnimations)
                    {
                        state.animator = new SharpGltfAnimator(state.model.Animation);
                        Console.WriteLine("[SharpGLTF] Animator created for animated model");
                    }
                    else
                    {
                        Console.WriteLine("[SharpGLTF] No animations found in model");
                    }
                    
                    state.modelLoaded = true;
                    Console.WriteLine($"[SharpGLTF] Model loaded successfully: {path}");
                }
                catch (Exception ex)
                {
                    Error($"[SharpGLTF] Error processing model: {ex.Message}");
                    Console.WriteLine($"[SharpGLTF] Stack trace: {ex.StackTrace}");
                }
            }
            else
            {
                Error($"[SharpGLTF] Failed to load file '{path}': {status}");
            }
        });

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
            
            float minDistance = 0.01f;
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
            
            Console.WriteLine($"=== AUTO-POSITIONING CAMERA ===");
            Console.WriteLine($"Scene bounds: Min={sceneMin}, Max={sceneMax}");
            Console.WriteLine($"Scene size: {sceneSize}");
            Console.WriteLine($"Scene center: {sceneCenter}");
            Console.WriteLine($"Final distance: {bestDistance:F3}");
            
            state.camera.Center = sceneCenter;
            state.camera.Distance = bestDistance;
            state.camera.Latitude = 0.0f;
            state.camera.Longitude = 0.0f;
            
            state.cameraInitialized = true;
        }

        // Handle WASD camera movement
        if (state.cameraInitialized)
        {
            float moveSpeed = 50.0f; // Units per second (increased from 5.0f)
            float deltaTime = (float)sapp_frame_duration();
            float moveAmount = moveSpeed * deltaTime;
            
            // Get camera forward, right, and up vectors
            Vector3 cameraPos = state.camera.EyePos;
            Vector3 forward = Vector3.Normalize(state.camera.Center - cameraPos);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            Vector3 up = Vector3.UnitY;
            
            Vector3 moveDir = Vector3.Zero;
            
            // WASD for forward/back/left/right movement
            if (state.keyW) moveDir += forward;
            if (state.keyS) moveDir -= forward;
            if (state.keyD) moveDir += right;
            if (state.keyA) moveDir -= right;
            
            // Q/E for up/down movement
            if (state.keyQ) moveDir += up;
            if (state.keyE) moveDir -= up;
            
            // Arrow keys for up/down movement (alternative to Q/E)
            if (state.keyUp) moveDir += up;
            if (state.keyDown) moveDir -= up;
            
            // Normalize and apply movement
            if (moveDir.LengthSquared() > 0)
            {
                moveDir = Vector3.Normalize(moveDir);
                Vector3 movement = moveDir * moveAmount;
                
                // Move both camera position and look-at center to maintain view direction
                state.camera.Center += movement;
            }
        }

        // Update camera
        state.camera.Update(fb_width, fb_height);

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
            // Console.WriteLine($"[SharpGLTF Frame {_frameCount}] Starting render, model has {state.model.Nodes.Count} nodes");
            
            // Prepare vertex shader uniforms (common for both pipelines)
            Matrix4x4 model = Matrix4x4.Identity;

            // Prepare fragment shader uniforms (lighting)
            // Use multiple lights for better PBR results (matching cgltf_scene)
            cgltf_light_params_t lightParams = new cgltf_light_params_t();
            lightParams.num_lights = 3;
            
            // Main directional light (like sun)
            lightParams.light_directions[0] = new Vector4(-0.3f, -0.8f, -0.5f, 0); // w=0 for directional
            lightParams.light_colors[0] = new Vector4(1.0f, 0.98f, 0.95f, 2.5f); // w=intensity
            
            // Key point light
            lightParams.light_positions[1] = new Vector4(5.0f, 8.0f, 5.0f, 1); // w=1 for point
            lightParams.light_colors[1] = new Vector4(1.0f, 1.0f, 1.0f, 25.0f);
            lightParams.light_params_data[1] = new Vector4(500.0f, 0, 0, 0); // range
            
            // Fill light
            lightParams.light_positions[2] = new Vector4(-5.0f, 3.0f, -3.0f, 1);
            lightParams.light_colors[2] = new Vector4(0.7f, 0.8f, 1.0f, 10.0f);
            lightParams.light_params_data[2] = new Vector4(500.0f, 0, 0, 0);

            // Debug output on first render when model exists
            bool shouldLogMeshInfo = !_loggedMeshInfoOnce;
            
            // Reset culling statistics
            state.totalMeshes = 0;
            state.visibleMeshes = 0;
            state.culledMeshes = 0;
            
            // Calculate view-projection matrix for frustum culling
            Matrix4x4 viewProjection = state.camera.ViewProj;
            
            // Draw each node (which references a mesh with its transform)
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
                    modelMatrix = node.Transform * scaleMatrix * rotationMatrix;
                }
                else
                {
                    // Use the node's original transform from the GLTF file
                    modelMatrix = node.Transform;
                }
                
                // FRUSTUM CULLING: Check if mesh is visible
                if (state.enableFrustumCulling && !mesh.IsVisible(modelMatrix, viewProjection))
                {
                    state.culledMeshes++;
                    continue;  // Skip this mesh
                }
                
                state.visibleMeshes++;
    
                
                // Use skinning if mesh has it and animator exists
                bool useSkinning = mesh.HasSkinning && state.animator != null;
                
                // Choose pipeline based on whether mesh has skinning
                sg_pipeline pipeline = useSkinning ? state.pipeline_skinned : state.pipeline_static;

                // Debug: Log which pipeline we're using (only once)
                if (!_loggedPipelineOnce)
                {
                    Console.WriteLine($"[SharpGLTF] Rendering mesh with {(mesh.HasSkinning ? "SKINNED" : "STATIC")} pipeline");
                    Console.WriteLine($"[SharpGLTF] Animator is {(state.animator != null ? "ACTIVE" : "NULL")}");
                    _loggedPipelineOnce = true;
                }

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

                    sg_apply_uniforms(UB_skinning_metallic_params, SG_RANGE(ref metallicParams));

                    // Light uniforms (cast to skinning version)
                    skinning_light_params_t skinningLightParams = new skinning_light_params_t();
                    skinningLightParams.num_lights = lightParams.num_lights;
                    skinningLightParams._pad1 = lightParams._pad1;
                    skinningLightParams._pad2 = lightParams._pad2;
                    skinningLightParams._pad3 = lightParams._pad3;
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

                    sg_apply_uniforms(UB_cgltf_metallic_params, SG_RANGE(ref metallicParams));

                    // Light uniforms
                    sg_apply_uniforms(UB_cgltf_light_params, SG_RANGE(ref lightParams));
                }

                // Draw the mesh
                mesh.Draw(pipeline);
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
        igSetNextWindowPos(new Vector2(30, 30), ImGuiCond.Once, Vector2.Zero);
        igSetNextWindowBgAlpha(0.85f);
        byte open = 1;
        if (igBegin("Animation Controls", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            igText("SharpGLTF Animation Viewer");
            igSeparator();

            if (state.model != null)
            {
                igText($"Model: {filename}");
                igText($"Meshes: {state.model.Meshes.Count}");
                igText($"Nodes: {state.model.Nodes.Count}");
                igText($"Bones: {state.model.BoneCounter}");
                igSeparator();

                if (state.animator != null && state.model.HasAnimations)
                {
                    // Animation info
                    int animCount = state.model.GetAnimationCount();
                    string currentAnimName = state.model.GetCurrentAnimationName();
                    int currentAnimIndex = state.model.CurrentAnimationIndex;

                    igText($"Animations: {animCount}");
                    igText($"Current: {currentAnimName} ({currentAnimIndex + 1}/{animCount})");
                    
                    // Animation switching buttons (only if multiple animations)
                    if (animCount > 1)
                    {
                        igSeparator();
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

                    igSeparator();

                    var currentAnim = state.animator.GetCurrentAnimation();
                    if (currentAnim != null)
                    {
                        float duration = currentAnim.GetDuration();
                        float currentTime = state.animator.GetCurrentTime();
                        float ticksPerSecond = currentAnim.GetTicksPerSecond();

                        // Convert to seconds for display
                        float durationInSeconds = duration / ticksPerSecond;
                        float currentTimeInSeconds = currentTime / ticksPerSecond;

                        igText("Animation: Playing");
                        igText($"Duration: {durationInSeconds:F2}s");
                        igText($"Current Time: {currentTimeInSeconds:F2}s");
                        igText($"Progress: {(currentTime / duration * 100):F1}%%");
                    }
                }
                else
                {
                    igText("No animations in model");
                }

                igSeparator();
                igText($"Camera Distance: {state.camera.Distance:F2}");
                igText($"Camera Latitude: {state.camera.Latitude:F2}");
                igText($"Camera Longitude: {state.camera.Longitude:F2}");
                
                // Frustum culling statistics
                igSeparator();
                igText("Frustum Culling:");
                byte frustumEnabled = (byte)(state.enableFrustumCulling ? 1 : 0);
                if (igCheckbox("Enabled", ref frustumEnabled))
                {
                    state.enableFrustumCulling = frustumEnabled != 0;
                }
                igText($"  Total Meshes: {state.totalMeshes}");
                igText($"  Visible: {state.visibleMeshes}");
                igText($"  Culled: {state.culledMeshes}");
                if (state.totalMeshes > 0)
                {
                    float cullRate = (state.culledMeshes * 100.0f) / state.totalMeshes;
                    igText($"  Cull Rate: {cullRate:F1}%%");
                }
                
                // Texture cache statistics
                igSeparator();
                var (hits, misses, total) = TextureCache.Instance.GetStats();
                var hitRate = hits + misses > 0 ? (hits * 100.0 / (hits + misses)) : 0.0;
                igText($"Texture Cache:");
                igText($"  Unique: {total}");
                igText($"  Hits: {hits}, Misses: {misses}");
                igText($"  Hit Rate: {hitRate:F1}%%");
            }
            else
            {
                igText("Loading model...");
            }
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

        state.camera.HandleEvent(e);
        
        // Handle keyboard input for WASD camera movement
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN || e->type == sapp_event_type.SAPP_EVENTTYPE_KEY_UP)
        {
            bool isDown = e->type == sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN;
            
            switch (e->key_code)
            {
                case sapp_keycode.SAPP_KEYCODE_W:
                    state.keyW = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_A:
                    state.keyA = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_S:
                    state.keyS = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_D:
                    state.keyD = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_Q:
                    state.keyQ = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_E:
                    state.keyE = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_UP:
                    state.keyUp = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_DOWN:
                    state.keyDown = isDown;
                    break;
            }
        }
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        // Print texture cache statistics before cleanup
        Console.WriteLine("[SharpGLTF] Cleanup - Texture Cache Statistics:");
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
