using System.Numerics;
using static Sokol.SG;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_blend_factor;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_pixel_format;
using static Sokol.Utils;
using static grid_shader_cs.Shaders;

namespace GameEditor.Framework.Renderer
{
    /// <summary>
    /// Renders an infinite XZ editor grid using the compiled grid.glsl shader.
    /// Must be drawn inside an active offscreen pass.
    /// </summary>
    public static unsafe class GridRenderer
    {
        private static sg_pipeline _pip;
        private static sg_buffer _vbuf;
        private static sg_buffer _ibuf;

        public static void Init()
        {
            // 4-vertex quad in XZ space (-1..1). The VS scales each coord by 500.
            float[] verts = { -1f, -1f,  1f, -1f,  1f, 1f,  -1f, 1f };
            ushort[] indices = { 0, 1, 2,  0, 2, 3 };

            _vbuf = sg_make_buffer(new sg_buffer_desc
            {
                data = SG_RANGE(verts),
                label = "grid-vbuf"
            });

            _ibuf = sg_make_buffer(new sg_buffer_desc
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(indices),
                label = "grid-ibuf"
            });

            _pip = sg_make_pipeline(new sg_pipeline_desc
            {
                shader = sg_make_shader(grid_shader_desc(sg_query_backend())),
                layout =
                {
                    attrs =
                    {
                        [ATTR_grid_in_pos] = new sg_vertex_attr_state { format = SG_VERTEXFORMAT_FLOAT2 }
                    }
                },
                index_type = sg_index_type.SG_INDEXTYPE_UINT16,
                cull_mode = sg_cull_mode.SG_CULLMODE_NONE,
                sample_count = 1,
                depth = new sg_depth_state
                {
                    pixel_format = SG_PIXELFORMAT_DEPTH,
                    compare = SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = false  // grid doesn't occlude scene objects
                },
                colors =
                {
                    [0] = new sg_color_target_state
                    {
                        pixel_format = SG_PIXELFORMAT_RGBA8,
                        blend = new sg_blend_state
                        {
                            enabled = true,
                            src_factor_rgb   = SG_BLENDFACTOR_SRC_ALPHA,
                            dst_factor_rgb   = SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                            src_factor_alpha = SG_BLENDFACTOR_ONE,
                            dst_factor_alpha = SG_BLENDFACTOR_ZERO
                        }
                    }
                },
                label = "grid-pipeline"
            });
        }

        public static void Draw(Matrix4x4 viewProj, Vector3 eyePos, float nearZ, float farZ)
        {
            if (_pip.id == 0) return;

            var vsParams = new grid_vs_params_t { viewproj = viewProj, eye_pos = eyePos };
            var fsParams = new grid_fs_params_t
            {
                viewproj = viewProj, eye_pos = eyePos,
                near_plane = nearZ, far_plane = farZ
            };

            sg_apply_pipeline(_pip);
            sg_apply_bindings(new sg_bindings
            {
                vertex_buffers = { [0] = _vbuf },
                index_buffer = _ibuf
            });
            sg_apply_uniforms(UB_grid_vs_params, SG_RANGE(ref vsParams));
            sg_apply_uniforms(UB_grid_fs_params, SG_RANGE(ref fsParams));
            sg_draw(0, 6, 1);
        }

        public static void Cleanup()
        {
            if (_pip.id != 0) { sg_destroy_pipeline(_pip); _pip = default; }
            if (_vbuf.id != 0) { sg_destroy_buffer(_vbuf); _vbuf = default; }
            if (_ibuf.id != 0) { sg_destroy_buffer(_ibuf); _ibuf = default; }
        }
    }
}
