// Text3DRenderer.cs — G8.4 (examples/GameEditor/docs/TEXT3D_SVG_DRAW_DESIGN.md §4): text in the world.
//
// Each Text3D's text is drawn into a texture by the game UI's NanoVG context (its fonts, right-to-left shaping) — again
// only when what it shows changes. NanoVG's sokol backend makes its pipelines in the swapchain's formats, so the texture's
// target is made in exactly those (the environment's colour format, DEPTH_STENCIL, its sample count — with a resolve image
// when that is more than one). The text is drawn outside every pass and outside the UI's own NanoVG frame (Prepare, before
// the scene's passes). Its quad is placed on the CPU — turned with its entity, or to the camera — and drawn by a pipeline
// of its own after the opaque scene, depth-tested, not writing depth.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using Sokol.GUI;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using T3 = text3d_shader_cs_text3d.Shaders;

namespace GameEditor.Framework.Renderer.Server
{
    internal static class Text3DRenderer
    {
        private const float LinePx = 64f;        // a line's height in the texture
        private const int MaxPx = 2048;          // the texture's longest side
        private const int KeepFrames = 120;      // a text not shown this long frees its texture

        private sealed class Entry
        {
            public string Shown = "";             // what its texture holds (text, font, colours, outline)
            public sg_image Color, Depth, Resolve;
            public sg_view ColorAtt, DepthAtt, ResolveAtt, Tex;
            public int W, H, Seen;
        }

        private static readonly Dictionary<Entity, Entry> _entries = new();
        private static readonly List<Entity> _gone = new();
        private static sg_shader _shader;
        private static sg_pipeline _pipOffscreen, _pipSwapchain;
        private static sg_sampler _sampler;
        private static sg_buffer _vbuf;
        private static Sokol.GUI.Renderer? _renderer;
        private static int _frame;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Vertex { public Vector3 Pos; public Vector2 Uv; }
        private const int MaxQuads = 1024;
        private static readonly Vertex[] _verts = new Vertex[6];

        /// <summary>How many texts hold a texture now (a check: freed when not shown).</summary>
        public static int Textures => _entries.Count;

        /// <summary>
        /// Once a frame, before the scene's passes: the textures of the texts that changed, drawn; those of texts gone,
        /// freed. Nothing without a UI NanoVG context (a test, a tool).
        /// </summary>
        public static void Prepare(ECSWorld world)
        {
            _frame++;
            IntPtr vg = UI.GameUI.TextVg;
            if (vg == IntPtr.Zero) return;
            if (_renderer == null || _renderer.VGContext != vg) _renderer = new Sokol.GUI.Renderer(vg);
            foreach (var row in world.Query<ActiveFlag, Text3D>().EnumerateWithEntities<ActiveFlag, Text3D>())
            {
                if (!row.Item1.Value.Active) continue;
                ref readonly var t = ref row.Item2.Value;
                string text = UI.GameUI.Shown(t.Key, t.Text);
                string font = UI.GameUI.FontName(t.Font);
                string shown = string.Concat(text, "\u0001", font, "\u0001", t.Color.ToString(), "\u0001", t.Outline.ToString(),
                                             "\u0001", t.OutlineColor.ToString(), "\u0001", t.Align.ToString());
                if (!_entries.TryGetValue(row.Entity, out var e)) _entries[row.Entity] = e = new Entry();
                e.Seen = _frame;
                if (e.Shown == shown) continue;
                e.Shown = shown;
                Draw(e, text, font, t);
            }
            _gone.Clear();
            foreach (var pair in _entries) if (_frame - pair.Value.Seen > KeepFrames) _gone.Add(pair.Key);
            foreach (var g in _gone) { Free(_entries[g]); _entries.Remove(g); }
        }

