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

public static unsafe partial class SharpGLTFApp
{
    /// <summary>
    /// Applies glass material overrides if enabled, otherwise returns original values.
    /// </summary>
    static (float transmission, float ior, Vector3 attenuationColor, float attenuationDistance, float thickness) 
        GetGlassMaterialValues(Sokol.Mesh mesh)
    {
        if (state.overrideGlassMaterials)
        {
            return (
                state.overrideTransmission,
                state.overrideIOR,
                state.overrideAttenuationColor,
                state.overrideAttenuationDistance,
                mesh.ThicknessFactor * state.overrideThickness
            );
        }
        else
        {
            return (
                mesh.TransmissionFactor,
                mesh.IOR,
                mesh.AttenuationColor,
                mesh.AttenuationDistance,
                mesh.ThicknessFactor
            );
        }
    }

    /// <summary>
    /// Calculates a bounding sphere that contains the entire axis-aligned bounding box.
    /// </summary>
    static (Vector3 center, float radius) CalculateBoundingSphere(Vector3 min, Vector3 max)
    {
        Vector3 center = (min + max) * 0.5f;
        float radius = Vector3.Distance(center, max);
        return (center, radius);
    }

    private static unsafe void RunSingleFrame()
    {
        // Update FileSystem to process pending file loads
        FileSystem.Instance.Update();

        // Handle async model dependency loading (one file per frame to avoid blocking)
        if (state.isLoadingModel && state.pendingModelRoot != null && state.asyncLoadState != null)
        {
            var modelRoot = state.pendingModelRoot;
            var loadState = state.asyncLoadState;
            string? baseDirectory = Path.GetDirectoryName(state.pendingModelPath);

            // Check if we have failed
            if (loadState.HasFailed)
            {
                Error($"[SharpGLTF] Dependency loading failed: {loadState.Error}");
                state.isLoadingModel = false;
                state.pendingModelRoot = null;
                state.asyncLoadState = null;
                state.pendingModelPath = null;
            }
            // Check if loading is complete
            else if (loadState.IsComplete)
            {
                // All dependencies loaded - finalize the model
                Info($"[SharpGLTF] All dependencies loaded, validating and finalizing model...");
                
                try
                {
                    // Validate content now that all dependencies are loaded
                    modelRoot._ValidateContentAfterAsyncLoad(SharpGLTF.Validation.ValidationMode.Strict);
                    Info($"[SharpGLTF] Model validation passed");
                }
                catch (Exception ex)
                {
                    Error($"[SharpGLTF] Model validation failed: {ex.Message}");
                    state.isLoadingModel = false;
                    state.pendingModelRoot = null;
                    state.asyncLoadState = null;
                    state.pendingModelPath = null;
                    return; // Don't proceed with loading
                }
                
                // Calculate model bounds
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
                float boundingRadius = Vector3.Distance(state.modelBoundsMin, state.modelBoundsMax) * 0.5f;

                Info($"[SharpGLTF] Model bounds: Min={state.modelBoundsMin}, Max={state.modelBoundsMax}");
                Info($"[SharpGLTF] Model size: {size}, Center: {center}");
                Info($"[SharpGLTF] Bounding sphere radius: {boundingRadius:F6}");

                // Log if bounds seem unusually large
                if (boundingRadius > 1000.0f)
                {
                    Info($"[SharpGLTF] WARNING: Very large bounding radius detected!");
                    float clampedRadius = Math.Min(boundingRadius, 10.0f);
                    state.modelBoundsMin = center - new Vector3(clampedRadius);
                    state.modelBoundsMax = center + new Vector3(clampedRadius);
                    Info($"[SharpGLTF] Clamped bounds: Min={state.modelBoundsMin}, Max={state.modelBoundsMax}");
                }

                // Safety check: if bounds are invalid or too small, use defaults
                if (float.IsInfinity(size.X) || float.IsNaN(size.X) || size.Length() < 0.01f)
                {
                    Info("[SharpGLTF] Warning: Invalid bounds detected, using defaults");
                    state.modelBoundsMin = new Vector3(-1, 0, -1);
                    state.modelBoundsMax = new Vector3(1, 2, 1);
                }

                // Create the model wrapper
                state.model = new SharpGltfModel(modelRoot, state.pendingModelPath!);

                // Detect Mixamo models
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
                    state.ui.animation_open = true;
                    Info("[SharpGLTF] Animator created for animated model");
                }
                else
                {
                    state.ui.animation_open = false;
                    Info("[SharpGLTF] No animations found in model");
                }

                state.modelLoaded = true;
                state.isLoadingModel = false;
                state.pendingModelRoot = null;
                state.asyncLoadState = null;
                state.pendingModelPath = null;
                Info($"[SharpGLTF] Model loaded successfully!");
            }
            else
            {
                // Continue loading the next dependency
                SharpGLTF.Schema2.ModelRoot.AsyncFileLoadCallback asyncLoader = (assetName, onComplete) =>
                {
                    // Construct full path
                    string fullAssetPath = string.IsNullOrEmpty(baseDirectory)
                        ? assetName
                        : Path.Combine(baseDirectory, assetName);

                    Info($"[SharpGLTF] Loading dependency: {assetName} ({loadState.LoadedDependencies + 1}/{loadState.TotalDependencies})");

                    // Use FileSystem async load
                    FileSystem.Instance.LoadFile(fullAssetPath, (filePath, data, status) =>
                    {
                        bool success = status == FileLoadStatus.Success && data != null;
                        
                        if (success)
                        {
                            Info($"[SharpGLTF] Loaded {assetName} ({data!.Length} bytes)");
                            onComplete(true, new ArraySegment<byte>(data));
                        }
                        else
                        {
                            Error($"[SharpGLTF] Failed to load {assetName}: {status}");
                            onComplete(false, default);
                        }
                    });
                };

                // Create image decoder to convert images to GPU textures as they load
                var imageDecoder = CreateImageDecoder();

                // Continue loading (this will start loading one dependency and return)
                modelRoot._ContinueAsyncResolveSatelliteDependencies(loadState, asyncLoader, imageDecoder);

                // Update loading progress for UI
                state.loadingProgress = (int)(loadState.Progress * 100);
                state.loadingStage = $"Loading {loadState.CurrentLoadingAsset} ({loadState.LoadedDependencies}/{loadState.TotalDependencies})";
            }
        }

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

