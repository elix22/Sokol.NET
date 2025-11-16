#nullable disable
using System;
using Sokol;
using System.Numerics;
using static Sokol.SApp;
using Imgui;
using static Imgui.ImguiNative;

public static unsafe partial class SharpGLTFApp
{
    static void DrawUI()
    {
        // Main menu bar
        if (igBeginMainMenuBar())
        {
            if (igBeginMenu("Windows", true))
            {
                byte model_info_open = state.ui.model_info_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Model Info...", null, ref model_info_open, true))
                {
                    state.ui.model_info_open = model_info_open != 0;
                }

                byte model_browser_open = state.ui.model_browser_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Model Browser...", null, ref model_browser_open, true))
                {
                    state.ui.model_browser_open = model_browser_open != 0;
                }

                byte animation_open = state.ui.animation_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Animation...", null, ref animation_open, true))
                {
                    state.ui.animation_open = animation_open != 0;
                }

                byte lighting_open = state.ui.lighting_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Lighting...", null, ref lighting_open, true))
                {
                    state.ui.lighting_open = lighting_open != 0;
                }

                byte bloom_open = state.ui.bloom_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Bloom...", null, ref bloom_open, true))
                {
                    state.ui.bloom_open = bloom_open != 0;
                }

                byte tonemap_open = state.ui.tonemap_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Tone Mapping...", null, ref tonemap_open, true))
                {
                    state.ui.tonemap_open = tonemap_open != 0;
                }

                byte glass_materials_open = state.ui.glass_materials_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Glass Materials...", null, ref glass_materials_open, true))
                {
                    state.ui.glass_materials_open = glass_materials_open != 0;
                }

                byte culling_open = state.ui.culling_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Culling...", null, ref culling_open, true))
                {
                    state.ui.culling_open = culling_open != 0;
                }

                byte statistics_open = state.ui.statistics_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Statistics...", null, ref statistics_open, true))
                {
                    state.ui.statistics_open = statistics_open != 0;
                }

                byte camera_info_open = state.ui.camera_info_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Camera Info...", null, ref camera_info_open, true))
                {
                    state.ui.camera_info_open = camera_info_open != 0;
                }

                byte camera_controls_open = state.ui.camera_controls_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Camera Controls...", null, ref camera_controls_open, true))
                {
                    state.ui.camera_controls_open = camera_controls_open != 0;
                }

                byte debug_view_open = state.ui.debug_view_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Debug View...", null, ref debug_view_open, true))
                {
                    state.ui.debug_view_open = debug_view_open != 0;
                }

                igEndMenu();
            }

            if (igBeginMenu("Options", true))
            {
                if (igRadioButton_IntPtr("Dark Theme", ref state.ui.theme, 0))
                {
                    igStyleColorsDark(null);
                }
                if (igRadioButton_IntPtr("Light Theme", ref state.ui.theme, 1))
                {
                    igStyleColorsLight(null);
                }
                if (igRadioButton_IntPtr("Classic Theme", ref state.ui.theme, 2))
                {
                    igStyleColorsClassic(null);
                }
                igEndMenu();
            }

            if (igBeginMenu("Help", true))
            {
                byte help_open = state.ui.help_open ? (byte)1 : (byte)0;
                if (igMenuItem_BoolPtr("Show Help...", null, ref help_open, true))
                {
                    state.ui.help_open = help_open != 0;
                }

                igEndMenu();
            }

            igEndMainMenuBar();
        }

        Vector2 pos = new Vector2(30, 60);

        // Model Info Window
        if (state.ui.model_info_open)
        {
            DrawModelInfoWindow(ref pos);
            pos.X += 20; pos.Y += 20;
        }

        // Model Browser Window
        if (state.ui.model_browser_open)
        {
            DrawModelBrowserWindow(ref pos);
            pos.X += 20; pos.Y += 20;
        }

        // Animation Window
        if (state.ui.animation_open)
        {
            DrawAnimationWindow(ref pos);
            pos.X += 20; pos.Y += 20;
        }

        // Lighting Window
        if (state.ui.lighting_open)
        {
            DrawLightingWindow(ref pos);
            pos.X += 20; pos.Y += 20;
        }

        // Bloom Window
        if (state.ui.bloom_open)
        {
            DrawBloomWindow(ref pos);
            pos.X += 20; pos.Y += 20;
        }

        // Tone Mapping Window
        if (state.ui.tonemap_open)
        {
            DrawTonemapWindow(ref pos);
            pos.X += 20; pos.Y += 20;
        }

        // Glass Materials Window
        if (state.ui.glass_materials_open)
        {
            DrawGlassMaterialsWindow(ref pos);
            pos.X += 20; pos.Y += 20;
        }

        // Culling Window
        if (state.ui.culling_open)
        {
            DrawCullingWindow(ref pos);
            pos.X += 20; pos.Y += 20;
        }

        // Statistics Window
        if (state.ui.statistics_open)
        {
            DrawStatisticsWindow(ref pos);
            pos.X += 20; pos.Y += 20;
        }

        // Camera Info Window
        if (state.ui.camera_info_open)
        {
            DrawCameraInfoWindow(ref pos);
            pos.X += 20; pos.Y += 20;
        }

        // Camera Controls Window
        if (state.ui.camera_controls_open)
        {
            DrawCameraControlsWindow(ref pos);
        }

        // Debug View Window
        if (state.ui.debug_view_open)
        {
            DrawDebugViewWindow(ref pos);
        }

        // Help Window
        if (state.ui.help_open)
        {
            DrawHelpWindow();
        }
    }

