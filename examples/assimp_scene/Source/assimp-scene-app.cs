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

public static unsafe class AssimpSceneApp
{
    static string modelPath = "raceTrack.glb";

    class _state
    {
        public sg_pass_action pass_action;

        public sg_pipeline pip;
        public Sokol.Camera camera = new Sokol.Camera();

        public Sokol.Model? model;
        public Sokol.AnimationManager? animationManager;
        public Sokol.Animator? animator;
        
        // Culling statistics
        public int totalMeshes = 0;
        public int visibleMeshes = 0;
        public int culledMeshes = 0;
        
        // Hierarchical culling statistics
        public int nodesTestedForCulling = 0;
        public int nodesCulled = 0;  // Entire branches culled
        public bool enableHierarchicalCulling = true;
        
        // Octree culling
        public bool enableOctreeCulling = true;
        public int octreeNodesTestedForCulling = 0;
        public int octreeNodesCulled = 0;
        
        // Distance culling settings
        public float maxDrawDistance = 500.0f;  // Maximum distance to draw objects
        public bool enableDistanceCulling = false;  // TEMPORARILY DISABLED FOR DEBUG
        public int distanceCulledMeshes = 0;
    }

    static _state state = new _state();


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        Console.WriteLine("Assimp: Init()");
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

        // duck center = new Vector3(0.0f, 150, 0.0f);
        // Distance = 400.0f,
        state.camera.Init(new CameraDesc()
        {
            Aspect = 60.0f,
            NearZ = 0.1f,    // Increased from 0.01 - improves depth precision dramatically
            FarZ = 1500.0f,
            Center = new Vector3(0.0f, 10f, 0.0f),
            Distance = 3.0f,
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
        // pipeline_desc.layout.buffers[0].stride = (17 * sizeof(float)); // 3 pos + 4 color + 2 texcoord + 4 boneIds + 4 weights
        pipeline_desc.layout.attrs[ATTR_assimp_position].format = SG_VERTEXFORMAT_FLOAT3;
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

        // Wait for model to load, then initialize animation
        // Animation will be created in Frame() once model is loaded


        Console.WriteLine($"Assimp: Requested file load for: {filePath}");

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
        //     Console.WriteLine("Animation loading started...");
        // }

        // // Initialize animator once animation is loaded
        // if (state.animationManager != null &&
        //     state.animationManager.GetAnimationCount() > 0 &&
        //     state.animator == null)
        // {
        //     var animation = state.animationManager.GetFirstAnimation();
        //     if (animation != null)
        //     {
        //         state.animator = new Sokol.Animator(animation);
        //         Console.WriteLine("Animator initialized successfully");
        //     }
        // }

        // Update animator
        // if (state.animator != null)
        // {
        //     state.animator.UpdateAnimation((float)sapp_frame_duration());
        // }

        state.camera.Update(sapp_width(), sapp_height());

        // Prepare uniform data
        vs_params_t vs_params = new vs_params_t();
        vs_params.projection = state.camera.Proj;
        vs_params.view = state.camera.View;
        vs_params.model = Matrix4x4.Identity;

        // // Get bone matrices from animator
        // if (state.animator != null)
        // {
        //     // Optimized bulk copy - both arrays are exactly AnimationConstants.MAX_BONES elements
        //     var boneMatrices = state.animator.GetFinalBoneMatrices();
        //     var destSpan = MemoryMarshal.CreateSpan(ref vs_params.finalBonesMatrices[0], AnimationConstants.MAX_BONES);
        //     boneMatrices.AsSpan().CopyTo(destSpan);
        // }
        // else
        {
            // Initialize with identity matrices - optimized bulk initialization
            // var bonesSpan = MemoryMarshal.CreateSpan(ref vs_params.finalBonesMatrices[0], AnimationConstants.MAX_BONES);
            // bonesSpan.Fill(Matrix4x4.Identity);
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
                Console.WriteLine("Building Octree spatial index...");
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
            
            // Calculate view-projection matrix for frustum culling
            Matrix4x4 viewProjection = vs_params.view * vs_params.projection;
            
            // Get camera position for distance culling
            Matrix4x4 viewInverse;
            Matrix4x4.Invert(vs_params.view, out viewInverse);
            Vector3 cameraPosition = new Vector3(viewInverse.M41, viewInverse.M42, viewInverse.M43);
            
            // Use octree culling if enabled and available
            if (state.enableOctreeCulling && state.model.SceneGraph.SpatialIndex != null)
            {
                DrawSceneUsingOctree(ref vs_params, viewProjection, cameraPosition);
            }
            else
            {
                DrawSceneNode(state.model.SceneGraph.RootNode, ref vs_params, viewProjection, cameraPosition);
            }
        }

        simgui_render();
        sg_end_pass();
        sg_commit();
    }

    static void DrawSceneUsingOctree(ref vs_params_t vs_params, Matrix4x4 viewProjection, Vector3 cameraPosition)
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
            mesh.Draw();
        }
    }

    static void DrawSceneNode(Sokol.Node? node, ref vs_params_t vs_params, Matrix4x4 viewProjection, Vector3 cameraPosition)
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
            DrawSceneNode(child, ref vs_params, viewProjection, cameraPosition);
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
        igSetNextWindowPos(new Vector2(30, 30), ImGuiCond.Once, Vector2.Zero);
        igSetNextWindowBgAlpha(0.85f);
        byte open = 1;
        if (igBegin("Animation Controls", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            igText("Assimp Animation App");
            igSeparator();

            if (state.animationManager != null && state.animationManager.GetAnimationCount() > 0)
            {
                int animCount = state.animationManager.GetAnimationCount();
                string? currentAnimName = state.animationManager.GetCurrentAnimationName();
                int currentIndex = state.animationManager.GetCurrentAnimationIndex();

                igText($"Current Animation: {currentAnimName ?? "None"} (Index: {currentIndex})");
                igText($"Total Animations: {animCount}");

                // Display animation timing info
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

                        igText($"Duration: {durationInSeconds:F2}s");
                        igText($"Current Time: {currentTimeInSeconds:F2}s");
                        igText($"Progress: {(currentTime / duration * 100):F1}%%");
                    }
                }

                igSeparator();

                if (animCount > 1)
                {
                    if (igButton("<- Previous Animation", Vector2.Zero))
                    {
                        var prevAnimation = state.animationManager.GetPreviousAnimation();
                        if (prevAnimation != null && state.animator != null)
                        {
                            state.animator.SetAnimation(prevAnimation);
                            string? animName = state.animationManager.GetCurrentAnimationName();
                            Console.WriteLine($"Switched to animation: {animName}");
                        }
                    }

                    igSameLine(0, 10);

                    if (igButton("Next Animation ->", Vector2.Zero))
                    {
                        var nextAnimation = state.animationManager.GetNextAnimation();
                        if (nextAnimation != null && state.animator != null)
                        {
                            state.animator.SetAnimation(nextAnimation);
                            string? animName = state.animationManager.GetCurrentAnimationName();
                            Console.WriteLine($"Switched to animation: {animName}");
                        }
                    }
                }
                else
                {
                    igText("Only one animation available");
                }
            }
            else
            {
                igText("Loading animations...");
            }

            igSeparator();
            
            // Display FPS (calculated from frame duration)
            double frameDuration = sapp_frame_duration();
            float fps = frameDuration > 0 ? (float)(1.0 / frameDuration) : 0.0f;
            igText($"FPS: {fps:F1}");
            igText($"Frame Time: {frameDuration * 1000.0:F2} ms");
            
            igSeparator();
            
            // Display frustum culling statistics
            igText("=== Culling Statistics ===");
            igText($"Total Meshes: {state.totalMeshes}");
            igText($"Visible: {state.visibleMeshes}");
            igText($"Culled: {state.culledMeshes}");
            float cullPercent = state.totalMeshes > 0 ? (state.culledMeshes * 100.0f / state.totalMeshes) : 0;
            igText($"Culled: {cullPercent:F1}%%");
            
            igSeparator();
            igText("=== Octree Culling ===");
            byte octreeCullingEnabled = state.enableOctreeCulling ? (byte)1 : (byte)0;
            if (igCheckbox("Enable Octree Culling", ref octreeCullingEnabled))
            {
                state.enableOctreeCulling = octreeCullingEnabled != 0;
                // Clear octree to rebuild next frame
                if (state.model?.SceneGraph != null)
                {
                    state.model.SceneGraph.SpatialIndex = null;
                }
            }
            
            if (state.enableOctreeCulling && state.model?.SceneGraph?.SpatialIndex != null)
            {
                var octreeStats = state.model.SceneGraph.SpatialIndex.GetStats();
                igText($"Octree Nodes: {octreeStats.totalNodes} ({octreeStats.leafNodes} leaves)");
                igText($"Nodes Tested: {state.octreeNodesTestedForCulling}");
                igText($"Nodes Culled: {state.octreeNodesCulled}");
                
                if (state.octreeNodesCulled > 0)
                {
                    float octreeCullPercent = state.octreeNodesTestedForCulling > 0 
                        ? (state.octreeNodesCulled * 100.0f / state.octreeNodesTestedForCulling) 
                        : 0;
                    igTextColored(new Vector4(0, 1, 0, 1), $"Spatial culling: {octreeCullPercent:F1}%% branches");
                }
            }
            else if (!state.enableOctreeCulling)
            {
                igTextColored(new Vector4(1, 1, 0, 1), "Octree disabled - using scene graph");
            }
            
            igSeparator();
            igText("=== Hierarchical Culling ===");
            
            if (!state.enableOctreeCulling)
            {
                byte hierarchicalCullingEnabled = state.enableHierarchicalCulling ? (byte)1 : (byte)0;
                if (igCheckbox("Enable Hierarchical Culling", ref hierarchicalCullingEnabled))
                {
                    state.enableHierarchicalCulling = hierarchicalCullingEnabled != 0;
                }
                igText($"Nodes Tested: {state.nodesTestedForCulling}");
                igText($"Nodes Culled: {state.nodesCulled}");
                if (state.nodesCulled > 0 && state.enableHierarchicalCulling)
                {
                    igTextColored(new Vector4(0, 1, 0, 1), $"Branch culling active!");
                }
            }
            else
            {
                igTextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "(Disabled when Octree is active)");
            }
            
            igSeparator();
            igText("=== Distance Culling ===");
            byte distanceCullingEnabled = state.enableDistanceCulling ? (byte)1 : (byte)0;
            if (igCheckbox("Enable Distance Culling", ref distanceCullingEnabled))
            {
                state.enableDistanceCulling = distanceCullingEnabled != 0;
            }
            if (state.enableDistanceCulling)
            {
                igSliderFloat("Max Distance", ref state.maxDrawDistance, 50.0f, 2000.0f, "%.0f", ImGuiSliderFlags.None);
                igText($"Distance Culled: {state.distanceCulledMeshes}");
            }
            
            igSeparator();
            igText($"Camera Distance: {state.camera.Distance:F2}");
            igText($"Camera Latitude: {state.camera.Latitude:F2}");
            igText($"Camera Longitude: {state.camera.Longitude:F2}");
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
