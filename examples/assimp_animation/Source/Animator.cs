using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sokol
{
    public class Animator
    {
        private Matrix4x4[] m_FinalBoneMatrices = new Matrix4x4[AnimationConstants.MAX_BONES];
        private Animation? m_CurrentAnimation;
        private float m_CurrentTime;
        private float m_DeltaTime;

        public Animator(Animation? animation)
        {
            m_CurrentTime = 0.0f;
            m_CurrentAnimation = animation;

            // Initialize bone matrices to identity
            Array.Fill(m_FinalBoneMatrices, Matrix4x4.Identity);
        }

        public void SetAnimation(Animation? animation)
        {
            m_CurrentAnimation = animation;
            m_CurrentTime = 0.0f;
            
            // Reset bone matrices to identity
            Array.Fill(m_FinalBoneMatrices, Matrix4x4.Identity);
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

        public Matrix4x4[] GetFinalBoneMatrices()
        {
            return m_FinalBoneMatrices;
        }

        public float GetCurrentTime()
        {
            return m_CurrentTime;
        }

        public Animation? GetCurrentAnimation()
        {
            return m_CurrentAnimation;
        }
    }
}
