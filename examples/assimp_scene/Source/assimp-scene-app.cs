using System;
using System.IO;
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
using Assimp.Configs;
using static Sokol.SFetch;
using static Sokol.SDebugText;
using static assimp_scene_app_shader_cs.Shaders;
using Assimp;
using static Sokol.SImgui;
using Imgui;
using static Imgui.ImguiNative;
using System.Diagnostics.Metrics;
using static Sokol.SBasisu;

public static unsafe class AssimpSceneApp
{
    static string modelPath = "assimpScene.glb";
    class _state
    {
        public sg_pass_action pass_action;

        public sg_pipeline pip;
        public Sokol.Camera camera = new Sokol.Camera();

        public Sokol.Model? model;
        public Sokol.AnimationManager? animationManager;
        public Sokol.Animator? animator;
        
        // Camera auto-positioning
        public bool cameraInitialized = false;
        
        // Culling statistics
        public int totalMeshes = 0;
        public int visibleMeshes = 0;
        public int culledMeshes = 0;
        
        // Rendering statistics
        public int totalVertices = 0;
        public int totalIndices = 0;
        public int totalFaces = 0;
        
        // Hierarchical culling statistics
        public int nodesTestedForCulling = 0;
        public int nodesCulled = 0;  // Entire branches culled
        public bool enableHierarchicalCulling = true;
        
        // Octree culling
        public bool enableOctreeCulling = false;  // Disabled by default on mobile - scene graph culling is faster
        public int octreeNodesTestedForCulling = 0;
        public int octreeNodesCulled = 0;
        
        // Distance culling settings
        public float maxDrawDistance = 500.0f;  // Maximum distance to draw objects
        public bool enableDistanceCulling = false;  
        public int distanceCulledMeshes = 0;
        
        // Lighting
        public bool enableLighting = true;  // Toggle for performance comparison
        public List<Sokol.Light> lights = new List<Sokol.Light>();
        public Vector3 ambientColor = new Vector3(0.5f, 0.6f, 0.75f);  // Sky blue ambient for sunny day
        public float ambientIntensity = 0.6f;  // Moderate ambient (shadows still visible but not too dark)
    }

