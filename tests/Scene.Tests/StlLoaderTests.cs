using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using GameEditor.Framework.Renderer.Server.Resources;
using Xunit;

namespace Scene.Tests
{
    /// <summary>StlLoader: binary + ASCII parsing, outward facet normals, tangents, bounds.</summary>
    public sealed class StlLoaderTests
    {
        // Unit tetrahedron, every face CCW seen from outside.
        static readonly Vector3[] P = { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1) };
        static readonly int[][] Faces = { new[] { 0, 2, 1 }, new[] { 0, 1, 3 }, new[] { 0, 3, 2 }, new[] { 1, 2, 3 } };

        static byte[] Binary(string header, bool reverseWinding, bool zeroNormals)
        {
            var bytes = new byte[84 + Faces.Length * 50];
            Encoding.ASCII.GetBytes(header).CopyTo(bytes, 0);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80), (uint)Faces.Length);
            int o = 84;
            foreach (var f in Faces)
            {
                Vector3 a = P[f[0]], b = P[f[1]], c = P[f[2]];
                Vector3 n = zeroNormals ? Vector3.Zero : Vector3.Normalize(Vector3.Cross(b - a, c - a));
                if (reverseWinding) (b, c) = (c, b);
                foreach (var v in new[] { n, a, b, c })
                {
                    BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(o), v.X);
                    BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(o + 4), v.Y);
                    BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(o + 8), v.Z);
                    o += 12;
                }
                o += 2;
            }
            return bytes;
        }

        // Every triangle: 3 own vertices sharing the unit facet normal, which points away from the
        // tetrahedron's centroid and agrees with the CCW winding.
        static void AssertOutwardTetra(ObjMesh mesh)
        {
            var sm = Assert.Single(mesh.SubMeshes);
            Assert.Equal(12, sm.Vertices.Length);
            Assert.Equal(12, sm.Indices.Length);
            var centroid = new Vector3(0.25f);
            for (int t = 0; t < 4; t++)
            {
                ObjVertex a = sm.Vertices[sm.Indices[3 * t]], b = sm.Vertices[sm.Indices[3 * t + 1]], c = sm.Vertices[sm.Indices[3 * t + 2]];
                Vector3 n = a.Normal;
                Assert.Equal(n, b.Normal);
                Assert.Equal(n, c.Normal);
                Assert.Equal(1f, n.Length(), 5);
                var tan = new Vector3(a.Tangent.X, a.Tangent.Y, a.Tangent.Z);   // usable tangent frame (the −X face too)
                Assert.Equal(1f, tan.Length(), 5);
                Assert.Equal(0f, Vector3.Dot(tan, n), 5);
                Assert.True(Vector3.Dot(n, (a.Position + b.Position + c.Position) / 3f - centroid) > 0f);
                Assert.True(Vector3.Dot(n, Vector3.Cross(b.Position - a.Position, c.Position - a.Position)) > 0f);
            }
            Assert.Equal(Vector3.Zero, mesh.Bounds.Min);
            Assert.Equal(Vector3.One, mesh.Bounds.Max);
        }

        [Fact]
        public void Binary_OutwardNormals_EvenWhenHeaderStartsWithSolid()
        {
            AssertOutwardTetra(StlLoader.Load(Binary("binary", false, false)));
            AssertOutwardTetra(StlLoader.Load(Binary("solid exported by CAD", false, false)));
        }

        [Fact]
        public void Binary_ZeroStoredNormals_UseWinding()
            => AssertOutwardTetra(StlLoader.Load(Binary("", false, true)));

        [Fact]
        public void Binary_ReversedWinding_FlippedToMatchStoredNormal()
            => AssertOutwardTetra(StlLoader.Load(Binary("", true, false)));

        [Fact]
        public void Ascii_TwoSolids_ExponentFloats_DegenerateFacetDropped()
        {
            var sb = new StringBuilder();
            void Facet(Vector3 n, Vector3 a, Vector3 b, Vector3 c)
            {
                sb.Append($"  facet normal {n.X:E} {n.Y:E} {n.Z:E}\n    outer loop\n");
                foreach (var v in new[] { a, b, c }) sb.Append($"      vertex {v.X:E} {v.Y:E} {v.Z:E}\n");
                sb.Append("    endloop\n  endfacet\n");
            }
            for (int s = 0; s < 2; s++)
            {
                sb.Append($"solid part{s}\n");
                for (int f = 2 * s; f < 2 * s + 2; f++)
                {
                    Vector3 a = P[Faces[f][0]], b = P[Faces[f][1]], c = P[Faces[f][2]];
                    Facet(Vector3.Normalize(Vector3.Cross(b - a, c - a)), a, b, c);
                }
                if (s == 0) Facet(Vector3.UnitZ, P[1], P[1], P[2]);   // zero area
                sb.Append($"endsolid part{s}\n");
            }
            AssertOutwardTetra(StlLoader.Load(Encoding.ASCII.GetBytes(sb.ToString()), "x.stl"));
        }

        [Fact]
        public void Garbage_GivesEmptyMesh()
        {
            Assert.Empty(StlLoader.Load(new byte[10]).SubMeshes);
            Assert.Empty(StlLoader.Load(new byte[200]).SubMeshes);   // count 0 but size ≠ 84
            Assert.Empty(StlLoader.Load(Encoding.ASCII.GetBytes("solid empty\nendsolid empty\n")).SubMeshes);
        }
    }
}
