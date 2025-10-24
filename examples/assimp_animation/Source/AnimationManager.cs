using System;
using System.Collections.Generic;
using System.Numerics;
using Assimp;
namespace Sokol
{
    public class AnimationManager
    {
        private Dictionary<string, Animation> m_Animations = new Dictionary<string, Animation>();
        private List<string> m_AnimationNames = new List<string>();
        private int m_CurrentAnimationIndex = 0;

        public Animation? GetAnimation(string animationName)
        {
            if (m_Animations.ContainsKey(animationName))
            {
                return m_Animations[animationName];
            }
            return null;
        }

        public int GetAnimationCount()
        {
            return m_Animations.Count;
        }

        public Animation? GetAnimation(int index)
        {
            if (index < 0 || index >= m_Animations.Count)
                return null;

            int currentIndex = 0;
            foreach (var anim in m_Animations.Values)
            {
                if (currentIndex == index)
                    return anim;
                currentIndex++;
            }
            return null;
        }

        public Animation? GetFirstAnimation()
        {
            if (m_AnimationNames.Count > 0)
            {
                m_CurrentAnimationIndex = 0;
                return m_Animations[m_AnimationNames[0]];
            }
            return null;
        }

        public Animation? GetCurrentAnimation()
        {
            if (m_CurrentAnimationIndex >= 0 && m_CurrentAnimationIndex < m_AnimationNames.Count)
            {
                return m_Animations[m_AnimationNames[m_CurrentAnimationIndex]];
            }
            return null;
        }

        public Animation? GetNextAnimation()
        {
            if (m_AnimationNames.Count == 0)
                return null;

            m_CurrentAnimationIndex = (m_CurrentAnimationIndex + 1) % m_AnimationNames.Count;
            return m_Animations[m_AnimationNames[m_CurrentAnimationIndex]];
        }

        public Animation? GetPreviousAnimation()
        {
            if (m_AnimationNames.Count == 0)
                return null;

            m_CurrentAnimationIndex = (m_CurrentAnimationIndex - 1 + m_AnimationNames.Count) % m_AnimationNames.Count;
            return m_Animations[m_AnimationNames[m_CurrentAnimationIndex]];
        }

        public string? GetCurrentAnimationName()
        {
            if (m_CurrentAnimationIndex >= 0 && m_CurrentAnimationIndex < m_AnimationNames.Count)
            {
                return m_AnimationNames[m_CurrentAnimationIndex];
            }
            return null;
        }

        public int GetCurrentAnimationIndex()
        {
            return m_CurrentAnimationIndex;
        }

        public string? GetAnimationName(int index)
        {
            if (index >= 0 && index < m_AnimationNames.Count)
            {
                return m_AnimationNames[index];
            }
            return null;
        }

        public Animation? GetAnimationByName(string animationName)
        {
            if (m_Animations.ContainsKey(animationName))
            {
                return m_Animations[animationName];
            }
            return null;
        }

        public int GetAnimationIndex(string animationName)
        {
            return m_AnimationNames.IndexOf(animationName);
        }

        public List<string> GetAllAnimationNames()
        {
            return new List<string>(m_AnimationNames);
        }


        public void LoadAnimation(string filePath, Model model)
        {
            FileSystem.Instance.LoadFile(filePath, (path, buffer, status) => OnFileLoaded(path, buffer, status, model));
        }
        void OnFileLoaded(string filePath, byte[]? buffer, FileLoadStatus status, Sokol.Model model)
        {
            if (status == FileLoadStatus.Success && buffer != null)
            {
                Console.WriteLine($"Animation: File '{filePath}' loaded successfully, size: {buffer.Length} bytes");

                var stream = new System.IO.MemoryStream(buffer);
                PostProcessSteps ppSteps = PostProcessSteps.Triangulate;

                AssimpContext importer = new AssimpContext();
                string formatHint = System.IO.Path.GetExtension(filePath).TrimStart('.');
                if (string.IsNullOrEmpty(formatHint))
                {
                    formatHint = "gltf2";
                }

                Scene? scene = importer.ImportFileFromStream(stream, ppSteps, formatHint);

                if (scene == null || scene.RootNode == null || scene.AnimationCount == 0)
                {
                    Console.WriteLine($"Animation: Failed to load animation from {filePath}");
                    return;
                }

                foreach (var anim in scene.Animations)
                {
                    Sokol.Animation animation = new Sokol.Animation();
                    animation.SetAsssimpAnimation(scene, anim, model);
                    m_Animations[anim.Name] = animation;
                    m_AnimationNames.Add(anim.Name);
                    Console.WriteLine($"Animation: Loaded animation '{anim.Name}' from {filePath}");
                }
          
          
            }
            else
            {
                Console.WriteLine($"Animation: Failed to load file '{filePath}'");
            }
           
        }
    }

}