            // Calculate camera distance using simple formula based on bounding sphere
            // This is more reliable than binary search for small models
            float fovDegrees = 60.0f;  // Standard FOV
            float fovRadians = fovDegrees * (float)Math.PI / 180.0f;
            float aspectRatio = (float)fb_width / (float)fb_height;

            // Use vertical FOV for calculation (account for aspect ratio if needed)
            float verticalFOV = fovRadians;
            
            // Simple formula: distance = radius / tan(fov/2)
            // For models with radius < 1.0, use tighter framing (likely miniature/detailed models)
            // For normal sized models (radius >= 1.0), use standard framing
            float paddingFactor = (sphereRadius < 1.0f) ? 0.8f : 1.1f;
            
            if (sphereRadius < 1.0f)
            {
                Info($"[Camera] Small model detected (radius={sphereRadius:F3}), using tight framing: padding={paddingFactor}");
            }
            
            float bestDistance = (sphereRadius * paddingFactor) / (float)Math.Tan(verticalFOV * 0.5f);
            
            // Clamp to reasonable range
            float minDistance = sphereRadius * 0.5f;
            float maxDistance = sphereRadius * 100.0f;
            bestDistance = Math.Clamp(bestDistance, minDistance, maxDistance);

            Info($"=== AUTO-POSITIONING CAMERA ===");
            Info($"Scene bounds: Min={sceneMin}, Max={sceneMax}");
            Info($"Scene size: {sceneSize}");
            Info($"Scene center: {sceneCenter}");
            Info($"Bounding sphere radius: {sphereRadius:F6}");
            Info($"Final distance: {bestDistance:F3}");
            Info($"Distance / Sphere Radius ratio: {bestDistance / sphereRadius:F2}");

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
        // Priority: Transmission > Bloom > Regular
        // Auto-detect if transmission is needed by checking if any mesh has transmission_factor > 0
        bool modelHasTransmission = state.modelLoaded && state.model != null && 
                                   state.model.Meshes.Any(m => m.TransmissionFactor > 0.0f);
        bool useTransmission = modelHasTransmission && state.transmission.screen_color_img.id != 0;
        bool useBloom = !useTransmission && state.enableBloom && state.modelLoaded && state.model != null && state.bloom.scene_color_img.id != 0;
        
