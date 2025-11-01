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

        // Initialize bloom post-processing
        InitializeBloom();

        // Initialize transmission (glass/refraction) rendering
        InitializeTransmission();

        // Initialize FileSystem
        FileSystem.Instance.Initialize();
        
        // Load model asynchronously (FileSystem will handle platform-specific path conversion)
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

    static void InitializeBloom()
    {
        // Get screen dimensions (we'll create bloom textures at 1/2 resolution for performance)
        int fb_width = sapp_width();
        int fb_height = sapp_height();
        int bloom_width = Math.Max(fb_width / 2, 256);
        int bloom_height = Math.Max(fb_height / 2, 256);

        Info($"[Bloom] Initializing bloom: fb={fb_width}x{fb_height}, bloom={bloom_width}x{bloom_height}, backend={sg_query_backend()}");

        // Get swapchain info to match formats
        var swapchain = sglue_swapchain();
        
        // Create color texture for main scene rendering (full resolution)
        // NOTE: Offscreen rendering uses explicit formats and sample_count = 1 (no MSAA) as per offscreen example
        var scene_color_desc = new sg_image_desc()
        {
            usage = { color_attachment = true },
            width = fb_width,
            height = fb_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,  // Explicit format for offscreen rendering
            sample_count = 1,  // Offscreen passes don't use MSAA
            label = "bloom-scene-color"
        };
        state.bloom.scene_color_img = sg_make_image(scene_color_desc);

        // Create depth texture for main scene rendering  
        // Use SG_PIXELFORMAT_DEPTH exactly like the offscreen example
        var scene_depth_desc = new sg_image_desc()
        {
            usage = { depth_stencil_attachment = true },
            width = fb_width,
            height = fb_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,  // Same as offscreen example - works on all platforms
            sample_count = 1,  // Offscreen passes don't use MSAA
            label = "bloom-scene-depth"
        };
        state.bloom.scene_depth_img = sg_make_image(scene_depth_desc);

        // Create bloom processing textures (reduced resolution)
        var bloom_desc = new sg_image_desc()
        {
            usage = { color_attachment = true },
            width = bloom_width,
            height = bloom_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            sample_count = 1
        };

        bloom_desc.label = "bloom-bright";
        state.bloom.bright_img = sg_make_image(bloom_desc);
        
        bloom_desc.label = "bloom-blur-h";
        state.bloom.blur_h_img = sg_make_image(bloom_desc);
        
        bloom_desc.label = "bloom-blur-v";
        state.bloom.blur_v_img = sg_make_image(bloom_desc);

        // WebGL workaround: Create a dummy depth buffer matching bloom resolution
        // WebGL requires consistent framebuffer attachments - we can't switch between
        // passes with depth and without depth on the same FBO without explicitly unbinding.
        // The workaround is to always have a depth attachment, even if unused.
        // IMPORTANT: All attachments must have the same dimensions!
        var dummy_depth_desc = new sg_image_desc()
        {
            usage = { depth_stencil_attachment = true },
            width = bloom_width,
            height = bloom_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
            sample_count = 1,
            label = "bloom-dummy-depth"
        };
        state.bloom.dummy_depth_img = sg_make_image(dummy_depth_desc);

        // Create sampler for all bloom passes
        state.bloom.sampler = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = sg_filter.SG_FILTER_LINEAR,
            mag_filter = sg_filter.SG_FILTER_LINEAR,
            wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
            wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
            label = "bloom-sampler"
        });

        // Create render passes
        // Scene pass (renders main scene to offscreen buffer)
        // Use same pattern as offscreen example for WebGL compatibility
        sg_view_desc scene_color_view_desc = default;
        scene_color_view_desc.color_attachment.image = state.bloom.scene_color_img;
        scene_color_view_desc.label = "scene-color-view";
        
        sg_view_desc scene_depth_view_desc = default;
        scene_depth_view_desc.depth_stencil_attachment.image = state.bloom.scene_depth_img;
        scene_depth_view_desc.label = "scene-depth-view";
        
        var scene_color_view = sg_make_view(scene_color_view_desc);
        var scene_depth_view = sg_make_view(scene_depth_view_desc);
        Info($"[Bloom] Scene views created: color={scene_color_view.id}, depth={scene_depth_view.id}");
        
        // Create attachments struct explicitly (like offscreen example)
        sg_attachments scene_attachments = default;
        scene_attachments.colors[0] = scene_color_view;
        scene_attachments.depth_stencil = scene_depth_view;
        
        // Create action
        sg_pass_action scene_action = default;
        scene_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        scene_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };
        scene_action.depth.load_action = sg_load_action.SG_LOADACTION_CLEAR;
        scene_action.depth.clear_value = 1.0f;
        
        // Assign to pass
        state.bloom.scene_pass = default;
        state.bloom.scene_pass.attachments = scene_attachments;
        state.bloom.scene_pass.action = scene_action;
        state.bloom.scene_pass.label = "bloom-scene-pass";

        // Bright pass - WebGL requires depth attachment for FBO consistency
        sg_view_desc bright_view_desc = default;
        bright_view_desc.color_attachment.image = state.bloom.bright_img;
        bright_view_desc.label = "bright-view";
        sg_view bright_color_view = sg_make_view(bright_view_desc);
        
        sg_view_desc bright_depth_view_desc = default;
        bright_depth_view_desc.depth_stencil_attachment.image = state.bloom.dummy_depth_img;
        bright_depth_view_desc.label = "bright-depth-view";
        sg_view bright_depth_view = sg_make_view(bright_depth_view_desc);
        
        sg_attachments bright_attachments = default;
        bright_attachments.colors[0] = bright_color_view;
        bright_attachments.depth_stencil = bright_depth_view;
        
        sg_pass_action bright_action = default;
        bright_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        bright_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };
        
        state.bloom.bright_pass.attachments = bright_attachments;
        state.bloom.bright_pass.action = bright_action;
        state.bloom.bright_pass.label = "bloom-bright-pass";

        // Horizontal blur pass - WebGL requires depth attachment for FBO consistency
        sg_view_desc blur_h_view_desc = default;
        blur_h_view_desc.color_attachment.image = state.bloom.blur_h_img;
        blur_h_view_desc.label = "blur-h-view";
        
        sg_view_desc blur_h_depth_view_desc = default;
        blur_h_depth_view_desc.depth_stencil_attachment.image = state.bloom.dummy_depth_img;
        blur_h_depth_view_desc.label = "blur-h-depth-view";
        
        sg_attachments blur_h_attachments = default;
        blur_h_attachments.colors[0] = sg_make_view(blur_h_view_desc);
        blur_h_attachments.depth_stencil = sg_make_view(blur_h_depth_view_desc);
        
        sg_pass_action blur_h_action = default;
        blur_h_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        blur_h_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };
        
        state.bloom.blur_h_pass.attachments = blur_h_attachments;
        state.bloom.blur_h_pass.action = blur_h_action;
        state.bloom.blur_h_pass.label = "bloom-blur-h-pass";

        // Vertical blur pass - WebGL requires depth attachment for FBO consistency
        sg_view_desc blur_v_view_desc = default;
        blur_v_view_desc.color_attachment.image = state.bloom.blur_v_img;
        blur_v_view_desc.label = "blur-v-view";
        
        sg_view_desc blur_v_depth_view_desc = default;
        blur_v_depth_view_desc.depth_stencil_attachment.image = state.bloom.dummy_depth_img;
        blur_v_depth_view_desc.label = "blur-v-depth-view";
        
        sg_attachments blur_v_attachments = default;
        blur_v_attachments.colors[0] = sg_make_view(blur_v_view_desc);
        blur_v_attachments.depth_stencil = sg_make_view(blur_v_depth_view_desc);
        
        sg_pass_action blur_v_action = default;
        blur_v_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        blur_v_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };
        
        state.bloom.blur_v_pass.attachments = blur_v_attachments;
        state.bloom.blur_v_pass.action = blur_v_action;
        state.bloom.blur_v_pass.label = "bloom-blur-v-pass";

        // Note: Composite pass renders to swapchain and must be created each frame
        // with the current swapchain, so we don't create it here.

        // Create offscreen pipelines for rendering the model to bloom scene pass
        // Use SG_PIXELFORMAT_DEPTH exactly like the offscreen example
        state.bloom.scene_standard_pipeline = PipeLineManager.CreateOffscreenPipeline(
            PipelineType.Standard, sg_pixel_format.SG_PIXELFORMAT_RGBA8, sg_pixel_format.SG_PIXELFORMAT_DEPTH);
        state.bloom.scene_skinned_pipeline = PipeLineManager.CreateOffscreenPipeline(
            PipelineType.Skinned, sg_pixel_format.SG_PIXELFORMAT_RGBA8, sg_pixel_format.SG_PIXELFORMAT_DEPTH);
        state.bloom.scene_standard_blend_pipeline = PipeLineManager.CreateOffscreenPipeline(
            PipelineType.StandardBlend, sg_pixel_format.SG_PIXELFORMAT_RGBA8, sg_pixel_format.SG_PIXELFORMAT_DEPTH);
        state.bloom.scene_skinned_blend_pipeline = PipeLineManager.CreateOffscreenPipeline(
            PipelineType.SkinnedBlend, sg_pixel_format.SG_PIXELFORMAT_RGBA8, sg_pixel_format.SG_PIXELFORMAT_DEPTH);
        state.bloom.scene_standard_mask_pipeline = PipeLineManager.CreateOffscreenPipeline(
            PipelineType.StandardMask, sg_pixel_format.SG_PIXELFORMAT_RGBA8, sg_pixel_format.SG_PIXELFORMAT_DEPTH);
        state.bloom.scene_skinned_mask_pipeline = PipeLineManager.CreateOffscreenPipeline(
            PipelineType.SkinnedMask, sg_pixel_format.SG_PIXELFORMAT_RGBA8, sg_pixel_format.SG_PIXELFORMAT_DEPTH);

        // Create fullscreen quad vertices for post-processing passes
        float[] fullscreen_quad_vertices = {
            // Triangle 1: Full-screen triangle (covers entire NDC)
            -1.0f, -1.0f,   // Bottom-left
             3.0f, -1.0f,   // Bottom-right (extends past screen)
            -1.0f,  3.0f    // Top-left (extends past screen)
        };

        // Create vertex buffer for fullscreen quad
        var fullscreen_vbuf = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(fullscreen_quad_vertices),
            label = "bloom-fullscreen-vbuf"
        });

        // Create pipelines for bloom post-processing passes
        // Bright pass pipeline (fullscreen quad, no depth testing needed)
        state.bloom.bright_pipeline = sg_make_pipeline(new sg_pipeline_desc()
        {
            layout = new sg_vertex_layout_state()
            {
                attrs = {
                    [cgltf_sapp_shader_cs_cgltf.Shaders.ATTR_cgltf_bright_pass_position] = new sg_vertex_attr_state()
                    {
                        format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2
                    }
                }
            },
            shader = sg_make_shader(cgltf_sapp_shader_cs_cgltf.Shaders.cgltf_bright_pass_shader_desc(sg_query_backend())),
            primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
            sample_count = 1,
            depth = new sg_depth_state()
            {
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,  // Must match dummy depth attachment
                write_enabled = false,
                compare = sg_compare_func.SG_COMPAREFUNC_ALWAYS
            },
            colors = {
                [0] = new sg_color_target_state()
                {
                    pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8
                }
            },
            label = "bloom-bright-pipeline"
        });

        // Horizontal blur pipeline (fullscreen quad, no depth testing needed)
        state.bloom.blur_h_pipeline = sg_make_pipeline(new sg_pipeline_desc()
        {
            layout = new sg_vertex_layout_state()
            {
                attrs = {
                    [cgltf_sapp_shader_cs_cgltf.Shaders.ATTR_cgltf_blur_horizontal_position] = new sg_vertex_attr_state()
                    {
                        format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2
                    }
                }
            },
            shader = sg_make_shader(cgltf_sapp_shader_cs_cgltf.Shaders.cgltf_blur_horizontal_shader_desc(sg_query_backend())),
            primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
            sample_count = 1,
            depth = new sg_depth_state()
            {
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,  // Must match dummy depth attachment
                write_enabled = false,
                compare = sg_compare_func.SG_COMPAREFUNC_ALWAYS
            },
            colors = {
                [0] = new sg_color_target_state()
                {
                    pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8
                }
            },
            label = "bloom-blur-h-pipeline"
        });

        // Vertical blur pipeline (fullscreen quad, no depth testing needed)
        state.bloom.blur_v_pipeline = sg_make_pipeline(new sg_pipeline_desc()
        {
            layout = new sg_vertex_layout_state()
            {
                attrs = {
                    [cgltf_sapp_shader_cs_cgltf.Shaders.ATTR_cgltf_blur_vertical_position] = new sg_vertex_attr_state()
                    {
                        format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2
                    }
                }
            },
            shader = sg_make_shader(cgltf_sapp_shader_cs_cgltf.Shaders.cgltf_blur_vertical_shader_desc(sg_query_backend())),
            primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
            sample_count = 1,
            depth = new sg_depth_state()
            {
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,  // Must match dummy depth attachment
                write_enabled = false,
                compare = sg_compare_func.SG_COMPAREFUNC_ALWAYS
            },
            colors = {
                [0] = new sg_color_target_state()
                {
                    pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8
                }
            },
            label = "bloom-blur-v-pipeline"
        });

        // Composite pipeline (renders to swapchain, fullscreen quad doesn't need depth testing)
        state.bloom.composite_pipeline = sg_make_pipeline(new sg_pipeline_desc()
        {
            layout = new sg_vertex_layout_state()
            {
                attrs = {
                    [cgltf_sapp_shader_cs_cgltf.Shaders.ATTR_cgltf_bloom_composite_position] = new sg_vertex_attr_state()
                    {
                        format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2
                    }
                }
            },
            shader = sg_make_shader(cgltf_sapp_shader_cs_cgltf.Shaders.cgltf_bloom_composite_shader_desc(sg_query_backend())),
            primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
            sample_count = swapchain.sample_count,  // Match swapchain MSAA
            depth = new sg_depth_state()
            {
                write_enabled = false,  // Explicitly disable depth writes
                compare = sg_compare_func.SG_COMPAREFUNC_ALWAYS  // Always pass depth test
            },
            colors = {
                [0] = new sg_color_target_state()
                {
                    pixel_format = swapchain.color_format  // Match swapchain color format
                }
            },
            label = "bloom-composite-pipeline"
        });

        // Create resource bindings
        // Bright pass bindings (scene texture -> bright pass)
        state.bloom.bright_bindings = new sg_bindings()
        {
            vertex_buffers = { [0] = fullscreen_vbuf },
            views = {
                [0] = sg_make_view(new sg_view_desc
                {
                    texture = { image = state.bloom.scene_color_img },
                    label = "bright-scene-texture-view"
                })
            },
            samplers = { [0] = state.bloom.sampler }
        };

        // Horizontal blur bindings (bright pass -> blur horizontal)
        state.bloom.blur_h_bindings = new sg_bindings()
        {
            vertex_buffers = { [0] = fullscreen_vbuf },
            views = {
                [0] = sg_make_view(new sg_view_desc
                {
                    texture = { image = state.bloom.bright_img },
                    label = "blur-h-input-view"
                })
            },
            samplers = { [0] = state.bloom.sampler }
        };

        // Vertical blur bindings (horizontal blur -> blur vertical)
        state.bloom.blur_v_bindings = new sg_bindings()
        {
            vertex_buffers = { [0] = fullscreen_vbuf },
            views = {
                [0] = sg_make_view(new sg_view_desc
                {
                    texture = { image = state.bloom.blur_h_img },
                    label = "blur-v-input-view"
                })
            },
            samplers = { [0] = state.bloom.sampler }
        };

        // Composite bindings (scene + final bloom -> swapchain)
        state.bloom.composite_bindings = new sg_bindings()
        {
            vertex_buffers = { [0] = fullscreen_vbuf },
            views = {
                [0] = sg_make_view(new sg_view_desc
                {
                    texture = { image = state.bloom.scene_color_img },
                    label = "composite-scene-view"
                }),
                [1] = sg_make_view(new sg_view_desc
                {
                    texture = { image = state.bloom.blur_v_img },
                    label = "composite-bloom-view"
                })
            },
            samplers = { [0] = state.bloom.sampler, [1] = state.bloom.sampler }
        };

        Info("[Bloom] Bloom system initialized successfully");
    }

    static void InitializeTransmission()
    {
        int fb_width = sapp_width();
        int fb_height = sapp_height();

        Info($"[Transmission] Initializing screen-space refraction: {fb_width}x{fb_height}, backend={sg_query_backend()}");

        // Create screen texture for capturing scene behind transparent objects
        var screen_color_desc = new sg_image_desc()
        {
            usage = { color_attachment = true },
            width = fb_width,
            height = fb_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            sample_count = 1,  // No MSAA for offscreen rendering
            label = "transmission-screen-color"
        };
        state.transmission.screen_color_img = sg_make_image(screen_color_desc);

        // Create depth texture for opaque pass
        var screen_depth_desc = new sg_image_desc()
        {
            usage = { depth_stencil_attachment = true },
            width = fb_width,
            height = fb_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
            sample_count = 1,
            label = "transmission-screen-depth"
        };
        state.transmission.screen_depth_img = sg_make_image(screen_depth_desc);

        // Create sampler for screen texture sampling
        state.transmission.sampler = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = sg_filter.SG_FILTER_LINEAR,
            mag_filter = sg_filter.SG_FILTER_LINEAR,
            wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
            wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
            label = "transmission-sampler"
        });

        // Create view for screen texture (create once, reuse every frame)
        state.transmission.screen_color_view = sg_make_view(new sg_view_desc
        {
            texture = { image = state.transmission.screen_color_img },
            label = "transmission-screen-color-view"
        });
        Info("[Transmission] Created screen color view for refraction sampling");

        // Create opaque pass (renders opaque objects to screen texture)
        sg_view_desc opaque_color_view_desc = default;
        opaque_color_view_desc.color_attachment.image = state.transmission.screen_color_img;
        opaque_color_view_desc.label = "opaque-color-view";
        
        sg_view_desc opaque_depth_view_desc = default;
        opaque_depth_view_desc.depth_stencil_attachment.image = state.transmission.screen_depth_img;
        opaque_depth_view_desc.label = "opaque-depth-view";
        
        var opaque_color_view = sg_make_view(opaque_color_view_desc);
        var opaque_depth_view = sg_make_view(opaque_depth_view_desc);
        
        sg_attachments opaque_attachments = default;
        opaque_attachments.colors[0] = opaque_color_view;
        opaque_attachments.depth_stencil = opaque_depth_view;
        
        sg_pass_action opaque_action = default;
        opaque_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        opaque_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };
        opaque_action.depth.load_action = sg_load_action.SG_LOADACTION_CLEAR;
        opaque_action.depth.clear_value = 1.0f;
        
        state.transmission.opaque_pass = default;
        state.transmission.opaque_pass.attachments = opaque_attachments;
        state.transmission.opaque_pass.action = opaque_action;
        state.transmission.opaque_pass.label = "transmission-opaque-pass";

        // Create pipelines for rendering opaque objects to screen texture
        // These pipelines are identical to standard pipelines but render to offscreen texture
        Info("[Transmission] Creating opaque rendering pipelines...");
        state.transmission.opaque_standard_pipeline = PipeLineManager.CreatePipelineForPass(
            PipelineType.TransmissionOpaque,
            sg_pixel_format.SG_PIXELFORMAT_RGBA8,  // screen_color_img format
            sg_pixel_format.SG_PIXELFORMAT_DEPTH,   // screen_depth_img format
            1                                        // sample_count
        );
        state.transmission.opaque_skinned_pipeline = PipeLineManager.CreatePipelineForPass(
            PipelineType.TransmissionOpaqueSkinned,
            sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            sg_pixel_format.SG_PIXELFORMAT_DEPTH,
            1
        );
        Info("[Transmission] Opaque pipelines created (standard + skinned)");

        Info("[Transmission] Transmission system initialized successfully");
        Info("[Transmission] Two-pass rendering ready: Pass 1 (opaque objects → screen texture), Pass 2 (transparent with refraction)");
        Info("[Transmission] Screen texture available for refraction shader sampling");
    }
}