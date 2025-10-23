using System;
using System.Collections.Generic;
using System.Numerics;
using Assimp;

namespace Sokol
{
    public struct AssimpNodeData
    {
        public Matrix4x4 Transformation;
        public string Name;
        public int ChildrenCount;
        public List<AssimpNodeData> Children;
    }

    public class Animation
    {
        private float m_Duration;
        private float m_TicksPerSecond;
        private List<Bone> m_Bones = new List<Bone>();
        private AssimpNodeData m_RootNode;
        private Dictionary<string, BoneInfo> m_BoneInfoMap = new Dictionary<string, BoneInfo>();

        public Animation() { }

        public Animation(string animationPath, Sokol.AnimatedModel model)
        {
            var importer = new AssimpContext();
            Scene? scene = importer.ImportFile(animationPath, PostProcessSteps.Triangulate);
            
            if (scene == null || scene.RootNode == null)
            {
                Console.WriteLine($"Failed to load animation from {animationPath}");
                return;
            }

            var animation = scene.Animations[0];
            m_Duration = (float)animation.DurationInTicks;
            m_TicksPerSecond = (float)animation.TicksPerSecond;
            
            // Assimp already uses Matrix4x4 from System.Numerics
            Matrix4x4 globalTransformation = scene.RootNode.Transform;
            Matrix4x4.Invert(globalTransformation, out globalTransformation);
            
            m_RootNode = new AssimpNodeData();
            ReadHierarchyData(ref m_RootNode, scene.RootNode, 0);
            ReadMissingBones(animation, model);
        }

        public Bone? FindBone(string name)
        {
            return m_Bones.Find(b => b.GetBoneName() == name);
        }

        public float GetTicksPerSecond() => m_TicksPerSecond;
        public float GetDuration() => m_Duration;
        public ref AssimpNodeData GetRootNode() => ref m_RootNode;
        public Dictionary<string, BoneInfo> GetBoneIDMap() => m_BoneInfoMap;

        private void ReadMissingBones(Assimp.Animation animation, Sokol.AnimatedModel model)
        {
            int size = animation.NodeAnimationChannelCount;
            var boneInfoMap = model.GetBoneInfoMap();
            int boneCount = model.GetBoneCount();

            // Reading channels (bones engaged in an animation and their keyframes)
            for (int i = 0; i < size; i++)
            {
                var channel = animation.NodeAnimationChannels[i];
                string boneName = channel.NodeName;

                if (!boneInfoMap.ContainsKey(boneName))
                {
                    boneInfoMap[boneName] = new BoneInfo { Id = boneCount };
                    boneCount++;
                }
                
                m_Bones.Add(new Bone(channel.NodeName, boneInfoMap[channel.NodeName].Id, channel));
            }

            model.SetBoneCount(boneCount);
            m_BoneInfoMap = boneInfoMap;
        }

        private int indentation = 0;
        
        private void ReadHierarchyData(ref AssimpNodeData dest, Node src, int depth)
        {
            if (src == null) return;

            indentation++;

            dest.Name = src.Name;
            dest.Transformation = AssimpHelpers.ToNumerics(src.Transform);  // Transpose to row-major
            dest.ChildrenCount = src.ChildCount;
            dest.Children = new List<AssimpNodeData>();

            Console.WriteLine($"{new string(' ', indentation * 2)}{dest.Name}");
            Console.WriteLine($"{new string(' ', indentation * 2)}{{");

            for (int i = 0; i < src.ChildCount; i++)
            {
                AssimpNodeData newData = new AssimpNodeData();
                ReadHierarchyData(ref newData, src.Children[i], depth + 1);
                dest.Children.Add(newData);
            }

            Console.WriteLine($"{new string(' ', indentation * 2)}}}");
            indentation--;
        }
    }
}