        if (useTransmission)
        {
            // TRANSMISSION PASS 1: Render opaque objects to offscreen screen texture
            // This captures the background for transparent objects to refract
            sg_begin_pass(state.transmission.opaque_pass);
        }
        else if (useBloom)
        {
            // BLOOM PASS 1: Render scene to offscreen buffer
            sg_begin_pass(state.bloom.scene_pass);
        }
        else
        {
            // Regular rendering to swapchain
            sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        }

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

            // Reset culling and rendering statistics
            state.totalMeshes = 0;
            state.visibleMeshes = 0;
            state.culledMeshes = 0;
            state.totalVertices = 0;
            state.totalIndices = 0;
            state.totalFaces = 0;

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
                
                // Track rendering statistics
                state.totalVertices += mesh.VertexCount;
                state.totalIndices += mesh.IndexCount;
                state.totalFaces += mesh.IndexCount / 3;

                // Calculate distance to camera for sorting
                // Use the center of the mesh's bounding box
                BoundingBox worldBounds = mesh.Bounds.Transform(modelMatrix);
                Vector3 meshCenter = (worldBounds.Min + worldBounds.Max) * 0.5f;
                float distanceToCamera = Vector3.Distance(meshCenter, state.camera.EyePos);

                // Categorize as opaque or transparent
                // When transmission is enabled, use TransmissionFactor; otherwise use AlphaMode
                bool isTransparent = useTransmission ? (mesh.TransmissionFactor > 0.0f) : (mesh.AlphaMode == SharpGLTF.Schema2.AlphaMode.BLEND);
                
                if (isTransparent)
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
            // useScreenTexture: When true, bind the screen texture for refraction (transmission Pass 2)
            // renderToOffscreen: When true, use offscreen pipelines (transmission Pass 1 or bloom)
            void RenderNode(SharpGltfNode node, bool useScreenTexture = false, bool renderToOffscreen = false)
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
                
                // Check if mesh uses 32-bit indices (based on IndexType field)
                bool needs32BitIndices = (mesh.IndexType == sg_index_type.SG_INDEXTYPE_UINT32);

                // Choose pipeline based on alpha mode, skinning, index type, and rendering mode
                PipelineType pipelineType = PipeLineManager.GetPipelineTypeForMaterial(mesh.AlphaMode, useSkinning, needs32BitIndices);
                