        // The text into the entry's texture: measured at LinePx a line, outlined by drawing it in the outline's colour
        // around itself first.
        private static void Draw(Entry e, string text, string font, in Text3D t)
        {
            var r = _renderer!;
            float outline = MathF.Max(0f, t.Outline) * LinePx;
            float pad = 4f + outline;
            r.SetFont(font);
            r.SetFontSize(LinePx);
            var (tw, th) = r.MeasureTextBounds(0f, 0f, MaxPx, text.Length == 0 ? " " : text);
            float scale = MathF.Min(1f, (MaxPx - 2 * pad) / MathF.Max(1f, MathF.Max(tw, th)));   // a very long text, smaller
            int w = Math.Clamp((int)MathF.Ceiling(tw * scale + 2 * pad + 8f), 4, MaxPx);
            int h = Math.Clamp((int)MathF.Ceiling(th * scale + 2 * pad), 4, MaxPx);
            if (e.W != w || e.H != h || e.Color.id == 0) { Free(e); Make(e, w, h); }

            sg_begin_pass(new sg_pass
            {
                attachments = { colors = { [0] = e.ColorAtt }, resolves = { [0] = e.ResolveAtt }, depth_stencil = e.DepthAtt },
                action =
                {
                    colors = { [0] = new sg_color_attachment_action { load_action = sg_load_action.SG_LOADACTION_CLEAR, clear_value = default } },
                    depth = new sg_depth_attachment_action { load_action = sg_load_action.SG_LOADACTION_CLEAR, clear_value = 1f },
                    stencil = new sg_stencil_attachment_action { load_action = sg_load_action.SG_LOADACTION_CLEAR },
                },
                label = "text3d-pass",
            });
            r.BeginFrame(w, h, 1f);
            r.SetFont(font);
            r.SetFontSize(LinePx * scale);
            var align = t.Align switch { UIAlign.Left => TextHAlign.Left, UIAlign.Right => TextHAlign.Right, _ => TextHAlign.Center };
            r.SetTextAlign(align, TextVAlign.Top);
            // The box a little wider than the measured text: the text's own line breaks only, never one from rounding.
            float boxW = w - 2 * pad + 8f;
            if (outline > 0f)
            {
                var oc = new UIColor(t.OutlineColor.X, t.OutlineColor.Y, t.OutlineColor.Z, t.OutlineColor.W);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * MathF.PI / 4f;
                    r.DrawTextBox(pad + MathF.Cos(a) * outline, pad + MathF.Sin(a) * outline, boxW, text, oc);
                }
            }
            r.DrawTextBox(pad, pad, boxW, text, new UIColor(t.Color.X, t.Color.Y, t.Color.Z, t.Color.W));
            r.EndFrame();
            sg_end_pass();
        }

        private static void Make(Entry e, int w, int h)
        {
            var env = sglue_environment().defaults;
            int samples = Math.Max(1, env.sample_count);
            e.W = w; e.H = h;
            e.Color = sg_make_image(new sg_image_desc
            {
                width = w, height = h, pixel_format = env.color_format, sample_count = samples,
                usage = { color_attachment = true }, label = "text3d-color",
            });
            e.Depth = sg_make_image(new sg_image_desc
            {
                width = w, height = h, pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH_STENCIL, sample_count = samples,
                usage = { depth_stencil_attachment = true }, label = "text3d-depth",
            });
            e.ColorAtt = sg_make_view(new sg_view_desc { color_attachment = new sg_image_view_desc { image = e.Color } });
            e.DepthAtt = sg_make_view(new sg_view_desc { depth_stencil_attachment = new sg_image_view_desc { image = e.Depth } });
            sg_image sampled = e.Color;
            if (samples > 1)
            {
                e.Resolve = sg_make_image(new sg_image_desc
                {
                    width = w, height = h, pixel_format = env.color_format, sample_count = 1,
                    usage = { resolve_attachment = true }, label = "text3d-resolve",
                });
                e.ResolveAtt = sg_make_view(new sg_view_desc { resolve_attachment = new sg_image_view_desc { image = e.Resolve } });
                sampled = e.Resolve;
            }
            e.Tex = sg_make_view(new sg_view_desc { texture = new sg_texture_view_desc { image = sampled } });
        }

