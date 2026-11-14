// ParticleBatch.cs — G8.7 (examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §3.0): the world's particles as instanced quads, in
// the order given, inside the pass that is open (right after the sprites): a run of the same texture, filter and blend is one
// draw call. Alpha or Additive blending; depth tested, not written. Steady state allocates nothing.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using GameEditor.Framework.ECS.Components;
using static Sokol.SG;
using static Sokol.SGlue;
using P = particles_shader_cs_particles.Shaders;

namespace GameEditor.Framework.Renderer.Server.Sprites2D
{
    internal sealed unsafe class ParticleBatch : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Inst
        {
            public Vector3 Origin, AxisX, AxisY;
            public Vector4 Uv;      // xy = uv at the quad's (0,0) corner, zw = at its (1,1) corner
            public uint Rgba;
        }

        private struct Cmd { public sg_view Tex; public bool Nearest; public ParticleBlend Blend; public int Start, Count; }

        private const int InitQuads = 4 * 1024;

        private sg_shader _shader;
        private readonly sg_pipeline[] _pips = new sg_pipeline[4];     // [blend * 2 + offscreen]
        private sg_buffer _quadVbuf, _instBuf;
        private int _instBufCap, _wantCap;
        private sg_sampler _linear, _nearest;
        private sg_image _whiteImg;
        private sg_view _whiteView;
        private bool _inited, _overflowLogged;

        private Inst[] _insts = new Inst[InitQuads];
        private int _count;
        private readonly List<Cmd> _cmds = new();

        public int DrawCalls { get; private set; }
        public int QuadCount { get; private set; }

        private void Init()
        {
            if (_inited) return;
            _inited = true;
            _shader = sg_make_shader(P.particles_particles_shader_desc(sg_query_backend()));
            float[] quad = { 0f, 0f, 1f, 0f, 1f, 1f, 0f, 0f, 1f, 1f, 0f, 1f };
            fixed (float* q = quad)
                _quadVbuf = sg_make_buffer(new sg_buffer_desc { data = new sg_range { ptr = q, size = (nuint)(quad.Length * sizeof(float)) }, label = "particles-quad" });
            _instBufCap = InitQuads;
            _instBuf = MakeInstBuffer(_instBufCap);
            _linear = MakeSampler(sg_filter.SG_FILTER_LINEAR, "particles-linear");
            _nearest = MakeSampler(sg_filter.SG_FILTER_NEAREST, "particles-nearest");
            uint white = 0xFFFFFFFFu;
            _whiteImg = sg_make_image(new sg_image_desc
            {
                width = 1, height = 1, pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                data = { mip_levels = { [0] = new sg_range { ptr = &white, size = 4 } } }, label = "particles-white",
            });
            _whiteView = sg_make_view(new sg_view_desc { texture = { image = _whiteImg }, label = "particles-white-view" });
        }

        private static sg_sampler MakeSampler(sg_filter filter, string label) => sg_make_sampler(new sg_sampler_desc
        {
            min_filter = filter, mag_filter = filter, wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE, wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE, label = label,
        });

        private static sg_buffer MakeInstBuffer(int quads) => sg_make_buffer(new sg_buffer_desc
        {
            size = (nuint)(quads * sizeof(Inst)), usage = { vertex_buffer = true, stream_update = true }, label = "particles-instances",
        });

