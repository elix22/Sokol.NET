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
    static void InitApplication()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            buffer_pool_size = 4096 * 2,//increased to handle very large scene graphs
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
            FarZ = 5000.0f,
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

                    // Create our model wrapper (pass file path for extension extraction)
                    state.model = new SharpGltfModel(modelRoot, path);

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
}