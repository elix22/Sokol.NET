using System;
using System.Numerics;

namespace Sokol
{
    public class SharpGltfAnimator
    {
        private Matrix4x4[] _finalBoneMatrices = new Matrix4x4[AnimationConstants.MAX_BONES];
        private SharpGltfAnimation? _currentAnimation;
        private float _currentTime;

        public SharpGltfAnimator(SharpGltfAnimation? animation)
        {
            _currentTime = 0.0f;
            _currentAnimation = animation;
            Array.Fill(_finalBoneMatrices, Matrix4x4.Identity);
        }

        public void UpdateAnimation(float dt)
        {
            if (_currentAnimation != null)
            {
                _currentTime += _currentAnimation.GetTicksPerSecond() * dt;
                _currentTime = _currentTime % _currentAnimation.GetDuration();

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

            if (bone != null)
            {
                bone.Update(_currentTime);
                nodeTransform = bone.GetLocalTransform();
            }

            Matrix4x4 globalTransformation = nodeTransform * parentTransform;

            var boneInfoMap = _currentAnimation?.GetBoneIDMap();
            if (boneInfoMap != null && boneInfoMap.ContainsKey(nodeName))
            {
                int index = boneInfoMap[nodeName].Id;
                Matrix4x4 offset = boneInfoMap[nodeName].Offset;
                _finalBoneMatrices[index] = offset * globalTransformation;
            }

            for (int i = 0; i < node.ChildrenCount; i++)
                CalculateBoneTransform(node.Children[i], globalTransformation);
        }

        public Matrix4x4[] GetFinalBoneMatrices() => _finalBoneMatrices;
        public float GetCurrentTime() => _currentTime;
        public SharpGltfAnimation? GetCurrentAnimation() => _currentAnimation;
    }
}
