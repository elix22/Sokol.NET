// CGltfAnimator.cs — ported from examples/CGltfViewer/Source/CGltfAnimator.cs.
// Drives per-frame bone transforms using CGltfAnimation/CGltfBone (no SharpGLTF dependency).
//
// Port note: the CGltfViewer original also drove KHR material-property animation through a
// `Dictionary<int,List<Mesh>>` (CGltfViewer's Mesh class). That hook is the engine's ONLY coupling
// to the renderer mesh type, so it is dropped here to keep the animation engine self-contained
// in the Framework (it will be re-added against the Framework's material type when/if needed).
// The morph-weight animation path stays — it only writes into a dictionary keyed by node index.
using System;
using System.Collections.Generic;
using System.Numerics;
using GameEditor.Framework.Core;

namespace GameEditor.Framework.Renderer.Server.Animation
{
    public class CGltfAnimator
    {
        private Matrix4x4[] _finalBoneMatrices;
        private CGltfAnimation? _currentAnimation;
        private float _currentTime;

        private Dictionary<string, Matrix4x4> _nodeGlobalTransforms = new();
        private Dictionary<int, float[]> _animatedMorphWeights = new();

        // Fast lookup: node name → list of CGltfNodes (multiple primitives per glTF node)
        // _nodesByName: non-skinned nodes for TRS animation
        // _allNodesByName: ALL nodes (including skinned) for morph weight lookup
        private Dictionary<string, List<CGltfNode>> _nodesByName = new();
        private Dictionary<string, List<CGltfNode>> _allNodesByName = new();
        private List<CGltfNode> _allNodes;

        private Dictionary<string, BoneInfo>? _characterBoneInfoMap;

        public float PlaybackSpeed { get; set; } = 1.0f;

        /// <summary>Round and round (the default); off, time stops at the clip's end and the last pose holds (G6 A0).</summary>
        public bool Loop { get; set; } = true;

        /// <summary>Renderer bookkeeping: the frame this animator last advanced, so a shared
        /// animator (multiple primitives / multiple views) ticks exactly once per frame.</summary>
        public int LastUpdatedFrame = -1;

        public CGltfAnimator(
            CGltfAnimation? animation,
            List<CGltfNode> nodes,
            int boneCount,
            Dictionary<string, BoneInfo>? characterBoneInfoMap = null)
        {
            _currentTime = 0f;
            _currentAnimation = animation;
            _characterBoneInfoMap = characterBoneInfoMap;
            _allNodes = nodes;

            _finalBoneMatrices = new Matrix4x4[Math.Max(1, boneCount)];
            BuildNodeLookup(nodes);
            Array.Fill(_finalBoneMatrices, Matrix4x4.Identity);

            if (_currentAnimation != null)
            {
                ref CGltfNodeData rootNode = ref _currentAnimation.GetRootNode();
                CalculateBoneTransform(rootNode, Matrix4x4.Identity);
            }
        }

        private void BuildNodeLookup(List<CGltfNode> nodes)
        {
            _nodesByName.Clear();
            _allNodesByName.Clear();
            foreach (var node in nodes)
            {
                if (!string.IsNullOrEmpty(node.NodeName))
                {
                    // All-nodes map (for morph weights — morphs exist on skinned meshes too)
                    if (!_allNodesByName.TryGetValue(node.NodeName, out var allList))
                        _allNodesByName[node.NodeName] = allList = new List<CGltfNode>();
                    allList.Add(node);

                    // Non-skinned map (for TRS node animation)
                    if (!node.IsSkinned)
                    {
                        if (!_nodesByName.TryGetValue(node.NodeName, out var list))
                            _nodesByName[node.NodeName] = list = new List<CGltfNode>();
                        list.Add(node);
                    }
                }
            }
            Logger.Info($"[CGltfAnimator] built node lookup, {_nodesByName.Count} TRS names, {_allNodesByName.Count} total names");
        }

        public void SetAnimation(CGltfAnimation? animation)
        {
            _currentAnimation = animation;
            _currentTime = 0f;
            Array.Fill(_finalBoneMatrices, Matrix4x4.Identity);

            if (_currentAnimation != null)
            {
                // Rebuild lookup when animation changes (different set of bones may be present)
                BuildNodeLookup(_allNodes);
                ref CGltfNodeData rootNode = ref _currentAnimation.GetRootNode();
                CalculateBoneTransform(rootNode, Matrix4x4.Identity);
            }
        }