                // Get appropriate pipeline based on rendering mode
                sg_pipeline pipeline;
                if (renderToOffscreen && useTransmission)
                {
                    // Rendering opaque object to transmission offscreen pass (Pass 1)
                    pipeline = useSkinning ? state.transmission.opaque_skinned_pipeline : state.transmission.opaque_standard_pipeline;
                }
                else if (useBloom)
                {
                    // Use offscreen pipeline for bloom scene pass
                    pipeline = pipelineType switch
                    {
                        PipelineType.Standard => state.bloom.scene_standard_pipeline,
                        PipelineType.Skinned => state.bloom.scene_skinned_pipeline,
                        PipelineType.StandardBlend => state.bloom.scene_standard_blend_pipeline,
                        PipelineType.SkinnedBlend => state.bloom.scene_skinned_blend_pipeline,
                        PipelineType.StandardMask => state.bloom.scene_standard_mask_pipeline,
                        PipelineType.SkinnedMask => state.bloom.scene_skinned_mask_pipeline,
                        _ => PipeLineManager.GetOrCreatePipeline(pipelineType)
                    };
                }
                else
                {
                    // Use regular swapchain pipeline
                    pipeline = PipeLineManager.GetOrCreatePipeline(pipelineType);
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

                    // Set alpha cutoff (0.0 for OPAQUE/BLEND, actual value for MASK)
                    metallicParams.alpha_cutoff = mesh.AlphaMode == SharpGLTF.Schema2.AlphaMode.MASK ? mesh.AlphaCutoff : 0.0f;

                    // Set emissive strength (KHR_materials_emissive_strength extension)
                    metallicParams.emissive_strength = mesh.EmissiveStrength;

                    // Get glass material values (with overrides if enabled)
                    var glassValues = GetGlassMaterialValues(mesh);
                    
                    // Set transmission parameters (KHR_materials_transmission extension)
                    metallicParams.transmission_factor = glassValues.transmission;
                    metallicParams.ior = glassValues.ior;

                    // Set volume absorption parameters (KHR_materials_volume extension - Beer's Law)
                    metallicParams.attenuation_color = glassValues.attenuationColor;
                    metallicParams.attenuation_distance = glassValues.attenuationDistance;
                    metallicParams.thickness_factor = glassValues.thickness;

                    // Set clearcoat parameters (KHR_materials_clearcoat extension)
                    metallicParams.clearcoat_factor = mesh.ClearcoatFactor;
                    metallicParams.clearcoat_roughness = mesh.ClearcoatRoughness;

                    // Set texture transform for normal map (KHR_texture_transform extension)
                    unsafe {
                        metallicParams.normal_tex_offset[0] = mesh.NormalTexOffset.X;
                        metallicParams.normal_tex_offset[1] = mesh.NormalTexOffset.Y;
                        metallicParams.normal_tex_scale[0] = mesh.NormalTexScale.X;
                        metallicParams.normal_tex_scale[1] = mesh.NormalTexScale.Y;
                    }
                    metallicParams.normal_tex_rotation = mesh.NormalTexRotation;
                    metallicParams.normal_map_scale = mesh.NormalMapScale;

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

                    // Set emissive strength (KHR_materials_emissive_strength extension)
                    metallicParams.emissive_strength = mesh.EmissiveStrength;

                    // Get glass material values (with overrides if enabled)
                    var glassValues = GetGlassMaterialValues(mesh);
                    
                    // Set transmission parameters (KHR_materials_transmission extension)
                    metallicParams.transmission_factor = glassValues.transmission;
                    metallicParams.ior = glassValues.ior;

                    // Set volume absorption parameters (KHR_materials_volume extension - Beer's Law)
                    metallicParams.attenuation_color = glassValues.attenuationColor;
                    metallicParams.attenuation_distance = glassValues.attenuationDistance;
                    metallicParams.thickness_factor = glassValues.thickness;

                    // Set clearcoat parameters (KHR_materials_clearcoat extension)
                    metallicParams.clearcoat_factor = mesh.ClearcoatFactor;
                    metallicParams.clearcoat_roughness = mesh.ClearcoatRoughness;

                    // Set texture transform for normal map (KHR_texture_transform extension)
                    unsafe {
                        metallicParams.normal_tex_offset[0] = mesh.NormalTexOffset.X;
                        metallicParams.normal_tex_offset[1] = mesh.NormalTexOffset.Y;
                        metallicParams.normal_tex_scale[0] = mesh.NormalTexScale.X;
                        metallicParams.normal_tex_scale[1] = mesh.NormalTexScale.Y;
                    }
                    metallicParams.normal_tex_rotation = mesh.NormalTexRotation;
                    metallicParams.normal_map_scale = mesh.NormalMapScale;

                    sg_apply_uniforms(UB_cgltf_metallic_params, SG_RANGE(ref metallicParams));

                    // Light uniforms
                    sg_apply_uniforms(UB_cgltf_light_params, SG_RANGE(ref lightParams));
                }

