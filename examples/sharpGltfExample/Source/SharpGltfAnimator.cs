using System;
using System.Linq;
using System.Numerics;
using static Sokol.SLog;

namespace Sokol
{
    public class SharpGltfAnimator
    {
        private Matrix4x4[] _finalBoneMatrices;  // Sized dynamically based on actual bone count
        private SharpGltfAnimation? _currentAnimation;
        private float _currentTime;
        private Dictionary<string, Matrix4x4> _nodeGlobalTransforms = new Dictionary<string, Matrix4x4>();  // Global transforms for node animations
        private Dictionary<int, List<Mesh>> _materialToMeshMap;  // Material index to mesh mapping for property animations
        private Dictionary<int, float[]> _animatedMorphWeights = new Dictionary<int, float[]>();  // Node index to morph weights
        
        // Fast lookup for non-skinned node animations - stores ALL nodes with the same name
        private Dictionary<string, List<SharpGltfNode>> _nodesByName = new Dictionary<string, List<SharpGltfNode>>();
        
        // Store all nodes for rebuilding lookups when animation changes
        private List<SharpGltfNode> _allNodes;
        
        // Character-specific bone info map (overrides animation's global bone map)
        private Dictionary<string, BoneInfo>? _characterBoneInfoMap;
        
        /// <summary>
        /// Playback speed multiplier. 1.0 = normal speed, 0.5 = half speed, 2.0 = double speed
        /// </summary>
        public float PlaybackSpeed { get; set; } = 1.0f;

        public SharpGltfAnimator(SharpGltfAnimation? animation, Dictionary<int, List<Mesh>> materialToMeshMap, List<SharpGltfNode> nodes, int boneCount, Dictionary<string, BoneInfo>? characterBoneInfoMap = null)
        {
            _currentTime = 0.0f;
            _currentAnimation = animation;
            _materialToMeshMap = materialToMeshMap;
            _characterBoneInfoMap = characterBoneInfoMap;
            _allNodes = nodes;
            
            // Allocate bone matrices array based on actual bone count
            // This supports both uniform-based (max 100) and texture-based (unlimited) skinning
            _finalBoneMatrices = new Matrix4x4[Math.Max(1, boneCount)];  // At least 1 to avoid zero-length arrays

            // Build lookup for non-skinned animated nodes
            BuildNodeLookup(nodes);

            // Initialize with identity matrices
            Array.Fill(_finalBoneMatrices, Matrix4x4.Identity);

            // Update once to get the initial pose at time 0
            if (_currentAnimation != null)
            {
                ref SharpGltfNodeData rootNode = ref _currentAnimation.GetRootNode();
                CalculateBoneTransform(rootNode, Matrix4x4.Identity);
            }
        }

        /// <summary>
        /// Convenient constructor that accepts a SharpGltfModel (legacy - for backward compatibility)
        /// </summary>
        public SharpGltfAnimator(SharpGltfModel model)
            : this(model.Animation, model.MaterialToMeshMap, model.Nodes, model.BoneCounter, null)
        {
        }
        
        /// <summary>
        /// Build a fast lookup dictionary from node name to SharpGltfNode list.
        /// Only includes NON-SKINNED nodes (for node transform animations).
        /// Skinned nodes are handled via bone matrices and should not have their transforms updated.
        /// Note: A single glTF node can have multiple primitives, resulting in multiple SharpGltfNode instances with the same name.
        /// </summary>
        private void BuildNodeLookup(List<SharpGltfNode> nodes)
        {
            _nodesByName.Clear();
            int totalNodes = 0;
            
            foreach (var node in nodes)
            {
                // Only add non-skinned nodes with names
                if (!string.IsNullOrEmpty(node.NodeName) && !node.IsSkinned)
                {
                    if (!_nodesByName.ContainsKey(node.NodeName))
                    {
                        _nodesByName[node.NodeName] = new List<SharpGltfNode>();
                    }
                    _nodesByName[node.NodeName].Add(node);
                    totalNodes++;
                }
            }
            Info($"Built node lookup with {_nodesByName.Count} unique names, {totalNodes} total non-skinned nodes", "SharpGLTF");
        }
        
        public void SetAnimation(SharpGltfAnimation? animation)
        {
            _currentAnimation = animation;
            _currentTime = 0.0f;

            // Reset to initial pose
            Array.Fill(_finalBoneMatrices, Matrix4x4.Identity);

            if (_currentAnimation != null)
            {
                ref SharpGltfNodeData rootNode = ref _currentAnimation.GetRootNode();
                CalculateBoneTransform(rootNode, Matrix4x4.Identity);
            }
        }

