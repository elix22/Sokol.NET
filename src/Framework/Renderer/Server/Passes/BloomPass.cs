using System;
using static Sokol.SG;
using static Sokol.Utils;
using static bloom_shader_cs_bloom.Shaders;

namespace GameEditor.Framework.Renderer.Server.Passes
{
    /// <summary>
    /// The post effects' bloom (G8.9a, examples/GameEditor/docs/POST_EFFECTS_DESIGN.md §3): the scene's picture prefiltered
    /// into half size, halved level by level, then brought back up each level mixed with the one under it by Scatter. Its
    /// targets are made on first use, follow the picture's size and are freed by <see cref="Free"/> (bloom turned off).
    /// </summary>
    internal sealed class BloomPass
    {
        private const int MaxLevels = 6;

        private struct Level
        {
            public sg_image Image;
            public sg_view Att, Tex;
            public int W, H;
        }

        private readonly Level[] _down = new Level[MaxLevels], _up = new Level[MaxLevels];
        private int _levels, _w, _h;
        private sg_pixel_format _format;
        private sg_shader _preShader, _downShader, _upShader;
        private sg_pipeline _pre, _downPipe, _upPipe;
        private sg_sampler _sampler;

        /// <summary>Images the bloom holds now (none when it is off): for the checks.</summary>
        public int LiveImages { get; private set; }

        public void Shutdown()
        {
            Free();
            if (_sampler.id != 0) { sg_destroy_sampler(_sampler); _sampler = default; }
            foreach (var s in new[] { _preShader, _downShader, _upShader }) if (s.id != 0) sg_destroy_shader(s);
            _preShader = _downShader = _upShader = default;
        }

        /// <summary>The targets and pipelines freed (bloom off); made again on the next <see cref="Render"/>.</summary>
        public void Free()
        {
            for (int i = 0; i < MaxLevels; i++) { Destroy(ref _down[i]); Destroy(ref _up[i]); }
            foreach (var p in new[] { _pre, _downPipe, _upPipe }) if (p.id != 0) sg_destroy_pipeline(p);
            _pre = _downPipe = _upPipe = default;
            _levels = _w = _h = 0;
            _format = default;
            LiveImages = 0;
        }

        private void Destroy(ref Level l)
        {
            if (l.Tex.id != 0) sg_destroy_view(l.Tex);
            if (l.Att.id != 0) sg_destroy_view(l.Att);
            if (l.Image.id != 0) sg_destroy_image(l.Image);
            l = default;
        }

        private void Make(ref Level l, int w, int h, string label)
        {
            l.W = w; l.H = h;
            l.Image = sg_make_image(new sg_image_desc
            {
                width = w, height = h, pixel_format = _format, sample_count = 1,
                usage = { color_attachment = true }, label = label,
            });
            l.Att = sg_make_view(new sg_view_desc { color_attachment = new sg_image_view_desc { image = l.Image }, label = label });
            l.Tex = sg_make_view(new sg_view_desc { texture = new sg_texture_view_desc { image = l.Image }, label = label });
            LiveImages++;
        }

        private sg_pipeline Pipeline(sg_shader shader, string label) => sg_make_pipeline(new sg_pipeline_desc
        {
            shader = shader,
            primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
            colors = { [0] = new sg_color_target_state { pixel_format = _format } },
            depth = new sg_depth_state { pixel_format = sg_pixel_format.SG_PIXELFORMAT_NONE },
            sample_count = 1,
            label = label,
        });

