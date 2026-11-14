using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using static Sokol.SG.sg_pixel_format;
using static screen_copy_shader_cs_screen_copy.Shaders;

namespace GameEditor.Framework.Renderer.Server.Passes
{
    /// <summary>
    /// Exact-passthrough fullscreen blit. Copies one RGBA8 color texture into the
    /// currently active pass's color attachment unchanged (no tonemap / sRGB).
    /// <para>
    /// Used by the transmission path to snapshot the opaque scene into a sampleable
    /// "screen color" texture — you cannot sample the attachment you are drawing into,
    /// so transmissive surfaces refract a copy rather than the live target.
    /// </para>
    /// Call <see cref="Render"/> inside an active <c>sg_begin_pass / sg_end_pass</c>
    /// whose color attachment is RGBA8 and which has no depth attachment.
    /// </summary>
    internal sealed class ScreenCopyPass
    {
        private sg_shader   _shader;
        private sg_pipeline _pipeline;            // → RGBA8, sample_count 1, no depth (offscreen screen-copy)
        private sg_pipeline _swapchainPipeline;   // → swapchain colour/depth/sample_count (present)
        private sg_pipeline _hdrPipeline;         // → the HDR format (G8.3): the copy of an HDR scene, made when first needed
        private sg_sampler  _sampler;

        public void Init()
        {
            _shader = sg_make_shader(screen_copy_screen_copy_shader_desc(sg_query_backend()));

            _sampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter    = sg_filter.SG_FILTER_LINEAR,
                mag_filter    = sg_filter.SG_FILTER_LINEAR,
                mipmap_filter = sg_filter._SG_FILTER_DEFAULT,
                wrap_u        = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_v        = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                label         = "screen-copy-sampler",
            });

            // No vertex layout: the VS generates a fullscreen triangle from gl_VertexIndex.
            // The blit pass has NO depth attachment, so the pipeline's depth format must be pinned to
            // NONE — an unset depth.pixel_format defaults to the environment depth format and would
            // fail sg_apply_pipeline's depth-attachment-format validation. Color is RGBA8 to match
            // the screen-copy target.
            _pipeline = sg_make_pipeline(new sg_pipeline_desc
            {
                shader         = _shader,
                primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
                colors = { [0] = new sg_color_target_state { pixel_format = SG_PIXELFORMAT_RGBA8 } },
                depth  = new sg_depth_state { pixel_format = SG_PIXELFORMAT_NONE },
                // Pinned to 1: the screen-copy target is always single-sampled. Without this the
                // pipeline inherits the environment default (e.g. 4 in an MSAA standalone) and the
                // sample-count would mismatch the target.
                sample_count = 1,
                label = "screen-copy-pipeline",
            });

            // Present pipeline: copies a single-sampled scene texture into the swapchain backbuffer
            // (used by standalone apps that render the 3D scene offscreen, then blit it to the
            // swapchain). Matched to the swapchain's colour/depth format + sample count; depth test
            // disabled (fullscreen blit). The editor presents via ImGui instead, so it never uses this.
            var sc = sglue_swapchain();
            _swapchainPipeline = sg_make_pipeline(new sg_pipeline_desc
            {
                shader         = _shader,
                primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
                colors = { [0] = new sg_color_target_state { pixel_format = sc.color_format } },
                depth  = new sg_depth_state
                {
                    pixel_format  = sc.depth_format,
                    compare       = sg_compare_func.SG_COMPAREFUNC_ALWAYS,
                    write_enabled = false,
                },
                sample_count = sc.sample_count,
                label = "screen-copy-swapchain-pipeline",
            });
        }

        public void Shutdown()
        {
            if (_pipeline.id != 0)          { sg_destroy_pipeline(_pipeline);          _pipeline          = default; }
            if (_swapchainPipeline.id != 0) { sg_destroy_pipeline(_swapchainPipeline); _swapchainPipeline = default; }
            if (_hdrPipeline.id != 0)       { sg_destroy_pipeline(_hdrPipeline);       _hdrPipeline       = default; }
            if (_shader.id != 0)            { sg_destroy_shader(_shader);              _shader            = default; }
            if (_sampler.id != 0)           { sg_destroy_sampler(_sampler);            _sampler           = default; }
        }

        /// <summary>Draws a fullscreen triangle copying <paramref name="srcView"/> into the active pass.</summary>
        public void Render(sg_view srcView) => Blit(_pipeline, srcView);

        /// <summary>The same into a target of <paramref name="format"/> (RGBA8, or the HDR format of an HDR scene's copy).</summary>
        public void Render(sg_view srcView, sg_pixel_format format)
        {
            if (format == SG_PIXELFORMAT_RGBA8) { Blit(_pipeline, srcView); return; }
            if (_hdrPipeline.id == 0)
                _hdrPipeline = sg_make_pipeline(new sg_pipeline_desc
                {
                    shader         = _shader,
                    primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
                    colors = { [0] = new sg_color_target_state { pixel_format = format } },
                    depth  = new sg_depth_state { pixel_format = SG_PIXELFORMAT_NONE },
                    sample_count = 1,
                    label = "screen-copy-hdr-pipeline",
                });
            Blit(_hdrPipeline, srcView);
        }

        /// <summary>
        /// Copies <paramref name="srcView"/> into the active SWAPCHAIN pass (swapchain-format pipeline).
        /// Lets a standalone app present its offscreen-rendered scene to the backbuffer.
        /// </summary>
        public void RenderToSwapchain(sg_view srcView) => Blit(_swapchainPipeline, srcView);

        private void Blit(sg_pipeline pip, sg_view srcView)
        {
            if (pip.id == 0) return;
            sg_apply_pipeline(pip);
            sg_apply_bindings(new sg_bindings
            {
                views    = { [VIEW_screen_copy_src_tex] = srcView  },
                samplers = { [SMP_screen_copy_src_smp]  = _sampler },
            });
            sg_draw(0, 3, 1);
        }
    }
}
