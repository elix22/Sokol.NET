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
using static bloom_shader_cs.Shaders;

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

        // Initialize UI state - show model browser by default
        state.ui.model_browser_open = true;

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
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.5f, g = 0.5f, b = 0.5f, a = 1.0f };

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

                    // Create a dummy FileReaderCallback - we won't actually call it since we're using async loading
                    // This is just to satisfy the ReadContext.Create API
                    SharpGLTF.Schema2.FileReaderCallback fileReader = (assetName) =>
                    {
                        throw new InvalidOperationException("Synchronous file reading should not be used. Using async loading instead.");
                    };

                    var context = SharpGLTF.Schema2.ReadContext.Create(fileReader);

                    // Skip automatic satellite dependency resolution - we'll do it manually and asynchronously
                    context.SkipSatelliteDependencies = true;

                    // Read the JSON structure without loading satellite dependencies
                    ModelRoot modelRoot = context.ReadSchema2(memoryStream);

                    Info($"[SharpGLTF] Model structure loaded, beginning async dependency loading...");

                    // Store the modelRoot and path for async dependency loading
                    state.pendingModelRoot = modelRoot;
                    state.pendingModelPath = path;
                    state.isLoadingModel = true;
                    state.loadingStage = "Loading dependencies";

                    // Begin async loading of satellite dependencies
                    // Pass the context so embedded buffers (GLB binary chunk) can be resolved
                    state.asyncLoadState = modelRoot._BeginAsyncResolveSatelliteDependencies(context);

                    Info($"[SharpGLTF] Found {state.asyncLoadState.TotalDependencies} external dependencies to load");

                    // The actual dependency loading will happen in RunSingleFrame() one at a time
                    // Once all dependencies are loaded, we'll finalize the model there
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

    /// <summary>
    /// Destroy only the resources that MUST be recreated on window resize (images, views, samplers).
    /// Pipelines are NOT destroyed because they contain shaders and don't depend on framebuffer size.
    /// This prevents shader pool exhaustion while still handling resize correctly.
    /// </summary>
    private static void CleanupAllResources()
    {
        Info("[Resize] Cleaning up size-dependent resources...");

        // Uninitialize Bloom images and views (size-dependent) - following MRT example pattern
        if (state.bloom.scene_color_img.id != 0)
        {
            Info("[Resize] Uninitializing bloom resources...");
            sg_uninit_image(state.bloom.scene_color_img);
            sg_uninit_image(state.bloom.scene_depth_img);
            sg_uninit_image(state.bloom.bright_img);
            sg_uninit_image(state.bloom.blur_h_img);
            sg_uninit_image(state.bloom.blur_v_img);
            sg_uninit_image(state.bloom.dummy_depth_img);

            // Uninitialize bloom views
            sg_uninit_view(state.bloom.scene_pass.attachments.colors[0]);
            sg_uninit_view(state.bloom.scene_pass.attachments.depth_stencil);
            sg_uninit_view(state.bloom.bright_pass.attachments.colors[0]);
            sg_uninit_view(state.bloom.bright_pass.attachments.depth_stencil);
            sg_uninit_view(state.bloom.blur_h_pass.attachments.colors[0]);
            sg_uninit_view(state.bloom.blur_h_pass.attachments.depth_stencil);
            sg_uninit_view(state.bloom.blur_v_pass.attachments.colors[0]);
            sg_uninit_view(state.bloom.blur_v_pass.attachments.depth_stencil);

            // Uninitialize sampler
            if (state.bloom.sampler.id != 0)
            {
                sg_uninit_sampler(state.bloom.sampler);
            }

            Info("[Resize] Bloom resources uninitialized");
        }

        // Uninitialize Transmission images and views (size-dependent)
        if (state.transmission.screen_color_img.id != 0)
        {
            Info("[Resize] Uninitializing transmission resources...");
            sg_uninit_image(state.transmission.screen_color_img);
            sg_uninit_image(state.transmission.screen_depth_img);
            sg_uninit_view(state.transmission.screen_color_view);
            sg_uninit_view(state.transmission.opaque_pass.attachments.colors[0]);
            sg_uninit_view(state.transmission.opaque_pass.attachments.depth_stencil);

            // Uninitialize sampler
            if (state.transmission.sampler.id != 0)
            {
                sg_uninit_sampler(state.transmission.sampler);
            }

            Info("[Resize] Transmission resources uninitialized");
        }

        Info("[Resize] Cleanup complete");
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

        // Allocate/initialize color texture for main scene rendering (full resolution)
        // Following MRT example pattern: alloc once, then init/uninit/reinit on resize
        if (state.bloom.scene_color_img.id == 0)
        {
            state.bloom.scene_color_img = sg_alloc_image();
        }
        sg_init_image(state.bloom.scene_color_img, new sg_image_desc()
        {
            usage = { color_attachment = true },
            width = fb_width,
            height = fb_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,  // Explicit format for offscreen rendering
            sample_count = 1,  // Offscreen passes don't use MSAA
            label = "bloom-scene-color"
        });

        // Allocate/initialize depth texture for main scene rendering  
        if (state.bloom.scene_depth_img.id == 0)
        {
            state.bloom.scene_depth_img = sg_alloc_image();
        }
        sg_init_image(state.bloom.scene_depth_img, new sg_image_desc()
        {
            usage = { depth_stencil_attachment = true },
            width = fb_width,
            height = fb_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,  // Same as offscreen example - works on all platforms
            sample_count = 1,  // Offscreen passes don't use MSAA
            label = "bloom-scene-depth"
        });

        // Allocate/initialize bloom processing textures (reduced resolution)
        if (state.bloom.bright_img.id == 0)
        {
            state.bloom.bright_img = sg_alloc_image();
        }
        sg_init_image(state.bloom.bright_img, new sg_image_desc()
        {
            usage = { color_attachment = true },
            width = bloom_width,
            height = bloom_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            sample_count = 1,
            label = "bloom-bright"
        });

        if (state.bloom.blur_h_img.id == 0)
        {
            state.bloom.blur_h_img = sg_alloc_image();
        }
        sg_init_image(state.bloom.blur_h_img, new sg_image_desc()
        {
            usage = { color_attachment = true },
            width = bloom_width,
            height = bloom_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            sample_count = 1,
            label = "bloom-blur-h"
        });

        if (state.bloom.blur_v_img.id == 0)
        {
            state.bloom.blur_v_img = sg_alloc_image();
        }
        sg_init_image(state.bloom.blur_v_img, new sg_image_desc()
        {
            usage = { color_attachment = true },
            width = bloom_width,
            height = bloom_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            sample_count = 1,
            label = "bloom-blur-v"
        });

        // Allocate/initialize dummy depth buffer matching bloom resolution
        // WebGL requires consistent framebuffer attachments - we can't switch between
        // passes with depth and without depth on the same FBO without explicitly unbinding.
        // The workaround is to always have a depth attachment, even if unused.
        // IMPORTANT: All attachments must have the same dimensions!
        if (state.bloom.dummy_depth_img.id == 0)
        {
            state.bloom.dummy_depth_img = sg_alloc_image();
        }
        sg_init_image(state.bloom.dummy_depth_img, new sg_image_desc()
        {
            usage = { depth_stencil_attachment = true },
            width = bloom_width,
            height = bloom_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
            sample_count = 1,
            label = "bloom-dummy-depth"
        });

        // Allocate/initialize sampler for all bloom passes
        if (state.bloom.sampler.id == 0)
        {
            state.bloom.sampler = sg_alloc_sampler();
        }
        sg_init_sampler(state.bloom.sampler, new sg_sampler_desc()
        {
            min_filter = sg_filter.SG_FILTER_LINEAR,
            mag_filter = sg_filter.SG_FILTER_LINEAR,
            wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
            wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
            label = "bloom-sampler"
        });

        // Allocate/initialize render passes
        // Scene pass (renders main scene to offscreen buffer)
        // Following MRT example pattern: alloc once, then init/uninit/reinit on resize
        if (state.bloom.scene_pass.attachments.colors[0].id == 0)
        {
            state.bloom.scene_pass.attachments.colors[0] = sg_alloc_view();
        }
        sg_init_view(state.bloom.scene_pass.attachments.colors[0], new sg_view_desc()
        {
            color_attachment = { image = state.bloom.scene_color_img },
            label = "scene-color-view"
        });

        if (state.bloom.scene_pass.attachments.depth_stencil.id == 0)
        {
            state.bloom.scene_pass.attachments.depth_stencil = sg_alloc_view();
        }
        sg_init_view(state.bloom.scene_pass.attachments.depth_stencil, new sg_view_desc()
        {
            depth_stencil_attachment = { image = state.bloom.scene_depth_img },
            label = "scene-depth-view"
        });

        Info($"[Bloom] Scene views initialized: color={state.bloom.scene_pass.attachments.colors[0].id}, depth={state.bloom.scene_pass.attachments.depth_stencil.id}");

        // Create action
        sg_pass_action scene_action = default;
        scene_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        scene_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };
        scene_action.depth.load_action = sg_load_action.SG_LOADACTION_CLEAR;
        scene_action.depth.clear_value = 1.0f;

        // Assign action to pass (attachments already set above)
        state.bloom.scene_pass.action = scene_action;
        state.bloom.scene_pass.label = "bloom-scene-pass";

        // Allocate/initialize bright pass - WebGL requires depth attachment for FBO consistency
        if (state.bloom.bright_pass.attachments.colors[0].id == 0)
        {
            state.bloom.bright_pass.attachments.colors[0] = sg_alloc_view();
        }
        sg_init_view(state.bloom.bright_pass.attachments.colors[0], new sg_view_desc()
        {
            color_attachment = { image = state.bloom.bright_img },
            label = "bright-view"
        });

        if (state.bloom.bright_pass.attachments.depth_stencil.id == 0)
        {
            state.bloom.bright_pass.attachments.depth_stencil = sg_alloc_view();
        }
        sg_init_view(state.bloom.bright_pass.attachments.depth_stencil, new sg_view_desc()
        {
            depth_stencil_attachment = { image = state.bloom.dummy_depth_img },
            label = "bright-depth-view"
        });

        sg_pass_action bright_action = default;
        bright_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        bright_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };

        state.bloom.bright_pass.action = bright_action;
        state.bloom.bright_pass.label = "bloom-bright-pass";

        // Allocate/initialize horizontal blur pass - WebGL requires depth attachment for FBO consistency
        if (state.bloom.blur_h_pass.attachments.colors[0].id == 0)
        {
            state.bloom.blur_h_pass.attachments.colors[0] = sg_alloc_view();
        }
        sg_init_view(state.bloom.blur_h_pass.attachments.colors[0], new sg_view_desc()
        {
            color_attachment = { image = state.bloom.blur_h_img },
            label = "blur-h-view"
        });

        if (state.bloom.blur_h_pass.attachments.depth_stencil.id == 0)
        {
            state.bloom.blur_h_pass.attachments.depth_stencil = sg_alloc_view();
        }
        sg_init_view(state.bloom.blur_h_pass.attachments.depth_stencil, new sg_view_desc()
        {
            depth_stencil_attachment = { image = state.bloom.dummy_depth_img },
            label = "blur-h-depth-view"
        });

        sg_pass_action blur_h_action = default;
        blur_h_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        blur_h_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };

        state.bloom.blur_h_pass.action = blur_h_action;
        state.bloom.blur_h_pass.label = "bloom-blur-h-pass";

        // Allocate/initialize vertical blur pass - WebGL requires depth attachment for FBO consistency
        if (state.bloom.blur_v_pass.attachments.colors[0].id == 0)
        {
            state.bloom.blur_v_pass.attachments.colors[0] = sg_alloc_view();
        }
        sg_init_view(state.bloom.blur_v_pass.attachments.colors[0], new sg_view_desc()
        {
            color_attachment = { image = state.bloom.blur_v_img },
            label = "blur-v-view"
        });

        if (state.bloom.blur_v_pass.attachments.depth_stencil.id == 0)
        {
            state.bloom.blur_v_pass.attachments.depth_stencil = sg_alloc_view();
        }
        sg_init_view(state.bloom.blur_v_pass.attachments.depth_stencil, new sg_view_desc()
        {
            depth_stencil_attachment = { image = state.bloom.dummy_depth_img },
            label = "blur-v-depth-view"
        });

        sg_pass_action blur_v_action = default;
        blur_v_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        blur_v_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };

        state.bloom.blur_v_pass.action = blur_v_action;
        state.bloom.blur_v_pass.label = "bloom-blur-v-pass";

        // Note: Composite pass renders to swapchain and must be created each frame
        // with the current swapchain, so we don't create it here.

        // Create offscreen pipelines for rendering the model to bloom scene pass (only if not already created)
        // Use SG_PIXELFORMAT_DEPTH exactly like the offscreen example
        // These pipelines don't depend on framebuffer size, so we only create them once
        if (state.bloom.scene_standard_pipeline.id == 0)
        {
            state.bloom.scene_standard_pipeline = PipeLineManager.GetOrCreatePipeline(
                PipelineType.Standard, colorFormat: sg_pixel_format.SG_PIXELFORMAT_RGBA8, depthFormat: sg_pixel_format.SG_PIXELFORMAT_DEPTH, sampleCount: 1);
            state.bloom.scene_skinned_pipeline = PipeLineManager.GetOrCreatePipeline(
                PipelineType.Skinned, colorFormat: sg_pixel_format.SG_PIXELFORMAT_RGBA8, depthFormat: sg_pixel_format.SG_PIXELFORMAT_DEPTH, sampleCount: 1);
            state.bloom.scene_standard_blend_pipeline = PipeLineManager.GetOrCreatePipeline(
                PipelineType.StandardBlend, colorFormat: sg_pixel_format.SG_PIXELFORMAT_RGBA8, depthFormat: sg_pixel_format.SG_PIXELFORMAT_DEPTH, sampleCount: 1);
            state.bloom.scene_skinned_blend_pipeline = PipeLineManager.GetOrCreatePipeline(
                PipelineType.SkinnedBlend, colorFormat: sg_pixel_format.SG_PIXELFORMAT_RGBA8, depthFormat: sg_pixel_format.SG_PIXELFORMAT_DEPTH, sampleCount: 1);
            state.bloom.scene_standard_mask_pipeline = PipeLineManager.GetOrCreatePipeline(
                PipelineType.StandardMask, colorFormat: sg_pixel_format.SG_PIXELFORMAT_RGBA8, depthFormat: sg_pixel_format.SG_PIXELFORMAT_DEPTH, sampleCount: 1);
            state.bloom.scene_skinned_mask_pipeline = PipeLineManager.GetOrCreatePipeline(
                PipelineType.SkinnedMask, colorFormat: sg_pixel_format.SG_PIXELFORMAT_RGBA8, depthFormat: sg_pixel_format.SG_PIXELFORMAT_DEPTH, sampleCount: 1);
        }

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

        // Create pipelines for bloom post-processing passes (only if not already created)
        // These don't depend on framebuffer size, so we only create them once
        if (state.bloom.bright_pipeline.id == 0)
        {
            state.bloom.bright_pipeline = PipeLineManager.GetOrCreatePipeline(
                PipelineType.BloomBright,
                cullMode: sg_cull_mode.SG_CULLMODE_NONE,
                colorFormat: sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                depthFormat: sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                sampleCount: 1
            );
        }

        if (state.bloom.blur_h_pipeline.id == 0)
        {
            state.bloom.blur_h_pipeline = PipeLineManager.GetOrCreatePipeline(
                PipelineType.BloomBlurHorizontal,
                cullMode: sg_cull_mode.SG_CULLMODE_NONE,
                colorFormat: sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                depthFormat: sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                sampleCount: 1
            );
        }

        if (state.bloom.blur_v_pipeline.id == 0)
        {
            state.bloom.blur_v_pipeline = PipeLineManager.GetOrCreatePipeline(
                PipelineType.BloomBlurVertical,
                cullMode: sg_cull_mode.SG_CULLMODE_NONE,
                colorFormat: sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                depthFormat: sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                sampleCount: 1
            );
        }

        if (state.bloom.composite_pipeline.id == 0)
        {
            state.bloom.composite_pipeline = PipeLineManager.GetOrCreatePipeline(PipelineType.BloomComposite,cullMode: sg_cull_mode.SG_CULLMODE_NONE);
        }

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

        // Allocate/initialize screen texture for capturing scene behind transparent objects
        // Following MRT example pattern: alloc once, then init/uninit/reinit on resize
        if (state.transmission.screen_color_img.id == 0)
        {
            state.transmission.screen_color_img = sg_alloc_image();
        }
        sg_init_image(state.transmission.screen_color_img, new sg_image_desc()
        {
            usage = { color_attachment = true },
            width = fb_width,
            height = fb_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            sample_count = 1,  // No MSAA for offscreen rendering
            label = "transmission-screen-color"
        });

        // Allocate/initialize depth texture for opaque pass
        if (state.transmission.screen_depth_img.id == 0)
        {
            state.transmission.screen_depth_img = sg_alloc_image();
        }
        sg_init_image(state.transmission.screen_depth_img, new sg_image_desc()
        {
            usage = { depth_stencil_attachment = true },
            width = fb_width,
            height = fb_height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
            sample_count = 1,
            label = "transmission-screen-depth"
        });

        // Allocate/initialize sampler for screen texture sampling
        if (state.transmission.sampler.id == 0)
        {
            state.transmission.sampler = sg_alloc_sampler();
        }
        sg_init_sampler(state.transmission.sampler, new sg_sampler_desc()
        {
            min_filter = sg_filter.SG_FILTER_LINEAR,
            mag_filter = sg_filter.SG_FILTER_LINEAR,
            wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
            wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
            label = "transmission-sampler"
        });

        // Allocate/initialize view for screen texture (create once, reuse every frame)
        if (state.transmission.screen_color_view.id == 0)
        {
            state.transmission.screen_color_view = sg_alloc_view();
        }
        sg_init_view(state.transmission.screen_color_view, new sg_view_desc
        {
            texture = { image = state.transmission.screen_color_img },
            label = "transmission-screen-color-view"
        });
        Info("[Transmission] Initialized screen color view for refraction sampling");

        // Allocate/initialize opaque pass (renders opaque objects to screen texture)
        // Following MRT example pattern: alloc once, then init/uninit/reinit on resize
        if (state.transmission.opaque_pass.attachments.colors[0].id == 0)
        {
            state.transmission.opaque_pass.attachments.colors[0] = sg_alloc_view();
        }
        sg_init_view(state.transmission.opaque_pass.attachments.colors[0], new sg_view_desc()
        {
            color_attachment = { image = state.transmission.screen_color_img },
            label = "opaque-color-view"
        });

        if (state.transmission.opaque_pass.attachments.depth_stencil.id == 0)
        {
            state.transmission.opaque_pass.attachments.depth_stencil = sg_alloc_view();
        }
        sg_init_view(state.transmission.opaque_pass.attachments.depth_stencil, new sg_view_desc()
        {
            depth_stencil_attachment = { image = state.transmission.screen_depth_img },
            label = "opaque-depth-view"
        });

        sg_pass_action opaque_action = default;
        opaque_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        opaque_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };
        opaque_action.depth.load_action = sg_load_action.SG_LOADACTION_CLEAR;
        opaque_action.depth.clear_value = 1.0f;

        state.transmission.opaque_pass.action = opaque_action;
        state.transmission.opaque_pass.label = "transmission-opaque-pass";

        // Create pipelines for rendering opaque objects to screen texture (only if not already created)
        // These pipelines don't depend on framebuffer size, so we only create them once
        if (state.transmission.opaque_standard_pipeline.id == 0)
        {
            Info("[Transmission] Creating opaque rendering pipelines...");
            state.transmission.opaque_standard_pipeline = PipeLineManager.GetOrCreatePipeline(
                PipelineType.TransmissionOpaque,
                colorFormat: sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                depthFormat: sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                sampleCount: 1
            );
            state.transmission.opaque_skinned_pipeline = PipeLineManager.GetOrCreatePipeline(
                PipelineType.TransmissionOpaqueSkinned,
                colorFormat: sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                depthFormat: sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                sampleCount: 1
            );
            Info("[Transmission] Opaque pipelines created (standard + skinned)");
        }

        Info("[Transmission] Transmission system initialized successfully");
        Info("[Transmission] Two-pass rendering ready: Pass 1 (opaque objects → screen texture), Pass 2 (transparent with refraction)");
        Info("[Transmission] Screen texture available for refraction shader sampling");
    }

    /// <summary>
    /// Creates an ImageDecoder callback that decodes and uploads images to GPU during async loading.
    /// This spreads the expensive stb_image decode + GPU upload across multiple frames (one per frame).
    /// Returns true to keep compressed image in memory (needed for validation).
    /// </summary>
    /// <remarks>
    /// Strategy: Create ONE texture per image using a base identifier (just the LogicalIndex).
    /// SharpGltfModel.ProcessMesh() will look up textures using the same base identifier.
    /// The TextureCache handles the (identifier + format) key, so the same physical texture
    /// can be referenced by multiple materials/channels without duplication.
    /// </remarks>
    static ImageDecodeCallback CreateImageDecoder()
    {
        return (SharpGLTF.Schema2.Image image) =>
        {
            try
            {
                // Get the compressed image content (PNG/JPEG)
                var content = image.Content;
                if (content.IsEmpty)
                {
                    Error($"[ImageDecoder] Image {image.LogicalIndex} has no content");
                    return true;
                }

                Info($"[ImageDecoder] Decoding image {image.LogicalIndex} ({content.Content.Length} bytes)");

                // Get the compressed image bytes
                var imageBytes = content._GetBuffer().ToArray();

                // Create base texture identifier using only the image index
                // This is the key: ONE texture per image, not per channel
                string textureId = $"image_{image.LogicalIndex}";

                // All textures use RGBA8 format (shader handles sRGB conversion)
                sg_pixel_format format = sg_pixel_format.SG_PIXELFORMAT_RGBA8;

                // Decode image and upload to GPU NOW (spreads work across frames)
                // TextureCache will:
                // 1. Decompress PNG/JPEG using stb_image
                // 2. Upload RGBA pixels to GPU
                // 3. Cache the result by (textureId + format)
                var texture = TextureCache.Instance.GetOrCreate(textureId, imageBytes, format);

                if (texture != null)
                {
                    Info($"[ImageDecoder] Created GPU texture: {textureId}");
                }
                else
                {
                    Error($"[ImageDecoder] Failed to create texture: {textureId}");
                }

                // Return true to keep the compressed image in memory
                // This is needed for validation (which accesses image.Content)
                return true;
            }
            catch (Exception ex)
            {
                Error($"[ImageDecoder] Error decoding image {image.LogicalIndex}: {ex.Message}");
                return true;
            }
        };
    }

    public static void LoadNewModel()
    {
        if (state.isLoadingModel)
            return;

        state.isLoadingModel = true;
        state.loadingStage = "Preparing...";
        state.loadingProgress = 0;
        Info($"[ModelBrowser] Loading new model: {filename}");

        // Store old model for disposal after new one loads
        var oldModel = state.model;

        // Clear state BEFORE disposing (so rendering stops using it)
        state.model = null;
        state.animator = null;
        state.modelLoaded = false;
        state.cameraInitialized = false;

        // Dispose the old model (after rendering has stopped using it)
        oldModel?.Dispose();

        // Clear the texture cache since old textures are now invalid
        // This ensures the new model creates fresh textures instead of reusing disposed ones
        TextureCache.Instance.Clear();

        state.loadingStage = "Loading file...";
        state.loadingProgress = 20;

        // Load new model asynchronously
        FileSystem.Instance.LoadFile(filename, (path, buffer, status) =>
        {
            if (status == FileLoadStatus.Success && buffer != null)
            {
                try
                {
                    state.loadingStage = "Parsing glTF...";
                    state.loadingProgress = 40;

                    var memoryStream = new MemoryStream(buffer);

                    // Get the directory of the main GLTF file for resolving relative paths
                    string? baseDirectory = Path.GetDirectoryName(path);

                    // Create a dummy FileReaderCallback - we won't actually call it since we're using async loading
                    // This is just to satisfy the ReadContext.Create API
                    SharpGLTF.Schema2.FileReaderCallback fileReader = (assetName) =>
                    {
                        throw new InvalidOperationException("Synchronous file reading should not be used. Using async loading instead.");
                    };

                    var context = SharpGLTF.Schema2.ReadContext.Create(fileReader);

                    // Skip automatic satellite dependency resolution - we'll do it manually and asynchronously
                    context.SkipSatelliteDependencies = true;

                    // Read the JSON structure without loading satellite dependencies
                    ModelRoot modelRoot = context.ReadSchema2(memoryStream);

                    Info($"[SharpGLTF] Model structure loaded, beginning async dependency loading...");

                    // Store the modelRoot and path for async dependency loading
                    state.pendingModelRoot = modelRoot;
                    state.pendingModelPath = path;
                    state.loadingStage = "Loading dependencies";

                    // Begin async loading of satellite dependencies
                    // Pass the context so embedded buffers (GLB binary chunk) can be resolved
                    state.asyncLoadState = modelRoot._BeginAsyncResolveSatelliteDependencies(context);

                    Info($"[SharpGLTF] Found {state.asyncLoadState.TotalDependencies} external dependencies to load");

                    // The actual dependency loading will happen in RunSingleFrame() one at a time
                    // Once all dependencies are loaded, we'll finalize the model there

                    // Remove the old code that calculated bounds here - it will be done after async loading completes
                }
                catch (Exception ex)
                {
                    Error($"[ModelBrowser] Error starting async model load: {ex.Message}");
                    state.loadingStage = "Error!";
                    state.loadingProgress = 0;
                    state.isLoadingModel = false;
                    state.pendingModelRoot = null;
                    state.asyncLoadState = null;
                }
            }
            else
            {
                Error($"[ModelBrowser] Failed to load file '{path}': {status}");
                state.loadingStage = "Failed!";
                state.loadingProgress = 0;
                state.isLoadingModel = false;
            }
        });
    }
}