using System;
using System.Collections.Generic;
using System.Numerics;
using Assimp;
using static Sokol.SLog;

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

        public string FilePath { get; private set; } = "";
        public bool IsLoaded { get; private set; } = false;

        public Animation() { }

        public Animation(string animationPath, Sokol.Model model)
        {
            FilePath = animationPath;
            FileSystem.Instance.LoadFile(animationPath, (path, buffer, status) => OnFileLoaded(path, buffer, status, model));
        }

        void OnFileLoaded(string filePath, byte[]? buffer, FileLoadStatus status, Sokol.Model model)
        {
            if (status == FileLoadStatus.Success && buffer != null)
            {
                Info($"Animation: File '{filePath}' loaded successfully, size: {buffer.Length} bytes");

                var stream = new System.IO.MemoryStream(buffer);
                PostProcessSteps ppSteps = PostProcessSteps.Triangulate;

                AssimpContext importer = new AssimpContext();
                string formatHint = System.IO.Path.GetExtension(filePath).TrimStart('.');
                if (string.IsNullOrEmpty(formatHint))
                {
                    formatHint = "gltf2";
                }

                Assimp.Scene? scene = importer.ImportFileFromStream(stream, ppSteps, formatHint);

                if (scene == null || scene.RootNode == null || scene.AnimationCount == 0)
                {
                    Info($"Animation: Failed to load animation from {filePath}");
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

                IsLoaded = true;
                Info($"Animation: Successfully loaded animation from {filePath}");
            }
            else
            {
                Info($"Animation: Failed to load file: {status}");
            }
        }

        public void SetAsssimpAnimation(Assimp.Scene? scene, Assimp.Animation animation, Sokol.Model model)
        {
                
            m_Duration = (float)animation.DurationInTicks;
            m_TicksPerSecond = (float)animation.TicksPerSecond;

            m_RootNode = new AssimpNodeData();
            ReadHierarchyData(ref m_RootNode, scene.RootNode, 0);
            ReadMissingBones(animation, model);
            IsLoaded = true;
        }

        public Bone? FindBone(string name)
        {
            return m_Bones.Find(b => b.GetBoneName() == name);
        }

        public float GetTicksPerSecond() => m_TicksPerSecond;
        public float GetDuration() => m_Duration;
        public ref AssimpNodeData GetRootNode() => ref m_RootNode;
        public Dictionary<string, BoneInfo> GetBoneIDMap() => m_BoneInfoMap;

        private void ReadMissingBones(Assimp.Animation animation, Sokol.Model model)
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
        
        private void ReadHierarchyData(ref AssimpNodeData dest, Assimp.Node src, int depth)
        {
            if (src == null) return;

            indentation++;

            dest.Name = src.Name;
            dest.Transformation = AssimpHelpers.ToNumerics(src.Transform);  // Transpose to row-major
            dest.ChildrenCount = src.ChildCount;
            dest.Children = new List<AssimpNodeData>();

            Info($"{new string(' ', indentation * 2)}{dest.Name}");
            Info($"{new string(' ', indentation * 2)}{{");

            for (int i = 0; i < src.ChildCount; i++)
            {
                AssimpNodeData newData = new AssimpNodeData();
                ReadHierarchyData(ref newData, src.Children[i], depth + 1);
                dest.Children.Add(newData);
            }

            Info($"{new string(' ', indentation * 2)}}}");
            indentation--;
        }
    }
}
