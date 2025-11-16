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

    /// <summary>
    /// Holds animation data for morph target weights on a specific node
    /// </summary>
    public class MorphWeightAnimation
    {
        public int NodeIndex { get; set; }
        public string NodeName { get; set; } = "";
        
        // Keyframe data: time -> weights array
        // Each keyframe contains all weights for all morph targets
        public List<(float time, float[] weights)> Keyframes { get; set; } = new();

        /// <summary>
        /// Sample morph weights at given time using linear interpolation
        /// </summary>
        public float[] SampleWeightsAtTime(float time)
        {
            if (Keyframes.Count == 0) return Array.Empty<float>();
            if (Keyframes.Count == 1) return Keyframes[0].weights;

            // Find surrounding keyframes
            for (int i = 0; i < Keyframes.Count - 1; i++)
            {
                if (time >= Keyframes[i].time && time <= Keyframes[i + 1].time)
                {
                    float t = (time - Keyframes[i].time) / 
                             (Keyframes[i + 1].time - Keyframes[i].time);
                    
                    var weights1 = Keyframes[i].weights;
                    var weights2 = Keyframes[i + 1].weights;
                    var result = new float[weights1.Length];
                    
                    for (int w = 0; w < weights1.Length; w++)
                    {
                        result[w] = weights1[w] + t * (weights2[w] - weights1[w]);
                    }
                    
                    return result;
                }
            }

            // Before first or after last keyframe
            return time < Keyframes[0].time ? Keyframes[0].weights : Keyframes[^1].weights;
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
        
        // Morph target weight animations
        public List<MorphWeightAnimation> MorphAnimations { get; set; } = new();

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
        
        /// <summary>
        /// Creates a new animation containing only bones that match the given bone info map.
        /// This is used to split a multi-character animation into separate per-character animations.
        /// Each bone is CLONED so characters can animate independently without interfering with each other.
        /// </summary>
        public SharpGltfAnimation CreateSubsetForCharacter(Dictionary<string, BoneInfo> characterBoneInfoMap, string characterName)
        {
            // Create new animation with same duration and timing
            var subset = new SharpGltfAnimation(_duration, _ticksPerSecond, _rootNode, characterBoneInfoMap);
            subset.Name = $"{Name}_{characterName}";
            
            // DEBUG: Log bone names
            Console.WriteLine($"[DEBUG] CreateSubsetForCharacter '{characterName}': Animation has {_bones.Count} bones, Character has {characterBoneInfoMap.Count} bones");
            Console.WriteLine($"[DEBUG] Character bone names: {string.Join(", ", characterBoneInfoMap.Keys.Take(5))}...");
            Console.WriteLine($"[DEBUG] Animation bone names: {string.Join(", ", _bones.Take(5).Select(b => b.Name))}...");
            
            // Clone only bones that belong to this character
            int matchedCount = 0;
            foreach (var bone in _bones)
            {
                if (characterBoneInfoMap.ContainsKey(bone.Name))
                {
                    // Clone the bone so each character has independent bone state
                    var clonedBone = bone.Clone(characterBoneInfoMap[bone.Name].Id);
                    subset.AddBone(clonedBone);
                    matchedCount++;
                }
            }
            
            Console.WriteLine($"[DEBUG] Matched {matchedCount} bones for character '{characterName}'");
            
            // Don't copy material/morph animations (they're not character-specific)
            
            return subset;
        }
    }
}
