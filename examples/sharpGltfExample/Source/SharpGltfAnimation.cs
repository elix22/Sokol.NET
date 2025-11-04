using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sokol
{
    public struct SharpGltfNodeData
    {
        public Matrix4x4 Transformation;
        public string Name;
        public int ChildrenCount;
        public List<SharpGltfNodeData> Children;
    }

    /// <summary>
    /// Defines the type of material property being animated via KHR_animation_pointer
    /// </summary>
    public enum MaterialAnimationTarget
    {
        NormalTextureRotation,
        NormalTextureOffset,
        NormalTextureScale,
        ThicknessTextureRotation,
        ThicknessTextureOffset,
        ThicknessTextureScale,
        // Future: Add other animatable properties (emissive, IOR, etc.)
    }

    /// <summary>
    /// Holds animation data for a single material property (KHR_animation_pointer)
    /// </summary>
    public class MaterialPropertyAnimation
    {
        public int MaterialIndex { get; set; }
        public MaterialAnimationTarget Target { get; set; }
        public string PropertyPath { get; set; } = "";
        
        // Keyframe data (time -> value mapping)
        // For floats (rotation): List<(float time, float value)>
        // For Vector2 (offset/scale): List<(float time, Vector2 value)>
        public List<(float time, float value)> FloatKeyframes { get; set; } = new();
        public List<(float time, Vector2 value)> Vector2Keyframes { get; set; } = new();
        
        public bool IsFloatType => Target == MaterialAnimationTarget.NormalTextureRotation || 
                                    Target == MaterialAnimationTarget.ThicknessTextureRotation;

        /// <summary>
        /// Sample animation value at given time using linear interpolation
        /// </summary>
        public float SampleFloatAtTime(float time)
        {
            if (FloatKeyframes.Count == 0) return 0f;
            if (FloatKeyframes.Count == 1) return FloatKeyframes[0].value;

            // Find surrounding keyframes
            for (int i = 0; i < FloatKeyframes.Count - 1; i++)
            {
                if (time >= FloatKeyframes[i].time && time <= FloatKeyframes[i + 1].time)
                {
                    float t = (time - FloatKeyframes[i].time) / 
                             (FloatKeyframes[i + 1].time - FloatKeyframes[i].time);
                    return FloatKeyframes[i].value + t * (FloatKeyframes[i + 1].value - FloatKeyframes[i].value);
                }
            }

            // Before first or after last keyframe
            return time < FloatKeyframes[0].time ? FloatKeyframes[0].value : FloatKeyframes[^1].value;
        }

        /// <summary>
        /// Sample animation value at given time using linear interpolation
        /// </summary>
        public Vector2 SampleVector2AtTime(float time)
        {
            if (Vector2Keyframes.Count == 0) return Vector2.Zero;
            if (Vector2Keyframes.Count == 1) return Vector2Keyframes[0].value;

            // Find surrounding keyframes
            for (int i = 0; i < Vector2Keyframes.Count - 1; i++)
            {
                if (time >= Vector2Keyframes[i].time && time <= Vector2Keyframes[i + 1].time)
                {
                    float t = (time - Vector2Keyframes[i].time) / 
                             (Vector2Keyframes[i + 1].time - Vector2Keyframes[i].time);
                    return Vector2.Lerp(Vector2Keyframes[i].value, Vector2Keyframes[i + 1].value, t);
                }
            }

            // Before first or after last keyframe
            return time < Vector2Keyframes[0].time ? Vector2Keyframes[0].value : Vector2Keyframes[^1].value;
        }
    }

    public class SharpGltfAnimation
    {
        public string Name { get; set; } = "";
        private float _duration;
        private int _ticksPerSecond;
        private List<SharpGltfBone> _bones = new List<SharpGltfBone>();
        private SharpGltfNodeData _rootNode;
        private Dictionary<string, BoneInfo> _boneInfoMap;
        
        // KHR_animation_pointer support: material property animations
        public List<MaterialPropertyAnimation> MaterialAnimations { get; set; } = new();

        public SharpGltfAnimation(float duration, int ticksPerSecond, SharpGltfNodeData rootNode,
            Dictionary<string, BoneInfo> boneInfoMap)
        {
            _duration = duration;
            _ticksPerSecond = ticksPerSecond;
            _rootNode = rootNode;
            _boneInfoMap = boneInfoMap;
        }

        public void AddBone(SharpGltfBone bone)
        {
            _bones.Add(bone);
        }

        public SharpGltfBone? FindBone(string name)
        {
            return _bones.Find(b => b.Name == name);
        }

        public float GetTicksPerSecond() => _ticksPerSecond;
        public float GetDuration() => _duration;
        public ref SharpGltfNodeData GetRootNode() => ref _rootNode;
        public Dictionary<string, BoneInfo> GetBoneIDMap() => _boneInfoMap;
        public List<SharpGltfBone> GetBones() => _bones;
    }
}