        public void UpdateAnimation(float dt)
        {
            if (_currentAnimation != null)
            {
                // Apply playback speed multiplier
                _currentTime += _currentAnimation.GetTicksPerSecond() * dt * PlaybackSpeed;
                _currentTime = _currentTime % _currentAnimation.GetDuration();

                // Batch update all bones at once before hierarchy traversal (optimization for WebAssembly)
                var bones = _currentAnimation.GetBones();
                foreach (var bone in bones)
                {
                    bone.Update(_currentTime);
                }

                // Only recalculate bone transforms when we update the bone data
                ref SharpGltfNodeData rootNode = ref _currentAnimation.GetRootNode();
                CalculateBoneTransform(rootNode, Matrix4x4.Identity);
                
                // Apply animated transforms to non-skinned nodes
                ApplyAnimationToNodes();
                
                // NEW: Update material property animations (KHR_animation_pointer)
                UpdateMaterialPropertyAnimations(_currentTime);
                
                // NEW: Update morph target weight animations
                UpdateMorphWeightAnimations(_currentTime);
            }
        }

        /// <summary>
        /// Apply animated transform values to NON-SKINNED nodes only.
        /// Skinned nodes are handled via bone matrices and should not have their local transforms modified.
        /// For node animations: only update the components that are actually animated.
        /// If a channel (T/R/S) is not animated, preserve the node's original value.
        /// </summary>
        private void ApplyAnimationToNodes()
        {
            if (_currentAnimation == null) return;
            
            // PERFORMANCE: Early exit if there are no non-skinned animated nodes
            // For skinned characters (like DancingGangster), all nodes are skinned,
            // so this loop would wastefully iterate through all bones finding no matches
            if (_nodesByName.Count == 0)
            {
                return;
            }

            int nodesUpdated = 0;
            var bones = _currentAnimation.GetBones();
            
            foreach (var bone in bones)
            {
                // Update ALL nodes with this name (handles multiple primitives per glTF node)
                if (_nodesByName.TryGetValue(bone.Name, out var renderNodes))
                {
                    // Get which channels are animated and their values
                    bone.GetAnimatedChannels(out bool hasTranslation, out bool hasRotation, out bool hasScale,
                                             out Vector3 translation, out Quaternion rotation, out Vector3 scale);
                    
                    // Apply to all nodes with this name
                    foreach (var renderNode in renderNodes)
                    {
                        // Preserve original values for non-animated channels
                        Vector3 finalTranslation = hasTranslation ? translation : renderNode.Position;
                        Quaternion finalRotation = hasRotation ? rotation : renderNode.Rotation;
                        Vector3 finalScale = hasScale ? scale : renderNode.Scale;
                        
                        // Update the node's local transform (this marks it and children as dirty)
                        renderNode.SetLocalTransform(finalTranslation, finalRotation, finalScale);
                        nodesUpdated++;
                    }
                }
            }
        }

        public void PlayAnimation(SharpGltfAnimation animation)
        {
            _currentAnimation = animation;
            _currentTime = 0.0f;
        }

        private void CalculateBoneTransform(SharpGltfNodeData node, Matrix4x4 parentTransform)
        {
            string nodeName = node.Name;
            Matrix4x4 nodeTransform = node.Transformation;

            SharpGltfBone? bone = _currentAnimation?.FindBone(nodeName);

            // Bone was already updated in batch, just get the transform
            if (bone != null)
            {
                nodeTransform = bone.GetLocalTransform();
            }

            Matrix4x4 globalTransformation = nodeTransform * parentTransform;

            // Store global transform for node animations (non-skinned)
            _nodeGlobalTransforms[nodeName] = globalTransformation;

            // Use character-specific bone info map if available, otherwise fallback to animation's global map
            var boneInfoMap = _characterBoneInfoMap ?? _currentAnimation?.GetBoneIDMap();
            if (boneInfoMap != null && boneInfoMap.ContainsKey(nodeName))
            {
                int index = boneInfoMap[nodeName].Id;
                Matrix4x4 offset = boneInfoMap[nodeName].Offset;
                
                // Safety check: only write if index is within bounds
                if (index >= 0 && index < _finalBoneMatrices.Length)
                {
                    _finalBoneMatrices[index] = offset * globalTransformation;
                }
            }

            for (int i = 0; i < node.ChildrenCount; i++)
                CalculateBoneTransform(node.Children[i], globalTransformation);
        }

