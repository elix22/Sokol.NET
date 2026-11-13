// SkyboxPass.cs — draws the IBL environment cubemap as the scene background.
//
// Port of examples/CGltfViewer/Source/SkyboxRenderer.cs onto the Server's EnvironmentMap.
// Renders a unit cube whose vertex shader (cubemap.glsl) forces gl_Position = pos.xyww so
// every fragment sits on the far plane, sampling the GGX env cube by direction. Depth test
// is LESS_EQUAL with no depth write, so it fills only the background.
//
// Translation handling: cubemap.glsl zeroes the matrix's translation (mat[3]) itself, so we
// pass the combined view-projection directly — no need to split out the view matrix.

using System.Numerics;
using static Sokol.SG;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SGlue;
using static Sokol.Utils;
using static cubemap_shader_cs_cubemap.Shaders;
using GameEditor.Framework.Renderer.Server.Lighting;

namespace GameEditor.Framework.Renderer.Server.Passes
{
    public sealed unsafe class SkyboxPass
    {
        private sg_shader   _shader;
        private sg_pipeline _offscreenPip;   // RGBA8 / DEPTH, sample_count 1
        private sg_pipeline _swapchainPip;    // matches the default framebuffer
        private sg_buffer   _vbuf;
        private sg_buffer   _ibuf;
        private uint        _indexCount;
        private bool        _initialized;

        public void Init()
        {
            if (_initialized) return;

            // Unit cube corner positions — also used as cubemap sample directions.
            float[] vertices =
            {
                -1f,  1f,  1f,   1f,  1f,  1f,   1f,  1f, -1f,  -1f,  1f, -1f,
                -1f, -1f,  1f,   1f, -1f,  1f,   1f, -1f, -1f,  -1f, -1f, -1f,
            };
            ushort[] indices =
            {
                0, 4, 5, 0, 5, 1,   // front
                1, 5, 6, 1, 6, 2,   // right
                2, 6, 7, 2, 7, 3,   // back
                3, 7, 4, 3, 4, 0,   // left
                3, 0, 1, 3, 1, 2,   // top
                4, 7, 6, 4, 6, 5,   // bottom
            };
            _indexCount = (uint)indices.Length;

            _vbuf = sg_make_buffer(new sg_buffer_desc { data = SG_RANGE(vertices), label = "skybox-vertices" });
            _ibuf = sg_make_buffer(new sg_buffer_desc
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data  = SG_RANGE(indices),
                label = "skybox-indices"
            });

            _shader = sg_make_shader(cubemap_cubemap_program_shader_desc(sg_query_backend()));

            _offscreenPip = MakePipeline(SG_PIXELFORMAT_RGBA8, sg_pixel_format.SG_PIXELFORMAT_DEPTH, 1, "skybox-offscreen-pip");

            var sc = sglue_swapchain();
            _swapchainPip = MakePipeline(sc.color_format, sc.depth_format, sc.sample_count, "skybox-swapchain-pip");

            _initialized = true;
        }

        private sg_pipeline MakePipeline(sg_pixel_format color, sg_pixel_format depth, int sampleCount, string label)
        {
            var desc = new sg_pipeline_desc
            {
                shader       = _shader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT16,
                cull_mode    = sg_cull_mode.SG_CULLMODE_NONE,
                sample_count = sampleCount,
                depth = new sg_depth_state
                {
                    pixel_format  = depth,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL, // far plane
                    write_enabled = false,                                     // background only
                },
                label = label
            };
            desc.colors[0].pixel_format = color;
            desc.layout.attrs[ATTR_cubemap_cubemap_program_a_position].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
            desc.layout.buffers[0].stride = 12; // vec3
            return sg_make_pipeline(desc);
        }

        /// <summary>
        /// Draws the environment cube as the background. Must be called inside an active pass,
        /// before opaque geometry (it does not write depth, so geometry layers on top).
        /// </summary>
        public void Render(in Matrix4x4 viewProj, EnvironmentMap env, bool offscreen,
                           float exposure, int tonemapType)
        {
            if (!_initialized || !env.IsLoaded) return;

            var vs = new cubemap_vs_cubemap_params_t
            {
                u_ViewProjectionMatrix = viewProj,   // cubemap.glsl strips translation (mat[3])
                u_EnvRotation          = env.Rotation,
            };
            var fs = new cubemap_fs_cubemap_params_t
            {
                u_EnvIntensity      = env.Intensity,
                u_EnvBlurNormalized = 0f,
                u_MipCount          = env.MipCount,
                u_LinearOutput      = 0,             // LDR RGBA8 target → tonemap in-shader
            };
            var tm = new cubemap_tonemapping_params_t { u_Exposure = exposure, u_type = tonemapType };

            sg_apply_pipeline(offscreen ? _offscreenPip : _swapchainPip);

            var b = new sg_bindings
            {
                vertex_buffers = { [0] = _vbuf },
                index_buffer   = _ibuf,
                views    = { [VIEW_cubemap_u_GGXEnvTexture] = env.BackgroundCubeView },
                samplers = { [SMP_cubemap_u_GGXEnvSampler]  = env.CubeSampler },
            };
            sg_apply_bindings(b);

            sg_apply_uniforms(UB_cubemap_vs_cubemap_params,  SG_RANGE(ref vs));
            sg_apply_uniforms(UB_cubemap_fs_cubemap_params,  SG_RANGE(ref fs));
            sg_apply_uniforms(UB_cubemap_tonemapping_params, SG_RANGE(ref tm));

            sg_draw(0u, _indexCount, 1u);
        }

        public void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;
            if (_vbuf.id        != 0) { sg_destroy_buffer(_vbuf);          _vbuf = default; }
            if (_ibuf.id        != 0) { sg_destroy_buffer(_ibuf);          _ibuf = default; }
            if (_offscreenPip.id != 0) { sg_destroy_pipeline(_offscreenPip); _offscreenPip = default; }
            if (_swapchainPip.id != 0) { sg_destroy_pipeline(_swapchainPip); _swapchainPip = default; }
            if (_shader.id       != 0) { sg_destroy_shader(_shader);        _shader = default; }
        }
    }
}
