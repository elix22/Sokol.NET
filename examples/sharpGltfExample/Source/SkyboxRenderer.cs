using System.Numerics;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using static cubemap_shader_cs.Shaders;

namespace Sokol
{
    /// <summary>
    /// Renders the environment cubemap as a skybox background using the cubemap shader.
    /// </summary>
    public class SkyboxRenderer
    {
        private sg_shader shader;
        private sg_pipeline offscreen_pipeline;  // For offscreen passes (RGBA8/DEPTH)
        private sg_pipeline swapchain_pipeline;  // For swapchain (default framebuffer)
        private sg_buffer vertex_buffer;
        private sg_buffer index_buffer;
        private uint index_count;
        private bool is_initialized = false;

        public bool IsInitialized => is_initialized;
        public void Initialize()
        {
            if (is_initialized)
                return;

            // Create cube geometry (8 vertices for a cube)
            // Note: These are direction vectors, not positions
            // The vertex shader will use these to sample the cubemap
            float[] vertices = new float[]
            {
                // Cube vertices as position vectors
                -1.0f,  1.0f,  1.0f,  // 0: left-top-front
                 1.0f,  1.0f,  1.0f,  // 1: right-top-front
                 1.0f,  1.0f, -1.0f,  // 2: right-top-back
                -1.0f,  1.0f, -1.0f,  // 3: left-top-back
                -1.0f, -1.0f,  1.0f,  // 4: left-bottom-front
                 1.0f, -1.0f,  1.0f,  // 5: right-bottom-front
                 1.0f, -1.0f, -1.0f,  // 6: right-bottom-back
                -1.0f, -1.0f, -1.0f   // 7: left-bottom-back
            };

            // Cube indices (6 faces * 2 triangles * 3 vertices = 36 indices)
            ushort[] indices = new ushort[]
            {
                // Front face
                0, 4, 5, 0, 5, 1,
                // Right face
                1, 5, 6, 1, 6, 2,
                // Back face
                2, 6, 7, 2, 7, 3,
                // Left face
                3, 7, 4, 3, 4, 0,
                // Top face
                3, 0, 1, 3, 1, 2,
                // Bottom face
                4, 7, 6, 4, 6, 5
            };

            index_count = (uint)indices.Length;

            // Create vertex buffer
            vertex_buffer = sg_make_buffer(new sg_buffer_desc
            {
                data = SG_RANGE(vertices),
                label = "skybox-vertices"
            });

            // Create index buffer
            index_buffer = sg_make_buffer(new sg_buffer_desc
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(indices),
                label = "skybox-indices"
            });

            // Create shader - use cubemap shader
            shader = sg_make_shader(cubemap_program_shader_desc(sg_query_backend()));

            // Create pipeline for offscreen passes (RGBA8/DEPTH)
            var offscreen_pip_desc = new sg_pipeline_desc
            {
                shader = shader,
                index_type = sg_index_type.SG_INDEXTYPE_UINT16,
                cull_mode = sg_cull_mode.SG_CULLMODE_NONE,  // No culling for skybox
                sample_count = 1,  // Match offscreen pass sample count (no MSAA)
                depth = new sg_depth_state
                {
                    compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,  // Draw at far plane
                    write_enabled = false,  // Don't write to depth buffer
                    pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH
                },
                label = "skybox-offscreen-pipeline"
            };

            // Configure vertex attributes for offscreen
            offscreen_pip_desc.layout.attrs[ATTR_cubemap_program_a_position].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
            offscreen_pip_desc.layout.buffers[0].stride = 12; // 3 floats (xyz)
            offscreen_pip_desc.colors[0].pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8;

            offscreen_pipeline = sg_make_pipeline(offscreen_pip_desc);

            var swapchain = sglue_swapchain();
            // Create pipeline for swapchain (default framebuffer)
            var swapchain_pip_desc = new sg_pipeline_desc
            {
                shader = shader,
                index_type = sg_index_type.SG_INDEXTYPE_UINT16,
                cull_mode = sg_cull_mode.SG_CULLMODE_NONE,  // No culling for skybox
                sample_count = swapchain.sample_count,  // Match swapchain sample count
                depth = new sg_depth_state
                {
                    pixel_format = swapchain.depth_format,
                    compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,  // Draw at far plane
                    write_enabled = false  // Don't write to depth buffer
                },
                label = "skybox-swapchain-pipeline"
            };

            // Configure vertex attributes for swapchain
            swapchain_pip_desc.layout.attrs[ATTR_cubemap_program_a_position].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
            swapchain_pip_desc.layout.buffers[0].stride = 12; // 3 floats (xyz)

            swapchain_pipeline = sg_make_pipeline(swapchain_pip_desc);

            is_initialized = true;
        }

        public void Render(Camera camera, EnvironmentMap environmentMap, int width, int height, float exposure, int tonemapType, bool useOffscreenPipeline = false)
        {
            if (!is_initialized)
                return;

            // Get view and projection matrices from camera
            Matrix4x4 projection = camera.Proj;
            Matrix4x4 view = camera.View;

            // Remove translation from view matrix (skybox should not move with camera)
            view.M41 = 0;
            view.M42 = 0;
            view.M43 = 0;
            view.M44 = 1;

            Matrix4x4 viewProjection = view * projection;

            // Prepare vertex shader uniforms
            var vs_params = new vs_cubemap_params_t
            {
                u_ViewProjectionMatrix = viewProjection,
                u_EnvRotation = environmentMap.Rotation
            };

            // Prepare fragment shader uniforms
            var fs_params = new fs_cubemap_params_t
            {
                u_EnvIntensity = environmentMap.Intensity,
                u_EnvBlurNormalized = 0.0f,  // No blur for skybox
                u_MipCount = environmentMap.MipCount,
                u_LinearOutput = 0  // Linear output (tonemapping applied later)
            };

            // Apply correct pipeline based on render target
            sg_apply_pipeline(useOffscreenPipeline ? offscreen_pipeline : swapchain_pipeline);

            // Bind cubemap texture and sampler FIRST
            sg_bindings bindings = default;
            bindings.vertex_buffers[0] = vertex_buffer;
            bindings.index_buffer = index_buffer;
            bindings.views[VIEW_u_GGXEnvTexture] = environmentMap.SpecularCubemapView;
            bindings.samplers[SMP_u_GGXEnvSampler] = environmentMap.CubemapSampler;

            sg_apply_bindings(bindings);

            // THEN apply uniforms
            sg_apply_uniforms(UB_vs_cubemap_params, SG_RANGE(ref vs_params));
            sg_apply_uniforms(UB_fs_cubemap_params, SG_RANGE(ref fs_params));

            // Apply tonemapping params (required by cubemap shader)
            var tonemap_params = new tonemapping_params_t
            {
                u_Exposure = exposure,
                u_type = tonemapType  // Apply the selected tone mapping algorithm to skybox
            };
            sg_apply_uniforms(UB_tonemapping_params, SG_RANGE(ref tonemap_params));

            // Draw cube
            sg_draw(0, index_count, 1);
        }

        public void Dispose()
        {
            if (!is_initialized)
                return;

            sg_destroy_buffer(vertex_buffer);
            sg_destroy_buffer(index_buffer);
            sg_destroy_pipeline(offscreen_pipeline);
            sg_destroy_pipeline(swapchain_pipeline);
            sg_destroy_shader(shader);

            is_initialized = false;
        }
    }
}