        public Matrix4x4[] GetFinalBoneMatrices() => _finalBoneMatrices;
        public float GetCurrentTime() => _currentTime;
        public SharpGltfAnimation? GetCurrentAnimation() => _currentAnimation;
        
        /// <summary>
        /// Gets the global (world) transform for a node by name (for node animations)
        /// </summary>
        public bool TryGetNodeGlobalTransform(string nodeName, out Matrix4x4 globalTransform)
        {
            return _nodeGlobalTransforms.TryGetValue(nodeName, out globalTransform);
        }

        /// <summary>
        /// Updates material property animations (KHR_animation_pointer support)
        /// </summary>
        private void UpdateMaterialPropertyAnimations(float currentTime)
        {
            if (_currentAnimation == null || _currentAnimation.MaterialAnimations.Count == 0 || _materialToMeshMap == null)
                return;

            foreach (var matAnim in _currentAnimation.MaterialAnimations)
            {
                // Sample the animation at the current time
                if (matAnim.IsFloatType)
                {
                    float value = matAnim.SampleFloatAtTime(currentTime);
                    ApplyMaterialFloatProperty(matAnim.MaterialIndex, matAnim.Target, value);
                }
                else
                {
                    Vector2 value = matAnim.SampleVector2AtTime(currentTime);
                    ApplyMaterialVector2Property(matAnim.MaterialIndex, matAnim.Target, value);
                }
            }
        }

        /// <summary>
        /// Apply float property value to all meshes using the given material
        /// </summary>
        private void ApplyMaterialFloatProperty(int materialIndex, MaterialAnimationTarget target, float value)
        {
            if (_materialToMeshMap == null || !_materialToMeshMap.TryGetValue(materialIndex, out var meshes))
                return;

            foreach (var mesh in meshes)
            {
                switch (target)
                {
                    case MaterialAnimationTarget.NormalTextureRotation:
                        mesh.NormalTexRotation = value;
                        break;
                    case MaterialAnimationTarget.ThicknessTextureRotation:
                        // TODO: Add thickness texture rotation property to Mesh if needed
                        break;
                }
            }
        }

        /// <summary>
        /// Apply Vector2 property value to all meshes using the given material
        /// </summary>
        private void ApplyMaterialVector2Property(int materialIndex, MaterialAnimationTarget target, Vector2 value)
        {
            if (_materialToMeshMap == null || !_materialToMeshMap.TryGetValue(materialIndex, out var meshes))
                return;

            foreach (var mesh in meshes)
            {
                switch (target)
                {
                    case MaterialAnimationTarget.NormalTextureOffset:
                        mesh.NormalTexOffset = value;
                        break;
                    case MaterialAnimationTarget.NormalTextureScale:
                        mesh.NormalTexScale = value;
                        break;
                    case MaterialAnimationTarget.ThicknessTextureOffset:
                        // TODO: Add thickness texture offset property to Mesh if needed
                        break;
                    case MaterialAnimationTarget.ThicknessTextureScale:
                        // TODO: Add thickness texture scale property to Mesh if needed
                        break;
                }
            }
        }
        
        /// <summary>
        /// Update morph target weights from animation
        /// </summary>
        private void UpdateMorphWeightAnimations(float currentTime)
        {
            if (_currentAnimation == null || _currentAnimation.MorphAnimations.Count == 0)
                return;

            foreach (var morphAnim in _currentAnimation.MorphAnimations)
            {
                // Sample the weights at the current time
                float[] weights = morphAnim.SampleWeightsAtTime(currentTime);
                
                // Store in our dictionary for Frame.cs to read
                _animatedMorphWeights[morphAnim.NodeIndex] = weights;
                
                // Debug: Log first frame only
                // if (currentTime < 0.1f)
                // {
                //     string weightsStr = string.Join(", ", weights.Select(w => w.ToString("F3")));
                //     Info($"[MorphAnim] Node {morphAnim.NodeIndex} ({morphAnim.NodeName}) weights: [{weightsStr}]", "SharpGLTF");
                // }
            }
        }
        
        /// <summary>
        /// Get the animated morph weights for a specific node index
        /// </summary>
        public float[]? GetAnimatedMorphWeights(int nodeIndex)
        {
            return _animatedMorphWeights.TryGetValue(nodeIndex, out var weights) ? weights : null;
        }
    }
}
