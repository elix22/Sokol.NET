// ScriptLinesRenderer.cs — G8.4 D3 (examples/GameEditor/docs/TEXT3D_SVG_DRAW_DESIGN.md §5c): the lines scripts draw in the
// world (Scripting.Draw), into an open offscreen RGBA8 pass, depth-tested or over everything as each asked. Only where a
// view asks for them (RenderingServer.DrawScriptLines: the editor's Scene view, its Game view with Gizmos on).

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Sokol.SG;
using static Sokol.Utils;
using L = lines_shader_cs_lines.Shaders;

namespace GameEditor.Framework.Renderer.Server
{
    internal static unsafe class ScriptLinesRenderer
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex { public Vector3 Pos; public Vector4 Color; }

        private const int MaxVertices = 2 * 65536;
        private static readonly Vertex[] _depth = new Vertex[MaxVertices], _over = new Vertex[MaxVertices];
        private static sg_shader _shader;
        private static sg_pipeline _pipDepth, _pipOver;
        private static sg_buffer _vbuf;

        private static void Ensure()
        {
            if (_shader.id != 0) return;
            _shader = sg_make_shader(L.lines_lines_shader_desc(sg_query_backend()));
            _vbuf = sg_make_buffer(new sg_buffer_desc
            {
                size = (nuint)(2 * MaxVertices * Unsafe.SizeOf<Vertex>()),
                usage = new sg_buffer_usage { vertex_buffer = true, stream_update = true }, label = "script-lines",
            });
            _pipDepth = Make(sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL, "script-lines-depth");
            _pipOver = Make(sg_compare_func.SG_COMPAREFUNC_ALWAYS, "script-lines-over");
        }

        private static sg_pipeline Make(sg_compare_func compare, string label)
        {
            var d = new sg_pipeline_desc
            {
                shader = _shader,
                primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_LINES,
                sample_count = 1,
                depth = new sg_depth_state { pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH, compare = compare, write_enabled = false },
                label = label,
            };
            d.layout.buffers[0].stride = Unsafe.SizeOf<Vertex>();
            d.layout.attrs[L.ATTR_lines_lines_position].format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
            d.layout.attrs[L.ATTR_lines_lines_color0] = new sg_vertex_attr_state { offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            d.colors[0].pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8;
            d.colors[0].blend = new sg_blend_state
            {
                enabled = true,
                src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA, dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE, dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
            };
            return sg_make_pipeline(d);
        }

        /// <summary>The lines, into the open RGBA8 offscreen pass.</summary>
        public static void Draw(in Matrix4x4 viewProj)
        {
            var lines = Scripting.Draw.Lines;
            if (lines.Count == 0) return;
            Ensure();
            int nd = 0, no = 0;
            foreach (var l in lines)
            {
                if (l.Depth) { if (nd + 2 > MaxVertices) continue; _depth[nd++] = new Vertex { Pos = l.A, Color = l.Color }; _depth[nd++] = new Vertex { Pos = l.B, Color = l.Color }; }
                else { if (no + 2 > MaxVertices) continue; _over[no++] = new Vertex { Pos = l.A, Color = l.Color }; _over[no++] = new Vertex { Pos = l.B, Color = l.Color }; }
            }
            var vs = new L.lines_lines_vs_params_t { view_proj = viewProj };
            Submit(_pipDepth, _depth, nd, ref vs);
            Submit(_pipOver, _over, no, ref vs);
        }

        private static void Submit(sg_pipeline pip, Vertex[] verts, int n, ref L.lines_lines_vs_params_t vs)
        {
            if (n == 0) return;
            int off;
            fixed (Vertex* p = verts)
                off = sg_append_buffer(_vbuf, new sg_range { ptr = p, size = (nuint)(n * Unsafe.SizeOf<Vertex>()) });
            sg_apply_pipeline(pip);
            sg_apply_uniforms(L.UB_lines_lines_vs_params, SG_RANGE(ref vs));
            sg_apply_bindings(new sg_bindings { vertex_buffers = { [0] = _vbuf }, vertex_buffer_offsets = { [0] = off } });
            sg_draw(0, (uint)n, 1);
        }

        public static void Shutdown()
        {
            if (_pipDepth.id != 0) sg_destroy_pipeline(_pipDepth);
            if (_pipOver.id != 0) sg_destroy_pipeline(_pipOver);
            if (_shader.id != 0) sg_destroy_shader(_shader);
            if (_vbuf.id != 0) sg_destroy_buffer(_vbuf);
            _pipDepth = _pipOver = default; _shader = default; _vbuf = default;
        }
    }
}
