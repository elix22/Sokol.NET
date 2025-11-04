using System;
using System.Numerics;
using static Sokol.SLog;

namespace Sokol
{
    public class SharpGltfAnimator
    {
        private Matrix4x4[] _finalBoneMatrices = new Matrix4x4[AnimationConstants.MAX_BONES];
        private SharpGltfAnimation? _currentAnimation;
        private float _currentTime;
        private int _debugBoneCount = 0;  // Debug counter
        private Dictionary<string, Matrix4x4> _nodeGlobalTransforms = new Dictionary<string, Matrix4x4>();  // Global transforms for node animations
        private SharpGLTF.Schema2.ModelRoot? _modelRoot;  // Reference to glTF model for updating nodes

        public SharpGltfAnimator(SharpGltfAnimation? animation, SharpGLTF.Schema2.ModelRoot? modelRoot = null)
        {
            _currentTime = 0.0f;
            _currentAnimation = animation;
            _modelRoot = modelRoot;

            // Initialize with identity matrices
            Array.Fill(_finalBoneMatrices, Matrix4x4.Identity);

            // Update once to get the initial pose at time 0
            if (_currentAnimation != null)
            {
                ref SharpGltfNodeData rootNode = ref _currentAnimation.GetRootNode();
                Info($"Root node: '{rootNode.Name}' with {rootNode.ChildrenCount} children", "SharpGltfAnimator");
                CalculateBoneTransform(rootNode, Matrix4x4.Identity);
                Info($"Initialized with {_currentAnimation.GetBoneIDMap().Count} bones", "SharpGltfAnimator");
            }
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
                Info($"Switched to animation '{_currentAnimation.Name}' with {_currentAnimation.GetBoneIDMap().Count} bones", "SharpGltfAnimator");
            }
        }

        public void UpdateAnimation(float dt)
        {
            if (_currentAnimation != null)
            {
                _currentTime += _currentAnimation.GetTicksPerSecond() * dt;
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
                
                // Apply animated values back to glTF nodes (for node animations)
                ApplyAnimationToNodes();
            }
        }

        // Apply animated transform values back to the glTF node properties
        private void ApplyAnimationToNodes()
        {
            if (_currentAnimation == null || _modelRoot == null) return;

            var bones = _currentAnimation.GetBones();
            foreach (var bone in bones)
            {
                // Find the corresponding glTF node
                var gltfNode = _modelRoot.LogicalNodes.FirstOrDefault(n => n.Name == bone.Name);
                if (gltfNode != null)
                {
                    // Get the animated TRS from the bone
                    bone.GetTRS(out Vector3 translation, out Quaternion rotation, out Vector3 scale);
                    
                    // Update the glTF node's properties (this is what LocalMatrix reads from)
                    gltfNode.LocalTransform = new SharpGLTF.Transforms.AffineTransform(scale, rotation, translation);
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

            var boneInfoMap = _currentAnimation?.GetBoneIDMap();
            if (boneInfoMap != null && boneInfoMap.ContainsKey(nodeName))
            {
                int index = boneInfoMap[nodeName].Id;
                Matrix4x4 offset = boneInfoMap[nodeName].Offset;
                _finalBoneMatrices[index] = offset * globalTransformation;

                // Debug: Print first bone calculation
                if (_debugBoneCount < 2 && index == 0)
                {
                    Info($"[Bone {index}] '{nodeName}': offset.M44={offset.M44:F3}, global.M44={globalTransformation.M44:F3}, final.M44={_finalBoneMatrices[index].M44:F3}");
                    _debugBoneCount++;
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
    }
}
