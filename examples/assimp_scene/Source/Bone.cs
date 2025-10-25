using System;
using System.Collections.Generic;
using System.Numerics;
using Assimp;
using static Sokol.SLog;
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

    public class Bone
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

        public Bone(string name, int id, NodeAnimationChannel channel)
        {
            m_Name = name;
            m_ID = id;
            m_LocalTransform = Matrix4x4.Identity;

            m_NumPositions = channel.PositionKeyCount;
            for (int positionIndex = 0; positionIndex < m_NumPositions; ++positionIndex)
            {
                var aiPosition = channel.PositionKeys[positionIndex];
                KeyPosition data = new KeyPosition
                {
                    Position = aiPosition.Value,  // Already Vector3
                    TimeStamp = (float)aiPosition.Time
                };
                m_Positions.Add(data);
            }

            m_NumRotations = channel.RotationKeyCount;
            for (int rotationIndex = 0; rotationIndex < m_NumRotations; ++rotationIndex)
            {
                var aiOrientation = channel.RotationKeys[rotationIndex];
                KeyRotation data = new KeyRotation
                {
                    // elix22 , Assimp nonsense  , doesn't make sense , I don't get it
                    Orientation = new Quaternion(aiOrientation.Value.Y, aiOrientation.Value.Z, aiOrientation.Value.W, aiOrientation.Value.X),
                    TimeStamp = (float)aiOrientation.Time
                };
                m_Rotations.Add(data);
            }

            m_NumScalings = channel.ScalingKeyCount;
            for (int keyIndex = 0; keyIndex < m_NumScalings; ++keyIndex)
            {
                var scale = channel.ScalingKeys[keyIndex];
                KeyScale data = new KeyScale
                {
                    Scale = scale.Value,  // Already Vector3
                    TimeStamp = (float)scale.Time
                };
                m_Scales.Add(data);
            }

            Info($"new Bone {name} ID:{id} Positions:{m_NumPositions} Rotations:{m_NumRotations} Scalings:{m_NumScalings}");
        }

        public void Update(float animationTime)
        {
            Matrix4x4 translation = InterpolatePosition(animationTime);
            Matrix4x4 rotation = InterpolateRotation(animationTime);
            Matrix4x4 scale = InterpolateScaling(animationTime);
            // m_LocalTransform = translation * rotation * scale;
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
            return m_NumPositions - 2;
        }

        private int GetRotationIndex(float animationTime)
        {
            for (int index = 0; index < m_NumRotations - 1; ++index)
            {
                if (animationTime < m_Rotations[index + 1].TimeStamp)
                    return index;
            }
            return m_NumRotations - 2;
        }

        private int GetScaleIndex(float animationTime)
        {
            for (int index = 0; index < m_NumScalings - 1; ++index)
            {
                if (animationTime < m_Scales[index + 1].TimeStamp)
                    return index;
            }
            return m_NumScalings - 2;
        }

        private float GetScaleFactor(float lastTimeStamp, float nextTimeStamp, float animationTime)
        {
            float midWayLength = animationTime - lastTimeStamp;
            float framesDiff = nextTimeStamp - lastTimeStamp;
            return midWayLength / framesDiff;
        }

        private Matrix4x4 InterpolatePosition(float animationTime)
        {
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
