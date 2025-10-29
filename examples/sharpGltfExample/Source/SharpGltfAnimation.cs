using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sokol
{
    public struct SharpGltfNodeData
    {
        public Matrix4x4 Transformation;
        public string Name;
        public int ChildrenCount;
        public List<SharpGltfNodeData> Children;
    }

    public class SharpGltfAnimation
    {
        private float _duration;
        private int _ticksPerSecond;
        private List<SharpGltfBone> _bones = new List<SharpGltfBone>();
        private SharpGltfNodeData _rootNode;
        private Dictionary<string, BoneInfo> _boneInfoMap;

        public SharpGltfAnimation(float duration, int ticksPerSecond, SharpGltfNodeData rootNode,
            Dictionary<string, BoneInfo> boneInfoMap)
        {
            _duration = duration;
            _ticksPerSecond = ticksPerSecond;
            _rootNode = rootNode;
            _boneInfoMap = boneInfoMap;
        }

        public void AddBone(SharpGltfBone bone)
        {
            _bones.Add(bone);
        }

        public SharpGltfBone? FindBone(string name)
        {
            return _bones.Find(b => b.Name == name);
        }

        public float GetTicksPerSecond() => _ticksPerSecond;
        public float GetDuration() => _duration;
        public ref SharpGltfNodeData GetRootNode() => ref _rootNode;
        public Dictionary<string, BoneInfo> GetBoneIDMap() => _boneInfoMap;
        public List<SharpGltfBone> GetBones() => _bones;
    }
}
