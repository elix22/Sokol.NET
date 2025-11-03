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

        public SharpGltfAnimator(SharpGltfAnimation? animation)
        {
            _currentTime = 0.0f;
            _currentAnimation = animation;

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
    }
}