    static void DrawModelInfoWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(250, 180), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Model Info", ref open, ImGuiWindowFlags.None))
        {
            state.ui.model_info_open = open != 0;

            if (state.model != null)
            {
                igText($"File: {filename}");
                igText($"Meshes: {state.model.Meshes.Count}");
                igText($"Nodes: {state.model.Nodes.Count}");
                igText($"Bones: {state.model.BoneCounter}");
                
                igSeparator();
                igText("Model Rotation:");
                igText("Middle Mouse: Rotate");
                float rotationYDegrees = state.modelRotationY * 180.0f / MathF.PI;
                float rotationXDegrees = state.modelRotationX * 180.0f / MathF.PI;
                igText($"Y: {rotationYDegrees:F1}°");
                igText($"X: {rotationXDegrees:F1}°");

                if (igButton("Reset Rotation", Vector2.Zero))
                {
                    state.modelRotationY = 0.0f;
                    state.modelRotationX = 0.0f;
                }
            }
            else
            {
                igText("Loading model...");
            }
        }
        igEnd();
    }

    static void DrawModelBrowserWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(350, 150), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Model Browser", ref open, ImGuiWindowFlags.None))
        {
            state.ui.model_browser_open = open != 0;

            // Extract model name from path
            string currentModelPath = availableModels[state.currentModelIndex];
            string modelName = System.IO.Path.GetFileNameWithoutExtension(currentModelPath);
            
            igText($"Current Model: {modelName}");
            igText($"Model {state.currentModelIndex + 1} of {availableModels.Length}");
            
            igSeparator();

            // Previous button
            if (igButton("<- Previous", new Vector2(160, 0)))
            {
                if (!state.isLoadingModel)
                {
                    state.currentModelIndex--;
                    if (state.currentModelIndex < 0)
                        state.currentModelIndex = availableModels.Length - 1;
                    LoadNewModel();
                }
            }

            igSameLine(0, 10);

            // Next button
            if (igButton("Next ->", new Vector2(160, 0)))
            {
                if (!state.isLoadingModel)
                {
                    state.currentModelIndex++;
                    if (state.currentModelIndex >= availableModels.Length)
                        state.currentModelIndex = 0;
                    LoadNewModel();
                }
            }

            if (state.isLoadingModel)
            {
                igSeparator();
                igText($"Loading: {state.loadingStage}");
                igText($"Progress: {state.loadingProgress}%");
                
                // Progress bar
                float progress = state.loadingProgress / 100.0f;
                igProgressBar(progress, new Vector2(-1, 0), null);
            }
        }
        igEnd();
    }

    static void DrawAnimationWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(250, 200), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Animation", ref open, ImGuiWindowFlags.None))
        {
            state.ui.animation_open = open != 0;

            // Check if we have animations - either via characters or legacy animator
            bool hasAnimations = (state.model != null && state.model.HasAnimations && state.model.Animations.Count > 0);
            bool hasAnimator = hasAnimations && ((state.model.Characters.Count > 0) || state.animator != null);

            if (hasAnimator)
            {
                int animCount = state.model.GetAnimationCount();
                string currentAnimName = state.model.GetCurrentAnimationName();

                igText($"Current: {currentAnimName}");
                igText($"Total Anims: {animCount}");

                if (animCount > 1)
                {
                    igSeparator();
                    if (igButton("<- Previous", new Vector2(110, 0)))
                    {
                        state.model.PreviousAnimation();
                        
                        // Update animation for all characters (multi-character support)
                        if (state.model.Characters.Count > 0)
                        {
                            foreach (var character in state.model.Characters)
                            {
                                // Try to find appropriate animation for this character
                                var newAnim = state.model.Animations[state.model.CurrentAnimationIndex];
                                character.Animator.SetAnimation(newAnim);
                            }
                        }
                        // Legacy: also update global animator if it exists
                        else if (state.animator != null)
                        {
                            state.animator.SetAnimation(state.model.Animation);
                        }
                    }

                    igSameLine(0, 10);

                    if (igButton("Next ->", new Vector2(110, 0)))
                    {
                        state.model.NextAnimation();
                        
                        // Update animation for all characters (multi-character support)
                        if (state.model.Characters.Count > 0)
                        {
                            foreach (var character in state.model.Characters)
                            {
                                // Try to find appropriate animation for this character
                                var newAnim = state.model.Animations[state.model.CurrentAnimationIndex];
                                character.Animator.SetAnimation(newAnim);
                            }
                        }
                        // Legacy: also update global animator if it exists
                        else if (state.animator != null)
                        {
                            state.animator.SetAnimation(state.model.Animation);
                        }
                    }
                }

                // Animation timing - get from first character or legacy animator
                SharpGltfAnimator? activeAnimator = state.model.Characters.Count > 0 
                    ? state.model.Characters[0].Animator 
                    : state.animator;
                    
                var currentAnim = activeAnimator?.GetCurrentAnimation();
                if (currentAnim != null)
                {
                    igSeparator();
                    float duration = currentAnim.GetDuration();
                    float currentTime = activeAnimator.GetCurrentTime();
                    float ticksPerSecond = currentAnim.GetTicksPerSecond();
                    float durationInSeconds = duration / ticksPerSecond;
                    float currentTimeInSeconds = currentTime / ticksPerSecond;

                    igText($"Duration: {durationInSeconds:F2}s");
                    igText($"Time: {currentTimeInSeconds:F2}s");
                    igText($"Progress: {(currentTime / duration * 100):F1}%%");
                }
                
                // Playback speed control
                igSeparator();
                igText("Playback Speed:");
                float playbackSpeed = activeAnimator != null ? activeAnimator.PlaybackSpeed : 1.0f;
                if (igSliderFloat("##speed", ref playbackSpeed, 0.1f, 2.0f, "%.2fx", ImGuiSliderFlags.None))
                {
                    // Update playback speed for all characters
                    if (state.model.Characters.Count > 0)
                    {
                        foreach (var character in state.model.Characters)
                        {
                            character.Animator.PlaybackSpeed = playbackSpeed;
                        }
                    }
                    // Legacy: also update global animator if it exists
                    else if (state.animator != null)
                    {
                        state.animator.PlaybackSpeed = playbackSpeed;
                    }
                }
                
                if (igButton("Reset to 1.0x", new Vector2(110, 0)))
                {
                    // Reset playback speed for all characters
                    if (state.model.Characters.Count > 0)
                    {
                        foreach (var character in state.model.Characters)
                        {
                            character.Animator.PlaybackSpeed = 1.0f;
                        }
                    }
                    // Legacy: also update global animator if it exists
                    else if (state.animator != null)
                    {
                        state.animator.PlaybackSpeed = 1.0f;
                    }
                }
                
                // Skinning mode selection
                igSeparator();
                igText("Skinning Mode:");
                
                // Check if we must use texture-based skinning
                bool mustUseTextureBased = state.model != null && state.model.BoneCounter >= AnimationConstants.MAX_BONES;
                
                if (mustUseTextureBased)
                {
                    // Show read-only info when texture-based is required
                    igTextColored(new Vector4(1, 1, 0, 1), "Texture-Based (Required)");
                    igText($"Model has {state.model.BoneCounter} bones");
                    igText($"(max {AnimationConstants.MAX_BONES} for uniform mode)");
                }
                else
                {
                    // Allow user to choose when under the limit
                    int skinningMode = (int)state.skinningMode;
                    string[] skinningModeNames = new[] { "Uniform-Based (Fast)", "Texture-Based (Unlimited)" };
                    if (igCombo_Str_arr("##skinning", ref skinningMode, skinningModeNames, skinningModeNames.Length, -1))
                    {
                        state.skinningMode = (SkinningMode)skinningMode;
                    }
                    
                    // Show info about current mode
                    if (state.skinningMode == SkinningMode.UniformBased)
                    {
                        igTextColored(new Vector4(0, 1, 0, 1), "Fast (60 FPS on mobile)");
                        igText($"Max {AnimationConstants.MAX_BONES} bones");
                    }
                    else
                    {
                        igTextColored(new Vector4(1, 1, 0, 1), "Slower (30 FPS on mobile)");
                        igText("Unlimited bones");
                    }
                }
            }
            else
            {
                igText("No animations available");
            }
        }
        igEnd();
    }

    static void DrawLightingWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(280, 350), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Lighting", ref open, ImGuiWindowFlags.None))
        {
            state.ui.lighting_open = open != 0;

            // IBL (Image-Based Lighting) toggle
            igText("Image-Based Lighting:");
            byte iblEnabled = (byte)(state.useIBL ? 1 : 0);
            if (igCheckbox("Enable IBL", ref iblEnabled))
            {
                state.useIBL = iblEnabled != 0;
            }
            
            // Render environment map as background
            byte renderEnvMap = (byte)(state.renderEnvironmentMap ? 1 : 0);
            if (igCheckbox("Environment Map Background", ref renderEnvMap))
            {
                state.renderEnvironmentMap = renderEnvMap != 0;
            }
            
            igSeparator();

            // Ambient light slider
            igText("Ambient Light:");
            float ambientStrength = state.ambientStrength;
            if (igSliderFloat("##ambient", ref ambientStrength, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.None))
            {
                state.ambientStrength = ambientStrength;
            }

            igSeparator();
            int activeCount = state.lights.Count(l => l.Enabled);
            igText($"Active: {activeCount}/{state.lights.Count}");

            igSeparator();
            // Individual light controls
            for (int i = 0; i < state.lights.Count; i++)
            {
                var light = state.lights[i];

                igPushID_Int(i);
                byte lightEnabled = light.Enabled ? (byte)1 : (byte)0;
                if (igCheckbox($"Light {i + 1}", ref lightEnabled))
                {
                    light.Enabled = lightEnabled != 0;
                }

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
        }
        igEnd();
    }

    static void DrawBloomWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(250, 150), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Bloom Post-Processing", ref open, ImGuiWindowFlags.None))
        {
            state.ui.bloom_open = open != 0;

            byte bloomEnabled = (byte)(state.enableBloom ? 1 : 0);
            if (igCheckbox("Enable Bloom", ref bloomEnabled))
            {
                state.enableBloom = bloomEnabled != 0;
            }

            if (state.enableBloom)
            {
                igSeparator();
                igText("Intensity:");
                float bloomIntensity = state.bloomIntensity;
                if (igSliderFloat("##bloom_intensity", ref bloomIntensity, 0.0f, 3.0f, "%.2f", ImGuiSliderFlags.None))
                {
                    state.bloomIntensity = bloomIntensity;
                }

                igText("Threshold:");
                float bloomThreshold = state.bloomThreshold;
                if (igSliderFloat("##bloom_threshold", ref bloomThreshold, 0.1f, 5.0f, "%.2f", ImGuiSliderFlags.None))
                {
                    state.bloomThreshold = bloomThreshold;
                }
            }
        }
        igEnd();
    }

    static void DrawTonemapWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(280, 200), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Tone Mapping", ref open, ImGuiWindowFlags.None))
        {
            state.ui.tonemap_open = open != 0;

            igText("Exposure:");
            float exposure = state.exposure;
            if (igSliderFloat("##exposure", ref exposure, 0.1f, 10.0f, "%.2f", ImGuiSliderFlags.None))
            {
                state.exposure = exposure;
            }

            igSeparator();
            igText("Tone Map Algorithm:");
            
            int tonemapType = state.tonemapType;
            string[] tonemapNames = new[] { 
                "None",
                "ACES Narkowicz", 
                "ACES Hill", 
                "ACES Hill + Exposure Boost",
                "Khronos PBR Neutral"
            };
            
            if (igCombo_Str_arr("##tonemap", ref tonemapType, tonemapNames, tonemapNames.Length, -1))
            {
                state.tonemapType = tonemapType;
            }

            igSeparator();
            igTextWrapped("Tone mapping converts HDR (High Dynamic Range) colors to displayable LDR (Low Dynamic Range). Different algorithms produce different looks.");
        }
        igEnd();
    }

    static void DrawGlassMaterialsWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(400, 500), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Glass Materials (Transmission)", ref open, ImGuiWindowFlags.None))
        {
            state.ui.glass_materials_open = open != 0;

            igTextWrapped("Glass materials use screen-space refraction with Index of Refraction (IOR) for realistic light bending through transparent objects. Transmission is automatically enabled for meshes with transmission_factor > 0.");

            // Model info section
            if (state.model != null)
            {
                igSeparator();
                if (igCollapsingHeader_TreeNodeFlags("Model Properties", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    int transmissiveMeshes = 0;
                    foreach (var mesh in state.model.Meshes)
                    {
                        if (mesh.TransmissionFactor > 0.0f)
                        {
                            transmissiveMeshes++;
                        }
                    }
                    
                    igText($"Transmissive meshes: {transmissiveMeshes}/{state.model.Meshes.Count}");
                    
                    if (transmissiveMeshes > 0)
                    {
                        igText("Examples found:");
                        int shown = 0;
                        foreach (var mesh in state.model.Meshes)
                        {
                            if (mesh.TransmissionFactor > 0.0f && shown < 3)
                            {
                                igBulletText($"IOR: {mesh.IOR:F2}, Transmission: {mesh.TransmissionFactor:F2}");
                                if (mesh.AttenuationColor != Vector3.One)
                                {
                                    igBulletText($"  Attenuation: ({mesh.AttenuationColor.X:F2}, {mesh.AttenuationColor.Y:F2}, {mesh.AttenuationColor.Z:F2})");
                                }
                                shown++;
                            }
                        }
                    }
                }

                // Override section
                igSeparator();
                if (igCollapsingHeader_TreeNodeFlags("Material Overrides", ImGuiTreeNodeFlags.None))
                {
                    byte overrideEnabled = (byte)(state.overrideGlassMaterials ? 1 : 0);
                    if (igCheckbox("Enable Overrides", ref overrideEnabled))
                    {
                        state.overrideGlassMaterials = overrideEnabled != 0;
                    }
                    
                    if (state.overrideGlassMaterials)
                    {
                        igPushItemWidth(150);
                        
                        // IOR slider
                        float ior = state.overrideIOR;
                        if (igSliderFloat("IOR", ref ior, 1.0f, 2.4f, "%.2f", ImGuiSliderFlags.None))
                        {
                            state.overrideIOR = ior;
                        }
                        if (igIsItemHovered(ImGuiHoveredFlags.None))
                        {
                            igSetTooltip("Index of Refraction\nWater: 1.33, Glass: 1.5, Diamond: 2.4");
                        }

                        // Transmission slider
                        float transmission = state.overrideTransmission;
                        if (igSliderFloat("Transmission", ref transmission, 0.0f, 1.0f, "%.2f", ImGuiSliderFlags.None))
                        {
                            state.overrideTransmission = transmission;
                        }
                        if (igIsItemHovered(ImGuiHoveredFlags.None))
                        {
                            igSetTooltip("Transmission Factor\n0.0 = opaque, 1.0 = fully transparent");
                        }

                        // Thickness slider
                        float thickness = state.overrideThickness;
                        if (igSliderFloat("Thickness", ref thickness, 0.0f, 5.0f, "%.2f", ImGuiSliderFlags.None))
                        {
                            state.overrideThickness = thickness;
                        }
                        if (igIsItemHovered(ImGuiHoveredFlags.None))
                        {
                            igSetTooltip("Thickness multiplier for volume absorption");
                        }

                        // Attenuation distance slider
                        float attenuationDist = state.overrideAttenuationDistance;
                        if (igSliderFloat("Attenuation Dist", ref attenuationDist, 0.01f, 10.0f, "%.2f", ImGuiSliderFlags.Logarithmic))
                        {
                            state.overrideAttenuationDistance = attenuationDist;
                        }
                        if (igIsItemHovered(ImGuiHoveredFlags.None))
                        {
                            igSetTooltip("Distance for Beer's Law absorption\nSmaller = more absorption");
                        }

                        // Attenuation color picker
                        Vector3 attenuationColor = state.overrideAttenuationColor;
                        if (igColorEdit3("Attenuation Color", ref attenuationColor, ImGuiColorEditFlags.None))
                        {
                            state.overrideAttenuationColor = attenuationColor;
                        }
                        if (igIsItemHovered(ImGuiHoveredFlags.None))
                        {
                            igSetTooltip("Color absorption for volume\nWhite = no absorption (clear glass)");
                        }

                        igPopItemWidth();

                        // Preset buttons
                        igSeparator();
                        igText("Presets:");
                        if (igButton("Clear Glass", new Vector2(120, 0)))
                        {
                            state.overrideIOR = 1.5f;
                            state.overrideTransmission = 1.0f;
                            state.overrideAttenuationColor = Vector3.One;
                            state.overrideAttenuationDistance = 1.0f;
                            state.overrideThickness = 1.0f;
                        }
                        igSameLine(0, 5);
                        if (igButton("Amber", new Vector2(120, 0)))
                        {
                            state.overrideIOR = 1.55f;
                            state.overrideTransmission = 0.9f;
                            state.overrideAttenuationColor = new Vector3(1.0f, 0.6f, 0.2f);
                            state.overrideAttenuationDistance = 0.5f;
                            state.overrideThickness = 1.0f;
                        }
                        
                        if (igButton("Water", new Vector2(120, 0)))
                        {
                            state.overrideIOR = 1.33f;
                            state.overrideTransmission = 0.95f;
                            state.overrideAttenuationColor = new Vector3(0.8f, 0.9f, 1.0f);
                            state.overrideAttenuationDistance = 2.0f;
                            state.overrideThickness = 1.0f;
                        }
                        igSameLine(0, 5);
                        if (igButton("Emerald", new Vector2(120, 0)))
                        {
                            state.overrideIOR = 1.57f;
                            state.overrideTransmission = 0.85f;
                            state.overrideAttenuationColor = new Vector3(0.2f, 0.8f, 0.3f);
                            state.overrideAttenuationDistance = 0.3f;
                            state.overrideThickness = 1.0f;
                        }

                        if (igButton("Ruby", new Vector2(120, 0)))
                        {
                            state.overrideIOR = 1.77f;
                            state.overrideTransmission = 0.8f;
                            state.overrideAttenuationColor = new Vector3(0.9f, 0.1f, 0.2f);
                            state.overrideAttenuationDistance = 0.4f;
                            state.overrideThickness = 1.0f;
                        }
                        igSameLine(0, 5);
                        if (igButton("Diamond", new Vector2(120, 0)))
                        {
                            state.overrideIOR = 2.42f;
                            state.overrideTransmission = 1.0f;
                            state.overrideAttenuationColor = Vector3.One;
                            state.overrideAttenuationDistance = 1.0f;
                            state.overrideThickness = 0.5f;
                        }
                    }
                }
            }
        }
        igEnd();
    }

    static void DrawCullingWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(220, 120), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Frustum Culling", ref open, ImGuiWindowFlags.None))
        {
            state.ui.culling_open = open != 0;

            byte frustumEnabled = (byte)(state.enableFrustumCulling ? 1 : 0);
            if (igCheckbox("Enable Culling", ref frustumEnabled))
            {
                state.enableFrustumCulling = frustumEnabled != 0;
            }

            if (state.model != null)
            {
                igSeparator();
                igText($"Total: {state.totalMeshes}");
                igText($"Visible: {state.visibleMeshes}");
                igText($"Culled: {state.culledMeshes}");
                if (state.totalMeshes > 0)
                {
                    float cullPercent = (state.culledMeshes * 100.0f / state.totalMeshes);
                    igText($"Culled: {cullPercent:F1}%%");
                }
            }
        }
        igEnd();
    }

    static void DrawStatisticsWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(240, 280), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Statistics", ref open, ImGuiWindowFlags.None))
        {
            state.ui.statistics_open = open != 0;

            double frameDuration = sapp_frame_duration();
            float fps = frameDuration > 0 ? (float)(1.0 / frameDuration) : 0.0f;
            igText($"FPS: {fps:F1}");
            igText($"Frame: {frameDuration * 1000.0:F2} ms");

            if (state.model != null)
            {
                igSeparator();
                igText("Rendering:");
                igText($"  Vertices: {state.totalVertices:N0}");
                igText($"  Indices: {state.totalIndices:N0}");
                igText($"  Faces: {state.totalFaces:N0}");
                
                igSeparator();
                var (hits, misses, total) = TextureCache.Instance.GetStats();
                var hitRate = hits + misses > 0 ? (hits * 100.0 / (hits + misses)) : 0.0;
                igText("Texture Cache:");
                igText($"  Unique: {total}");
                igText($"  Hits: {hits}");
                igText($"  Misses: {misses}");
                igText($"  Hit Rate: {hitRate:F1}%%");
                
                igSeparator();
                igText("Shader Cache:");
                igText($"  Shaders: {PipeLineManager.ShaderCount}");
            }
        }
        igEnd();
    }

    static void DrawCameraInfoWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(200, 120), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Camera Info", ref open, ImGuiWindowFlags.None))
        {
            state.ui.camera_info_open = open != 0;

            igText($"Distance: {state.camera.Distance:F2}");
            igText($"Latitude: {state.camera.Latitude:F2}");
            igText($"Longitude: {state.camera.Longitude:F2}");
            igText($"Center: ({state.camera.Center.X:F1}, {state.camera.Center.Y:F1}, {state.camera.Center.Z:F1})");
        }
        igEnd();
    }

    static void DrawCameraControlsWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(220, 180), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Camera Controls", ref open, ImGuiWindowFlags.None))
        {
            state.ui.camera_controls_open = open != 0;

            // Calculate forward and right vectors
            Vector3 forward = Vector3.Normalize(state.camera.Center - state.camera.EyePos);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));

            float moveSpeed = 50.0f * (float)sapp_frame_duration();

            // Forward button (centered)
            igIndent(50);
            igButton("Forward", new Vector2(80, 40));
            if (igIsItemActive())
            {
                state.camera.Center = state.camera.Center + forward * moveSpeed;
            }
            igUnindent(50);

            // Left and Right buttons
            igButton("Left", new Vector2(80, 40));
            if (igIsItemActive())
            {
                state.camera.Center = state.camera.Center - right * moveSpeed;
            }
            igSameLine(0, 10);
            igButton("Right", new Vector2(80, 40));
            if (igIsItemActive())
            {
                state.camera.Center = state.camera.Center + right * moveSpeed;
            }

            // Backward button (centered)
            igIndent(50);
            igButton("Back", new Vector2(80, 40));
            if (igIsItemActive())
            {
                state.camera.Center = state.camera.Center - forward * moveSpeed;
            }
            igUnindent(50);
        }
        igEnd();
    }

    static void DrawHelpWindow()
    {
        igSetNextWindowSize(new Vector2(600, 500), ImGuiCond.Once);
        igSetNextWindowPos(new Vector2(100, 100), ImGuiCond.Once, Vector2.Zero);
        
        byte open = 1;
        if (igBegin("Help", ref open, ImGuiWindowFlags.None))
        {
            if (open == 0)
            {
                state.ui.help_open = false;
            }

            igTextWrapped("Sharp GLTF Viewer - Help");
            igSeparator();
            igNewLine();

            // Camera Controls
            igTextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Camera Controls:");
            igBulletText("Left Mouse: Orbit camera around model");
            igBulletText("1 Finger Touch: Orbit camera (mobile)");
            igBulletText("Mouse Wheel: Zoom in/out");
            igBulletText("WASD/Arrow Keys: Move camera position");
            igBulletText("Q/E: Move camera up/down");
            igBulletText("Hold Shift: Faster camera movement");
            igNewLine();

            // Model Controls
            igTextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Model Controls:");
            igBulletText("Middle Mouse: Rotate model");
            igBulletText("2 Finger Touch: Rotate model (mobile)");
            igNewLine();

            // Windows
            igTextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Available Windows (Menu: Windows):");
            igBulletText("Model Info: Display model statistics and rotation controls");
            igBulletText("Animation: Control model animations (play/pause/select)");
            igBulletText("Lighting: Adjust ambient light and toggle individual lights");
            igBulletText("Bloom: Enable HDR bloom effect with intensity/threshold controls");
            igBulletText("Culling: View frustum culling statistics");
            igBulletText("Statistics: FPS, frame time, geometry and texture stats");
            igBulletText("Camera Info: Current camera position and orientation");
            igBulletText("Camera Controls: Touch-friendly movement buttons (mobile)");
            igNewLine();

            // Theme
            igTextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Theme Options (Menu: Options):");
            igBulletText("Dark: Dark color scheme (default)");
            igBulletText("Light: Light color scheme");
            igBulletText("Classic: ImGui classic color scheme");
            igNewLine();

            igSeparator();
            igTextWrapped("Tip: All windows can be moved, resized, and closed. Use the Windows menu to reopen them.");
        }
        igEnd();
    }

    static void DrawDebugViewWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(300, 500), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Debug View", ref open, ImGuiWindowFlags.None))
        {
            state.ui.debug_view_open = open != 0;

            igText("PBR Material Debugger");
            igSeparator();

            // Enable/Disable debug view
            byte debugEnabled = state.ui.debug_view_enabled != 0 ? (byte)1 : (byte)0;
            if (igCheckbox("Enable Debug View", ref debugEnabled))
            {
                state.ui.debug_view_enabled = debugEnabled != 0 ? 1 : 0;
            }

            if (state.ui.debug_view_enabled != 0)
            {
                igNewLine();
                igText("Select Debug Mode:");
                igSeparator();

                // Debug view mode selection
                if (igRadioButton_IntPtr("None (Standard Rendering)", ref state.ui.debug_view_mode, 0)) { }
                
                igNewLine();
                igTextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "UV Coordinates:");
                if (igRadioButton_IntPtr("UV Channel 0", ref state.ui.debug_view_mode, 1)) { }
                if (igRadioButton_IntPtr("UV Channel 1", ref state.ui.debug_view_mode, 2)) { }
                
                igNewLine();
                igTextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Normals:");
                if (igRadioButton_IntPtr("Normal Texture", ref state.ui.debug_view_mode, 3)) { }
                if (igRadioButton_IntPtr("Normal (Shading)", ref state.ui.debug_view_mode, 4)) { }
                if (igRadioButton_IntPtr("Normal (Geometry)", ref state.ui.debug_view_mode, 5)) { }
                if (igRadioButton_IntPtr("Tangent", ref state.ui.debug_view_mode, 6)) { }
                if (igRadioButton_IntPtr("Bitangent", ref state.ui.debug_view_mode, 7)) { }
                if (igRadioButton_IntPtr("Tangent W", ref state.ui.debug_view_mode, 8)) { }
                
                igNewLine();
                igTextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Material Properties:");
                if (igRadioButton_IntPtr("Alpha", ref state.ui.debug_view_mode, 9)) { }
                if (igRadioButton_IntPtr("Occlusion", ref state.ui.debug_view_mode, 10)) { }
                if (igRadioButton_IntPtr("Emissive", ref state.ui.debug_view_mode, 11)) { }
                if (igRadioButton_IntPtr("Metallic", ref state.ui.debug_view_mode, 12)) { }
                if (igRadioButton_IntPtr("Roughness", ref state.ui.debug_view_mode, 13)) { }
                if (igRadioButton_IntPtr("Base Color", ref state.ui.debug_view_mode, 14)) { }
                
                igNewLine();
                igTextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Clearcoat:");
                if (igRadioButton_IntPtr("Clearcoat Factor", ref state.ui.debug_view_mode, 15)) { }
                if (igRadioButton_IntPtr("Clearcoat Roughness", ref state.ui.debug_view_mode, 16)) { }
                if (igRadioButton_IntPtr("ClearCoat Normal", ref state.ui.debug_view_mode, 17)) { }
                
                igNewLine();
                igTextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Transmission/Glass:");
                if (igRadioButton_IntPtr("Transmission Factor", ref state.ui.debug_view_mode, 21)) { }
                if (igRadioButton_IntPtr("Volume Thickness", ref state.ui.debug_view_mode, 22)) { }
                if (igRadioButton_IntPtr("Index of Refraction (IOR)", ref state.ui.debug_view_mode, 23)) { }
                if (igRadioButton_IntPtr("F0 (Fresnel at 0°)", ref state.ui.debug_view_mode, 24)) { }
                if (igRadioButton_IntPtr("Attenuation Distance", ref state.ui.debug_view_mode, 25)) { }
                if (igRadioButton_IntPtr("Attenuation Color", ref state.ui.debug_view_mode, 26)) { }
                if (igRadioButton_IntPtr("Transmission Result (Before Mix)", ref state.ui.debug_view_mode, 27)) { }
                if (igRadioButton_IntPtr("Refraction Framebuffer (Raw)", ref state.ui.debug_view_mode, 28)) { }
                if (igRadioButton_IntPtr("Refraction Coordinates", ref state.ui.debug_view_mode, 29)) { }
                if (igRadioButton_IntPtr("Final Alpha", ref state.ui.debug_view_mode, 30)) { }
                
                igNewLine();
                igTextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "Advanced Transmission Debug:");
                if (igRadioButton_IntPtr("Beer's Law Attenuation", ref state.ui.debug_view_mode, 31)) { }
                if (igRadioButton_IntPtr("Base Color Tint", ref state.ui.debug_view_mode, 32)) { }
                if (igRadioButton_IntPtr("Surface Color (Before Mix)", ref state.ui.debug_view_mode, 33)) { }
                if (igRadioButton_IntPtr("Transmission After Beer's Law", ref state.ui.debug_view_mode, 34)) { }
                if (igRadioButton_IntPtr("Transmission After Tint", ref state.ui.debug_view_mode, 35)) { }
            }
        }
        igEnd();
    }

}