        public void UpdateAnimation(float dt)
        {
            if (_currentAnimation == null) return;

            _currentTime += _currentAnimation.GetTicksPerSecond() * dt * PlaybackSpeed;
            float duration = _currentAnimation.GetDuration();
            _currentTime = Loop ? _currentTime % duration : MathF.Min(_currentTime, duration);

            // Batch-update all bones
            foreach (var bone in _currentAnimation.GetBones())
                bone.Update(_currentTime);

            ref CGltfNodeData rootNode = ref _currentAnimation.GetRootNode();
            CalculateBoneTransform(rootNode, Matrix4x4.Identity);

            ApplyAnimationToNodes();
            UpdateMorphWeightAnimations(_currentTime);
        }

        private void ApplyAnimationToNodes()
        {
            if (_currentAnimation == null || _nodesByName.Count == 0) return;

            foreach (var bone in _currentAnimation.GetBones())
            {
                if (_nodesByName.TryGetValue(bone.Name, out var renderNodes))
                {
                    bone.GetAnimatedChannels(out bool hasT, out bool hasR, out bool hasS,
                                             out Vector3 t, out Quaternion r, out Vector3 s);
                    foreach (var node in renderNodes)
                    {
                        Vector3 ft = hasT ? t : node.Position;
                        Quaternion fr = hasR ? r : node.Rotation;
                        Vector3 fs = hasS ? s : node.Scale;
                        node.SetLocalTransform(ft, fr, fs);
                    }
                }
            }
        }

        public void PlayAnimation(CGltfAnimation animation)
        {
            _currentAnimation = animation;
            _currentTime = 0f;
        }

        private void CalculateBoneTransform(CGltfNodeData node, Matrix4x4 parentTransform)
        {
            Matrix4x4 nodeTransform = node.Transformation;

            var bone = _currentAnimation?.FindBone(node.Name);
            if (bone != null)
                nodeTransform = bone.GetLocalTransform();

            Matrix4x4 globalTransformation = nodeTransform * parentTransform;
            _nodeGlobalTransforms[node.Name] = globalTransformation;

            var boneInfoMap = _characterBoneInfoMap ?? _currentAnimation?.GetBoneIDMap();
            if (boneInfoMap != null && boneInfoMap.TryGetValue(node.Name, out var boneInfo))
            {
                int index = boneInfo.Id;
                if (index >= 0 && index < _finalBoneMatrices.Length)
                    _finalBoneMatrices[index] = boneInfo.Offset * globalTransformation;
            }

            for (int i = 0; i < node.ChildrenCount; i++)
                CalculateBoneTransform(node.Children[i], globalTransformation);
        }

        public Matrix4x4[] GetFinalBoneMatrices() => _finalBoneMatrices;
        public float GetCurrentTime() => _currentTime;
        public CGltfAnimation? GetCurrentAnimation() => _currentAnimation;

        public bool TryGetNodeGlobalTransform(string nodeName, out Matrix4x4 globalTransform)
            => _nodeGlobalTransforms.TryGetValue(nodeName, out globalTransform);

        private void UpdateMorphWeightAnimations(float t)
        {
            if (_currentAnimation == null || _currentAnimation.MorphAnimations.Count == 0) return;
            foreach (var morphAnim in _currentAnimation.MorphAnimations)
            {
                var weights = morphAnim.SampleWeightsAtTime(t);
                // Use name-based lookup so all primitives sharing the same node get the weights.
                if (!string.IsNullOrEmpty(morphAnim.NodeName) &&
                    _allNodesByName.TryGetValue(morphAnim.NodeName, out var matchingNodes))
                {
                    foreach (var node in matchingNodes)
                        _animatedMorphWeights[node.NodeIndex] = weights;
                }
                else if (morphAnim.NodeIndex >= 0)
                {
                    // Fallback: use the stored index directly
                    _animatedMorphWeights[morphAnim.NodeIndex] = weights;
                }
            }
        }

        public float[]? GetAnimatedMorphWeights(int nodeIndex)
            => _animatedMorphWeights.TryGetValue(nodeIndex, out var w) ? w : null;
    }
}
