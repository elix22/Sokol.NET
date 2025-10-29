using System;
using System.Collections.Generic;
using System.Numerics;
using SharpGLTF.Schema2;

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

    public class SharpGltfBone
    {
        private List<KeyPosition> _positions = new List<KeyPosition>();
        private List<KeyRotation> _rotations = new List<KeyRotation>();
        private List<KeyScale> _scales = new List<KeyScale>();

        private int _numPositions;
        private int _numRotations;
        private int _numScalings;

        public Matrix4x4 LocalTransform { get; private set; }
        public string Name { get; private set; }
        public int ID { get; private set; }

        public SharpGltfBone(string name, int id, Node node)
        {
            Name = name;
            ID = id;
            LocalTransform = Matrix4x4.Identity;
        }

        public void AddPositionKey(float time, Vector3 position)
        {
            _positions.Add(new KeyPosition { Position = position, TimeStamp = time });
            _numPositions++;
        }

        public void AddRotationKey(float time, Quaternion rotation)
        {
            _rotations.Add(new KeyRotation { Orientation = rotation, TimeStamp = time });
            _numRotations++;
        }

        public void AddScaleKey(float time, Vector3 scale)
        {
            _scales.Add(new KeyScale { Scale = scale, TimeStamp = time });
            _numScalings++;
        }

        public void Update(float animationTime)
        {
            Matrix4x4 translation = InterpolatePosition(animationTime);
            Matrix4x4 rotation = InterpolateRotation(animationTime);
            Matrix4x4 scale = InterpolateScaling(animationTime);
            LocalTransform = scale * rotation * translation;
        }

        private int GetPositionIndex(float animationTime)
        {
            for (int index = 0; index < _numPositions - 1; ++index)
            {
                if (animationTime < _positions[index + 1].TimeStamp)
                    return index;
            }
            return 0;
        }

        private int GetRotationIndex(float animationTime)
        {
            for (int index = 0; index < _numRotations - 1; ++index)
            {
                if (animationTime < _rotations[index + 1].TimeStamp)
                    return index;
            }
            return 0;
        }

        private int GetScaleIndex(float animationTime)
        {
            for (int index = 0; index < _numScalings - 1; ++index)
            {
                if (animationTime < _scales[index + 1].TimeStamp)
                    return index;
            }
            return 0;
        }

        private float GetScaleFactor(float lastTimeStamp, float nextTimeStamp, float animationTime)
        {
            float scaleFactor = 0.0f;
            float midWayLength = animationTime - lastTimeStamp;
            float framesDiff = nextTimeStamp - lastTimeStamp;
            scaleFactor = midWayLength / framesDiff;
            return scaleFactor;
        }

        private Matrix4x4 InterpolatePosition(float animationTime)
        {
            if (_numPositions == 1)
                return Matrix4x4.CreateTranslation(_positions[0].Position);

            int p0Index = GetPositionIndex(animationTime);
            int p1Index = p0Index + 1;
            float scaleFactor = GetScaleFactor(_positions[p0Index].TimeStamp,
                _positions[p1Index].TimeStamp, animationTime);
            Vector3 finalPosition = Vector3.Lerp(_positions[p0Index].Position,
                _positions[p1Index].Position, scaleFactor);
            return Matrix4x4.CreateTranslation(finalPosition);
        }

        private Matrix4x4 InterpolateRotation(float animationTime)
        {
            if (_numRotations == 1)
            {
                var rotation = Quaternion.Normalize(_rotations[0].Orientation);
                return Matrix4x4.CreateFromQuaternion(rotation);
            }

            int p0Index = GetRotationIndex(animationTime);
            int p1Index = p0Index + 1;
            float scaleFactor = GetScaleFactor(_rotations[p0Index].TimeStamp,
                _rotations[p1Index].TimeStamp, animationTime);
            Quaternion finalRotation = Quaternion.Slerp(_rotations[p0Index].Orientation,
                _rotations[p1Index].Orientation, scaleFactor);
            finalRotation = Quaternion.Normalize(finalRotation);
            return Matrix4x4.CreateFromQuaternion(finalRotation);
        }

        private Matrix4x4 InterpolateScaling(float animationTime)
        {
            if (_numScalings == 1)
                return Matrix4x4.CreateScale(_scales[0].Scale);

            int p0Index = GetScaleIndex(animationTime);
            int p1Index = p0Index + 1;
            float scaleFactor = GetScaleFactor(_scales[p0Index].TimeStamp,
                _scales[p1Index].TimeStamp, animationTime);
            Vector3 finalScale = Vector3.Lerp(_scales[p0Index].Scale,
                _scales[p1Index].Scale, scaleFactor);
            return Matrix4x4.CreateScale(finalScale);
        }

        public Matrix4x4 GetLocalTransform() => LocalTransform;
    }
}