        // The chain for a picture of w x h in format: made again when either changed.
        private void Prepare(int w, int h, sg_pixel_format format)
        {
            if (_w == w && _h == h && _format == format && _levels > 0) return;
            Free();
            _w = w; _h = h; _format = format;
            if (_preShader.id == 0)
            {
                sg_backend backend = sg_query_backend();
                _preShader = sg_make_shader(bloom_bloom_prefilter_shader_desc(backend));
                _downShader = sg_make_shader(bloom_bloom_down_shader_desc(backend));
                _upShader = sg_make_shader(bloom_bloom_up_shader_desc(backend));
                _sampler = sg_make_sampler(new sg_sampler_desc
                {
                    min_filter = sg_filter.SG_FILTER_LINEAR, mag_filter = sg_filter.SG_FILTER_LINEAR,
                    wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE, wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE, label = "bloom-sampler",
                });
            }
            _pre = Pipeline(_preShader, "bloom-prefilter");
            _downPipe = Pipeline(_downShader, "bloom-down");
            _upPipe = Pipeline(_upShader, "bloom-up");
            _levels = PostEffects.BloomLevels(w, h);
            int lw = Math.Max(1, w / 2), lh = Math.Max(1, h / 2);
            for (int i = 0; i < _levels; i++)
            {
                Make(ref _down[i], lw, lh, "bloom-down");
                if (i < _levels - 1) Make(ref _up[i], lw, lh, "bloom-up");
                lw = Math.Max(1, lw / 2); lh = Math.Max(1, lh / 2);
            }
        }

        private void Pass(in Level target, sg_pipeline pipe, sg_view source, int sw, int sh, System.Numerics.Vector4 p, sg_view high = default)
        {
            sg_begin_pass(new sg_pass
            {
                attachments = { colors = { [0] = target.Att } },
                action = { colors = { [0] = new sg_color_attachment_action { load_action = sg_load_action.SG_LOADACTION_DONTCARE, store_action = sg_store_action.SG_STOREACTION_STORE } } },
                label = "bloom-pass",
            });
            sg_apply_pipeline(pipe);
            var u = new bloom_bloom_params_t
            {
                u_Target = new System.Numerics.Vector4(1f / target.W, 1f / target.H, 0f, 0f),
                u_Source = new System.Numerics.Vector4(1f / sw, 1f / sh, 0f, 0f),
                u_Params = p,
            };
            sg_apply_uniforms(UB_bloom_bloom_params, SG_RANGE(ref u));
            var b = new sg_bindings();
            b.views[VIEW_bloom_src_tex] = source;
            b.samplers[SMP_bloom_src_smp] = _sampler;
            if (high.id != 0) { b.views[VIEW_bloom_high_tex] = high; b.samplers[SMP_bloom_high_smp] = _sampler; }
            sg_apply_bindings(b);
            sg_draw(0, 3, 1);
            sg_end_pass();
        }

        /// <summary>
        /// The bloom of a picture (<paramref name="source"/>, <paramref name="width"/> × <paramref name="height"/>):
        /// returns the half-size glow to add to it. <paramref name="encoded"/>: the picture is display-encoded (no float
        /// target): the threshold is taken as it is. Outside any pass.
        /// </summary>
        public sg_view Render(sg_view source, int width, int height, in PostEffects.BloomSettings s, bool encoded, sg_pixel_format format)
        {
            Prepare(width, height, format);
            float threshold = encoded ? s.Threshold : PostEffects.LinearThreshold(s.Threshold);
            var pre = new System.Numerics.Vector4(threshold, MathF.Max(1e-4f, threshold * 0.5f), MathF.Max(0f, s.Clamp), 0f);
            Pass(in _down[0], _pre, source, width, height, pre);
            var down = new System.Numerics.Vector4(s.HighQuality ? 1f : 0f, 0f, 0f, 0f);
            for (int i = 1; i < _levels; i++)
                Pass(in _down[i], _downPipe, _down[i - 1].Tex, _down[i - 1].W, _down[i - 1].H, down);
            if (_levels == 1) return _down[0].Tex;
            var up = new System.Numerics.Vector4(PostEffects.ScatterMix(s.Scatter), 0f, 0f, 0f);
            Level low = _down[_levels - 1];
            for (int i = _levels - 2; i >= 0; i--)
            {
                Pass(in _up[i], _upPipe, low.Tex, low.W, low.H, up, _down[i].Tex);
                low = _up[i];
            }
            return _up[0].Tex;
        }
    }
}
