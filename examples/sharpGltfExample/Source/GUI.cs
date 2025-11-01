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

            igEndMainMenuBar();
        }

        Vector2 pos = new Vector2(30, 60);

        // Model Info Window
        if (state.ui.model_info_open)
        {
            DrawModelInfoWindow(ref pos);
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

    static void DrawAnimationWindow(ref Vector2 pos)
    {
        igSetNextWindowSize(new Vector2(250, 150), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Animation", ref open, ImGuiWindowFlags.None))
        {
            state.ui.animation_open = open != 0;

            if (state.animator != null && state.model != null && state.model.HasAnimations)
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
                        state.animator.SetAnimation(state.model.Animation);
                    }

                    igSameLine(0, 10);

                    if (igButton("Next ->", new Vector2(110, 0)))
                    {
                        state.model.NextAnimation();
                        state.animator.SetAnimation(state.model.Animation);
                    }
                }

                // Animation timing
                var currentAnim = state.animator.GetCurrentAnimation();
                if (currentAnim != null)
                {
                    igSeparator();
                    float duration = currentAnim.GetDuration();
                    float currentTime = state.animator.GetCurrentTime();
                    float ticksPerSecond = currentAnim.GetTicksPerSecond();
                    float durationInSeconds = duration / ticksPerSecond;
                    float currentTimeInSeconds = currentTime / ticksPerSecond;

                    igText($"Duration: {durationInSeconds:F2}s");
                    igText($"Time: {currentTimeInSeconds:F2}s");
                    igText($"Progress: {(currentTime / duration * 100):F1}%%");
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
        igSetNextWindowSize(new Vector2(280, 300), ImGuiCond.Once);
        igSetNextWindowPos(pos, ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Lighting", ref open, ImGuiWindowFlags.None))
        {
            state.ui.lighting_open = open != 0;

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
        igSetNextWindowSize(new Vector2(240, 250), ImGuiCond.Once);
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

}