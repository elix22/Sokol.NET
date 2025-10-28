using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sokol
{
    public struct KeyPosition
    {
        public Vector3 Position;
        public float TimeStamp;
    }

    public struct KeyRotation
    {
        public Quaternion Orientation;
        public float TimeStamp;
    }

    public struct KeyScale
    {
        public Vector3 Scale;
        public float TimeStamp;
    }

    /// <summary>
    /// Represents a single bone/joint with animation keyframes
    /// </summary>
    public class CGltfBone
    {
        private List<KeyPosition> m_Positions = new List<KeyPosition>();
        private List<KeyRotation> m_Rotations = new List<KeyRotation>();
        private List<KeyScale> m_Scales = new List<KeyScale>();
        private int m_NumPositions;
        private int m_NumRotations;
        private int m_NumScalings;

        private Matrix4x4 m_LocalTransform;
        private string m_Name;
        private int m_ID;

        public CGltfBone(string name, int id, List<KeyPosition> positions, List<KeyRotation> rotations, List<KeyScale> scales)
        {
            m_Name = name;
            m_ID = id;
            m_LocalTransform = Matrix4x4.Identity;

            m_Positions = positions;
            m_Rotations = rotations;
            m_Scales = scales;
            
            m_NumPositions = positions.Count;
            m_NumRotations = rotations.Count;
            m_NumScalings = scales.Count;

            Console.WriteLine($"CGltfBone: {name} ID:{id} Positions:{m_NumPositions} Rotations:{m_NumRotations} Scalings:{m_NumScalings}");
        }

        public void Update(float animationTime)
        {
            Matrix4x4 translation = InterpolatePosition(animationTime);
            Matrix4x4 rotation = InterpolateRotation(animationTime);
            Matrix4x4 scale = InterpolateScaling(animationTime);
            m_LocalTransform = scale * rotation * translation;
        }

        public Matrix4x4 GetLocalTransform() => m_LocalTransform;
        public string GetBoneName() => m_Name;
        public int GetBoneID() => m_ID;

        private int GetPositionIndex(float animationTime)
        {
            for (int index = 0; index < m_NumPositions - 1; ++index)
            {
                if (animationTime < m_Positions[index + 1].TimeStamp)
                    return index;
            }
            return Math.Max(0, m_NumPositions - 2);
        }

        private int GetRotationIndex(float animationTime)
        {
            for (int index = 0; index < m_NumRotations - 1; ++index)
            {
                if (animationTime < m_Rotations[index + 1].TimeStamp)
                    return index;
            }
            return Math.Max(0, m_NumRotations - 2);
        }

        private int GetScaleIndex(float animationTime)
        {
            for (int index = 0; index < m_NumScalings - 1; ++index)
            {
                if (animationTime < m_Scales[index + 1].TimeStamp)
                    return index;
            }
            return Math.Max(0, m_NumScalings - 2);
        }

        private float GetScaleFactor(float lastTimeStamp, float nextTimeStamp, float animationTime)
        {
            float midWayLength = animationTime - lastTimeStamp;
            float framesDiff = nextTimeStamp - lastTimeStamp;
            if (framesDiff < 0.0001f) return 0.0f;
            return midWayLength / framesDiff;
        }

        private Matrix4x4 InterpolatePosition(float animationTime)
        {
            if (m_NumPositions == 0)
                return Matrix4x4.Identity;
                
            if (m_NumPositions == 1)
                return Matrix4x4.CreateTranslation(m_Positions[0].Position);

            int p0Index = GetPositionIndex(animationTime);
            int p1Index = p0Index + 1;
            float scaleFactor = GetScaleFactor(m_Positions[p0Index].TimeStamp,
                m_Positions[p1Index].TimeStamp, animationTime);
            Vector3 finalPosition = Vector3.Lerp(m_Positions[p0Index].Position,
                m_Positions[p1Index].Position, scaleFactor);
            return Matrix4x4.CreateTranslation(finalPosition);
        }

        private Matrix4x4 InterpolateRotation(float animationTime)
        {
            if (m_NumRotations == 0)
                return Matrix4x4.Identity;
                
            if (m_NumRotations == 1)
            {
                var rotation = Quaternion.Normalize(m_Rotations[0].Orientation);
                return Matrix4x4.CreateFromQuaternion(rotation);
            }

            int p0Index = GetRotationIndex(animationTime);
            int p1Index = p0Index + 1;
            float scaleFactor = GetScaleFactor(m_Rotations[p0Index].TimeStamp,
                m_Rotations[p1Index].TimeStamp, animationTime);
            Quaternion finalRotation = Quaternion.Slerp(m_Rotations[p0Index].Orientation,
                m_Rotations[p1Index].Orientation, scaleFactor);
            finalRotation = Quaternion.Normalize(finalRotation);
            return Matrix4x4.CreateFromQuaternion(finalRotation);
        }

        private Matrix4x4 InterpolateScaling(float animationTime)
        {
            if (m_NumScalings == 0)
                return Matrix4x4.Identity;
                
            if (m_NumScalings == 1)
                return Matrix4x4.CreateScale(m_Scales[0].Scale);

            int p0Index = GetScaleIndex(animationTime);
            int p1Index = p0Index + 1;
            float scaleFactor = GetScaleFactor(m_Scales[p0Index].TimeStamp,
                m_Scales[p1Index].TimeStamp, animationTime);
            Vector3 finalScale = Vector3.Lerp(m_Scales[p0Index].Scale,
                m_Scales[p1Index].Scale, scaleFactor);
            return Matrix4x4.CreateScale(finalScale);
        }
    }
}
