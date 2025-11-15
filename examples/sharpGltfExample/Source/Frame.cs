using Sokol;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_cull_mode;
using static Sokol.Utils;
using static Sokol.SLog;
using static Sokol.SImgui;
using static pbr_shader_cs.Shaders;
using static bloom_shader_cs.Shaders;

public static unsafe partial class SharpGLTFApp
{
    // Debug counter for morph weight logging
    private static int morphWeightLogCount = 0;
    
    /// <summary>
    /// Load IBL environment from glTF model if available.
    /// Called after model is fully loaded.
    /// </summary>
    static void LoadIBLFromModel(SharpGLTF.Schema2.ModelRoot? modelRoot)
    {
        if (modelRoot == null)
            return;

        try
        {
            // Try to load IBL from the model (only if glTF has IBL extension)
            var newEnvironmentMap = EnvironmentMapLoader.LoadFromGltfOrCreateTest(modelRoot, "model-environment");
            
            if (newEnvironmentMap != null && newEnvironmentMap.IsLoaded)
            {
                // Dispose old environment map
                state.environmentMap?.Dispose();
                
                // Update with new environment map
                state.environmentMap = newEnvironmentMap;
                
                Info($"[IBL] Updated environment map from model");
                Info($"[IBL]   - Mip count: {state.environmentMap.MipCount}");
                Info($"[IBL]   - Intensity: {state.iblIntensity}");
            }
            else
            {
                // Keep existing HDR environment if model doesn't have IBL
                Info($"[IBL] Model has no IBL, keeping existing environment map");
            }
        }
        catch (Exception ex)
        {
            Warning($"[IBL] Failed to load IBL from model: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Set up default scene lights when model has no punctual lights.
    /// </summary>
    static void SetupDefaultLights()
    {
        state.lights.Clear();
        
        // Light 1: Main directional light
        state.lights.Add(Light.CreateDirectionalLight(
            new Vector3(-0.5f, 0.3f, -0.3f),
            new Vector3(1.0f, 0.95f, 0.85f),
            1f
        ));

        // Light 2: Fill light
        state.lights.Add(Light.CreateDirectionalLight(
            new Vector3(0.5f, -0.3f, 0.3f),
            new Vector3(1.0f, 1f, 1f),
            1f
        ));

        // Light 3: Point light
        state.lights.Add(Light.CreatePointLight(
            new Vector3(0.0f, 15.0f, 0.0f),
            new Vector3(1.0f, 0.9f, 0.8f),
            2.0f,      // intensity
            100.0f     // range
        ));

        // Light 4: Back light
        state.lights.Add(Light.CreateDirectionalLight(
            new Vector3(0.2f, 0.1f, 0.8f),
            new Vector3(0.8f, 0.85f, 1.0f),
            0.5f
        ));
        
        // Reset ambient to default
        state.ambientStrength = 0.8f;
        
        Info($"[Lights] Set up {state.lights.Count} default scene lights");
    }
    
    /// <summary>
    /// Load punctual lights from glTF model (KHR_lights_punctual extension).
    /// Called after model is fully loaded.
    /// </summary>
    static void LoadLightsFromModel(SharpGLTF.Schema2.ModelRoot? modelRoot)
    {
        if (modelRoot == null)
            return;

        try
        {
            var punctualLights = modelRoot.LogicalPunctualLights;
            
            // Always clear existing lights when loading a new model
            state.lights.Clear();
            state.lightNodes.Clear();
            
            if (punctualLights == null || punctualLights.Count == 0)
            {
                Info($"[Lights] Model has no punctual lights - using default scene lights");
                SetupDefaultLights();
                return;
            }

            Info($"[Lights] Found {punctualLights.Count} punctual lights in model");

            // Calculate how many lights we can fit from the model
            int availableSlots = RenderingConstants.MAX_LIGHTS;
            int modelLightsToLoad = Math.Min(punctualLights.Count, availableSlots);

            // increase ambient light significantly so model lights are visible
            state.ambientStrength = 1f;
            
            Info($"[Lights] Loading {modelLightsToLoad} model lights (max: {availableSlots})");
            Info($"[Lights] Reduced ambient strength to {state.ambientStrength} to make point lights visible");

            // Store references to light nodes for animation updates
            state.lightNodes.Clear();

            // Find nodes with lights attached and create Light instances
            foreach (var node in modelRoot.LogicalNodes)
            {
                var punctualLight = node.PunctualLight;
                if (punctualLight == null)
                    continue;

                // Get light properties
                var lightType = punctualLight.LightType;
                var color = new Vector3(punctualLight.Color.X, punctualLight.Color.Y, punctualLight.Color.Z);
                float intensity = punctualLight.Intensity;
                float range = punctualLight.Range; // Already a float, includes default of PositiveInfinity
                if (float.IsInfinity(range) || range <= 0)
                {
                    // Set a default range for lights without a specified range
                    range = 1.0f;
                }
                
                // Boost intensity MASSIVELY for very dim lights (like fireflies at 0.05)
                // Many glTF lights are authored for physically-based renderers and need significant boosting
                float intensityBoost = 100.0f; // Aggressive boost for visibility
                float originalIntensity = intensity;
                intensity *= intensityBoost;
                Info($"[Lights] Boosted intensity from {originalIntensity} to {intensity} (boost: {intensityBoost}x)");

                // Get world transform for the light node
                var worldTransform = node.WorldMatrix;
                var position = new Vector3(worldTransform.M41, worldTransform.M42, worldTransform.M43);
                var direction = Vector3.TransformNormal(new Vector3(0, 0, -1), worldTransform);
                direction = Vector3.Normalize(direction);

                // Create Light object based on type
                Light light;
                switch (lightType)
                {
                    case SharpGLTF.Schema2.PunctualLightType.Point:
                        light = Light.CreatePointLight(position, color, intensity, range);
                        Info($"[Lights] Created point light: {node.Name ?? "unnamed"} at {position}, color={color}, intensity={intensity}, range={range}");
                        break;

                    case SharpGLTF.Schema2.PunctualLightType.Directional:
                        light = Light.CreateDirectionalLight(direction, color, intensity);
                        Info($"[Lights] Created directional light: {node.Name ?? "unnamed"} dir={direction}, color={color}, intensity={intensity}");
                        break;

                    case SharpGLTF.Schema2.PunctualLightType.Spot:
                        float innerConeAngle = (float)(punctualLight.InnerConeAngle * 180.0 / Math.PI);
                        float outerConeAngle = (float)(punctualLight.OuterConeAngle * 180.0 / Math.PI);
                        light = Light.CreateSpotLight(position, direction, color, intensity, range, innerConeAngle, outerConeAngle);
                        Info($"[Lights] Created spot light: {node.Name ?? "unnamed"} at {position}, dir={direction}, color={color}, intensity={intensity}, range={range}");
                        break;

                    default:
                        Warning($"[Lights] Unknown light type: {lightType}");
                        continue;
                }

                // Check if we've reached the maximum number of lights
                if (state.lights.Count >= RenderingConstants.MAX_LIGHTS)
                {
                    Warning($"[Lights] Maximum light count ({RenderingConstants.MAX_LIGHTS}) reached. Skipping remaining lights from model.");
                    Warning($"[Lights] To increase light count, update MAX_LIGHTS in RenderingConstants.cs and pbr_fs_uniforms.glsl, then recompile shaders.");
                    break;
                }

                // Add to lights list
                state.lights.Add(light);

                // Find the corresponding SharpGltfNode wrapper for animation updates
                // We need the wrapper because that's what gets updated by the animator
                // Match by node name since SharpGltfNode stores the original glTF node name
                
                // Debug: Log all available node names for troubleshooting
                Info($"[Lights] Looking for wrapper node '{node.Name}' among {state.model?.Nodes.Count ?? 0} nodes");
                if (state.model != null && state.model.Nodes.Count > 0)
                {
                    Info($"[Lights] Available node names: {string.Join(", ", state.model.Nodes.Where(n => n.NodeName != null).Select(n => $"'{n.NodeName}'"))}");
                }
                
                var wrapperNode = state.model?.Nodes.FirstOrDefault(n => n.NodeName == node.Name);
                if (wrapperNode != null)
                {
                    // Store wrapper node reference for animation updates
                    state.lightNodes.Add((wrapperNode, state.lights.Count - 1));
                    Info($"[Lights] Registered light node '{node.Name}' (wrapper found) for animation updates");
                }
                else
                {
                    Warning($"[Lights] Could not find wrapper node for light '{node.Name}' - light will not animate");
                }
            }

            Info($"[Lights] Loaded {state.lightNodes.Count} animated light nodes from model (Total lights: {state.lights.Count}/{RenderingConstants.MAX_LIGHTS})");
            
            // Log active light configuration for debugging
            Info($"[Lights] Active lights breakdown:");
            for (int i = 0; i < state.lights.Count; i++)
            {
                var light = state.lights[i];
                Info($"[Lights]   Light {i}: Type={light.Type}, Enabled={light.Enabled}, Pos={light.Position}, Color={light.Color}, Intensity={light.Intensity}, Range={light.Range}");
            }
            Info($"[Lights] Ambient strength: {state.ambientStrength}");
        }
        catch (Exception ex)
        {
            Warning($"[Lights] Failed to load lights from model: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Update light positions from animated nodes.
    /// Called every frame when animation is active.
    /// </summary>
    static void UpdateLightPositions()
    {
        if (state.lightNodes == null || state.lightNodes.Count == 0)
            return;

        foreach (var (node, lightIndex) in state.lightNodes)
        {
            if (lightIndex >= state.lights.Count)
                continue;

            var light = state.lights[lightIndex];
            
            // Get current world transform for the node (WorldTransform on SharpGltfNode)
            var worldTransform = node.WorldTransform;
            var position = new Vector3(worldTransform.M41, worldTransform.M42, worldTransform.M43);
            var direction = Vector3.TransformNormal(new Vector3(0, 0, -1), worldTransform);
            direction = Vector3.Normalize(direction);

            // Update light position/direction based on type
            if (light.Type == LightType.Point)
            {
                light.Position = position;
            }
            else if (light.Type == LightType.Directional)
            {
                light.Direction = direction;
            }
            else if (light.Type == LightType.Spot)
            {
                light.Position = position;
                light.Direction = direction;
            }
        }
    }
    
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
                
                // Create the model wrapper first (needed for CalculateModelBounds)
                state.model = new SharpGltfModel(modelRoot, state.pendingModelPath!);
                
                // Calculate model bounds using the model's method
                state.modelBounds = state.model.CalculateModelBounds();

                Vector3 size = state.modelBounds.Size;
                Vector3 center = state.modelBounds.Center;
                float boundingRadius = state.modelBounds.Radius;

                Info($"[SharpGLTF] Model bounds: Min={state.modelBounds.Min}, Max={state.modelBounds.Max}");
                Info($"[SharpGLTF] Model size: {size}, Center: {center}");
                Info($"[SharpGLTF] Bounding sphere radius: {boundingRadius:F6}");

                // Log if bounds seem unusually large
                if (boundingRadius > 1000.0f)
                {
                    Info($"[SharpGLTF] WARNING: Very large bounding radius detected!");
                    float clampedRadius = Math.Min(boundingRadius, 10.0f);
                    state.modelBounds = new BoundingBox(
                        center - new Vector3(clampedRadius),
                        center + new Vector3(clampedRadius)
                    );
                    Info($"[SharpGLTF] Clamped bounds: Min={state.modelBounds.Min}, Max={state.modelBounds.Max}");
                }

                // Safety check: if bounds are invalid or too small, use defaults
                if (float.IsInfinity(size.X) || float.IsNaN(size.X) || size.Length() < 0.01f)
                {
                    Info("[SharpGLTF] Warning: Invalid bounds detected, using defaults");
                    state.modelBounds = new BoundingBox(
                        new Vector3(-1, 0, -1),
                        new Vector3(1, 2, 1)
                    );
                }

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
                    state.animator = new SharpGltfAnimator(state.model);
                    state.ui.animation_open = true;
                    Info("[SharpGLTF] Animator created for animated model");
                    
                    // Create joint matrix texture only for texture-based skinning mode
                    if (state.skinningMode == SkinningMode.TextureBased && state.model.BoneCounter > 0)
                    {
                        CreateJointMatrixTexture(state.model.BoneCounter);
                        Info($"[Skinning] Using TEXTURE-BASED skinning ({state.model.BoneCounter} bones)");
                    }
                    else if (state.model.BoneCounter > 0)
                    {
                        Info($"[Skinning] Using UNIFORM-BASED skinning ({state.model.BoneCounter} bones, max 85)");
                        if (state.model.BoneCounter > AnimationConstants.MAX_BONES)
                        {
                            Warning($"[Skinning] Model has {state.model.BoneCounter} bones but uniform-based skinning only supports {AnimationConstants.MAX_BONES}. Consider switching to texture-based mode.");
                        }
                    }
                }
                else
                {
                    state.ui.animation_open = false;
                    Info("[SharpGLTF] No animations found in model");
                }

                // Create morph target texture if model has morph targets
                bool hasAnyMorphTargets = state.model.Meshes.Any(m => m.HasMorphTargets);
                if (hasAnyMorphTargets)
                {
                    CreateMorphTargetTexture(state.model);
                }

                // Try to load IBL from glTF if available
                LoadIBLFromModel(state.pendingModelRoot);

                // Try to load punctual lights from glTF if available
                LoadLightsFromModel(state.pendingModelRoot);

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

            var sphereRadius = state.modelBounds.Radius;
            if (state.isMixamoModel && sphereRadius < 0.1f)
            {
                sphereRadius *= 100;
            }
            
            float bestDistance = (sphereRadius * 1.1f) / (float)Math.Tan(verticalFOV * 0.5f);
            
            // Clamp to reasonable range
            float minDistance = sphereRadius * 0.5f;
            float maxDistance = sphereRadius * 100.0f;
            bestDistance = Math.Clamp(bestDistance, minDistance, maxDistance);

            Info($"=== AUTO-POSITIONING CAMERA ===");
            Info($"Scene bounds: Min={state.modelBounds.Min}, Max={state.modelBounds.Max}");
            Info($"Scene size: {state.modelBounds.Size}");
            Info($"Scene center: {state.modelBounds.Center}");
            Info($"Bounding sphere radius: {sphereRadius:F6}");
            Info($"Final distance: {bestDistance:F3}");
            Info($"Distance / Sphere Radius ratio: {bestDistance / sphereRadius:F2}");

            // Calculate appropriate NearZ and FarZ based on model radius
            // Larger models need larger NearZ to avoid Z-fighting
            // Rule of thumb: NearZ should be roughly 0.1% to 1% of the scene radius
            // FarZ should be large enough to contain the entire scene
            float modelRadius = state.modelBounds.Radius;
            if (state.isMixamoModel && modelRadius < 0.1f)
            {
                modelRadius *= 100.0f; // Account for Mixamo scale
            }

            // Scale NearZ based on model size
            // Small models (< 1): use tight near plane (0.001 to 0.01)
            // Medium models (1-100): scale proportionally (0.01 to 1.0)
            // Large models (> 100): scale proportionally (1.0+)
            float nearZ = Math.Max(0.001f, modelRadius * 0.01f);
            
            // FarZ should be at least 10x the distance from camera to furthest point
            // Distance to furthest point = bestDistance + modelRadius
            float farZ = Math.Max(100.0f, (bestDistance + modelRadius) * 10.0f);
            
            Info($"Camera NearZ: {nearZ:F6}, FarZ: {farZ:F2}");

            state.camera.Init(new CameraDesc()
            {
                Aspect = 60.0f,
                NearZ = nearZ,
                FarZ = farZ,
                Center = new Vector3(0.0f, 1.0f, 0.0f),
                Distance = 3.0f,
                Latitude = 10.0f,
                Longitude = 0.0f,
            });

            if (state.isMixamoModel && state.modelBounds.Radius < 0.1f)
            {
                state.camera.Center = state.modelBounds.Center * 100.0f + new Vector3(0, 1, 0);
            }
             else{
                state.camera.Center = state.modelBounds.Center;
             }
            
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
            state.animator.UpdateAnimation(deltaTime);
            
            // Create joint texture if switching to texture-based mode and texture doesn't exist
            if (state.skinningMode == SkinningMode.TextureBased && 
                state.jointMatrixTexture.id == 0 && 
                state.model != null && 
                state.model.BoneCounter > 0)
            {
                CreateJointMatrixTexture(state.model.BoneCounter);
                Info($"[Skinning] Switched to TEXTURE-BASED skinning ({state.model.BoneCounter} bones)");
            }
            
            // PERFORMANCE: Only update joint texture for texture-based skinning mode
            // Uniform-based skinning passes matrices directly via uniforms (no texture upload needed)
            if (state.skinningMode == SkinningMode.TextureBased && 
                state.jointMatrixTexture.id != 0 && 
                state.animator.GetCurrentAnimation() != null)
            {
                var boneMatrices = state.animator.GetFinalBoneMatrices();
                UpdateJointMatrixTexture(boneMatrices);
            }
            
            // Update light positions from animated nodes
            UpdateLightPositions();
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
            
            // Render skybox to offscreen pass if enabled
            if (state.renderEnvironmentMap && state.environmentMap != null && state.environmentMap.IsLoaded)
            {
                if (!state.skybox.IsInitialized)
                {
                    state.skybox.Initialize();
                }
                state.skybox.Render(state.camera, state.environmentMap, sapp_width(), sapp_height(), state.exposure, state.tonemapType, useOffscreenPipeline: true);
            }
        }
        else if (useBloom)
        {
            // BLOOM PASS 1: Render scene to offscreen buffer
            sg_begin_pass(state.bloom.scene_pass);
            
            // Render skybox if enabled
            if (state.renderEnvironmentMap && state.environmentMap != null && state.environmentMap.IsLoaded)
            {
                if (!state.skybox.IsInitialized)
                {
                    state.skybox.Initialize();
                }
                state.skybox.Render(state.camera, state.environmentMap, sapp_width(), sapp_height(), state.exposure, state.tonemapType, useOffscreenPipeline: true);
            }
        }
        else
        {
            // Regular rendering to swapchain
            sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
            
            // Render skybox if enabled
            if (state.renderEnvironmentMap && state.environmentMap != null && state.environmentMap.IsLoaded)
            {
                if (!state.skybox.IsInitialized)
                {
                    state.skybox.Initialize();
                }
                state.skybox.Render(state.camera, state.environmentMap, sapp_width(), sapp_height(), state.exposure, state.tonemapType, useOffscreenPipeline: false);
            }
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
            Vector3 modelCenter = (state.modelBounds.Min + state.modelBounds.Max) * 0.5f;

            // Create transform: translate to origin -> rotate -> translate back
            Matrix4x4 model = Matrix4x4.CreateTranslation(-modelCenter) *
                             modelRotation *
                             Matrix4x4.CreateTranslation(modelCenter);

            // Prepare fragment shader uniforms (lighting)
            // Build light parameters from the lights list
            light_params_t lightParams = new light_params_t();

            // Count enabled lights (max supported by shader defined in RenderingConstants.MAX_LIGHTS)
            int enabledLightCount = 0;
            foreach (var light in state.lights)
            {
                if (!light.Enabled || enabledLightCount >= RenderingConstants.MAX_LIGHTS)
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

            // Separate nodes into opaque, transparent (blend), and transmissive (glass) lists
            // This matches the glTF Sample Viewer's classification:
            // - opaqueNodes: alphaMode != BLEND and no transmission
            // - transparentNodes: alphaMode == BLEND and no transmission  
            // - transmissiveNodes: has transmission extension (regardless of alphaMode)
            List<(SharpGltfNode node, Matrix4x4 transform, float distance)> opaqueNodes = new List<(SharpGltfNode, Matrix4x4, float)>();
            List<(SharpGltfNode node, Matrix4x4 transform, float distance)> transparentNodes = new List<(SharpGltfNode, Matrix4x4, float)>();
            List<(SharpGltfNode node, Matrix4x4 transform, float distance)> transmissiveNodes = new List<(SharpGltfNode, Matrix4x4, float)>();
                
            // Collect and categorize all visible nodes
            foreach (var node in state.model.Nodes)
            {
                // Skip nodes without meshes (e.g., bone nodes, empty transforms)
                if (node.MeshIndex < 0 || node.MeshIndex >= state.model.Meshes.Count)
                    continue;

                var mesh = state.model.Meshes[node.MeshIndex];
                state.totalMeshes++;

                // Use the world transform from node hierarchy
                // For non-skinned animated nodes: updated by animator via SetLocalTransform()
                // For skinned nodes: stays at bind pose, animation handled by bone matrices
                // For static nodes: calculated from initial local TRS + parent hierarchy
                Matrix4x4 nodeTransform = node.WorldTransform;

                // Apply Mixamo-specific transforms if needed
                Matrix4x4 modelMatrix;
                if (state.isMixamoModel && state.modelBounds.Volume < 0.1)
                {
                    // Mixamo models exported from Blender have 0.01 scale and need rotation correction
                    var scaleMatrix = Matrix4x4.CreateScale(100.0f);
                    var rotationMatrix = Matrix4x4.CreateRotationX(-MathF.PI / 2.0f);
                    modelMatrix = nodeTransform * scaleMatrix * rotationMatrix * model;
                }
                else
                {
                    // Both animated and static nodes use the same transform
                    // nodeTransform is the world transform (calculated through hierarchy)
                    // which is in model-local space and needs the user's model transform applied
                    modelMatrix = nodeTransform * model;
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

                // Categorize nodes according to glTF Sample Viewer logic:
                // - transmissiveNodes: has KHR_materials_transmission (regardless of alphaMode)
                // - transparentNodes: alphaMode == BLEND but no transmission
                // - opaqueNodes: everything else (alphaMode != BLEND and no transmission)
                if (mesh.TransmissionFactor > 0.0f)
                {
                    // Has transmission extension - render separately with transmission shader
                    transmissiveNodes.Add((node, modelMatrix, distanceToCamera));
                }
                else if (mesh.AlphaMode == SharpGLTF.Schema2.AlphaMode.BLEND)
                {
                    // Regular alpha blending without transmission
                    transparentNodes.Add((node, modelMatrix, distanceToCamera));
                }
                else
                {
                    // Opaque or masked - no special handling needed
                    opaqueNodes.Add((node, modelMatrix, distanceToCamera));
                }
            }

            // Sort back-to-front for proper alpha blending
            transparentNodes.Sort((a, b) => b.distance.CompareTo(a.distance));
            transmissiveNodes.Sort((a, b) => b.distance.CompareTo(a.distance));

            // Helper function to render a node
            // modelMatrix: Pre-calculated transform matrix (includes node transform + global rotation + animation)
            // useScreenTexture: When true, bind the screen texture for refraction (transmission Pass 2)
            // renderToOffscreen: When true, use offscreen pipelines (transmission Pass 1 or bloom)
            void RenderNode(SharpGltfNode node, Matrix4x4 modelMatrix, bool useScreenTexture = false, bool renderToOffscreen = false)
            {
                var mesh = state.model.Meshes[node.MeshIndex];

                // Use skinning if mesh has it and animator exists
                bool useSkinning = mesh.HasSkinning && state.animator != null;
                bool useMorphing = mesh.HasMorphTargets;
                
                // Check if mesh uses 32-bit indices (based on IndexType field)
                bool needs32BitIndices = (mesh.IndexType == sg_index_type.SG_INDEXTYPE_UINT32);

                // Choose pipeline based on alpha mode, skinning, morphing, index type, and rendering mode
                PipelineType pipelineType = PipeLineManager.GetPipelineTypeForMaterial(mesh.AlphaMode, useSkinning, useMorphing, needs32BitIndices);
                
                // Override cull mode for double-sided materials
                sg_cull_mode cullMode = mesh.DoubleSided ? SG_CULLMODE_NONE : SG_CULLMODE_BACK;
                
                // Get appropriate pipeline based on rendering mode
                sg_pipeline pipeline;
                if ((renderToOffscreen || useScreenTexture) && useTransmission)
                {
                    // Rendering with transmission shaders (Pass 1: opaque to offscreen, Pass 2: transparent with refraction)
                    // For materials with transmission, use transmission pipeline variant matching the alpha mode
                    PipelineType transmissionPipelineType = pipelineType switch
                    {
                        // Standard opaque
                        PipelineType.Standard => PipelineType.Transmission,
                        PipelineType.Standard32 => PipelineType.Transmission32,
                        // Skinned opaque
                        PipelineType.Skinned => PipelineType.TransmissionSkinned,
                        PipelineType.Skinned32 => PipelineType.TransmissionSkinned32,
                        // Morphing opaque
                        PipelineType.Morphing => PipelineType.TransmissionMorphing,
                        PipelineType.Morphing32 => PipelineType.TransmissionMorphing32,
                        // Skinned + Morphing opaque
                        PipelineType.SkinnedMorphing => PipelineType.TransmissionSkinnedMorphing,
                        PipelineType.SkinnedMorphing32 => PipelineType.TransmissionSkinnedMorphing32,
                        
                        // Blend variants
                        PipelineType.StandardBlend => PipelineType.TransmissionBlend,
                        PipelineType.StandardBlend32 => PipelineType.TransmissionBlend32,
                        PipelineType.SkinnedBlend => PipelineType.TransmissionSkinnedBlend,
                        PipelineType.SkinnedBlend32 => PipelineType.TransmissionSkinnedBlend32,
                        PipelineType.MorphingBlend => PipelineType.TransmissionMorphingBlend,
                        PipelineType.MorphingBlend32 => PipelineType.TransmissionMorphingBlend32,
                        PipelineType.SkinnedMorphingBlend => PipelineType.TransmissionSkinnedMorphingBlend,
                        PipelineType.SkinnedMorphingBlend32 => PipelineType.TransmissionSkinnedMorphingBlend32,
                        
                        // Mask variants
                        PipelineType.StandardMask => PipelineType.TransmissionMask,
                        PipelineType.StandardMask32 => PipelineType.TransmissionMask32,
                        PipelineType.SkinnedMask => PipelineType.TransmissionSkinnedMask,
                        PipelineType.SkinnedMask32 => PipelineType.TransmissionSkinnedMask32,
                        PipelineType.MorphingMask => PipelineType.TransmissionMorphingMask,
                        PipelineType.MorphingMask32 => PipelineType.TransmissionMorphingMask32,
                        PipelineType.SkinnedMorphingMask => PipelineType.TransmissionSkinnedMorphingMask,
                        PipelineType.SkinnedMorphingMask32 => PipelineType.TransmissionSkinnedMorphingMask32,
                        
                        _ => PipelineType.Transmission  // Fallback
                    };
                    // Use offscreen format for Pass 1, swapchain format for Pass 2
                    if (renderToOffscreen)
                    {
                        pipeline = PipeLineManager.GetOrCreatePipeline(transmissionPipelineType, cullMode, sg_pixel_format.SG_PIXELFORMAT_RGBA8, sg_pixel_format.SG_PIXELFORMAT_DEPTH, 1);
                    }
                    else
                    {
                        pipeline = PipeLineManager.GetOrCreatePipeline(transmissionPipelineType, cullMode);
                    }
                }
                else if (useBloom)
                {
                    // Use offscreen pipeline for bloom scene pass
                    pipeline = pipelineType switch
                    {
                        PipelineType.Standard => state.bloom.scene_standard_pipeline,
                        PipelineType.Skinned => state.bloom.scene_skinned_pipeline,
                        PipelineType.Morphing => state.bloom.scene_morphing_pipeline,
                        PipelineType.SkinnedMorphing => state.bloom.scene_skinned_morphing_pipeline,
                        PipelineType.StandardBlend => state.bloom.scene_standard_blend_pipeline,
                        PipelineType.SkinnedBlend => state.bloom.scene_skinned_blend_pipeline,
                        PipelineType.MorphingBlend => state.bloom.scene_morphing_blend_pipeline,
                        PipelineType.SkinnedMorphingBlend => state.bloom.scene_skinned_morphing_blend_pipeline,
                        PipelineType.StandardMask => state.bloom.scene_standard_mask_pipeline,
                        PipelineType.SkinnedMask => state.bloom.scene_skinned_mask_pipeline,
                        PipelineType.MorphingMask => state.bloom.scene_morphing_mask_pipeline,
                        PipelineType.SkinnedMorphingMask => state.bloom.scene_skinned_morphing_mask_pipeline,
                        _ => PipeLineManager.GetOrCreatePipeline(pipelineType, cullMode, sg_pixel_format.SG_PIXELFORMAT_RGBA8, sg_pixel_format.SG_PIXELFORMAT_DEPTH, 1)
                    };
                }
                else
                {
                    // Use regular swapchain pipeline with appropriate cull mode
                    pipeline = PipeLineManager.GetOrCreatePipeline(pipelineType, cullMode);
                }

                // Route to appropriate specialized renderer based on mesh features
                if (useSkinning && useMorphing)
                {
                    // Skinned + morphing mesh - use pbr-shader-skinning-morphing.cs
                    RenderSkinnedMorphingMesh(mesh, node, modelMatrix, pipeline, lightParams, useScreenTexture);
                }
                else if (useSkinning)
                {
                    // Skinned mesh (without morphing) - use pbr-shader-skinning.cs
                    RenderSkinnedMesh(mesh, node, modelMatrix, pipeline, lightParams, useScreenTexture);
                }
                else if (useMorphing)
                {
                    // Morphing mesh without skinning - use pbr-shader-morphing.cs
                    RenderMorphingMesh(mesh, node, modelMatrix, pipeline, lightParams, useScreenTexture);
                }
                else
                {
                    // Static mesh (no skinning, no morphing) - use pbr-shader.cs
                    RenderStaticMesh(mesh, node, modelMatrix, pipeline, lightParams, useScreenTexture);
                }
            }

            // Helper function to render a node with specific cull mode override
            // Used for double-sided transmissive materials that need separate front/back face passes
            void RenderNodeWithCullMode(SharpGltfNode node, Matrix4x4 modelMatrix, sg_cull_mode forcedCullMode, bool useScreenTexture = false, bool renderToOffscreen = false)
            {
                var mesh = state.model.Meshes[node.MeshIndex];

                // Use skinning if mesh has it and animator exists
                bool useSkinning = mesh.HasSkinning && state.animator != null;
                bool useMorphing = mesh.HasMorphTargets;
                
                // Check if mesh uses 32-bit indices (based on IndexType field)
                bool needs32BitIndices = (mesh.IndexType == sg_index_type.SG_INDEXTYPE_UINT32);

                // For double-sided transmission materials rendering BACK FACES:
                // - If material is OPAQUE, force BLEND mode to enable alpha blending for back faces
                //   (front faces fully transparent alpha=0, back faces semi-transparent alpha=0.2)
                // - If material is already BLEND, keep it (handles "Transmission /w Opacity" correctly)
                var effectiveAlphaMode = mesh.AlphaMode;
                bool isRenderingBackFaces = (forcedCullMode == SG_CULLMODE_FRONT); // Culling front = rendering back
                if (mesh.DoubleSided && mesh.TransmissionFactor > 0.0f && isRenderingBackFaces && 
                    mesh.AlphaMode == SharpGLTF.Schema2.AlphaMode.OPAQUE)
                {
                    effectiveAlphaMode = SharpGLTF.Schema2.AlphaMode.BLEND;
                }

                // Choose pipeline based on alpha mode, skinning, morphing, index type, and rendering mode
                PipelineType pipelineType = PipeLineManager.GetPipelineTypeForMaterial(effectiveAlphaMode, useSkinning, useMorphing, needs32BitIndices);
                
                // Get appropriate pipeline based on rendering mode
                sg_pipeline pipeline;
                if ((renderToOffscreen || useScreenTexture) && useTransmission)
                {
                    // Rendering with transmission shaders (Pass 1: opaque to offscreen, Pass 2: transparent with refraction)
                    // For materials with transmission, use transmission pipeline variant matching the alpha mode
                    PipelineType transmissionPipelineType = pipelineType switch
                    {
                        // Standard opaque
                        PipelineType.Standard => PipelineType.Transmission,
                        PipelineType.Standard32 => PipelineType.Transmission32,
                        // Skinned opaque
                        PipelineType.Skinned => PipelineType.TransmissionSkinned,
                        PipelineType.Skinned32 => PipelineType.TransmissionSkinned32,
                        // Morphing opaque
                        PipelineType.Morphing => PipelineType.TransmissionMorphing,
                        PipelineType.Morphing32 => PipelineType.TransmissionMorphing32,
                        // Skinned + Morphing opaque
                        PipelineType.SkinnedMorphing => PipelineType.TransmissionSkinnedMorphing,
                        PipelineType.SkinnedMorphing32 => PipelineType.TransmissionSkinnedMorphing32,
                        
                        // Blend variants
                        PipelineType.StandardBlend => PipelineType.TransmissionBlend,
                        PipelineType.StandardBlend32 => PipelineType.TransmissionBlend32,
                        PipelineType.SkinnedBlend => PipelineType.TransmissionSkinnedBlend,
                        PipelineType.SkinnedBlend32 => PipelineType.TransmissionSkinnedBlend32,
                        PipelineType.MorphingBlend => PipelineType.TransmissionMorphingBlend,
                        PipelineType.MorphingBlend32 => PipelineType.TransmissionMorphingBlend32,
                        PipelineType.SkinnedMorphingBlend => PipelineType.TransmissionSkinnedMorphingBlend,
                        PipelineType.SkinnedMorphingBlend32 => PipelineType.TransmissionSkinnedMorphingBlend32,
                        
                        // Mask variants
                        PipelineType.StandardMask => PipelineType.TransmissionMask,
                        PipelineType.StandardMask32 => PipelineType.TransmissionMask32,
                        PipelineType.SkinnedMask => PipelineType.TransmissionSkinnedMask,
                        PipelineType.SkinnedMask32 => PipelineType.TransmissionSkinnedMask32,
                        PipelineType.MorphingMask => PipelineType.TransmissionMorphingMask,
                        PipelineType.MorphingMask32 => PipelineType.TransmissionMorphingMask32,
                        PipelineType.SkinnedMorphingMask => PipelineType.TransmissionSkinnedMorphingMask,
                        PipelineType.SkinnedMorphingMask32 => PipelineType.TransmissionSkinnedMorphingMask32,
                        
                        _ => PipelineType.Transmission  // Fallback
                    };
                    // Use forced cull mode instead of mesh.DoubleSided logic
                    // Use offscreen format for Pass 1, swapchain format for Pass 2
                    if (renderToOffscreen)
                    {
                        pipeline = PipeLineManager.GetOrCreatePipeline(transmissionPipelineType, forcedCullMode, sg_pixel_format.SG_PIXELFORMAT_RGBA8, sg_pixel_format.SG_PIXELFORMAT_DEPTH, 1);
                    }
                    else
                    {
                        pipeline = PipeLineManager.GetOrCreatePipeline(transmissionPipelineType, forcedCullMode);
                    }
                }
                else if (useBloom)
                {
                    // Bloom doesn't need special cull mode handling
                    PipelineType bloomPipelineType = pipelineType;
                    pipeline = PipeLineManager.GetOrCreatePipeline(bloomPipelineType, forcedCullMode, sg_pixel_format.SG_PIXELFORMAT_RGBA8, sg_pixel_format.SG_PIXELFORMAT_DEPTH, 1);
                }
                else
                {
                    // Use regular swapchain pipeline with forced cull mode
                    pipeline = PipeLineManager.GetOrCreatePipeline(pipelineType, forcedCullMode);
                }

                // Route to appropriate specialized renderer based on mesh features
                if (useSkinning && useMorphing)
                {
                    // Skinned + morphing mesh - use pbr-shader-skinning-morphing.cs
                    RenderSkinnedMorphingMesh(mesh, node, modelMatrix, pipeline, lightParams, useScreenTexture);
                }
                else if (useSkinning)
                {
                    // Skinned mesh (without morphing) - use pbr-shader-skinning.cs
                    RenderSkinnedMesh(mesh, node, modelMatrix, pipeline, lightParams, useScreenTexture);
                }
                else if (useMorphing)
                {
                    // Morphing mesh without skinning - use pbr-shader-morphing.cs
                    RenderMorphingMesh(mesh, node, modelMatrix, pipeline, lightParams, useScreenTexture);
                }
                else
                {
                    // Static mesh (no skinning, no morphing) - use pbr-shader.cs
                    RenderStaticMesh(mesh, node, modelMatrix, pipeline, lightParams, useScreenTexture);
                }
            }

            // Render based on mode: Transmission / Bloom / Regular
            if (useTransmission)
            {
                // TRANSMISSION TWO-PASS RENDERING (matches glTF Sample Viewer)
                // Pass 1: Render to offscreen texture for refraction sampling
                // Render opaque objects
                foreach (var (node, transform, _) in opaqueNodes)
                {
                    RenderNode(node, transform, useScreenTexture: false, renderToOffscreen: true);
                }
                
                // CRITICAL: Also render non-transmissive transparent objects (BLEND mode)
                // These are objects with alpha blending but NO transmission extension
                // Reference: glTF Sample Viewer renderer.js lines 541-551
                foreach (var (node, transform, _) in transparentNodes)
                {
                    RenderNode(node, transform, useScreenTexture: false, renderToOffscreen: true);
                }
                
                // End offscreen pass
                sg_end_pass();
                
                // Pass 2: Render scene to swapchain with refraction
                sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
                
                // Render skybox if enabled
                if (state.renderEnvironmentMap && state.environmentMap != null && state.environmentMap.IsLoaded)
                {
                    if (!state.skybox.IsInitialized)
                    {
                        state.skybox.Initialize();
                    }
                    state.skybox.Render(state.camera, state.environmentMap, sapp_width(), sapp_height(), state.exposure, state.tonemapType, useOffscreenPipeline: false);
                }
                
                // Render opaque objects to screen
                foreach (var (node, transform, _) in opaqueNodes)
                {
                    RenderNode(node, transform, useScreenTexture: false, renderToOffscreen: false);
                }
                
                // Render transmissive objects with screen texture for refraction
                // For double-sided materials, render in two passes: FRONT faces first, THEN back faces
                // This ensures back faces can sample the already-rendered front faces
                foreach (var (node, transform, _) in transmissiveNodes)
                {
                    var mesh = state.model.Meshes[node.MeshIndex];
                    if (mesh.DoubleSided)
                    {
                        // PASS 1: Render front faces first (with back face culling)  
                        RenderNodeWithCullMode(node, transform, SG_CULLMODE_BACK, useScreenTexture: true, renderToOffscreen: false);
                        // PASS 2: Render back faces after (with front face culling)
                        // Back faces can now sample the front faces that were just rendered
                        RenderNodeWithCullMode(node, transform, SG_CULLMODE_FRONT, useScreenTexture: true, renderToOffscreen: false);
                    }
                    else
                    {
                        // Single-sided: render normally
                        RenderNode(node, transform, useScreenTexture: true, renderToOffscreen: false);
                    }
                }
                
                // Render regular transparent objects (BLEND mode, no transmission)
                foreach (var (node, transform, _) in transparentNodes)
                {
                    RenderNode(node, transform, useScreenTexture: false, renderToOffscreen: false);
                }
            }
            else
            {
                // REGULAR RENDERING (Bloom or swapchain)
                // PASS 1: Render all opaque objects (no specific order needed)
                foreach (var (node, transform, _) in opaqueNodes)
                {
                    RenderNode(node, transform);
                }

                // PASS 2: Render all transparent objects (back-to-front order)
                foreach (var (node, transform, _) in transparentNodes)
                {
                    RenderNode(node, transform);
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
        var bloomParams = new bloom_params_t();
        bloomParams.brightness_threshold = state.bloomThreshold;
        bloomParams.bloom_intensity = state.bloomIntensity;
        bloomParams.texel_size[0] = 1.0f / (screenWidth / 2);  // Half resolution for blur
        bloomParams.texel_size[1] = 1.0f / (screenHeight / 2);

        // PASS 2: Bright pass - extract bright pixels
        sg_begin_pass(state.bloom.bright_pass);
        sg_apply_pipeline(state.bloom.bright_pipeline);
        sg_apply_bindings(state.bloom.bright_bindings);
        sg_apply_uniforms(UB_bloom_params, SG_RANGE(ref bloomParams));
        sg_draw(0, 3, 1);  // Fullscreen triangle
        sg_end_pass();

        // PASS 3: Horizontal blur
        sg_begin_pass(state.bloom.blur_h_pass);
        sg_apply_pipeline(state.bloom.blur_h_pipeline);
        sg_apply_bindings(state.bloom.blur_h_bindings);
        sg_apply_uniforms(UB_bloom_params, SG_RANGE(ref bloomParams));
        sg_draw(0, 3, 1);  // Fullscreen triangle
        sg_end_pass();

        // PASS 4: Vertical blur
        sg_begin_pass(state.bloom.blur_v_pass);
        sg_apply_pipeline(state.bloom.blur_v_pipeline);
        sg_apply_bindings(state.bloom.blur_v_bindings);
        sg_apply_uniforms(UB_bloom_params, SG_RANGE(ref bloomParams));
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
        sg_apply_uniforms(UB_bloom_params, SG_RANGE(ref bloomParams));
        sg_draw(0, 3, 1);  // Fullscreen triangle
        // Don't end pass here - continue with UI rendering on same pass
    }

     /// <summary>
    /// Updates the joint matrix texture with current bone matrices.
    /// Packs transform and normal matrices for each joint into RGBA32F format.
    /// </summary>
    static unsafe void UpdateJointMatrixTexture(Matrix4x4[] boneMatrices)
    {
        if (state.jointMatrixTexture.id == 0 || boneMatrices == null || boneMatrices.Length == 0)
        {
            return;
        }

        int jointCount = boneMatrices.Length;
        int width = state.jointTextureWidth;

        // Allocate float array: width² × 4 (RGBA)
        int texelCount = width * width;

        if (state.jointTextureData == null || state.jointTextureData.Length != texelCount * 4)
        {
            state.jointTextureData = new float[texelCount * 4];
        }

        // Initialize to zero
        Array.Clear(state.jointTextureData, 0, state.jointTextureData.Length);
        
        // Only update as many joints as we have space for
        int maxJoints = Math.Min(jointCount, texelCount / 8);
        for (int i = 0; i < maxJoints; i++)
        {
            Matrix4x4 jointMatrix = boneMatrices[i];
            
            // Store transform matrix at offset i*32 (4 vec4 = 16 floats)
            CopyMatrix4x4ToFloatArray(jointMatrix, state.jointTextureData, i * 32);
            
            // Store same matrix for normals at offset i*32 + 16 (uniform-based uses same matrix)
            // This matches the behavior of uniform-based skinning
            CopyMatrix4x4ToFloatArray(jointMatrix, state.jointTextureData, i * 32 + 16);
        }
        
        // Upload to GPU
        fixed (float* ptr = state.jointTextureData)
        {
            var imageData = new sg_image_data();
            imageData.mip_levels[0].ptr = ptr;
            imageData.mip_levels[0].size = (nuint)(state.jointTextureData.Length * sizeof(float));
            
            sg_update_image(state.jointMatrixTexture, in imageData);
        }
    }

     /// <summary>
    /// Creates a joint matrix texture for skinning animation.
    /// Each joint stores 2 matrices (transform + normal) = 32 floats = 8 vec4 (RGBA)
    /// </summary>
    static void CreateJointMatrixTexture(int jointCount)
    {
        if (jointCount <= 0)
        {
            Info("[JointTexture] No joints, skipping texture creation");
            return;
        }

        // Calculate texture size to hold all joint matrices
        // Each joint needs 2 mat4 (transform + normal) = 32 floats = 8 vec4 (RGBA)
        int width = (int)Math.Ceiling(Math.Sqrt(jointCount * 8));
        state.jointTextureWidth = width;
        
        Info($"[JointTexture] Creating {width}x{width} RGBA32F texture for {jointCount} joints");
        Info($"[JointTexture] Each joint uses 8 vec4 (32 floats): transform matrix at offset i*32, normal matrix at offset i*32+16");

        // Create sampler with NEAREST filtering and CLAMP_TO_EDGE wrapping
        if (state.jointMatrixSampler.id == 0)
        {
            state.jointMatrixSampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = sg_filter.SG_FILTER_NEAREST,
                mag_filter = sg_filter.SG_FILTER_NEAREST,
                wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                label = "joint-matrix-sampler"
            });
        }

        // Create texture with initial identity matrices
        int texelCount = width * width;
        
        // Create empty stream texture (no initial data allowed with stream_update)
        state.jointMatrixTexture = sg_make_image(new sg_image_desc
        {
            width = width,
            height = width,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA32F,
            usage = new sg_image_usage { stream_update = true }, // Allow per-frame updates
            label = "joint-matrix-texture"
        });
        
        // Create view once for the joint texture
        state.jointMatrixView = sg_make_view(new sg_view_desc
        {
            texture = new sg_texture_view_desc { image = state.jointMatrixTexture },
            label = "joint-matrix-view"
        });
        
        Info($"[JointTexture] Texture created successfully (id: {state.jointMatrixTexture.id}, view: {state.jointMatrixView.id})");
    }

   

    /// <summary>
    /// Copies a Matrix4x4 into a float array in ROW-MAJOR order for texture storage.
    /// Unlike uniforms which expect column-major, texture storage with texelFetch 
    /// expects row-major data because the shader reads vec4s as matrix rows.
    /// </summary>
    static void CopyMatrix4x4ToFloatArray(Matrix4x4 mat, float[] arr, int offset)
    {
        // Row-major order (don't transpose) - texelFetch reads vec4 as matrix rows
        // Store as: [M11,M12,M13,M14], [M21,M22,M23,M24], [M31,M32,M33,M34], [M41,M42,M43,M44]
        arr[offset + 0] = mat.M11; arr[offset + 1] = mat.M12; arr[offset + 2] = mat.M13; arr[offset + 3] = mat.M14;
        arr[offset + 4] = mat.M21; arr[offset + 5] = mat.M22; arr[offset + 6] = mat.M23; arr[offset + 7] = mat.M24;
        arr[offset + 8] = mat.M31; arr[offset + 9] = mat.M32; arr[offset + 10] = mat.M33; arr[offset + 11] = mat.M34;
        arr[offset + 12] = mat.M41; arr[offset + 13] = mat.M42; arr[offset + 14] = mat.M43; arr[offset + 15] = mat.M44;
    }

    /// <summary>
    /// Creates a morph target texture array for vertex displacement animation.
    /// Stores position, normal, and tangent displacements for each morph target.
    /// Uses texture2DArray with one layer per attribute per target.
    /// </summary>
    static unsafe void CreateMorphTargetTexture(SharpGltfModel model)
    {
        // Find the mesh with most morph targets to determine array size
        int maxTargets = 0;
        int maxVertices = 0;
        
        foreach (var mesh in model.Meshes)
        {
            if (mesh.HasMorphTargets && mesh.GltfPrimitive != null)
            {
                maxTargets = Math.Max(maxTargets, mesh.MorphTargetCount);
                maxVertices = Math.Max(maxVertices, mesh.VertexCount);
            }
        }
        
        if (maxTargets == 0 || maxVertices == 0)
        {
            Info("[MorphTexture] No morph targets found, skipping texture creation");
            return;
        }
        
        // Calculate texture size based on vertex count
        // Each vertex displacement is stored as vec4 (with padding for vec3 data)
        int width = (int)Math.Ceiling(Math.Sqrt(maxVertices));
        state.morphTextureWidth = width;
        
        // Calculate layer count: position, normal, tangent for each target
        // Layer layout: [pos0, pos1, ..., posN, norm0, norm1, ..., normN, tan0, tan1, ..., tanN]
        int layersPerAttributeType = maxTargets;
        int totalLayers = layersPerAttributeType * 3; // position + normal + tangent
        state.morphTextureLayerCount = totalLayers;
        
        Info($"[MorphTexture] Creating {width}x{width}x{totalLayers} RGBA32F texture array");
        Info($"[MorphTexture] {maxTargets} targets, {maxVertices} max vertices");
        Info($"[MorphTexture] Layer 0-{maxTargets-1}: positions, {maxTargets}-{maxTargets*2-1}: normals, {maxTargets*2}-{totalLayers-1}: tangents");
        
        // Create sampler with NEAREST filtering
        if (state.morphTargetSampler.id == 0)
        {
            state.morphTargetSampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = sg_filter.SG_FILTER_NEAREST,
                mag_filter = sg_filter.SG_FILTER_NEAREST,
                wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                label = "morph-target-sampler"
            });
        }
        
        // Allocate texture data
        int texelsPerLayer = width * width;
        int totalTexels = texelsPerLayer * totalLayers;
        float[] textureData = new float[totalTexels * 4]; // RGBA32F
        
        // Initialize to zero (no displacement by default)
        Array.Clear(textureData, 0, textureData.Length);
        
        // Process each mesh and populate its morph target data
        foreach (var mesh in model.Meshes)
        {
            if (!mesh.HasMorphTargets || mesh.GltfPrimitive == null)
                continue;
                
            var primitive = mesh.GltfPrimitive;
            int targetCount = primitive.MorphTargetsCount;
            
            Info($"[MorphTexture] Processing mesh with {targetCount} targets, {mesh.VertexCount} vertices");
            
            // Extract displacement data for each target
            for (int targetIdx = 0; targetIdx < targetCount; targetIdx++)
            {
                var morphTarget = primitive.GetMorphTargetAccessors(targetIdx);
                
                // Position displacements (layer = targetIdx)
                if (morphTarget.ContainsKey("POSITION"))
                {
                    var positions = morphTarget["POSITION"].AsVector3Array();
                    int layerOffset = targetIdx * texelsPerLayer * 4;
                    
                    for (int i = 0; i < positions.Count && i < mesh.VertexCount; i++)
                    {
                        int offset = layerOffset + i * 4;
                        textureData[offset + 0] = positions[i].X;
                        textureData[offset + 1] = positions[i].Y;
                        textureData[offset + 2] = positions[i].Z;
                        textureData[offset + 3] = 0.0f; // Padding
                    }
                }
                
                // Normal displacements (layer = maxTargets + targetIdx)
                if (morphTarget.ContainsKey("NORMAL"))
                {
                    var normals = morphTarget["NORMAL"].AsVector3Array();
                    int layerOffset = (maxTargets + targetIdx) * texelsPerLayer * 4;
                    
                    for (int i = 0; i < normals.Count && i < mesh.VertexCount; i++)
                    {
                        int offset = layerOffset + i * 4;
                        textureData[offset + 0] = normals[i].X;
                        textureData[offset + 1] = normals[i].Y;
                        textureData[offset + 2] = normals[i].Z;
                        textureData[offset + 3] = 0.0f; // Padding
                    }
                }
                
                // Tangent displacements (layer = maxTargets*2 + targetIdx)
                if (morphTarget.ContainsKey("TANGENT"))
                {
                    var tangents = morphTarget["TANGENT"].AsVector3Array();
                    int layerOffset = (maxTargets * 2 + targetIdx) * texelsPerLayer * 4;
                    
                    for (int i = 0; i < tangents.Count && i < mesh.VertexCount; i++)
                    {
                        int offset = layerOffset + i * 4;
                        textureData[offset + 0] = tangents[i].X;
                        textureData[offset + 1] = tangents[i].Y;
                        textureData[offset + 2] = tangents[i].Z;
                        textureData[offset + 3] = 0.0f; // Padding
                    }
                }
            }
        }
        
        // Create texture2DArray
        fixed (float* ptr = textureData)
        {
            var imageData = new sg_image_data();
            imageData.mip_levels[0].ptr = ptr;
            imageData.mip_levels[0].size = (nuint)(textureData.Length * sizeof(float));
            
            state.morphTargetTexture = sg_make_image(new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_ARRAY,
                width = width,
                height = width,
                num_slices = totalLayers,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA32F,
                data = imageData,
                label = "morph-target-texture"
            });
        }
        
        // Create view for the morph texture
        state.morphTargetView = sg_make_view(new sg_view_desc
        {
            texture = new sg_texture_view_desc { image = state.morphTargetTexture },
            label = "morph-target-view"
        });
        
        Info($"[MorphTexture] Texture created successfully (id: {state.morphTargetTexture.id}, view: {state.morphTargetView.id})");
    }
}