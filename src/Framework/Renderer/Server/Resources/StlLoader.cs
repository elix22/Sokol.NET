// StlLoader.cs — STL (binary + ASCII) parser for RenderingServer.
// Produces the same ObjMesh / ObjSubMesh / ObjVertex the OBJ loader and the glTF
// importer feed to MeshRegistry, so an .stl renders through the existing pipelines.
// STL carries one normal per facet: every triangle gets its own 3 vertices with the
// flat facet normal (no welding). Vertices are kept in the file's axes and units.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.Resources
{
    /// <summary>Binary and ASCII STL parser.</summary>
    public static class StlLoader
    {
        /// <summary>
        /// Parse an STL file. Binary when the size is exactly 84 + 50 × the header's triangle
        /// count, otherwise ASCII when it starts with "solid". Zero-area triangles are dropped.
        /// Returns an empty mesh (no sub-meshes) when nothing could be read.
        /// </summary>
        public static ObjMesh Load(ReadOnlySpan<byte> data, string sourcePath = "")
        {
            var tris = new List<(Vector3 n, Vector3 a, Vector3 b, Vector3 c)>();
            if (data.Length >= 84 && 84L + 50L * BinaryPrimitives.ReadUInt32LittleEndian(data[80..]) == data.Length)
                ReadBinary(data, tris);
            else if (data.Length >= 5 && data[..5].SequenceEqual("solid"u8))
                ReadAscii(System.Text.Encoding.ASCII.GetString(data), tris);

            var verts   = new List<ObjVertex>(tris.Count * 3);
            var indices = new List<uint>(tris.Count * 3);
            var bmin = new Vector3(float.MaxValue);
            var bmax = new Vector3(float.MinValue);
            foreach (var (stored, a, b0, c0) in tris)
            {
                Vector3 b = b0, c = c0;
                Vector3 n = Vector3.Cross(b - a, c - a);
                float len = n.Length();
                if (len <= 0f || !float.IsFinite(len)) continue;
                n /= len;
                // The winding is the authority (CCW = front); a stored normal pointing the other
                // way means the file's winding is reversed, so flip the triangle to match it.
                if (Vector3.Dot(stored, n) < 0f) { (b, c) = (c, b); n = -n; }
                // No UVs, so any tangent perpendicular to n works; a fixed (1,0,0) would be parallel to
                // ±X facets and give the shaders a zero bitangent (NaN normal → black facet).
                var t = new Vector4(Vector3.Normalize(Vector3.Cross(MathF.Abs(n.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY, n)), 1f);

                foreach (var p in (ReadOnlySpan<Vector3>)[a, b, c])
                {
                    indices.Add((uint)verts.Count);
                    verts.Add(new ObjVertex { Position = p, Normal = n, Tangent = t });
                    bmin = Vector3.Min(bmin, p);
                    bmax = Vector3.Max(bmax, p);
                }
            }

            var mesh = new ObjMesh { SourcePath = sourcePath };
            if (verts.Count == 0) return mesh;
            mesh.SubMeshes = new[] { new ObjSubMesh { Vertices = verts.ToArray(), Indices = indices.ToArray() } };
            mesh.Bounds    = new Aabb(bmin, bmax);
            return mesh;
        }

        // ─── private ─────────────────────────────────────────────────────────────────

        // 80-byte header, uint32 count, then per triangle: normal + 3 vertices (12 floats) + uint16 attribute.
        private static void ReadBinary(ReadOnlySpan<byte> data, List<(Vector3, Vector3, Vector3, Vector3)> tris)
        {
            int count = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[80..]);
            tris.Capacity = count;
            for (int t = 0, o = 84; t < count; t++, o += 50)
                tris.Add((ReadVec3(data[o..]), ReadVec3(data[(o + 12)..]), ReadVec3(data[(o + 24)..]), ReadVec3(data[(o + 36)..])));
        }

        private static Vector3 ReadVec3(ReadOnlySpan<byte> s) => new(
            BinaryPrimitives.ReadSingleLittleEndian(s),
            BinaryPrimitives.ReadSingleLittleEndian(s[4..]),
            BinaryPrimitives.ReadSingleLittleEndian(s[8..]));

        // "facet normal nx ny nz / outer loop / vertex x y z ×3 / endloop / endfacet", possibly several solids.
        private static void ReadAscii(string text, List<(Vector3, Vector3, Vector3, Vector3)> tris)
        {
            var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            Vector3 normal = default;
            Span<Vector3> v = stackalloc Vector3[3];
            int nv = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                switch (tokens[i])
                {
                    case "facet":
                        nv = 0;
                        normal = i + 4 < tokens.Length && tokens[i + 1] == "normal" ? ParseVec3(tokens, i + 2) : default;
                        break;
                    case "vertex" when i + 3 < tokens.Length:
                        if (nv < 3) v[nv] = ParseVec3(tokens, i + 1);
                        if (++nv == 3) tris.Add((normal, v[0], v[1], v[2]));
                        i += 3;
                        break;
                }
            }
        }

        private static Vector3 ParseVec3(string[] t, int i) => new(ParseFloat(t[i]), ParseFloat(t[i + 1]), ParseFloat(t[i + 2]));

        private static float ParseFloat(string s)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : float.NaN;
    }
}
