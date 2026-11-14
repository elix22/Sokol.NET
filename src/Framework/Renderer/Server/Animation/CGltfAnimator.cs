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
        // A crossfade (G6 A6): the clip fading out, its time, and how far through the fade it is.
        private CGltfAnimation? _fadingAnimation;
        private float _fadingTime, _fadeTime, _fadeLength;

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

            if (_fadingAnimation != null)
            {
                _fadeTime += dt;
                if (_fadeTime >= _fadeLength) _fadingAnimation = null;     // the fade done: one clip again, today's path
                else { UpdateFade(dt); return; }
            }

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
            _fadingAnimation = null;
        }

        /// <summary>
        /// Fades <paramref name="animation"/> in over <paramref name="seconds"/> while the clip that plays fades out (from
        /// its time now, going on): each bone's local translation / rotation / scale blended, never a jump. 0 seconds, or
        /// nothing playing: <see cref="PlayAnimation"/>.
        /// </summary>
        public void CrossFade(CGltfAnimation animation, float seconds)
        {
            if (seconds <= 0f || _currentAnimation == null || ReferenceEquals(animation, _currentAnimation)) { PlayAnimation(animation); return; }
            _fadingAnimation = _currentAnimation;
            _fadingTime = _currentTime;
            _currentAnimation = animation;
            _currentTime = 0f;
            _fadeTime = 0f;
            _fadeLength = seconds;
        }

        /// <summary>How far a crossfade is, 0 → 1 (smoothstep of its time); 0 when none runs.</summary>
        public float FadeWeight => _fadingAnimation == null ? 0f : FadeWeightAt(_fadeTime / _fadeLength);

        public static float FadeWeightAt(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        // A frame of a crossfade: both clips' bones at their times, blended by the fade's weight, the hierarchy composed once.
        private void UpdateFade(float dt)
        {
            var fading = _fadingAnimation!;
            _fadingTime += fading.GetTicksPerSecond() * dt * PlaybackSpeed;
            float d = fading.GetDuration();
            _fadingTime = d > 0f ? (Loop ? _fadingTime % d : MathF.Min(_fadingTime, d)) : 0f;
            foreach (var bone in _currentAnimation!.GetBones()) bone.Update(_currentTime);
            foreach (var bone in fading.GetBones()) bone.Update(_fadingTime);
            ref CGltfNodeData rootNode = ref _currentAnimation.GetRootNode();
            CalculateBlended(rootNode, Matrix4x4.Identity, FadeWeight);
            ApplyAnimationToNodes();
            UpdateMorphWeightAnimations(_currentTime);
        }

        // ── Posed from outside (G6 A7: a controller's state machine) ───────────────────────────────────────────
        private readonly Dictionary<string, Matrix4x4> _lastLocal = new();     // each node's local matrix as last posed
        private readonly Dictionary<string, Matrix4x4> _snapshot = new();      // the pose a transition fades out from

        /// <summary>
        /// Poses the character from outside (a controller): clip <paramref name="a"/> at <paramref name="secondsA"/>,
        /// blended with <paramref name="b"/> at <paramref name="secondsB"/> by <paramref name="weightB"/> (a blend tree's two
        /// neighbours; null / 0: <paramref name="a"/> alone); then, while <paramref name="fromSnapshot"/> is ≥ 0, blended
        /// from the pose kept by the last <paramref name="takeSnapshot"/> (a transition fading out the pose as it was when it
        /// began). Times are seconds, round and round each clip.
        /// </summary>
        public void Pose(CGltfAnimation a, float secondsA, CGltfAnimation? b, float secondsB, float weightB, float fromSnapshot, bool takeSnapshot)
        {
            if (takeSnapshot) { _snapshot.Clear(); foreach (var kv in _lastLocal) _snapshot[kv.Key] = kv.Value; }
            _fadingAnimation = null;
            _currentAnimation = a;
            _currentTime = Ticks(a, secondsA);
            foreach (var bone in a.GetBones()) bone.Update(_currentTime);
            float tb = 0f;
            if (b != null && weightB > 0f) { tb = Ticks(b, secondsB); foreach (var bone in b.GetBones()) bone.Update(tb); }
            ref CGltfNodeData root = ref a.GetRootNode();
            CalculatePosed(root, Matrix4x4.Identity, a, b != null && weightB > 0f ? b : null, weightB, fromSnapshot);
            ApplyAnimationToNodes();
            UpdateMorphWeightAnimations(_currentTime);
        }

        private static float Ticks(CGltfAnimation clip, float seconds)
        {
            float t = seconds * clip.GetTicksPerSecond(), d = clip.GetDuration();
            if (d <= 0f) return 0f;
            t %= d;
            return t < 0f ? t + d : t;
        }

        private void CalculatePosed(CGltfNodeData node, Matrix4x4 parentTransform, CGltfAnimation a, CGltfAnimation? b, float wb, float fromSnapshot)
        {
            Matrix4x4 local = a.FindBone(node.Name)?.GetLocalTransform() ?? node.Transformation;
            if (b != null) local = Blend(local, b.FindBone(node.Name)?.GetLocalTransform() ?? node.Transformation, wb);
            if (fromSnapshot >= 0f) local = Blend(_snapshot.TryGetValue(node.Name, out var was) ? was : node.Transformation, local, FadeWeightAt(fromSnapshot));
            _lastLocal[node.Name] = local;
            Matrix4x4 globalTransformation = local * parentTransform;
            _nodeGlobalTransforms[node.Name] = globalTransformation;
            var boneInfoMap = _characterBoneInfoMap ?? a.GetBoneIDMap();
            if (boneInfoMap != null && boneInfoMap.TryGetValue(node.Name, out var boneInfo))
            {
                int index = boneInfo.Id;
                if (index >= 0 && index < _finalBoneMatrices.Length)
                    _finalBoneMatrices[index] = boneInfo.Offset * globalTransformation;
            }
            for (int i = 0; i < node.ChildrenCount; i++)
                CalculatePosed(node.Children[i], globalTransformation, a, b, wb, fromSnapshot);
        }

        /// <summary>Two local transforms blended: translation and scale lerped, rotation slerped, then composed (scale, rotation, translation).</summary>
        public static Matrix4x4 Blend(in Matrix4x4 from, in Matrix4x4 to, float w)
        {
            if (!Matrix4x4.Decompose(from, out var s0, out var r0, out var t0) || !Matrix4x4.Decompose(to, out var s1, out var r1, out var t1))
                return w < 0.5f ? from : to;
            return Matrix4x4.CreateScale(Vector3.Lerp(s0, s1, w))
                 * Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(r0, r1, w))
                 * Matrix4x4.CreateTranslation(Vector3.Lerp(t0, t1, w));
        }

        private void CalculateBlended(CGltfNodeData node, Matrix4x4 parentTransform, float w)
        {
            Matrix4x4 to = _currentAnimation?.FindBone(node.Name)?.GetLocalTransform() ?? node.Transformation;
            Matrix4x4 from = _fadingAnimation?.FindBone(node.Name)?.GetLocalTransform() ?? node.Transformation;
            Matrix4x4 globalTransformation = Blend(from, to, w) * parentTransform;
            _nodeGlobalTransforms[node.Name] = globalTransformation;

            var boneInfoMap = _characterBoneInfoMap ?? _currentAnimation?.GetBoneIDMap();
            if (boneInfoMap != null && boneInfoMap.TryGetValue(node.Name, out var boneInfo))
            {
                int index = boneInfo.Id;
                if (index >= 0 && index < _finalBoneMatrices.Length)
                    _finalBoneMatrices[index] = boneInfo.Offset * globalTransformation;
            }

            for (int i = 0; i < node.ChildrenCount; i++)
                CalculateBlended(node.Children[i], globalTransformation, w);
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