                // Draw the mesh with optional screen texture for refraction
                if (useScreenTexture)
                {
                    // Pass pre-created screen view for refraction sampling (transmission materials)
                    mesh.Draw(pipeline, state.transmission.screen_color_view, state.transmission.sampler);
                }
                else
                {
                    // Regular draw without screen texture
                    mesh.Draw(pipeline);
                }
            }

            // Render based on mode: Transmission / Bloom / Regular
            if (useTransmission)
            {
                // TRANSMISSION TWO-PASS RENDERING
                // Pass 1: Render opaque objects to offscreen texture (already in transmission.opaque_pass)
                // This captures the background for refraction sampling
                foreach (var (node, _) in opaqueNodes)
                {
                    RenderNode(node, useScreenTexture: false, renderToOffscreen: true);
                }
                
                // End opaque pass
                sg_end_pass();
                
                // Pass 2: Render scene to swapchain
                sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
                
                // Render opaque objects again to the actual screen (using regular swapchain pipelines)
                foreach (var (node, _) in opaqueNodes)
                {
                    RenderNode(node, useScreenTexture: false, renderToOffscreen: false);
                }
                
                // Render transparent objects with screen texture binding for refraction
                foreach (var (node, _) in transparentNodes)
                {
                    RenderNode(node, useScreenTexture: true, renderToOffscreen: false);
                }
            }
            else
            {
                // REGULAR RENDERING (Bloom or swapchain)
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
            }

            // Mark that we've logged mesh info
            if (shouldLogMeshInfo)
                _loggedMeshInfoOnce = true;
        }

        // Perform bloom post-processing if enabled
        if (state.enableBloom && state.modelLoaded && state.model != null && state.bloom.scene_color_img.id != 0)
        {
            // End the offscreen scene rendering pass
            sg_end_pass();
            
            // Perform bloom processing passes
            PerformBloomPasses(fb_width, fb_height);
            // After bloom, we're in the composite pass which renders to swapchain
            // Now render UI on top of the bloom composite
            DrawUI();
            simgui_render();
            sg_end_pass();
        }
        else
        {
            // No bloom - UI is rendered in the same pass as the model
            DrawUI();
            simgui_render();
            sg_end_pass();
        }

        sg_commit();

        _frameCount++;  // Increment frame counter
    }

    private static unsafe void PerformBloomPasses(int screenWidth, int screenHeight)
    {
        // Prepare bloom parameters
        cgltf_bloom_params_t bloomParams = new cgltf_bloom_params_t();
        bloomParams.brightness_threshold = state.bloomThreshold;
        bloomParams.bloom_intensity = state.bloomIntensity;
        bloomParams.texel_size[0] = 1.0f / (screenWidth / 2);  // Half resolution for blur
        bloomParams.texel_size[1] = 1.0f / (screenHeight / 2);

        // PASS 2: Bright pass - extract bright pixels
        sg_begin_pass(state.bloom.bright_pass);
        sg_apply_pipeline(state.bloom.bright_pipeline);
        sg_apply_bindings(state.bloom.bright_bindings);
        sg_apply_uniforms(UB_cgltf_bloom_params, SG_RANGE(ref bloomParams));
        sg_draw(0, 3, 1);  // Fullscreen triangle
        sg_end_pass();

        // PASS 3: Horizontal blur
        sg_begin_pass(state.bloom.blur_h_pass);
        sg_apply_pipeline(state.bloom.blur_h_pipeline);
        sg_apply_bindings(state.bloom.blur_h_bindings);
        sg_apply_uniforms(UB_cgltf_bloom_params, SG_RANGE(ref bloomParams));
        sg_draw(0, 3, 1);  // Fullscreen triangle
        sg_end_pass();

        // PASS 4: Vertical blur
        sg_begin_pass(state.bloom.blur_v_pass);
        sg_apply_pipeline(state.bloom.blur_v_pipeline);
        sg_apply_bindings(state.bloom.blur_v_bindings);
        sg_apply_uniforms(UB_cgltf_bloom_params, SG_RANGE(ref bloomParams));
        sg_draw(0, 3, 1);  // Fullscreen triangle
        sg_end_pass();

        // PASS 5: Composite bloom with scene (to swapchain)
        // Must create pass with current swapchain each frame (can't cache it)
        sg_begin_pass(new sg_pass
        {
            action = new sg_pass_action
            {
                colors = {
                    [0] = new sg_color_attachment_action
                    {
                        load_action = sg_load_action.SG_LOADACTION_CLEAR,
                        clear_value = new sg_color { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f }
                    }
                }
            },
            swapchain = sglue_swapchain()
        });
        sg_apply_pipeline(state.bloom.composite_pipeline);
        sg_apply_bindings(state.bloom.composite_bindings);
        sg_apply_uniforms(UB_cgltf_bloom_params, SG_RANGE(ref bloomParams));
        sg_draw(0, 3, 1);  // Fullscreen triangle
        // Don't end pass here - continue with UI rendering on same pass
    }
}