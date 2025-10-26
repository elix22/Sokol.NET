using System;
using System.IO;
using System.Collections.Generic;
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
using static Sokol.SFetch;
using static Sokol.SBasisu;
using static cgltf_scene_app_shader_cs.Shaders;

public static unsafe class CGLTFSceneApp
{
    static string modelPath = "gltf/DamagedHelmet/DamagedHelmet.gltf";

    class _state
    {
        public sg_pass_action pass_action;
        public Sokol.Camera camera = new Sokol.Camera();
        public CGltfParser? parser;
        public CGltfScene? scene;
        public bool sceneLoaded = false;
        public bool cameraInitialized = false;
        public Matrix4x4 rootTransform = Matrix4x4.Identity;
        
        // Lighting
        public List<Sokol.Light> lights = new List<Sokol.Light>();
        public Vector3 ambientColor = new Vector3(0.5f, 0.6f, 0.75f);
        public float ambientIntensity = 0.6f;
        
        // Placeholder texture for models without textures
        public sg_image placeholderTexture;
        public sg_sampler placeholderSampler;
        public sg_view placeholderView;
    }

    static _state state = new _state();

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        Log("cgltf-scene-app: Init()");
        
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            buffer_pool_size = 2048,
            image_pool_size = 512,
            sampler_pool_size = 256,
            logger = {
                func = &slog_func,
            }
        });

        // Initialize Basis Universal for texture loading
        sbasisu_setup();

        // Initialize FileSystem for async file loading
        FileSystem.Instance.Initialize();

        // Initialize camera
        state.camera.Init(new CameraDesc()
        {
            Aspect = 60.0f,
            NearZ = 0.1f,
            FarZ = 1000.0f,
            Center = Vector3.Zero,
            Distance = 3.0f,
            MaxDist = 100.0f
        });

        // Setup pass action
        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };

        // Create placeholder white texture
        CreatePlaceholderTexture();

        // Create shader
        sg_shader shd = sg_make_shader(cgltf_scene_shader_desc(sg_query_backend()));

        // Initialize CGLTF parser
        state.parser = new CGltfParser();
        state.parser.Init(shd, shd); // Use same shader for metallic and specular

        // Load GLTF model
        string filePath = util_get_file_path(modelPath);
        Info($"CGLTF: Loading file: {filePath}");
        
        state.parser.LoadFromFileAsync(filePath, 
            onComplete: (scene) => 
            {
                state.scene = scene;
                state.sceneLoaded = true;
                Info($"CGLTF: Scene loaded successfully - {scene.NumNodes} nodes, {scene.NumMeshes} meshes");
            },
            onFailed: (error) => 
            {
                Error($"CGLTF: Failed to load scene: {error}");
            }
        );

        // Setup sunny day lighting
        state.lights.Add(Sokol.Light.CreateDirectionalLight(
            new Vector3(0.3f, -0.8f, -0.2f),
            new Vector3(1.0f, 0.98f, 0.95f),
            1.2f
        ));
        
        state.lights.Add(Sokol.Light.CreateDirectionalLight(
            new Vector3(0.0f, -1.0f, 0.0f),
            new Vector3(0.6f, 0.7f, 1.0f),
            0.25f
        ));
        
        state.lights.Add(Sokol.Light.CreateDirectionalLight(
            new Vector3(0.0f, 0.5f, 0.0f),
            new Vector3(0.9f, 0.85f, 0.8f),
            0.15f
        ));
    }

    static void CreatePlaceholderTexture()
    {
        uint[] pixels = new uint[64];
        for (int i = 0; i < 64; i++)
            pixels[i] = 0xFFFFFFFF;

        sg_image_desc imgDesc = new sg_image_desc
        {
            width = 8,
            height = 8,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
        };

        fixed (uint* pixelsPtr = pixels)
        {
            imgDesc.data.mip_levels[0] = new sg_range { ptr = pixelsPtr, size = sizeof(uint) * 64 };
            state.placeholderTexture = sg_make_image(imgDesc);
        }

        state.placeholderSampler = sg_make_sampler(new sg_sampler_desc
        {
            min_filter = sg_filter.SG_FILTER_LINEAR,
            mag_filter = sg_filter.SG_FILTER_LINEAR
        });

        // Create placeholder view
        state.placeholderView = sg_make_view(new sg_view_desc { texture = { image = state.placeholderTexture } });
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // Update FileSystem to process pending requests
        FileSystem.Instance.Update();

        // Auto-position camera on first load - simple fixed distance for GLTF models
        if (!state.cameraInitialized && state.sceneLoaded && state.scene != null)
        {
            // For DamagedHelmet and similar GLTF models, a distance of 2.5-3 units works well
            // The model is typically centered at origin in GLTF files
            state.camera.Center = Vector3.Zero;
            state.camera.Distance = 2.5f;
            
            state.cameraInitialized = true;
            Info($"Camera initialized: Center={Vector3.Zero}, Distance=2.5");
        }

        int fbWidth = sapp_width();
        int fbHeight = sapp_height();
        state.camera.Update(fbWidth, fbHeight);

        // Rotate model
        state.rootTransform = Matrix4x4.CreateRotationY((float)(sapp_frame_count() * 0.01f));

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });

        if (state.sceneLoaded && state.scene != null)
        {
            RenderScene();
        }

        sg_end_pass();
        sg_commit();
    }

    static void RenderScene()
    {
        if (state.scene == null) return;

        // Render all nodes in the scene
        for (int nodeIdx = 0; nodeIdx < state.scene.NumNodes; nodeIdx++)
        {
            ref var node = ref state.scene.Nodes[nodeIdx];
            ref var mesh = ref state.scene.Meshes[node.MeshIndex];

            // Calculate final transform
            Matrix4x4 modelMatrix = state.rootTransform * node.Transform;

            // Render all primitives (submeshes) in this mesh
            for (int i = 0; i < mesh.NumPrimitives; i++)
            {
                int primIdx = mesh.FirstPrimitive + i;
                ref var prim = ref state.scene.Primitives[primIdx];

                // Apply pipeline
                sg_apply_pipeline(state.scene.Pipelines[prim.PipelineIndex]);

                // Bind vertex buffers
                var bindings = new sg_bindings();
                for (int vbSlot = 0; vbSlot < prim.VertexBuffers.Num; vbSlot++)
                {
                    bindings.vertex_buffers[vbSlot] = state.scene.Buffers[prim.VertexBuffers.BufferIndices[vbSlot]];
                }

                // Bind index buffer if present
                if (prim.IndexBuffer != CGltfSceneLimits.INVALID_INDEX)
                {
                    bindings.index_buffer = state.scene.Buffers[prim.IndexBuffer];
                }

                // Bind textures - always bind something to avoid validation errors
                ref var material = ref state.scene.Materials[prim.MaterialIndex];
                
                sg_view texView;
                sg_sampler texSampler;
                
                if (material.IsMetallic && material.Metallic.Images.BaseColor != CGltfSceneLimits.INVALID_INDEX)
                {
                    ref var img = ref state.scene.Images[material.Metallic.Images.BaseColor];
                    // Check if the image is actually loaded
                    if (img.TexView.id != 0 && img.Sampler.id != 0)
                    {
                        texView = img.TexView;
                        texSampler = img.Sampler;
                    }
                    else
                    {
                        // Image not loaded yet, use placeholder
                        texView = state.placeholderView;
                        texSampler = state.placeholderSampler;
                    }
                }
                else
                {
                    // No texture or not metallic, use placeholder
                    texView = state.placeholderView;
                    texSampler = state.placeholderSampler;
                }
                
                bindings.views[VIEW_tex] = texView;
                bindings.samplers[SMP_smp] = texSampler;

                sg_apply_bindings(bindings);

                // Setup vertex shader params
                var vsParams = new vs_params_t
                {
                    projection = state.camera.Proj,
                    view = state.camera.View,
                    model = modelMatrix
                };

                sg_apply_uniforms(UB_vs_params, SG_RANGE(ref vsParams));

                // Setup fragment shader params (lighting)
                var fsParams = PrepareFragmentShaderParams();

                sg_apply_uniforms(UB_fs_params, SG_RANGE(ref fsParams));

                // Draw
                if (prim.IndexBuffer != CGltfSceneLimits.INVALID_INDEX)
                {
                    sg_draw((uint)prim.BaseElement, (uint)prim.NumElements, 1);
                }
                else
                {
                    sg_draw((uint)prim.BaseElement, (uint)prim.NumElements, 1);
                }
            }
        }
    }

    static fs_params_t PrepareFragmentShaderParams()
    {
        var fsParams = new fs_params_t
        {
            camera_pos = state.camera.EyePos,
            num_lights = Math.Min(state.lights.Count, 4),
            ambient_color = state.ambientColor,
            ambient_intensity = state.ambientIntensity
        };

        // Pack lights into shader params
        for (int i = 0; i < Math.Min(state.lights.Count, 4); i++)
        {
            var light = state.lights[i];
            var position = new Vector4(light.Position.X, light.Position.Y, light.Position.Z, (float)light.Type);
            var direction = new Vector4(light.Direction.X, light.Direction.Y, light.Direction.Z, 
                (float)Math.Cos(light.SpotInnerAngle * Math.PI / 180.0));
            var color = new Vector4(light.Color.X, light.Color.Y, light.Color.Z, light.Intensity);
            var param = new Vector4(light.Range, (float)Math.Cos(light.SpotOuterAngle * Math.PI / 180.0), 0, 0);

            fsParams.light_positions[i] = position;
            fsParams.light_directions[i] = direction;
            fsParams.light_colors[i] = color;
            fsParams.light_params[i] = param;
        }

        return fsParams;
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        state.camera.HandleEvent(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        state.scene?.Dispose();
        sg_destroy_view(state.placeholderView);
        sg_destroy_image(state.placeholderTexture);
        sg_destroy_sampler(state.placeholderSampler);
        sbasisu_shutdown();
        FileSystem.Instance.Shutdown();
        sg_shutdown();

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
            width = 1280,
            height = 720,
            sample_count = 4,
            window_title = "CGLTF Scene Viewer (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }
}