    static _state state = new _state();


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        Log("assimp-scene-app: Init()");

        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            buffer_pool_size = 4096,
            sampler_pool_size = 512, // Reduced from 2048 - texture cache prevents duplicate samplers
            uniform_buffer_size = 64 * 1024 * 1024, // 64 MB - increased to handle very large scene graphs (2500+ nodes)
            logger = {
                func = &slog_func,
            }
        });
        
            sbasisu_setup();

        // duck center = new Vector3(0.0f, 150, 0.0f);
        // Distance = 400.0f,
        state.camera.Init(new CameraDesc()
        {
            Aspect = 60.0f,
            NearZ = 0.1f,    // Increased from 0.01 - improves depth precision dramatically
            FarZ = 2000.0f,
            Center = new Vector3(0.0f, 10f, 0.0f),
            Distance = 3.0f,
            MaxDist = 2000.0f
        });

        // Setup sokol-imgui
        simgui_setup(new simgui_desc_t
        {
            logger = { func = &slog_func }
        });

        // Initialize FileSystem for file loading
        FileSystem.Instance.Initialize();

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };

        sg_shader shd = sg_make_shader(assimp_shader_desc(sg_query_backend()));

        var pipeline_desc = default(sg_pipeline_desc);
        // pipeline_desc.layout.buffers[0].stride = (20 * sizeof(float)); // 3 pos + 3 normal + 4 color + 2 texcoord + 4 boneIds + 4 weights
        pipeline_desc.layout.attrs[ATTR_assimp_position].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_assimp_normal].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_assimp_color0].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.layout.attrs[ATTR_assimp_texcoord0].format = SG_VERTEXFORMAT_FLOAT2;
        pipeline_desc.layout.attrs[ATTR_assimp_boneIds].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.layout.attrs[ATTR_assimp_weights].format = SG_VERTEXFORMAT_FLOAT4;

        pipeline_desc.shader = shd;
        pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        pipeline_desc.cull_mode = SG_CULLMODE_BACK;  // Enable back-face culling for performance
        pipeline_desc.depth.write_enabled = true;
        pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pipeline_desc.label = "assimp-simple-pipeline";

        state.pip = sg_make_pipeline(pipeline_desc);

        // Use FileSystem to load the model file
        string filePath = util_get_file_path(modelPath);
        state.model = new Sokol.Model(filePath);

        // Initialize sunny day lighting for urban scene
        
        // Sun - Strong directional light from above (midday sun position)
        state.lights.Add(Sokol.Light.CreateDirectionalLight(
            new Vector3(0.3f, -0.8f, -0.2f),   // Direction (slightly from side, mostly from above)
            new Vector3(1.0f, 0.98f, 0.95f),   // Bright warm white (sunlight color)
            1.2f                                // Strong intensity for sunny day
        ));
        
        // Sky light - Soft fill from above (simulates blue sky dome)
        state.lights.Add(Sokol.Light.CreateDirectionalLight(
            new Vector3(0.0f, -1.0f, 0.0f),    // Direction (straight down from sky)
            new Vector3(0.6f, 0.7f, 1.0f),     // Sky blue color
            0.25f                               // Subtle fill intensity
        ));
        
        // Atmospheric scatter - Very soft bounce light (simulates light bouncing off ground/buildings)
        state.lights.Add(Sokol.Light.CreateDirectionalLight(
            new Vector3(0.0f, 0.5f, 0.0f),     // Direction (from below - ground bounce)
            new Vector3(0.9f, 0.85f, 0.8f),    // Warm ground reflection color
            0.15f                               // Very subtle
        ));

        // Wait for model to load, then initialize animation
        // Animation will be created in Frame() once model is loaded


        Info($"Assimp: Requested file load for: {filePath}");

    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // Update FileSystem to process pending requests
        FileSystem.Instance.Update();

        // Start new imgui frame
        simgui_new_frame(new simgui_frame_desc_t
        {
            width = sapp_width(),
            height = sapp_height(),
            delta_time = sapp_frame_duration(),
            dpi_scale = 1 // TBD ELI doesn't work on Android sapp_dpi_scale()
        });

        // Initialize animation manager once model is loaded
        // if (state.model != null &&
        //     state.model.SimpleMeshes != null &&
        //     state.model.SimpleMeshes.Count > 0 &&
        //     state.animationManager == null)
        // {
        //     state.animationManager = new Sokol.AnimationManager();
        //     string animPath = util_get_file_path(modelPath);
        //     state.animationManager.LoadAnimation(animPath, state.model);
        //     Info("Animation loading started...");
        // }

        // Initialize animator once animation is loaded
        if (state.animationManager != null &&
            state.animationManager.GetAnimationCount() > 0 &&
            state.animator == null)
        {
            var animation = state.animationManager.GetFirstAnimation();
            if (animation != null)
            {
                state.animator = new Sokol.Animator(animation);
                Info("Animator initialized successfully");
            }
        }

        // Auto-position camera on first load to capture entire scene
        if (!state.cameraInitialized && state.model?.SceneGraph != null)
        {
            var sceneBounds = state.model.SceneGraph.GetSceneBounds();
            Vector3 sceneCenter = (sceneBounds.Min + sceneBounds.Max) * 0.5f;
            Vector3 sceneSize = sceneBounds.Max - sceneBounds.Min;
            float maxDimension = Math.Max(Math.Max(sceneSize.X, sceneSize.Y), sceneSize.Z);
            
            // Calculate distance to fit entire scene in view (closer view with tight framing)
            float fovRadians = state.camera.Aspect * (float)Math.PI / 180.0f;
            float distance = (maxDimension * 0.5f) / (float)Math.Tan(fovRadians * 0.5f) * 0.95f; // 0.95x for tighter framing
            
            state.camera.Center = sceneCenter;
            state.camera.Distance = distance;
            
            state.cameraInitialized = true;
            Info($"Camera auto-positioned: Center={sceneCenter}, Distance={distance:F2}");
            Info($"Scene bounds: Min={sceneBounds.Min}, Max={sceneBounds.Max}, Size={sceneSize}");
        }

        // Update animator
        if (state.animator != null)
        {
            state.animator.UpdateAnimation((float)sapp_frame_duration());
        }

        state.camera.Update(sapp_width(), sapp_height());

        // Prepare uniform data
        vs_params_t vs_params = new vs_params_t();
        vs_params.projection = state.camera.Proj;
        vs_params.view = state.camera.View;
        vs_params.model = Matrix4x4.Identity;

        // Get bone matrices from animator
        if (state.animator != null)
        {
            // Optimized bulk copy - both arrays are exactly AnimationConstants.MAX_BONES elements
            var boneMatrices = state.animator.GetFinalBoneMatrices();
            var destSpan = MemoryMarshal.CreateSpan(ref vs_params.finalBonesMatrices[0], AnimationConstants.MAX_BONES);
            boneMatrices.AsSpan().CopyTo(destSpan);
        }
        else
        {
            // Initialize with identity matrices - optimized bulk initialization
            var bonesSpan = MemoryMarshal.CreateSpan(ref vs_params.finalBonesMatrices[0], AnimationConstants.MAX_BONES);
            bonesSpan.Fill(Matrix4x4.Identity);
        }

        // Prepare lighting uniform data
        fs_params_t fs_params = new fs_params_t();
        
        // Extract camera position from view matrix inverse
        Matrix4x4.Invert(vs_params.view, out Matrix4x4 viewInv);
        Vector3 cameraPos = new Vector3(viewInv.M41, viewInv.M42, viewInv.M43);
        fs_params.camera_pos = cameraPos;
        
        // Set lighting based on toggle
        if (state.enableLighting)
        {
            // Set ambient lighting
            fs_params.ambient_color = state.ambientColor;
            fs_params.ambient_intensity = state.ambientIntensity;
            
            // Count enabled lights and populate light arrays (max 4)
            int enabledLightIndex = 0;
            for (int i = 0; i < state.lights.Count && enabledLightIndex < 4; i++)
            {
                Sokol.Light light = state.lights[i];
                if (!light.Enabled) continue;  // Skip disabled lights
                
                // Light position with type in w component
                fs_params.light_positions[enabledLightIndex] = new Vector4(light.Position, (float)light.Type);
                
                // Light direction with spot inner cutoff (cosine) in w component
                float innerCutoff = (float)Math.Cos(light.SpotInnerAngle * Math.PI / 180.0);
                fs_params.light_directions[enabledLightIndex] = new Vector4(light.Direction, innerCutoff);
                
                // Light color with intensity in w component
                fs_params.light_colors[enabledLightIndex] = new Vector4(light.Color, light.Intensity);
                
                // Light parameters: range in x, spot outer cutoff (cosine) in y
                float outerCutoff = (float)Math.Cos(light.SpotOuterAngle * Math.PI / 180.0);
                fs_params.light_params[enabledLightIndex] = new Vector4(light.Range, outerCutoff, 0, 0);
                
                enabledLightIndex++;
            }
            
            // Set number of enabled lights
            fs_params.num_lights = enabledLightIndex;
        }
        else
        {
            // Lighting disabled - use simple full-bright ambient
            fs_params.ambient_color = new Vector3(1.0f, 1.0f, 1.0f);  // White
            fs_params.ambient_intensity = 1.0f;  // Full brightness
            fs_params.num_lights = 0;  // No lights
        }

        // Draw UI
        DrawUI();

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pip);

        // Draw the scene graph recursively
        if (state.model?.SceneGraph != null)
        {
            // Build octree on first frame if enabled
            if (state.enableOctreeCulling && state.model.SceneGraph.SpatialIndex == null)
            {
                Info("Building Octree spatial index...");
                state.model.SceneGraph.BuildSpatialIndex();
            }
            
            // Reset culling statistics
            state.totalMeshes = 0;
            state.visibleMeshes = 0;
            state.culledMeshes = 0;
            state.nodesTestedForCulling = 0;
            state.nodesCulled = 0;
            state.distanceCulledMeshes = 0;
            state.octreeNodesTestedForCulling = 0;
            state.octreeNodesCulled = 0;
            
            // Reset rendering statistics
            state.totalVertices = 0;
            state.totalIndices = 0;
            state.totalFaces = 0;
            
            // Calculate view-projection matrix for frustum culling
            Matrix4x4 viewProjection = vs_params.view * vs_params.projection;
            
            // Get camera position for distance culling
            Matrix4x4 viewInverse;
            Matrix4x4.Invert(vs_params.view, out viewInverse);
            Vector3 cameraPosition = new Vector3(viewInverse.M41, viewInverse.M42, viewInverse.M43);
            
            // Use octree culling if enabled and available
            if (state.enableOctreeCulling && state.model.SceneGraph.SpatialIndex != null)
            {
                DrawSceneUsingOctree(ref vs_params, ref fs_params, viewProjection, cameraPosition);
            }
            else
            {
                DrawSceneNode(state.model.SceneGraph.RootNode, ref vs_params, ref fs_params, viewProjection, cameraPosition);
            }
        }

        simgui_render();
        sg_end_pass();
        sg_commit();
    }

    static void DrawSceneUsingOctree(ref vs_params_t vs_params, ref fs_params_t fs_params, Matrix4x4 viewProjection, Vector3 cameraPosition)
    {
        if (state.model?.SceneGraph?.SpatialIndex == null)
            return;

        // Query visible meshes from octree
        var visibleMeshes = state.model.SceneGraph.SpatialIndex.QueryVisible(
            viewProjection,
            cameraPosition,
            state.maxDrawDistance,
            state.enableDistanceCulling,
            out state.octreeNodesTestedForCulling,
            out state.octreeNodesCulled);

        state.totalMeshes = state.model.SceneGraph.AllMeshes.Count;
        state.visibleMeshes = visibleMeshes.Count;
        state.culledMeshes = state.totalMeshes - state.visibleMeshes;

        // Draw all visible meshes
        foreach (var (mesh, transform) in visibleMeshes)
        {
            vs_params.model = transform;
            sg_apply_uniforms(UB_vs_params, SG_RANGE(ref vs_params));
            sg_apply_uniforms(UB_fs_params, SG_RANGE(ref fs_params));
            state.totalVertices += mesh.VertexCount;
            state.totalIndices += mesh.IndexCount;
            state.totalFaces += mesh.IndexCount / 3; // Convert indices to triangles
            mesh.Draw();
        }
    }

    static void DrawSceneNode(Sokol.Node? node, ref vs_params_t vs_params, ref fs_params_t fs_params, Matrix4x4 viewProjection, Vector3 cameraPosition)
    {
        if (node == null) return;
        
        state.nodesTestedForCulling++;
        
        // HIERARCHICAL CULLING: Test node's hierarchical bounding box first (if enabled)
        // If the entire node (including all children) is outside frustum, skip everything
        if (state.enableHierarchicalCulling)
        {
            var nodeWorldBounds = node.HierarchicalBounds.Transform(node.WorldTransform);
            
            // Extract frustum planes from view-projection matrix (same as in Mesh.IsVisible)
            Matrix4x4 m = viewProjection;
            
            // Left plane: m.M14 + m.M11, etc.
            Vector4 leftPlane = new Vector4(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41);
            Vector4 rightPlane = new Vector4(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41);
            Vector4 topPlane = new Vector4(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42);
            Vector4 bottomPlane = new Vector4(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42);
            Vector4 nearPlane = new Vector4(m.M14 + m.M13, m.M24 + m.M23, m.M34 + m.M33, m.M44 + m.M43);
            Vector4 farPlane = new Vector4(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43);
            
            Vector4[] frustumPlanes = { leftPlane, rightPlane, topPlane, bottomPlane, nearPlane, farPlane };
            
            // Test node's hierarchical bounds against frustum
            bool nodeVisible = true;
            foreach (var plane in frustumPlanes)
            {
                Vector3 planeNormal = new Vector3(plane.X, plane.Y, plane.Z);
                float planeDistance = plane.W;
                
                // Find the positive vertex (furthest point in the direction of plane normal)
                Vector3 positiveVertex = new Vector3(
                    planeNormal.X >= 0 ? nodeWorldBounds.Max.X : nodeWorldBounds.Min.X,
                    planeNormal.Y >= 0 ? nodeWorldBounds.Max.Y : nodeWorldBounds.Min.Y,
                    planeNormal.Z >= 0 ? nodeWorldBounds.Max.Z : nodeWorldBounds.Min.Z
                );
                
                // If positive vertex is outside this plane, the entire box is outside
                if (Vector3.Dot(planeNormal, positiveVertex) + planeDistance < 0)
                {
                    nodeVisible = false;
                    break;
                }
            }
            
            // If entire node is culled, skip this node and ALL children
            if (!nodeVisible)
            {
                state.nodesCulled++;
                
                // Count all meshes in this branch as culled
                int meshesInBranch = CountMeshesInBranch(node);
                state.totalMeshes += meshesInBranch;
                state.culledMeshes += meshesInBranch;
                return;  // EARLY EXIT - skip entire branch
            }
        }

        // Update model matrix with the node's world transform
        vs_params.model = node.WorldTransform;
        
        // Apply uniforms ONCE per node with the current node's transform
        sg_apply_uniforms(UB_vs_params, SG_RANGE(ref vs_params));
        sg_apply_uniforms(UB_fs_params, SG_RANGE(ref fs_params));

        // Draw all meshes attached to this node (with frustum and distance culling)
        foreach (var mesh in node.Meshes)
        {
            state.totalMeshes++;
            
            // DISTANCE CULLING: Check if mesh is within draw distance
            if (state.enableDistanceCulling)
            {
                // Calculate mesh center in world space
                var meshBounds = mesh.Bounds.Transform(node.WorldTransform);
                Vector3 meshCenter = (meshBounds.Min + meshBounds.Max) * 0.5f;
                float distanceToCamera = Vector3.Distance(cameraPosition, meshCenter);
                
                if (distanceToCamera > state.maxDrawDistance)
                {
                    state.distanceCulledMeshes++;
                    state.culledMeshes++;
                    continue;  // Skip this mesh
                }
            }
            
            // FRUSTUM CULLING: Check if mesh is visible (already implemented)
            if (mesh.IsVisible(node.WorldTransform, viewProjection))
            {
                state.visibleMeshes++;
                state.totalVertices += mesh.VertexCount;
                state.totalIndices += mesh.IndexCount;
                state.totalFaces += mesh.IndexCount / 3; // Convert indices to triangles
                mesh.Draw();
            }
            else
            {
                state.culledMeshes++;
            }
        }

        // Recursively draw all children
        foreach (var child in node.Children)
        {
            DrawSceneNode(child, ref vs_params, ref fs_params, viewProjection, cameraPosition);
        }
    }
    
    // Helper function to count all meshes in a node and its children (for statistics)
    static int CountMeshesInBranch(Sokol.Node? node)
    {
        if (node == null) return 0;
        
        int count = node.Meshes.Count;
        foreach (var child in node.Children)
        {
            count += CountMeshesInBranch(child);
        }
        return count;
    }

    static void DrawUI()
    {
        // Window 1: Controls (Lighting, Animation, Culling)
        igSetNextWindowPos(new Vector2(30, 30), ImGuiCond.Once, Vector2.Zero);
        igSetNextWindowBgAlpha(0.85f);
        byte open1 = 1;
        if (igBegin("Controls", ref open1, ImGuiWindowFlags.AlwaysAutoResize))
        {
            igText("Assimp Animation App");
            igSeparator();
            
            // Lighting toggle at the top for easy performance comparison
            igText("=== Lighting ===");
            byte lightingEnabled = state.enableLighting ? (byte)1 : (byte)0;
            if (igCheckbox("Enable Lighting", ref lightingEnabled))
            {
                state.enableLighting = lightingEnabled != 0;
                Info($"Lighting {(state.enableLighting ? "ENABLED" : "DISABLED")}");
            }
            if (!state.enableLighting)
            {
                igTextColored(new Vector4(1, 0.5f, 0, 1), "Full-bright mode (no shading)");
            }
            else
            {
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
                        Info($"Light {i + 1} {(light.Enabled ? "ON" : "OFF")}");
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
            igSeparator();

            if (state.animationManager != null && state.animationManager.GetAnimationCount() > 0)
            {
                igText("=== Animation ===");
                int animCount = state.animationManager.GetAnimationCount();
                string? currentAnimName = state.animationManager.GetCurrentAnimationName();
                int currentIndex = state.animationManager.GetCurrentAnimationIndex();

                igText($"Current: {currentAnimName ?? "None"}");
                igText($"Total: {animCount}");

                if (animCount > 1)
                {
                    if (igButton("<- Previous", Vector2.Zero))
                    {
                        var prevAnimation = state.animationManager.GetPreviousAnimation();
                        if (prevAnimation != null && state.animator != null)
                        {
                            state.animator.SetAnimation(prevAnimation);
                            string? animName = state.animationManager.GetCurrentAnimationName();
                            Info($"Switched to animation: {animName}");
                        }
                    }

                    igSameLine(0, 10);

                    if (igButton("Next ->", Vector2.Zero))
                    {
                        var nextAnimation = state.animationManager.GetNextAnimation();
                        if (nextAnimation != null && state.animator != null)
                        {
                            state.animator.SetAnimation(nextAnimation);
                            string? animName = state.animationManager.GetCurrentAnimationName();
                            Info($"Switched to animation: {animName}");
                        }
                    }
                }
                igSeparator();
            }
            
            igText("=== Octree Culling ===");
            byte octreeCullingEnabled = state.enableOctreeCulling ? (byte)1 : (byte)0;
            if (igCheckbox("Enable Octree", ref octreeCullingEnabled))
            {
                state.enableOctreeCulling = octreeCullingEnabled != 0;
                // Clear octree to rebuild next frame
                if (state.model?.SceneGraph != null)
                {
                    state.model.SceneGraph.SpatialIndex = null;
                }
            }
            
            if (!state.enableOctreeCulling)
            {
                igText("=== Hierarchical Culling ===");
                byte hierarchicalCullingEnabled = state.enableHierarchicalCulling ? (byte)1 : (byte)0;
                if (igCheckbox("Enable Hierarchical", ref hierarchicalCullingEnabled))
                {
                    state.enableHierarchicalCulling = hierarchicalCullingEnabled != 0;
                }
            }
            
            igSeparator();
            igText("=== Distance Culling ===");
            byte distanceCullingEnabled = state.enableDistanceCulling ? (byte)1 : (byte)0;
            if (igCheckbox("Enable Distance", ref distanceCullingEnabled))
            {
                state.enableDistanceCulling = distanceCullingEnabled != 0;
            }
            if (state.enableDistanceCulling)
            {
                igSliderFloat("Max Distance", ref state.maxDrawDistance, 50.0f, 2000.0f, "%.0f", ImGuiSliderFlags.None);
            }
        }
        igEnd();
        
        // Window 2: Statistics (FPS, Culling Stats, Camera Info)
        // Position at top-right corner with some padding
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
            float cullPercent = state.totalMeshes > 0 ? (state.culledMeshes * 100.0f / state.totalMeshes) : 0;
            igText($"Culled: {cullPercent:F1}%%");
            
            igSeparator();
            
            // Display rendering statistics
            igText("=== Rendering Statistics ===");
            igText($"Vertices: {state.totalVertices:N0}");
            igText($"Indices: {state.totalIndices:N0}");
            igText($"Triangles: {state.totalFaces:N0}");
            
            // Show vertex reuse ratio
            if (state.totalVertices > 0)
            {
                float reuseRatio = (float)state.totalIndices / state.totalVertices;
                igText($"Index/Vertex: {reuseRatio:F2}");
            }
            
            if (state.enableOctreeCulling && state.model?.SceneGraph?.SpatialIndex != null)
            {
                igSeparator();
                igText("=== Octree Stats ===");
                var octreeStats = state.model.SceneGraph.SpatialIndex.GetStats();
                igText($"Nodes: {octreeStats.totalNodes}");
                igText($"Leaves: {octreeStats.leafNodes}");
                igText($"Tested: {state.octreeNodesTestedForCulling}");
                igText($"Culled: {state.octreeNodesCulled}");
                
                if (state.octreeNodesCulled > 0)
                {
                    float octreeCullPercent = state.octreeNodesTestedForCulling > 0 
                        ? (state.octreeNodesCulled * 100.0f / state.octreeNodesTestedForCulling) 
                        : 0;
                    igTextColored(new Vector4(0, 1, 0, 1), $"Spatial: {octreeCullPercent:F1}%%");
                }
            }
            
            if (!state.enableOctreeCulling && state.enableHierarchicalCulling)
            {
                igSeparator();
                igText("=== Hierarchical Stats ===");
                igText($"Nodes Tested: {state.nodesTestedForCulling}");
                igText($"Nodes Culled: {state.nodesCulled}");
                if (state.nodesCulled > 0)
                {
                    igTextColored(new Vector4(0, 1, 0, 1), "Branch culling active!");
                }
            }
            
            if (state.enableDistanceCulling)
            {
                igSeparator();
                igText("=== Distance Stats ===");
                igText($"Distance Culled: {state.distanceCulledMeshes}");
            }
            
            igSeparator();
            igText("=== Camera ===");
            igText($"Distance: {state.camera.Distance:F2}");
            igText($"Latitude: {state.camera.Latitude:F2}");
            igText($"Longitude: {state.camera.Longitude:F2}");
        }
        igEnd();
        
        // Window 3: Mobile Camera Controls
        // Position at bottom-left corner
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
            float moveSpeed = 2.0f * (float)sapp_frame_duration();
            
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
        if (simgui_handle_event(in *e))
            return;
        state.camera.HandleEvent(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        // Print texture cache statistics before cleanup
        TextureCache.Instance.PrintStats();
        TextureCache.Instance.Clear();

        FileSystem.Instance.Shutdown();
        sbasisu_shutdown();
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
            window_title = "Assimp Animation (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
