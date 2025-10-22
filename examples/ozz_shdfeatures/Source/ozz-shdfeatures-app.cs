using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_load_action;
using static Sokol.SFetch;
using static Sokol.SGL;
using static Sokol.Utils;
using static Sokol.SLog;
using static Sokol.SImgui;
using static Sokol.OzzUtil;
using Imgui;
using static Imgui.ImguiNative;
using System.Diagnostics;


// Aliases for cleaner access
using NoneShader = shdfeatures_sapp_shader_none_cs_none.Shaders;
using SShader = shdfeatures_sapp_shader_s_cs_s.Shaders;
using LShader = shdfeatures_sapp_shader_l_cs_l.Shaders;
using MShader = shdfeatures_sapp_shader_m_cs_m.Shaders;
using SLShader = shdfeatures_sapp_shader_sl_cs_sl.Shaders;
using SMShader = shdfeatures_sapp_shader_sm_cs_sm.Shaders;
using LMShader = shdfeatures_sapp_shader_lm_cs_lm.Shaders;
using SLMShader = shdfeatures_sapp_shader_slm_cs_slm.Shaders;

public static unsafe class OzzShdFeaturesApp
{
    // Helper method for logging
    private static void LogMessage(uint level, string message)
    {
        // Use Console.WriteLine instead of slog_func to avoid UnmanagedCallersOnly issue
        if (level == 1)
        {
            Console.WriteLine($"[ERROR] {message}");
        }
        else
        {
            Console.WriteLine($"[INFO] {message}");
        }
    }
    // Shader feature flags
    const uint SHD_NONE = 0;
    const uint SHD_SKINNING = 1 << 0;  // skinning is enabled
    const uint SHD_LIGHTING = 1 << 1;  // lighting is enabled  
    const uint SHD_MATERIAL = 1 << 2;  // material attributes are enabled

    const int MAX_SHADER_VARIATIONS = 1 << 3; // 8 variations total
    const int MAX_UNIFORMBLOCK_SIZE = 256;

    // Generic uniform data upload buffers
    static byte[] vs_params_buffer = new byte[MAX_UNIFORMBLOCK_SIZE];
    static byte[] phong_params_buffer = new byte[MAX_UNIFORMBLOCK_SIZE];

    // Pointerized uniform-block structs filled at runtime from shader reflection
    struct VsParamsPtr
    {
        public bool valid;
        public int slot;
        public int num_bytes;
        public unsafe Matrix4x4* mvp;
        public unsafe Matrix4x4* model;
        public unsafe Vector2* joint_uv;
        public unsafe float* joint_pixel_width;
    }

    struct PhongParamsPtr
    {
        public bool valid;
        public int slot;
        public int num_bytes;
        public unsafe Vector3* light_dir;
        public unsafe Vector3* eye_pos;
        public unsafe Vector3* light_color;
        public unsafe Vector3* mat_diffuse;
        public unsafe Vector3* mat_specular;
        public unsafe float* mat_spec_power;
    }

    // Shader variation descriptor
    struct ShaderVariation
    {
        public bool valid;
        public sg_pipeline pip;
        public sg_bindings bind;
        public uint features; // Store the features for this variation

        // Pointerized uniform block structs
        public VsParamsPtr vs_params;
        public PhongParamsPtr phong_params;
    }

    // Camera helper
    struct Camera
    {
        public Vector3 center;
        public float distance;
        public float latitude;
        public float longitude;
        public float min_dist;
        public float max_dist;
        public Matrix4x4 view;
        public Matrix4x4 proj;
        public Matrix4x4 view_proj;
        public Vector3 eye_pos;
    }

    // Global state
    struct State
    {
        public sg_pass_action pass_action;
        public Camera camera;
        public IntPtr ozz_instance;
        public double frame_time_sec;

        // Skinning settings
        public bool skinning_enabled;
        public bool skinning_paused;
        public float time_factor;
        public double time_sec;

        // Lighting settings  
        public bool lighting_enabled;
        public bool light_dbg_draw;
        public float light_latitude;
        public float light_longitude;
        public Vector3 light_dir;
        public float light_intensity;
        public Vector3 light_color;

        // Material settings
        public bool material_enabled;
        public Vector3 mat_diffuse;
        public Vector3 mat_specular;
        public float mat_spec_power;

        public ShaderVariation[] variations;

        // Load buffers for assets
        public SharedBuffer skeleton_buffer;
        public SharedBuffer animation_buffer;
        public SharedBuffer mesh_buffer;

        // Load state
        public bool all_loaded;
        public bool load_failed;
    }