        private static void Free(Entry e)
        {
            foreach (var v in new[] { e.Tex, e.ColorAtt, e.DepthAtt, e.ResolveAtt }) if (v.id != 0) sg_destroy_view(v);
            foreach (var i in new[] { e.Color, e.Depth, e.Resolve }) if (i.id != 0) sg_destroy_image(i);
            e.Tex = e.ColorAtt = e.DepthAtt = e.ResolveAtt = default;
            e.Color = e.Depth = e.Resolve = default;
            e.W = e.H = 0;
        }

        private static void EnsurePipelines()
        {
            if (_shader.id != 0) return;
            _shader = sg_make_shader(T3.text3d_text3d_shader_desc(sg_query_backend()));
            _sampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = sg_filter.SG_FILTER_LINEAR, mag_filter = sg_filter.SG_FILTER_LINEAR,
                wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE, wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE, label = "text3d-sampler",
            });
            _vbuf = sg_make_buffer(new sg_buffer_desc
            {
                size = (nuint)(MaxQuads * 6 * Unsafe.SizeOf<Vertex>()),
                usage = new sg_buffer_usage { vertex_buffer = true, stream_update = true }, label = "text3d-vertices",
            });
            var sc = sglue_swapchain();
            _pipOffscreen = MakePipeline(sg_pixel_format.SG_PIXELFORMAT_RGBA8, sg_pixel_format.SG_PIXELFORMAT_DEPTH, 1, "text3d-offscreen");
            _pipSwapchain = MakePipeline(sc.color_format, sc.depth_format, sc.sample_count, "text3d-swapchain");
        }

        private static sg_pipeline MakePipeline(sg_pixel_format color, sg_pixel_format depth, int samples, string label)
        {
            var d = new sg_pipeline_desc
            {
                shader = _shader,
                cull_mode = sg_cull_mode.SG_CULLMODE_NONE,
                sample_count = samples,
                depth = new sg_depth_state { pixel_format = depth, compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL, write_enabled = false },
                label = label,
            };
            d.layout.buffers[0].stride = Unsafe.SizeOf<Vertex>();
            d.layout.attrs[T3.ATTR_text3d_text3d_position].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
            d.layout.attrs[T3.ATTR_text3d_text3d_texcoord] = new sg_vertex_attr_state { offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };
            d.colors[0].pixel_format = color;
            d.colors[0].blend = new sg_blend_state
            {
                enabled = true,
                src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE, dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE, dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
            };
            return sg_make_pipeline(d);
        }

        /// <summary>
        /// The texts' quads, into the open pass (an RGBA8 offscreen target, or the swapchain): each placed from its entity's
        /// world matrix (or turned to the camera), its line height <see cref="Text3D.Size"/> times its scale.
        /// </summary>
        public static unsafe void Draw(ECSWorld world, in Matrix4x4 viewProj, bool offscreen)
        {
            if (_entries.Count == 0) return;
            EnsurePipelines();
            // The camera's right and up in the world: two points of the screen taken back into it.
            Matrix4x4.Invert(viewProj, out var inv);
            Vector3 At(float x, float y) { var p = Vector4.Transform(new Vector4(x, y, 0.5f, 1f), inv); return new Vector3(p.X, p.Y, p.Z) / p.W; }
            Vector3 c0 = At(0, 0), camRight = Vector3.Normalize(At(0.1f, 0) - c0), camUp = Vector3.Normalize(At(0, 0.1f) - c0);
            bool flipV = !sg_query_features().origin_top_left;   // a GL render target's rows run bottom-up

            bool applied = false;
            foreach (var row in world.Query<ActiveFlag, Text3D, Transform>().EnumerateWithEntities<ActiveFlag, Text3D, Transform>())
            {
                if (!row.Item1.Value.Active || !_entries.TryGetValue(row.Entity, out var e) || e.Tex.id == 0) continue;
                ref readonly var t = ref row.Item2.Value;
                Matrix4x4 m = Transform.GetWorldMatrix(world, row.Item3.Value);
                var pos = m.Translation;
                // The entity's own axes: its text reads to a camera turned as it is (looking along its +Z), whose right is
                // its -X in this right-handed world.
                Vector3 right = -new Vector3(m.M11, m.M12, m.M13), up = new Vector3(m.M21, m.M22, m.M23);
                float scale = up.Length();
                if (t.Billboard == Text3DBillboard.Full) { right = camRight * scale; up = camUp * scale; }
                else if (t.Billboard == Text3DBillboard.Y)
                {
                    var flat = new Vector3(camRight.X, 0f, camRight.Z);
                    right = (flat.LengthSquared() > 1e-6f ? Vector3.Normalize(flat) : Vector3.UnitX) * scale;
                    up = Vector3.UnitY * scale;
                }
                else { right = Vector3.Normalize(right) * scale; up = Vector3.Normalize(up) * scale; }
                // The texture's height in lines (its pad included) sets the quad's: a line is Size high in the world.
                float hgt = t.Size * e.H / LinePx, wid = hgt * e.W / MathF.Max(1f, e.H);
                float x0 = t.Align == UIAlign.Left ? 0f : t.Align == UIAlign.Right ? -wid : -wid / 2f;
                Vector3 P(float x, float y) => pos + right * x + up * y;
                var a = P(x0, hgt / 2f); var b = P(x0 + wid, hgt / 2f); var c = P(x0 + wid, -hgt / 2f); var d = P(x0, -hgt / 2f);
                float vt = flipV ? 1f : 0f, vb = flipV ? 0f : 1f;
                _verts[0] = new Vertex { Pos = a, Uv = new Vector2(0, vt) };
                _verts[1] = new Vertex { Pos = b, Uv = new Vector2(1, vt) };
                _verts[2] = new Vertex { Pos = c, Uv = new Vector2(1, vb) };
                _verts[3] = _verts[0];
                _verts[4] = _verts[2];
                _verts[5] = new Vertex { Pos = d, Uv = new Vector2(0, vb) };
                int off;
                fixed (Vertex* p = _verts)
                    off = sg_append_buffer(_vbuf, new sg_range { ptr = p, size = (nuint)(6 * Unsafe.SizeOf<Vertex>()) });
                if (!applied)
                {
                    sg_apply_pipeline(offscreen ? _pipOffscreen : _pipSwapchain);
                    var vs = new T3.text3d_text3d_vs_params_t { view_proj = viewProj };
                    sg_apply_uniforms(T3.UB_text3d_text3d_vs_params, SG_RANGE(ref vs));
                    applied = true;
                }
                sg_apply_bindings(new sg_bindings
                {
                    vertex_buffers = { [0] = _vbuf }, vertex_buffer_offsets = { [0] = off },
                    views = { [T3.VIEW_text3d_text_tex] = e.Tex }, samplers = { [T3.SMP_text3d_text_smp] = _sampler },
                });
                sg_draw(0, 6, 1);
            }
        }

        public static void Shutdown()
        {
            foreach (var e in _entries.Values) Free(e);
            _entries.Clear();
            if (_pipOffscreen.id != 0) sg_destroy_pipeline(_pipOffscreen);
            if (_pipSwapchain.id != 0) sg_destroy_pipeline(_pipSwapchain);
            if (_shader.id != 0) sg_destroy_shader(_shader);
            if (_sampler.id != 0) sg_destroy_sampler(_sampler);
            if (_vbuf.id != 0) sg_destroy_buffer(_vbuf);
            _pipOffscreen = _pipSwapchain = default; _shader = default; _sampler = default; _vbuf = default;
            _renderer = null;
        }
    }
}
