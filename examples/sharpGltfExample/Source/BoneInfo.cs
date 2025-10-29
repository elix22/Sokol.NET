using System.Numerics;

namespace Sokol
{
    public struct BoneInfo
    {
        // id is index in finalBoneMatrices
        public int Id;

        // offset matrix transforms vertex from model space to bone space
        public Matrix4x4 Offset;
    }
}
