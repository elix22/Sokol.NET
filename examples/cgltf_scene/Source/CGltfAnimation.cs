using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sokol
{
    /// <summary>
    /// CGLTF Animation - stores animation data parsed from GLTF
    /// </summary>
    public class CGltfAnimation
    {
        private float m_Duration;
        private float m_TicksPerSecond;
        private List<CGltfBone> m_Bones = new List<CGltfBone>();
        private CGltfNodeData m_RootNode;
        private Dictionary<string, BoneInfo> m_BoneInfoMap = new Dictionary<string, BoneInfo>();

        public string Name { get; private set; } = "";
        public bool IsLoaded { get; private set; } = false;

        public CGltfAnimation()
        {
        }

        public void Initialize(string name, float duration, float ticksPerSecond, 
                              CGltfNodeData rootNode, List<CGltfBone> bones,
                              Dictionary<string, BoneInfo> boneInfoMap)
        {
            Name = name;
            m_Duration = duration;
            m_TicksPerSecond = ticksPerSecond;
            m_RootNode = rootNode;
            m_Bones = bones;
            m_BoneInfoMap = boneInfoMap;
            IsLoaded = true;
            
            Console.WriteLine($"CGltfAnimation: Initialized '{name}' Duration:{duration} TPS:{ticksPerSecond} Bones:{bones.Count}");
        }

        public CGltfBone? FindBone(string name)
        {
            return m_Bones.Find(b => b.GetBoneName() == name);
        }

        public float GetTicksPerSecond() => m_TicksPerSecond;
        public float GetDuration() => m_Duration;
        public ref CGltfNodeData GetRootNode() => ref m_RootNode;
        public Dictionary<string, BoneInfo> GetBoneIDMap() => m_BoneInfoMap;
    }
}