    static State state = new State
    {
        // Initialize default values
        skinning_enabled = true,
        time_factor = 1.0f,
        lighting_enabled = true,
        light_latitude = 25.0f,
        light_longitude = 315.0f,
        light_intensity = 1.0f,
        light_color = new Vector3(1.0f, 1.0f, 1.0f),
        material_enabled = true,
        mat_diffuse = new Vector3(1.0f, 0.5f, 0.0f),
        mat_specular = new Vector3(1.0f, 1.0f, 1.0f),
        mat_spec_power = 32.0f,
        variations = new ShaderVariation[MAX_SHADER_VARIATIONS]
    };

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        // Setup sokol-gfx
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });

        // Setup sokol-gl  
        sgl_setup(new sgl_desc_t
        {
            sample_count = sapp_sample_count(),
            logger = { func = &slog_func }
        });

        // Setup sokol-fetch
        sfetch_setup(new sfetch_desc_t()
        {
            max_requests = 3,
            num_channels = 1, 
            num_lanes = 3,
            logger = { func = &slog_func }
        });

        // Setup sokol-imgui
        simgui_setup(new simgui_desc_t
        {
            logger = { func = &slog_func }
        });

        // Initialize clear color
        state.pass_action = new sg_pass_action();
        state.pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.0f, g = 0.0f, b = 0.0f, a = 1.0f };

        // Initialize camera
        InitCamera();

        // Setup ozz-utility wrapper and create character instance
        ozz_setup(new ozz_desc_t
        {
            max_palette_joints = 64,
            max_instances = 1
        });
        state.ozz_instance = ozz_create_instance(0);

        // Initialize shader variations
        InitShaderVariations();

        // Allocate load buffers
        state.skeleton_buffer = SharedBuffer.Create(1024 * 1024);  // 1MB
        state.animation_buffer = SharedBuffer.Create(1024 * 1024); // 1MB  
        state.mesh_buffer = SharedBuffer.Create(2 * 1024 * 1024);  // 2MB

        // Start loading character data
        LoadCharacterAssets();
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        // Pump sokol-fetch message queues
        sfetch_dowork();

        int fb_width = sapp_width();
        int fb_height = sapp_height();
        
        // Move viewport slightly off-center because UI is on left side
        int vp_x = (int)(fb_width * 0.3f);
        int vp_y = 0;
        int vp_width = (int)(fb_width * 0.7f);
        int vp_height = fb_height;

        state.frame_time_sec = sapp_frame_duration();
        UpdateCamera(vp_width, vp_height);
        
        // Start new imgui frame
        simgui_new_frame(new simgui_frame_desc_t
        {
            width = fb_width,
            height = fb_height,
            delta_time = state.frame_time_sec,
            dpi_scale = 1//TBD ELI , Android issue sapp_dpi_scale()
        });

        // Update lighting
        if (state.lighting_enabled)
        {
            float lat = state.light_latitude * (float)Math.PI / 180.0f;
            float lng = state.light_longitude * (float)Math.PI / 180.0f;
            state.light_dir = new Vector3(
                (float)(Math.Cos(lat) * Math.Sin(lng)),
                (float)Math.Sin(lat),
                (float)(Math.Cos(lat) * Math.Cos(lng))
            );
            
            if (state.light_dbg_draw)
            {
                DrawLightDebug();
            }
        }

        DrawUI();

        // Render
        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_viewport(vp_x, vp_y, vp_width, vp_height, true);
        
        if (state.all_loaded)
        {
            DrawCharacter();
        }

        sgl_draw();
        simgui_render();
        sg_end_pass();
        sg_commit();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        if (simgui_handle_event(in *e))
        {
            return;
        }

        // Handle camera input
        switch (e->type)
        {
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN:
                if (e->mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT)
                {
                    sapp_lock_mouse(true);
                }
                break;
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP:
                if (e->mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT)
                {
                    sapp_lock_mouse(false);
                }
                break;
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_SCROLL:
                float zoom = e->scroll_y * 0.5f;
                state.camera.distance = Math.Max(state.camera.min_dist, 
                    Math.Min(state.camera.max_dist, state.camera.distance + zoom));
                break;
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE:
                if (sapp_mouse_locked())
                {
                    state.camera.longitude -= e->mouse_dx * 0.25f;
                    state.camera.latitude += e->mouse_dy * 0.25f;
                    
                    // Wrap longitude
                    if (state.camera.longitude < 0.0f)
                        state.camera.longitude += 360.0f;
                    if (state.camera.longitude > 360.0f)
                        state.camera.longitude -= 360.0f;
                    
                    // Clamp latitude
                    state.camera.latitude = Math.Max(-85.0f, Math.Min(85.0f, state.camera.latitude));
                }
                break;
        }
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        state.skeleton_buffer?.Dispose();
        state.animation_buffer?.Dispose(); 
        state.mesh_buffer?.Dispose();
        
        // Cleanup ozz resources
        if (state.ozz_instance != IntPtr.Zero)
        {
            ozz_destroy_instance(state.ozz_instance);
        }
        ozz_shutdown();
        
        sfetch_shutdown();
        simgui_shutdown();
        sgl_shutdown();
        sg_shutdown();

        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    static void InitCamera()
    {
        state.camera = new Camera
        {
            center = new Vector3(0.0f, 1.1f, 0.0f),
            distance = 3.0f,
            latitude = 20.0f,
            longitude = 20.0f,
            min_dist = 2.0f,
            max_dist = 10.0f
        };
    }

    static void UpdateCamera(int width, int height)
    {
        float aspect = (float)width / (float)height;
        state.camera.proj = Matrix4x4.CreatePerspectiveFieldOfView(
            60.0f * (float)Math.PI / 180.0f,
            aspect,
            0.01f,
            100.0f
        );

        // Convert spherical coordinates to cartesian for camera position
        float lat_rad = state.camera.latitude * (float)Math.PI / 180.0f;
        float lng_rad = state.camera.longitude * (float)Math.PI / 180.0f;
        
        Vector3 eye = state.camera.center + new Vector3(
            state.camera.distance * (float)(Math.Cos(lat_rad) * Math.Sin(lng_rad)),
            state.camera.distance * (float)Math.Sin(lat_rad),
            state.camera.distance * (float)(Math.Cos(lat_rad) * Math.Cos(lng_rad))
        );

        state.camera.view = Matrix4x4.CreateLookAt(eye, state.camera.center, Vector3.UnitY);
        state.camera.view_proj = state.camera.view * state.camera.proj;
        state.camera.eye_pos = eye;
    }

    static unsafe void InitShaderVariations()
    {
        // Initialize all variations to invalid first
        for (int i = 0; i < MAX_SHADER_VARIATIONS; i++)
        {
            state.variations[i].valid = false;
        }

        // Initialize valid variations - for now just the basic one
        InitShaderVariation(SHD_NONE);
        InitShaderVariation(SHD_SKINNING);
        InitShaderVariation(SHD_LIGHTING);
        InitShaderVariation(SHD_MATERIAL);
        InitShaderVariation(SHD_SKINNING | SHD_LIGHTING);
        InitShaderVariation(SHD_SKINNING | SHD_MATERIAL);
        InitShaderVariation(SHD_LIGHTING | SHD_MATERIAL);
        InitShaderVariation(SHD_SKINNING | SHD_LIGHTING | SHD_MATERIAL);
    }

    static sg_shader_desc GetShaderDesc(uint features)
    {
        return features switch
        {
            SHD_NONE => NoneShader.none_prog_shader_desc(sg_query_backend()),
            SHD_SKINNING => SShader.s_prog_shader_desc(sg_query_backend()),
            SHD_LIGHTING => LShader.l_prog_shader_desc(sg_query_backend()),
            SHD_MATERIAL => MShader.m_prog_shader_desc(sg_query_backend()),
            SHD_SKINNING | SHD_LIGHTING => SLShader.sl_prog_shader_desc(sg_query_backend()),
            SHD_SKINNING | SHD_MATERIAL => SMShader.sm_prog_shader_desc(sg_query_backend()),
            SHD_LIGHTING | SHD_MATERIAL => LMShader.lm_prog_shader_desc(sg_query_backend()),
            SHD_SKINNING | SHD_LIGHTING | SHD_MATERIAL => SLMShader.slm_prog_shader_desc(sg_query_backend()),
            _ => throw new ArgumentException($"Unknown shader features: {features}")
        };
    }

    static int GetUniformBlockSlot(uint features, string blockName)
    {
        return features switch
        {
            SHD_NONE => NoneShader.none_prog_uniformblock_slot(blockName),
            SHD_SKINNING => SShader.s_prog_uniformblock_slot(blockName),
            SHD_LIGHTING => LShader.l_prog_uniformblock_slot(blockName),
            SHD_MATERIAL => MShader.m_prog_uniformblock_slot(blockName),
            SHD_SKINNING | SHD_LIGHTING => SLShader.sl_prog_uniformblock_slot(blockName),
            SHD_SKINNING | SHD_MATERIAL => SMShader.sm_prog_uniformblock_slot(blockName),
            SHD_LIGHTING | SHD_MATERIAL => LMShader.lm_prog_uniformblock_slot(blockName),
            SHD_SKINNING | SHD_LIGHTING | SHD_MATERIAL => SLMShader.slm_prog_uniformblock_slot(blockName),
            _ => -1
        };
    }

    static int GetUniformBlockSize(uint features, string blockName)
    {
        return features switch
        {
            SHD_NONE => NoneShader.none_prog_uniformblock_size(blockName),
            SHD_SKINNING => SShader.s_prog_uniformblock_size(blockName),
            SHD_LIGHTING => LShader.l_prog_uniformblock_size(blockName),
            SHD_MATERIAL => MShader.m_prog_uniformblock_size(blockName),
            SHD_SKINNING | SHD_LIGHTING => SLShader.sl_prog_uniformblock_size(blockName),
            SHD_SKINNING | SHD_MATERIAL => SMShader.sm_prog_uniformblock_size(blockName),
            SHD_LIGHTING | SHD_MATERIAL => LMShader.lm_prog_uniformblock_size(blockName),
            SHD_SKINNING | SHD_LIGHTING | SHD_MATERIAL => SLMShader.slm_prog_uniformblock_size(blockName),
            _ => 0
        };
    }

    static int GetTextureSlot(uint features, string textureName)
    {
        return features switch
        {
            SHD_NONE => NoneShader.none_prog_texture_slot(textureName),
            SHD_SKINNING => SShader.s_prog_texture_slot(textureName),
            SHD_LIGHTING => LShader.l_prog_texture_slot(textureName),
            SHD_MATERIAL => MShader.m_prog_texture_slot(textureName),
            SHD_SKINNING | SHD_LIGHTING => SLShader.sl_prog_texture_slot(textureName),
            SHD_SKINNING | SHD_MATERIAL => SMShader.sm_prog_texture_slot(textureName),
            SHD_LIGHTING | SHD_MATERIAL => LMShader.lm_prog_texture_slot(textureName),
            SHD_SKINNING | SHD_LIGHTING | SHD_MATERIAL => SLMShader.slm_prog_texture_slot(textureName),
            _ => -1
        };
    }

    static int GetAttrSlot(uint features, string attrName)
    {
        return features switch
        {
            SHD_NONE => NoneShader.none_prog_attr_slot(attrName),
            SHD_SKINNING => SShader.s_prog_attr_slot(attrName),
            SHD_LIGHTING => LShader.l_prog_attr_slot(attrName),
            SHD_MATERIAL => MShader.m_prog_attr_slot(attrName),
            SHD_SKINNING | SHD_LIGHTING => SLShader.sl_prog_attr_slot(attrName),
            SHD_SKINNING | SHD_MATERIAL => SMShader.sm_prog_attr_slot(attrName),
            SHD_LIGHTING | SHD_MATERIAL => LMShader.lm_prog_attr_slot(attrName),
            SHD_SKINNING | SHD_LIGHTING | SHD_MATERIAL => SLMShader.slm_prog_attr_slot(attrName),
            _ => -1
        };
    }

    static int GetSamplerSlot(uint features, string samplerName)
    {
        return features switch
        {
            SHD_NONE => NoneShader.none_prog_sampler_slot(samplerName),
            SHD_SKINNING => SShader.s_prog_sampler_slot(samplerName),
            SHD_LIGHTING => LShader.l_prog_sampler_slot(samplerName),
            SHD_MATERIAL => MShader.m_prog_sampler_slot(samplerName),
            SHD_SKINNING | SHD_LIGHTING => SLShader.sl_prog_sampler_slot(samplerName),
            SHD_SKINNING | SHD_MATERIAL => SMShader.sm_prog_sampler_slot(samplerName),
            SHD_LIGHTING | SHD_MATERIAL => LMShader.lm_prog_sampler_slot(samplerName),
            SHD_SKINNING | SHD_LIGHTING | SHD_MATERIAL => SLMShader.slm_prog_sampler_slot(samplerName),
            _ => -1
        };
    }

    static int GetUniformOffset(uint features, string blockName, string uniformName)
    {
        return features switch
        {
            SHD_NONE => NoneShader.none_prog_uniform_offset(blockName, uniformName),
            SHD_SKINNING => SShader.s_prog_uniform_offset(blockName, uniformName),
            SHD_LIGHTING => LShader.l_prog_uniform_offset(blockName, uniformName),
            SHD_MATERIAL => MShader.m_prog_uniform_offset(blockName, uniformName),
            SHD_SKINNING | SHD_LIGHTING => SLShader.sl_prog_uniform_offset(blockName, uniformName),
            SHD_SKINNING | SHD_MATERIAL => SMShader.sm_prog_uniform_offset(blockName, uniformName),
            SHD_LIGHTING | SHD_MATERIAL => LMShader.lm_prog_uniform_offset(blockName, uniformName),
            SHD_SKINNING | SHD_LIGHTING | SHD_MATERIAL => SLMShader.slm_prog_uniform_offset(blockName, uniformName),
            _ => -1
        };
    }

    static unsafe void InitShaderVariation(uint features)
    {
        ref ShaderVariation var = ref state.variations[features];
        var.valid = true;
        var.features = features;

        try
        {
            // Create shader and pipeline
            sg_shader_desc shader_desc = GetShaderDesc(features);
            sg_shader shader = sg_make_shader(shader_desc);

            var.pip = sg_make_pipeline(new sg_pipeline_desc
            {
                shader = shader,
                layout = CreateVertexLayoutForVariation(features),
                index_type = sg_index_type.SG_INDEXTYPE_UINT16,
                face_winding = sg_face_winding.SG_FACEWINDING_CCW,
                cull_mode = sg_cull_mode.SG_CULLMODE_BACK,
                depth = new sg_depth_state
                {
                    write_enabled = true,
                    compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL
                }
            });

            // Setup uniform block pointers using shader reflection
            SetupUniformBlocks(ref var, features);

            // Setup bindings - check if the shader variation needs the joint texture
            var.bind = new sg_bindings();
            int tex_slot = GetTextureSlot(features, "joint_tex");
            if (tex_slot >= 0)
            {
                int smp_slot = GetSamplerSlot(features, "smp");
                var.bind.views[tex_slot] = ozz_joint_texture_view();
                var.bind.samplers[smp_slot] = ozz_joint_sampler();
            }
        }
        catch (Exception)
        {
            var.valid = false;
        }
    }

    static sg_vertex_layout_state CreateVertexLayoutForVariation(uint features)
    {
        sg_vertex_layout_state layout = default;
        
        // Buffer stride must be provided, because the vertex layout may have gaps
        // This matches sizeof(ozz_vertex_t) in C: 3 floats + 3 uint32_t = 24 bytes
        layout.buffers[0].stride = 24;

        // Populate the vertex attribute description depending on what
        // vertex attributes the shader variation requires, using shader reflection
        int position_slot = GetAttrSlot(features, "position");
        if (position_slot >= 0)
        {
            layout.attrs[position_slot].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
            layout.attrs[position_slot].offset = 0; // offsetof(ozz_vertex_t, position)
        }

        int normal_slot = GetAttrSlot(features, "normal");
        if (normal_slot >= 0)
        {
            layout.attrs[normal_slot].format = sg_vertex_format.SG_VERTEXFORMAT_BYTE4N;
            layout.attrs[normal_slot].offset = 12; // offsetof(ozz_vertex_t, normal)
        }

        int jindices_slot = GetAttrSlot(features, "jindices");
        if (jindices_slot >= 0)
        {
            layout.attrs[jindices_slot].format = sg_vertex_format.SG_VERTEXFORMAT_UBYTE4N;
            layout.attrs[jindices_slot].offset = 16; // offsetof(ozz_vertex_t, joint_indices)
        }

        int jweights_slot = GetAttrSlot(features, "jweights");
        if (jweights_slot >= 0)
        {
            layout.attrs[jweights_slot].format = sg_vertex_format.SG_VERTEXFORMAT_UBYTE4N;
            layout.attrs[jweights_slot].offset = 20; // offsetof(ozz_vertex_t, joint_weights)
        }

        return layout;
    }

    static unsafe void SetupUniformBlocks(ref ShaderVariation var, uint features)
    {
        // Setup vs_params uniform block using shader reflection
        var.vs_params.slot = GetUniformBlockSlot(features, "vs_params");
        if (var.vs_params.slot >= 0)
        {
            var.vs_params.valid = true;
            var.vs_params.num_bytes = GetUniformBlockSize(features, "vs_params");

            fixed (byte* ptr = vs_params_buffer)
            {
                // Get actual offsets from shader reflection
                int mvp_offset = GetUniformOffset(features, "vs_params", "mvp");
                int model_offset = GetUniformOffset(features, "vs_params", "model");
                
                if (mvp_offset >= 0)
                    var.vs_params.mvp = (Matrix4x4*)(ptr + mvp_offset);
                if (model_offset >= 0)
                    var.vs_params.model = (Matrix4x4*)(ptr + model_offset);

                if ((features & SHD_SKINNING) != 0)
                {
                    int joint_uv_offset = GetUniformOffset(features, "vs_params", "joint_uv");
                    int joint_pixel_width_offset = GetUniformOffset(features, "vs_params", "joint_pixel_width");
                    
                    if (joint_uv_offset >= 0)
                        var.vs_params.joint_uv = (Vector2*)(ptr + joint_uv_offset);
                    if (joint_pixel_width_offset >= 0)
                        var.vs_params.joint_pixel_width = (float*)(ptr + joint_pixel_width_offset);
                }
            }
        }
        else
        {
            var.vs_params.valid = false;
        }

        // Setup phong_params uniform block if lighting or material features are used
        if ((features & (SHD_LIGHTING | SHD_MATERIAL)) != 0)
        {
            var.phong_params.slot = GetUniformBlockSlot(features, "phong_params");
            if (var.phong_params.slot >= 0)
            {
                var.phong_params.valid = true;
                var.phong_params.num_bytes = GetUniformBlockSize(features, "phong_params");

                fixed (byte* ptr = phong_params_buffer)
                {
                    if ((features & SHD_LIGHTING) != 0)
                    {
                        int light_dir_offset = GetUniformOffset(features, "phong_params", "light_dir");
                        int eye_pos_offset = GetUniformOffset(features, "phong_params", "eye_pos");
                        int light_color_offset = GetUniformOffset(features, "phong_params", "light_color");
                        
                        if (light_dir_offset >= 0)
                            var.phong_params.light_dir = (Vector3*)(ptr + light_dir_offset);
                        if (eye_pos_offset >= 0)
                            var.phong_params.eye_pos = (Vector3*)(ptr + eye_pos_offset);
                        if (light_color_offset >= 0)
                            var.phong_params.light_color = (Vector3*)(ptr + light_color_offset);
                    }
                    
                    if ((features & SHD_MATERIAL) != 0)
                    {
                        int mat_diffuse_offset = GetUniformOffset(features, "phong_params", "mat_diffuse");
                        int mat_specular_offset = GetUniformOffset(features, "phong_params", "mat_specular");
                        int mat_spec_power_offset = GetUniformOffset(features, "phong_params", "mat_spec_power");
                        
                        if (mat_diffuse_offset >= 0)
                            var.phong_params.mat_diffuse = (Vector3*)(ptr + mat_diffuse_offset);
                        if (mat_specular_offset >= 0)
                            var.phong_params.mat_specular = (Vector3*)(ptr + mat_specular_offset);
                        if (mat_spec_power_offset >= 0)
                            var.phong_params.mat_spec_power = (float*)(ptr + mat_spec_power_offset);
                    }
                }
            }
            else
            {
                var.phong_params.valid = false;
            }
        }
        else
        {
            var.phong_params.valid = false;
        }
    }

    static unsafe void LoadCharacterAssets()
    {
        // Load skeleton data
        sfetch_send(new sfetch_request_t
        {
            path = util_get_file_path("ozz_skin_skeleton.ozz"),
            callback = &SkeletonDataLoaded,
            buffer = SFETCH_RANGE(state.skeleton_buffer)
        });

        // Load animation data  
        sfetch_send(new sfetch_request_t
        {
            path = util_get_file_path("ozz_skin_animation.ozz"),
            callback = &AnimationDataLoaded,
            buffer = SFETCH_RANGE(state.animation_buffer)
        });

        // Load mesh data
        sfetch_send(new sfetch_request_t
        {
            path = util_get_file_path("ozz_skin_mesh.ozz"),
            callback = &MeshDataLoaded,
            buffer = SFETCH_RANGE(state.mesh_buffer)
        });
    }

    [UnmanagedCallersOnly]
    static unsafe void SkeletonDataLoaded(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            // Set the skeleton data using ozz utility wrapper
            if (state.ozz_instance != IntPtr.Zero)
            {
                ozz_load_skeleton(state.ozz_instance, response->data.ptr, (nuint)response->data.size);
            }
            CheckAllLoaded();
        }
        else if (response->failed)
        {
            state.load_failed = true;
            LogMessage(1, "Failed to load skeleton data");
        }
    }

    [UnmanagedCallersOnly]
    static unsafe void AnimationDataLoaded(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            // Set the animation data using ozz utility wrapper
            if (state.ozz_instance != IntPtr.Zero)
            {
                ozz_load_animation(state.ozz_instance, response->data.ptr, (nuint)response->data.size);
            }
            CheckAllLoaded();
        }
        else if (response->failed)
        {
            state.load_failed = true;
            LogMessage(1, "Failed to load animation data");
        }
    }

    [UnmanagedCallersOnly]
    static unsafe void MeshDataLoaded(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            // Set the mesh data using ozz utility wrapper
            if (state.ozz_instance != IntPtr.Zero)
            {
                ozz_load_mesh(state.ozz_instance, response->data.ptr, (nuint)response->data.size);
            }
            CheckAllLoaded();
        }
        else if (response->failed)
        {
            state.load_failed = true;
            LogMessage(1, "Failed to load mesh data");
        }
    }

    static void CheckAllLoaded()
    {
        if (state.ozz_instance != IntPtr.Zero)
        {
            // Check if all ozz data has been loaded
            state.all_loaded = ozz_all_loaded(state.ozz_instance) && !ozz_load_failed(state.ozz_instance);
            
            if (state.all_loaded)
            {
                LogMessage(0, "All character assets loaded successfully");
            }
        }
        else
        {
            // If ozz instance is not created, fallback to simple check
            state.all_loaded = false;
        }
    }

    static unsafe void DrawCharacter()
    {
        // Update animation time
        if (state.skinning_enabled && !state.skinning_paused)
        {
            state.time_sec += state.frame_time_sec * state.time_factor;
        }

        // Update ozz instance with current animation time
        if (state.ozz_instance != IntPtr.Zero)
        {
            ozz_update_instance(state.ozz_instance, state.time_sec);
            ozz_update_joint_texture();
        }

        // Get current shader features
        uint features = GetCurrentShaderFeatures();
        ref ShaderVariation var = ref state.variations[features];

        if (!var.valid)
        {
            return; // Skip if shader variation not available
        }

        // Update uniforms
        UpdateUniforms(ref var, features);

        // Update bindings with current ozz buffers
        var.bind.vertex_buffers[0] = ozz_vertex_buffer(state.ozz_instance);
        var.bind.index_buffer = ozz_index_buffer(state.ozz_instance);

        // Apply pipeline and draw
        sg_apply_pipeline(var.pip);
        sg_apply_bindings(var.bind);
        
        // Apply uniform blocks
        if (var.vs_params.valid)
        {
            sg_apply_uniforms(var.vs_params.slot, new sg_range
            {
                ptr = Unsafe.AsPointer(ref vs_params_buffer[0]),
                size = (UIntPtr)var.vs_params.num_bytes
            });
        }
        
        if (var.phong_params.valid)
        {
            sg_apply_uniforms(var.phong_params.slot, new sg_range
            {
                ptr = Unsafe.AsPointer(ref phong_params_buffer[0]),
                size = (UIntPtr)var.phong_params.num_bytes
            });
        }

        // Draw the mesh using ozz instance
        if (state.ozz_instance != IntPtr.Zero)
        {
            sg_draw(0, (uint)ozz_num_triangle_indices(state.ozz_instance), 1);
        }
    }

    static uint GetCurrentShaderFeatures()
    {
        uint features = SHD_NONE;
        
        if (state.skinning_enabled)
            features |= SHD_SKINNING;
        if (state.lighting_enabled)
            features |= SHD_LIGHTING;
        if (state.material_enabled)
            features |= SHD_MATERIAL;
            
        return features;
    }

    static unsafe void UpdateUniforms(ref ShaderVariation var, uint features)
    {
        // Update vertex shader uniforms
        if (var.vs_params.mvp != null)
        {
            // Use view_proj directly as in the C code
            *var.vs_params.mvp = state.camera.view_proj;
        }
        
        if (var.vs_params.model != null)
        {
            *var.vs_params.model = Matrix4x4.Identity;
        }

        if ((features & SHD_SKINNING) != 0 && state.ozz_instance != IntPtr.Zero)
        {
            if (var.vs_params.joint_uv != null)
            {
                *var.vs_params.joint_uv = new Vector2(ozz_joint_texture_u(state.ozz_instance), ozz_joint_texture_v(state.ozz_instance));
            }
            if (var.vs_params.joint_pixel_width != null)
            {
                *var.vs_params.joint_pixel_width = ozz_joint_texture_pixel_width();
            }
        }

        // Update fragment shader uniforms
        if (var.phong_params.valid)
        {
            if ((features & SHD_LIGHTING) != 0)
            {
                if (var.phong_params.light_dir != null)
                    *var.phong_params.light_dir = state.light_dir;
                if (var.phong_params.eye_pos != null)
                {
                    // Use camera eye position directly
                    *var.phong_params.eye_pos = state.camera.eye_pos;
                }
                if (var.phong_params.light_color != null)
                    *var.phong_params.light_color = state.light_color * state.light_intensity;
            }

            if ((features & SHD_MATERIAL) != 0)
            {
                if (var.phong_params.mat_diffuse != null)
                    *var.phong_params.mat_diffuse = state.mat_diffuse;
                if (var.phong_params.mat_specular != null)
                    *var.phong_params.mat_specular = state.mat_specular;
                if (var.phong_params.mat_spec_power != null)
                    *var.phong_params.mat_spec_power = state.mat_spec_power;
            }
        }
    }

    static void DrawUI()
    {
        igSetNextWindowPos(new Vector2(10, 10), ImGuiCond.Once, Vector2.Zero);
        igSetNextWindowBgAlpha(0.8f);
        
        byte open = 1;
        if (igBegin("Shader Features", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            igText("Dynamic Shader Variation System");
            igSeparator();

            if (!state.all_loaded)
            {
                if (state.load_failed)
                {
                    igTextColored(new Vector4(1, 0, 0, 1), "Loading failed!");
                }
                else
                {
                    igText("Loading character data...");
                }
            }
            else
            {
                // Current shader info
                uint current_features = GetCurrentShaderFeatures();
                igText($"Active Shader: {GetShaderVariationName(current_features)}");
                igSeparator();

                // Skinning controls
                igText("Skinning");
                igSeparator();
                byte skinning_enabled = state.skinning_enabled ? (byte)1 : (byte)0;
                if (igCheckbox("Enable Skinning", ref skinning_enabled))
                {
                    state.skinning_enabled = skinning_enabled != 0;
                }

                if (state.skinning_enabled)
                {
                    byte paused = state.skinning_paused ? (byte)1 : (byte)0;
                    igCheckbox("Paused", ref paused);
                    state.skinning_paused = paused != 0;

                    igSliderFloat("Time Factor", ref state.time_factor, 0.0f, 2.0f, "%.1f", ImGuiSliderFlags.None);
                    
                    if (igButton("Reset Animation", Vector2.Zero))
                    {
                        state.time_sec = 0.0;
                    }
                }

                // Lighting controls  
                igText("Lighting");
                igSeparator();
                byte lighting_enabled = state.lighting_enabled ? (byte)1 : (byte)0;
                if (igCheckbox("Enable Lighting", ref lighting_enabled))
                {
                    state.lighting_enabled = lighting_enabled != 0;
                }

                if (state.lighting_enabled)
                {
                    igSliderFloat("Light Latitude", ref state.light_latitude, -90.0f, 90.0f, "%.1f", ImGuiSliderFlags.None);
                    igSliderFloat("Light Longitude", ref state.light_longitude, 0.0f, 360.0f, "%.1f", ImGuiSliderFlags.None);
                    igSliderFloat("Light Intensity", ref state.light_intensity, 0.0f, 3.0f, "%.1f", ImGuiSliderFlags.None);
                    igColorEdit3("Light Color", ref state.light_color, ImGuiColorEditFlags.None);
                    
                    byte debug_draw = state.light_dbg_draw ? (byte)1 : (byte)0;
                    igCheckbox("Debug Draw Light", ref debug_draw);
                    state.light_dbg_draw = debug_draw != 0;
                }

                // Material controls
                igText("Material");
                igSeparator();
                byte material_enabled = state.material_enabled ? (byte)1 : (byte)0;
                if (igCheckbox("Enable Material", ref material_enabled))
                {
                    state.material_enabled = material_enabled != 0;
                }

                if (state.material_enabled)
                {
                    igColorEdit3("Diffuse", ref state.mat_diffuse, ImGuiColorEditFlags.None);
                    igColorEdit3("Specular", ref state.mat_specular, ImGuiColorEditFlags.None);
                    igSliderFloat("Specular Power", ref state.mat_spec_power, 1.0f, 128.0f, "%.1f", ImGuiSliderFlags.None);
                }

                // Camera controls
                igText("Camera");
                igSeparator();
                igSliderFloat("Latitude", ref state.camera.latitude, -90.0f, 90.0f, "%.1f", ImGuiSliderFlags.None);
                igSliderFloat("Longitude", ref state.camera.longitude, 0.0f, 360.0f, "%.1f", ImGuiSliderFlags.None);
                igSliderFloat("Distance", ref state.camera.distance, state.camera.min_dist, state.camera.max_dist, "%.1f", ImGuiSliderFlags.None);
            }
        }
        igEnd();
    }

    static string GetShaderVariationName(uint features)
    {
        return features switch
        {
            SHD_NONE => "None (Basic)",
            SHD_SKINNING => "Skinning",
            SHD_LIGHTING => "Lighting",
            SHD_MATERIAL => "Material",
            SHD_SKINNING | SHD_LIGHTING => "Skinning + Lighting",
            SHD_SKINNING | SHD_MATERIAL => "Skinning + Material",
            SHD_LIGHTING | SHD_MATERIAL => "Lighting + Material",
            SHD_SKINNING | SHD_LIGHTING | SHD_MATERIAL => "Skinning + Lighting + Material",
            _ => "Unknown"
        };
    }

    static void DrawLightDebug()
    {
        // Draw a simple line from origin to light direction for visualization
        sgl_defaults();
        sgl_matrix_mode_modelview();
        
        sgl_begin_lines();
        sgl_c3f(state.light_color.X, state.light_color.Y, state.light_color.Z);
        sgl_v3f(0.0f, 0.0f, 0.0f);
        sgl_v3f(state.light_dir.X * 2.0f, state.light_dir.Y * 2.0f, state.light_dir.Z * 2.0f);
        sgl_end();
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 1024,
            height = 768,
            sample_count = 4,
            window_title = "Ozz Shader Features (sokol-app)",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }

}
