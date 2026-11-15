using System;
using static Sokol.SG;
using static Sokol.SG.sg_pixel_format;
using static Sokol.Utils;
using static Sokol.SGlue;
using static fxaa_shader_cs_fxaa.Shaders;

namespace GameEditor.Framework.Renderer.Server.Passes
{
    /// <summary>
    /// The post effects' FXAA (G8.9c, examples/GameEditor/docs/POST_EFFECTS_DESIGN.md §3): a picture drawn into the open pass
    /// with its edges smoothed - a game's copy to the swapchain, or the editor's Game view (into a target of its own, then
    /// copied back). Its pipelines are made on first use.
    /// </summary>
    internal sealed class FxaaPass
    {
        private sg_shader _shader;
        private sg_pipeline _swapchain, _offscreen;
        private sg_sampler _sampler;

        public void Shutdown()
        {
            if (_swapchain.id != 0) sg_destroy_pipeline(_swapchain);
            if (_offscreen.id != 0) sg_destroy_pipeline(_offscreen);
            if (_shader.id != 0) sg_destroy_shader(_shader);
            if (_sampler.id != 0) sg_destroy_sampler(_sampler);
            _swapchain = _offscreen = default;
            _shader = default;
            _sampler = default;
        }

        private void Prepare()
        {
            if (_shader.id != 0) return;
            _shader = sg_make_shader(fxaa_fxaa_shader_desc(sg_query_backend()));
            _sampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = sg_filter.SG_FILTER_LINEAR, mag_filter = sg_filter.SG_FILTER_LINEAR,
                wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE, wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE, label = "fxaa-sampler",
            });
        }

        /// <summary>Into the swapchain's pass (open): <paramref name="source"/> (<paramref name="srcW"/> × <paramref name="srcH"/>)
        /// over the whole <paramref name="dstW"/> × <paramref name="dstH"/>.</summary>
        public void RenderToSwapchain(sg_view source, int srcW, int srcH, int dstW, int dstH)
        {
            Prepare();
            if (_swapchain.id == 0)
            {
                var sc = sglue_swapchain();
                _swapchain = sg_make_pipeline(new sg_pipeline_desc
                {
                    shader = _shader, primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
                    colors = { [0] = new sg_color_target_state { pixel_format = sc.color_format } },
                    depth = new sg_depth_state { pixel_format = sc.depth_format, compare = sg_compare_func.SG_COMPAREFUNC_ALWAYS, write_enabled = false },
                    sample_count = sc.sample_count, label = "fxaa-swapchain-pipeline",
                });
            }
            Draw(_swapchain, source, srcW, srcH, dstW, dstH);
        }

        /// <summary>Into an RGBA8 colour-only pass (open) of <paramref name="w"/> × <paramref name="h"/>, from a source of that size.</summary>
        public void Render(sg_view source, int w, int h)
        {
            Prepare();
            if (_offscreen.id == 0)
                _offscreen = sg_make_pipeline(new sg_pipeline_desc
                {
                    shader = _shader, primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
                    colors = { [0] = new sg_color_target_state { pixel_format = SG_PIXELFORMAT_RGBA8 } },
                    depth = new sg_depth_state { pixel_format = SG_PIXELFORMAT_NONE },
                    sample_count = 1, label = "fxaa-offscreen-pipeline",
                });
            Draw(_offscreen, source, w, h, w, h);
        }

        private void Draw(sg_pipeline pipe, sg_view source, int srcW, int srcH, int dstW, int dstH)
        {
            sg_apply_pipeline(pipe);
            var u = new fxaa_fxaa_params_t
            {
                u_Target = new System.Numerics.Vector4(1f / Math.Max(1, dstW), 1f / Math.Max(1, dstH), 0f, 0f),
                u_Source = new System.Numerics.Vector4(1f / Math.Max(1, srcW), 1f / Math.Max(1, srcH), 0f, 0f),
            };
            sg_apply_uniforms(UB_fxaa_fxaa_params, SG_RANGE(ref u));
            sg_apply_bindings(new sg_bindings
            {
                views = { [VIEW_fxaa_src_tex] = source },
                samplers = { [SMP_fxaa_src_smp] = _sampler },
            });
            Diagnostics.FrameDebugger.Pass = "Post";
            if (Diagnostics.FrameDebugger.Draw("FXAA", 3, 1)) sg_draw(0, 3, 1);
        }
    }
}
