using System.Numerics;
using System.Runtime.InteropServices;

namespace Sokol
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector4 Color;        // Vertex color (RGBA)
        public Vector2 TexCoord;
        public float BoneID0;        // Bone IDs stored as floats for WebGL compatibility
        public float BoneID1;
        public float BoneID2;
        public float BoneID3;
        public Vector4 BoneWeights; // Corresponding weights

        public Vertex(Vector3 position, Vector3 normal, Vector2 texCoord, Vector4 color = default)
        {
            Position = position;
            Normal = normal;
            Color = color == default ? Vector4.One : color; // Default to white if not specified
            TexCoord = texCoord;
            BoneID0 = 0;
            BoneID1 = 0;
            BoneID2 = 0;
            BoneID3 = 0;
            BoneWeights = Vector4.Zero;
        }

        // Helper to set bone IDs from array
        public void SetBoneIDs(int[] boneIds)
        {
            BoneID0 = boneIds.Length > 0 ? (float)boneIds[0] : 0;
            BoneID1 = boneIds.Length > 1 ? (float)boneIds[1] : 0;
            BoneID2 = boneIds.Length > 2 ? (float)boneIds[2] : 0;
            BoneID3 = boneIds.Length > 3 ? (float)boneIds[3] : 0;
        }
    }
}
