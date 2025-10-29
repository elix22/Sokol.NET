using System.Numerics;
using System.Runtime.InteropServices;

namespace Sokol
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoord;
        public uint BoneID0;        // Bone IDs stored as unsigned integers (matching shader uvec4)
        public uint BoneID1;
        public uint BoneID2;
        public uint BoneID3;
        public Vector4 BoneWeights; // Corresponding weights

        public Vertex(Vector3 position, Vector3 normal, Vector2 texCoord)
        {
            Position = position;
            Normal = normal;
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
            BoneID0 = boneIds.Length > 0 ? (uint)boneIds[0] : 0;
            BoneID1 = boneIds.Length > 1 ? (uint)boneIds[1] : 0;
            BoneID2 = boneIds.Length > 2 ? (uint)boneIds[2] : 0;
            BoneID3 = boneIds.Length > 3 ? (uint)boneIds[3] : 0;
        }
    }
}
