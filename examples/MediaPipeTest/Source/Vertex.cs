using System.Numerics;
using System.Runtime.InteropServices;

namespace Sokol
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector3 Position;     // location=0
        public Vector3 Normal;       // location=1
        public Vector4 Tangent;      // location=2 (w component is handedness)
        public Vector2 TexCoord0;    // location=3 (renamed from TexCoord)
        public Vector2 TexCoord1;    // location=4
        public Vector4 Color;        // location=5 (Vertex color RGBA)
        public Vector4 Joints;       // location=6 (bone indices as vec4)
        public Vector4 BoneWeights;  // location=7 (bone weights)

        public Vertex(Vector3 position, Vector3 normal, Vector2 texCoord, Vector4 color = default, Vector4 tangent = default)
        {
            Position = position;
            Normal = normal;
            Tangent = tangent == default ? new Vector4(1, 0, 0, 1) : tangent; // Default tangent
            TexCoord0 = texCoord;
            TexCoord1 = Vector2.Zero;  // Default to zero
            Color = color == default ? Vector4.One : color; // Default to white if not specified
            Joints = Vector4.Zero;
            BoneWeights = Vector4.Zero;
        }

        // Helper to set bone IDs from array
        public void SetBoneIDs(int[] boneIds)
        {
            Joints = new Vector4(
                boneIds.Length > 0 ? (float)boneIds[0] : 0,
                boneIds.Length > 1 ? (float)boneIds[1] : 0,
                boneIds.Length > 2 ? (float)boneIds[2] : 0,
                boneIds.Length > 3 ? (float)boneIds[3] : 0
            );
        }
    }
}
