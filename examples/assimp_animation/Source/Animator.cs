using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sokol
{
    public class Animator
    {
        private List<Matrix4x4> m_FinalBoneMatrices = new List<Matrix4x4>();
        private Animation? m_CurrentAnimation;
        private float m_CurrentTime;
        private float m_DeltaTime;

        public Animator(Animation? animation)
        {
            m_CurrentTime = 0.0f;
            m_CurrentAnimation = animation;

            // Reserve space for 100 bone matrices
            for (int i = 0; i < 100; i++)
                m_FinalBoneMatrices.Add(Matrix4x4.Identity);
        }

        public void UpdateAnimation(float dt)
        {
            m_DeltaTime = dt;
            if (m_CurrentAnimation != null)
            {
                m_CurrentTime += m_CurrentAnimation.GetTicksPerSecond() * dt;
                m_CurrentTime = m_CurrentTime % m_CurrentAnimation.GetDuration();
                CalculateBoneTransform(m_CurrentAnimation.GetRootNode(), Matrix4x4.Identity);
            }
        }

        public void PlayAnimation(Animation animation)
        {
            m_CurrentAnimation = animation;
            m_CurrentTime = 0.0f;
        }

        private void CalculateBoneTransform(AssimpNodeData node, Matrix4x4 parentTransform)
        {
            string nodeName = node.Name;
            Matrix4x4 nodeTransform = node.Transformation;

            Bone? bone = m_CurrentAnimation?.FindBone(nodeName);

            if (bone != null)
            {
                bone.Update(m_CurrentTime);
                nodeTransform = bone.GetLocalTransform();
            }

            Matrix4x4 globalTransformation = nodeTransform * parentTransform;

            var boneInfoMap = m_CurrentAnimation?.GetBoneIDMap();
            if (boneInfoMap != null && boneInfoMap.ContainsKey(nodeName))
            {
                int index = boneInfoMap[nodeName].Id;
                Matrix4x4 offset = boneInfoMap[nodeName].Offset;
                
                // Try reversed multiplication order
                m_FinalBoneMatrices[index] = offset * globalTransformation;
            }

            for (int i = 0; i < node.ChildrenCount; i++)
                CalculateBoneTransform(node.Children[i], globalTransformation);
        }

        public List<Matrix4x4> GetFinalBoneMatrices()
        {
            return m_FinalBoneMatrices;
        }
    }
}
