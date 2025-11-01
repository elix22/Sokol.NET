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

}