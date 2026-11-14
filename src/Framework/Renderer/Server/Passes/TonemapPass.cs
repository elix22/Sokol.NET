using static Sokol.SG;
using static Sokol.Utils;
using static tonemap_pass_shader_cs_tonemap_pass.Shaders;

namespace GameEditor.Framework.Renderer.Server.Passes
{
    /// <summary>
    /// Fullscreen HDR → display tonemap pass.
    /// Reads an RGBA16F offscreen color attachment, applies exposure scaling and
    /// a tone-map operator, and writes gamma-corrected sRGB output.
    /// <para>
    /// Call <see cref="Render"/> inside an active swapchain
    /// <c>sg_begin_pass / sg_end_pass</c> block after all geometry has been drawn.
    /// </para>
    /// </summary>
    internal sealed class TonemapPass
    {
        private sg_shader   _shader;
        private sg_pipeline _pipeline;
        private sg_sampler  _sampler;

        public void Init()
        {
            _shader = sg_make_shader(tonemap_pass_tonemap_pass_shader_desc(sg_query_backend()));

            // Bilinear clamp sampler — no mip levels on an HDR render target.
            _sampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter    = sg_filter.SG_FILTER_LINEAR,
                mag_filter    = sg_filter.SG_FILTER_LINEAR,
                mipmap_filter = sg_filter._SG_FILTER_DEFAULT,
                wrap_u        = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_v        = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                label         = "tonemap-sampler",
            });

            // No vertex layout: the VS generates a fullscreen triangle from gl_VertexIndex.
            _pipeline = sg_make_pipeline(new sg_pipeline_desc
            {
                shader         = _shader,
                primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
                // G8.3: into a view's RGBA8 target whose depth (the HDR scene's) stays loaded for what is drawn after.
                colors = { [0] = new sg_color_target_state { pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8 } },
                depth = new sg_depth_state
                {
                    pixel_format  = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_ALWAYS,
                    write_enabled = false,
                },
                sample_count = 1,
                label = "tonemap-pipeline",
            });
        }

        public void Shutdown()
        {
            if (_pipeline.id != 0) { sg_destroy_pipeline(_pipeline); _pipeline = default; }
            if (_shader.id != 0)   { sg_destroy_shader(_shader);     _shader   = default; }
            if (_sampler.id != 0)  { sg_destroy_sampler(_sampler);   _sampler  = default; }
        }

        /// <summary>
        /// Draws a fullscreen triangle that tonemaps the HDR input into the current pass output.
        /// Must be called inside an active <c>sg_begin_pass / sg_end_pass</c>.
        /// </summary>
        /// <param name="hdrColorView">
        ///     <c>sg_view</c> for the RGBA16F HDR offscreen color attachment to read from.
        /// </param>
        /// <param name="exposure">
        ///     Linear exposure multiplier applied before tonemapping (default 1.0).
        /// </param>
        /// <param name="tonemapType">
        ///     Operator selection: 0 = Linear (exposure only), 1 = ACES Narkowicz,
        ///     2 = ACES Hill, 3 = KHR PBR Neutral (default 1).
        /// </param>
        public void Render(sg_view hdrColorView, float exposure = 1.0f, int tonemapType = 1)
        {
            if (_pipeline.id == 0) return;

            sg_apply_pipeline(_pipeline);

            var p = new tonemap_pass_tonemap_params_t
            {
                u_Exposure = exposure,
                u_Type     = tonemapType,
            };
            sg_apply_uniforms(UB_tonemap_pass_tonemap_params, SG_RANGE(ref p));

            sg_apply_bindings(new sg_bindings
            {
                views    = { [VIEW_tonemap_pass_hdr_tex] = hdrColorView },
                samplers = { [SMP_tonemap_pass_hdr_smp]  = _sampler     },
            });

            sg_draw(0, 3, 1);
        }
    }
}
