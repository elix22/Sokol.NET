using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sokol
{
    /// <summary>
    /// Animator for CGLTF animations - calculates final bone matrices for skinning
    /// </summary>
    public class CGltfAnimator
    {
        private Matrix4x4[] m_FinalBoneMatrices = new Matrix4x4[AnimationConstants.MAX_BONES];
        private CGltfAnimation? m_CurrentAnimation;
        private float m_CurrentTime;
        private float m_DeltaTime;

        public CGltfAnimator(CGltfAnimation? animation)
        {
            m_CurrentTime = 0.0f;
            m_CurrentAnimation = animation;

            // Initialize bone matrices to identity
            Array.Fill(m_FinalBoneMatrices, Matrix4x4.Identity);
        }

        public void SetAnimation(CGltfAnimation? animation)
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
                
                // Log only once
                if (!s_LoggedOnce)
                {
                    Console.WriteLine($"[CGLTF] UpdateAnimation: time={m_CurrentTime:F4}, dt={dt:F4}");
                    
                    // Log bone info map contents
                    var boneInfoMap = m_CurrentAnimation.GetBoneIDMap();
                    Console.WriteLine($"[CGLTF] BoneInfoMap has {boneInfoMap.Count} entries");
                    int count = 0;
                    foreach (var kvp in boneInfoMap)
                    {
                        Console.WriteLine($"[CGLTF]   Bone {kvp.Value.Id}: {kvp.Key}");
                        if (++count >= 5) break; // Just show first 5
                    }
                }
                
                ref CGltfNodeData rootNode = ref m_CurrentAnimation.GetRootNode();
                CalculateBoneTransform(rootNode, Matrix4x4.Identity);
            }
        }

        public void PlayAnimation(CGltfAnimation animation)
        {
            m_CurrentAnimation = animation;
            m_CurrentTime = 0.0f;
        }

        private static bool s_LoggedOnce = false;

        private void CalculateBoneTransform(CGltfNodeData node, Matrix4x4 parentTransform)
        {
            string nodeName = node.Name;
            Matrix4x4 nodeTransform = node.Transformation;

            if (!s_LoggedOnce)
            {
                Console.WriteLine($"[CGLTF] Visiting node: '{nodeName}'");
            }

            CGltfBone? bone = m_CurrentAnimation?.FindBone(nodeName);

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
                
                // Match assimp: offset * globalTransformation
                m_FinalBoneMatrices[index] = offset * globalTransformation;
                
                // Log specific bones for comparison with assimp
                if (!s_LoggedOnce && (nodeName == "mixamorig:Hips" || nodeName == "mixamorig:Spine" || 
                    nodeName == "mixamorig:LeftArm" || nodeName == "mixamorig:RightLeg"))
                {
                    Console.WriteLine($"[CGLTF MATRIX] Bone: {nodeName} (index={index})");
                    Console.WriteLine($"[CGLTF MATRIX]   Offset M11-M44:");
                    Console.WriteLine($"[CGLTF MATRIX]     {offset.M11:F6}, {offset.M12:F6}, {offset.M13:F6}, {offset.M14:F6}");
                    Console.WriteLine($"[CGLTF MATRIX]     {offset.M21:F6}, {offset.M22:F6}, {offset.M23:F6}, {offset.M24:F6}");
                    Console.WriteLine($"[CGLTF MATRIX]     {offset.M31:F6}, {offset.M32:F6}, {offset.M33:F6}, {offset.M34:F6}");
                    Console.WriteLine($"[CGLTF MATRIX]     {offset.M41:F6}, {offset.M42:F6}, {offset.M43:F6}, {offset.M44:F6}");
                    Console.WriteLine($"[CGLTF MATRIX]   GlobalTransform M11-M44:");
                    Console.WriteLine($"[CGLTF MATRIX]     {globalTransformation.M11:F6}, {globalTransformation.M12:F6}, {globalTransformation.M13:F6}, {globalTransformation.M14:F6}");
                    Console.WriteLine($"[CGLTF MATRIX]     {globalTransformation.M21:F6}, {globalTransformation.M22:F6}, {globalTransformation.M23:F6}, {globalTransformation.M24:F6}");
                    Console.WriteLine($"[CGLTF MATRIX]     {globalTransformation.M31:F6}, {globalTransformation.M32:F6}, {globalTransformation.M33:F6}, {globalTransformation.M34:F6}");
                    Console.WriteLine($"[CGLTF MATRIX]     {globalTransformation.M41:F6}, {globalTransformation.M42:F6}, {globalTransformation.M43:F6}, {globalTransformation.M44:F6}");
                    Console.WriteLine($"[CGLTF MATRIX]   Final (offset * global) M11-M44:");
                    Console.WriteLine($"[CGLTF MATRIX]     {m_FinalBoneMatrices[index].M11:F6}, {m_FinalBoneMatrices[index].M12:F6}, {m_FinalBoneMatrices[index].M13:F6}, {m_FinalBoneMatrices[index].M14:F6}");
                    Console.WriteLine($"[CGLTF MATRIX]     {m_FinalBoneMatrices[index].M21:F6}, {m_FinalBoneMatrices[index].M22:F6}, {m_FinalBoneMatrices[index].M23:F6}, {m_FinalBoneMatrices[index].M24:F6}");
                    Console.WriteLine($"[CGLTF MATRIX]     {m_FinalBoneMatrices[index].M31:F6}, {m_FinalBoneMatrices[index].M32:F6}, {m_FinalBoneMatrices[index].M33:F6}, {m_FinalBoneMatrices[index].M34:F6}");
                    Console.WriteLine($"[CGLTF MATRIX]     {m_FinalBoneMatrices[index].M41:F6}, {m_FinalBoneMatrices[index].M42:F6}, {m_FinalBoneMatrices[index].M43:F6}, {m_FinalBoneMatrices[index].M44:F6}");
                    Console.WriteLine();
                }
                // Log first 5 bones on first frame
                if (!s_LoggedOnce && index < 5)
                {
                    Console.WriteLine($"[CGLTF] Bone {index} ({nodeName}):");
                    Console.WriteLine($"  Offset:\n{MatrixToString(offset)}");
                    Console.WriteLine($"  GlobalTransform:\n{MatrixToString(globalTransformation)}");
                    Console.WriteLine($"  Final (offset * global):\n{MatrixToString(m_FinalBoneMatrices[index])}");
                    if (index == 4) s_LoggedOnce = true;
                }
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

        public CGltfAnimation? GetCurrentAnimation()
        {
            return m_CurrentAnimation;
        }
        
        private static string MatrixToString(Matrix4x4 m)
        {
            return $"  [{m.M11:F4}, {m.M12:F4}, {m.M13:F4}, {m.M14:F4}]\n" +
                   $"  [{m.M21:F4}, {m.M22:F4}, {m.M23:F4}, {m.M24:F4}]\n" +
                   $"  [{m.M31:F4}, {m.M32:F4}, {m.M33:F4}, {m.M34:F4}]\n" +
                   $"  [{m.M41:F4}, {m.M42:F4}, {m.M43:F4}, {m.M44:F4}]";
        }
    }
}
