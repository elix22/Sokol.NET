// CompactVertex.cs — a static mesh's vertex in 28 bytes instead of 56 
// : position as 3 floats, normal and tangent as 4 signed normalised bytes each, each UV as 2 unsigned normalised
// 16-bit numbers (0..1 exactly to a 65,536th). The GPU converts them back to the floats the shaders read, so the shaders
// are the same; the pipelines' layout differs (PipelineFlags.CompactVertices).

using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace GameEditor.Framework.Renderer.Server.Resources
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CompactVertex
    {
        public Vector3 Position;                 // @0   FLOAT3
        public sbyte Nx, Ny, Nz, Nw;             // @12  BYTE4N
        public ushort U, V;                      // @16  USHORT2N (TEXCOORD_0)
        public sbyte Tx, Ty, Tz, Tw;             // @20  BYTE4N (w: the bitangent's sign)
        public ushort U1, V1;                    // @24  USHORT2N (TEXCOORD_1)

        public const int SizeBytes = 28;

        /// <summary>Every UV of the vertices lies in 0..1 (a little rounding allowed): only then can they be compact.</summary>
        public static bool Fits(ReadOnlySpan<ObjVertex> vertices)
        {
            const float lo = -1e-4f, hi = 1f + 1e-4f;
            foreach (ref readonly var v in vertices)
                if (v.Uv.X < lo || v.Uv.X > hi || v.Uv.Y < lo || v.Uv.Y > hi || v.Uv1.X < lo || v.Uv1.X > hi || v.Uv1.Y < lo || v.Uv1.Y > hi)
                    return false;
            return true;
        }

        public static CompactVertex[] From(ReadOnlySpan<ObjVertex> vertices)
        {
            var c = new CompactVertex[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                ref readonly var v = ref vertices[i];
                c[i].Position = v.Position;
                c[i].Nx = S8(v.Normal.X); c[i].Ny = S8(v.Normal.Y); c[i].Nz = S8(v.Normal.Z);
                c[i].Tx = S8(v.Tangent.X); c[i].Ty = S8(v.Tangent.Y); c[i].Tz = S8(v.Tangent.Z); c[i].Tw = v.Tangent.W < 0 ? (sbyte)-127 : (sbyte)127;
                c[i].U = U16(v.Uv.X); c[i].V = U16(v.Uv.Y);
                c[i].U1 = U16(v.Uv1.X); c[i].V1 = U16(v.Uv1.Y);
            }
            return c;
        }

        static sbyte S8(float x) => (sbyte)MathF.Round(Math.Clamp(x, -1f, 1f) * 127f);
        static ushort U16(float x) => (ushort)MathF.Round(Math.Clamp(x, 0f, 1f) * 65535f);
    }
}