        private sg_pipeline Pipeline(ParticleBlend blend, bool offscreen)
        {
            ref sg_pipeline pip = ref _pips[(blend == ParticleBlend.Additive ? 2 : 0) + (offscreen ? 1 : 0)];
            if (pip.id != 0) return pip;
            bool add = blend == ParticleBlend.Additive;
            var desc = new sg_pipeline_desc
            {
                shader = _shader,
                primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
                cull_mode = sg_cull_mode.SG_CULLMODE_NONE,
                depth = new sg_depth_state { compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL, write_enabled = false },
                label = (add ? "particles-add-" : "particles-alpha-") + (offscreen ? "offscreen" : "swapchain"),
            };
            desc.colors[0].blend = new sg_blend_state
            {
                enabled = true,
                src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
                dst_factor_rgb = add ? sg_blend_factor.SG_BLENDFACTOR_ONE : sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                src_factor_alpha = add ? sg_blend_factor.SG_BLENDFACTOR_ZERO : sg_blend_factor.SG_BLENDFACTOR_ONE,
                dst_factor_alpha = add ? sg_blend_factor.SG_BLENDFACTOR_ONE : sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
            };
            if (offscreen)
            {
                desc.colors[0].pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8;
                desc.depth.pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH;
                desc.sample_count = 1;
            }
            else
            {
                var sc = sglue_swapchain();
                desc.colors[0].pixel_format = sc.color_format;
                desc.depth.pixel_format = sc.depth_format;
                desc.sample_count = sc.sample_count;
            }
            desc.layout.buffers[0].stride = 2 * sizeof(float);
            desc.layout.buffers[1].stride = sizeof(Inst);
            desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
            desc.layout.attrs[P.ATTR_particles_particles_corner] = new sg_vertex_attr_state { buffer_index = 0, offset = 0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };
            desc.layout.attrs[P.ATTR_particles_particles_i_origin] = new sg_vertex_attr_state { buffer_index = 1, offset = 0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[P.ATTR_particles_particles_i_axis_x] = new sg_vertex_attr_state { buffer_index = 1, offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[P.ATTR_particles_particles_i_axis_y] = new sg_vertex_attr_state { buffer_index = 1, offset = 24, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[P.ATTR_particles_particles_i_uvrect] = new sg_vertex_attr_state { buffer_index = 1, offset = 36, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[P.ATTR_particles_particles_i_color] = new sg_vertex_attr_state { buffer_index = 1, offset = 52, format = sg_vertex_format.SG_VERTEXFORMAT_UBYTE4N };
            pip = sg_make_pipeline(desc);
            return pip;
        }

        /// <summary>Once a frame, before the first view: a frame that did not fit gets a larger buffer here.</summary>
        public void BeginFrame()
        {
            if (!_inited || _wantCap <= _instBufCap) return;
            sg_destroy_buffer(_instBuf);
            _instBufCap = _wantCap;
            _instBuf = MakeInstBuffer(_instBufCap);
            _overflowLogged = false;
        }

        public void Begin() { _count = 0; _cmds.Clear(); }

        public void Draw(sg_view texture, bool nearest, ParticleBlend blend, Vector3 origin, Vector3 axisX, Vector3 axisY, Vector4 uv, uint rgba)
        {
            if (_count >= _insts.Length) Array.Resize(ref _insts, _insts.Length * 2);
            _insts[_count] = new Inst { Origin = origin, AxisX = axisX, AxisY = axisY, Uv = uv, Rgba = rgba };
            int last = _cmds.Count - 1;
            if (last >= 0 && _cmds[last].Tex.id == texture.id && _cmds[last].Nearest == nearest && _cmds[last].Blend == blend)
            {
                var c = _cmds[last]; c.Count++; _cmds[last] = c;
            }
            else _cmds.Add(new Cmd { Tex = texture, Nearest = nearest, Blend = blend, Start = _count, Count = 1 });
            _count++;
        }

        public void Flush(in Matrix4x4 viewProj, bool offscreen)
        {
            DrawCalls = 0; QuadCount = 0;
            if (_count == 0) return;
            Init();
            if (sg_query_buffer_will_overflow(_instBuf, (nuint)(_count * sizeof(Inst))))
            {
                _wantCap = Math.Max(_wantCap, Math.Max(_instBufCap * 2, _count * 2));
                if (!_overflowLogged) { _overflowLogged = true; Console.Error.WriteLine($"[ParticleBatch] {_count} particles did not fit this frame; the buffer grows to {_wantCap}."); }
                return;
            }
            int offset;
            fixed (Inst* p = _insts) offset = sg_append_buffer(_instBuf, new sg_range { ptr = p, size = (nuint)(_count * sizeof(Inst)) });
            var vs = new P.particles_particles_params_t { view_proj = viewProj };
            ParticleBlend applied = (ParticleBlend)255;
            for (int i = 0; i < _cmds.Count; i++)
            {
                var cmd = _cmds[i];
                if (cmd.Blend != applied)
                {
                    sg_apply_pipeline(Pipeline(cmd.Blend, offscreen));
                    applied = cmd.Blend;
                }
                var bind = default(sg_bindings);
                bind.vertex_buffers[0] = _quadVbuf;
                bind.vertex_buffers[1] = _instBuf;
                bind.vertex_buffer_offsets[1] = offset + cmd.Start * sizeof(Inst);
                bind.views[P.VIEW_particles_particles_tex] = cmd.Tex.id != 0 ? cmd.Tex : _whiteView;
                bind.samplers[P.SMP_particles_particles_smp] = cmd.Nearest ? _nearest : _linear;
                sg_apply_bindings(bind);
                sg_apply_uniforms(P.UB_particles_particles_params, new sg_range { ptr = &vs, size = (nuint)sizeof(P.particles_particles_params_t) });
                sg_draw(0, 6, (uint)cmd.Count);
                DrawCalls++;
            }
            QuadCount = _count;
        }

        public void Dispose()
        {
            if (!_inited) return;
            _inited = false;
            for (int i = 0; i < _pips.Length; i++) { if (_pips[i].id != 0) sg_destroy_pipeline(_pips[i]); _pips[i] = default; }
            sg_destroy_shader(_shader);
            sg_destroy_buffer(_quadVbuf);
            sg_destroy_buffer(_instBuf);
            sg_destroy_sampler(_linear);
            sg_destroy_sampler(_nearest);
            sg_destroy_view(_whiteView);
            sg_destroy_image(_whiteImg);
        }
    }
}
