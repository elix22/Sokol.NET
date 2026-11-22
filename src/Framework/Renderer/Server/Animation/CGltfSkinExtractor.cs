// CGltfSkinExtractor.cs — skin + animation extraction ported from
// examples/CGltfViewer/Source/CGltfModel.cs (ProcessSkin / ProcessNodeWithParent /
// ProcessAnimationsForCharacter / ProcessNodeAnimations / ParseBoneChannel /
// ParseMorphWeightChannel / BuildRootNodeHierarchy / BuildNodeData + helpers).
//
// Operates on an already-parsed cgltf_data* and produces exactly the data the ported
// CGltfAnimator consumes: the render-node hierarchy, the joint-name → BoneInfo (inverse-bind)
// map, and the CGltfAnimation clips. Decoupled from CGltfViewer's Mesh/Texture/PipelineManager:
//   - node building creates ONE CGltfNode per glTF node (the per-primitive split only mattered
//     for mesh association, which the skinning bone math doesn't need),
//   - the KHR_animation_pointer (material-property) channel hook is skipped (those channels drove
//     the Mesh material animation that was dropped from CGltfAnimator).
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using GameEditor.Framework.Core;
using Sokol;
using static Sokol.CGltf;

namespace GameEditor.Framework.Renderer.Server.Animation
{
    public sealed unsafe class CGltfSkinExtractor
    {
        /// <summary>Render-node hierarchy (names + local TRS + parent links + IsSkinned).</summary>
        public List<CGltfNode> Nodes { get; } = new();

        /// <summary>Skin 0's joint-name → {bone id, inverse-bind matrix} map.</summary>
        public Dictionary<string, BoneInfo> BoneInfoMap { get; private set; } = new();
        public int BoneCount { get; private set; }

        /// <summary>Each bone's matrix in the file's rest pose (inverse bind × the joint's world matrix), by bone id: what a
        /// character with no clip is drawn with. A KHR_mesh_quantization file's skinned positions are in quantized units and
        /// only these matrices (gltfpack folds the dequantization into the inverse binds) bring them to the model's space.</summary>
        public Matrix4x4[] RestBoneMatrices { get; private set; } = Array.Empty<Matrix4x4>();

        /// <summary>Animation clips (skin-filtered when a skin exists, else node animations).</summary>
        public List<CGltfAnimation> Animations { get; } = new();

        /// <summary>Names of every node referenced as a joint by any skin.</summary>
        public HashSet<string> SkinnedNodeNames { get; } = new();

        public bool HasSkin       => BoneCount > 0;
        public bool HasAnimations => Animations.Count > 0;

        /// <summary>Extracts skin + animation data from a parsed glTF. Buffers must be loaded.</summary>
        // <paramref name="skinIndex"/> selects WHICH skin to extract (a glTF can have several — e.g.
        // FishAndShark's fish + shark). Each call builds its OWN node tree + bone map + skin-filtered
        // clips, so per-skin animators are fully independent (no shared-node-tree contention).
        // skinIndex &lt; 0 (or no skins) → node/morph-weight animation only.
        public static CGltfSkinExtractor Extract(cgltf_data* data, int skinIndex = 0)
        {
            var e = new CGltfSkinExtractor();
            e.Process(data, skinIndex);
            return e;
        }

        private void Process(cgltf_data* data, int skinIndex)
        {
            // Step 1a — joint names of EVERY skin. ProcessAnimationsForCharacter treats a non-bone,
            // non-joint channel as a "node animation"; so a channel targeting ANOTHER character's joint
            // must be in this set, or it leaks into this character's clips (e.g. the shark binding the
            // fish's clip → wrong/no motion). Only THIS skin's bones go in BoneInfoMap below.
            for (int si = 0; si < (int)data->skins_count; si++)
            {
                cgltf_skin* sk = &data->skins[si];
                for (int ji = 0; ji < (int)sk->joints_count; ji++)
                {
                    string? jn = PtrToStr(sk->joints[ji]->name);
                    if (jn != null) SkinnedNodeNames.Add(jn);
                }
            }

            // Step 1b — the requested skin → bone-info map (only that skin's bones).
            if (skinIndex >= 0 && skinIndex < (int)data->skins_count)
            {
                var (bim, bc, rest) = ProcessSkin(&data->skins[skinIndex], skinIndex);
                BoneInfoMap = bim; BoneCount = bc; RestBoneMatrices = rest;
            }

            // Step 2 — scene node tree → CGltfNode hierarchy (one node per glTF node).
            cgltf_scene* scene = data->scene != null ? data->scene
                               : data->scenes_count > 0 ? data->scenes : null;
            if (scene != null)
            {
                for (int ni = 0; ni < (int)scene->nodes_count; ni++)
                    ProcessNode(scene->nodes[ni], null);
            }

            // Step 3 — animation clips. Skin-filtered when a skin exists, else node animations.
            if (data->animations_count == 0) return;
            Animations.AddRange(HasSkin
                ? ProcessAnimationsForCharacter(BoneInfoMap, data)
                : ProcessNodeAnimations(data));

            Logger.Info($"[CGltfSkinExtractor] {Nodes.Count} nodes, {BoneCount} bones, {Animations.Count} animations");
        }

        // ── ProcessSkin (CGltfModel.cs:361) ───────────────────────────────────────
        private (Dictionary<string, BoneInfo> bim, int count, Matrix4x4[] rest) ProcessSkin(cgltf_skin* skin, int skinIndex)
        {
            var boneInfoMap = new Dictionary<string, BoneInfo>();
            var rest = new List<Matrix4x4>();
            int counter = 0;
            float* wm = stackalloc float[16];

            for (int ji = 0; ji < (int)skin->joints_count; ji++)
            {
                cgltf_node* joint = skin->joints[ji];
                string name = PtrToStr(joint->name) ?? $"Joint_{skinIndex}_{ji}";
                if (boneInfoMap.ContainsKey(name)) continue;

                Matrix4x4 ibm = Matrix4x4.Identity;
                if (skin->inverse_bind_matrices != null)
                {
                    float* m = stackalloc float[16];
                    cgltf_accessor_read_float(*skin->inverse_bind_matrices, (nuint)ji, m, 16);
                    ibm = new Matrix4x4(
                        m[0], m[1], m[2], m[3],
                        m[4], m[5], m[6], m[7],
                        m[8], m[9], m[10], m[11],
                        m[12], m[13], m[14], m[15]);
                }
                boneInfoMap[name] = new BoneInfo { Id = counter++, Offset = ibm };
                cgltf_node_transform_world(in *joint, wm);
                rest.Add(ibm * new Matrix4x4(
                    wm[0],  wm[1],  wm[2],  wm[3],
                    wm[4],  wm[5],  wm[6],  wm[7],
                    wm[8],  wm[9],  wm[10], wm[11],
                    wm[12], wm[13], wm[14], wm[15]));
            }
            return (boneInfoMap, counter, rest.ToArray());
        }

        // ── Node hierarchy (simplified from ProcessNodeWithParent, CGltfModel.cs:732) ──
        private void ProcessNode(cgltf_node* node, CGltfNode? parent)
        {
            Vector3 pos = Vector3.Zero;
            Quaternion rot = Quaternion.Identity;
            Vector3 scale = Vector3.One;

            if (node->has_matrix != 0)
            {
                var mat = new Matrix4x4(
                    node->matrix[0],  node->matrix[1],  node->matrix[2],  node->matrix[3],
                    node->matrix[4],  node->matrix[5],  node->matrix[6],  node->matrix[7],
                    node->matrix[8],  node->matrix[9],  node->matrix[10], node->matrix[11],
                    node->matrix[12], node->matrix[13], node->matrix[14], node->matrix[15]);
                Matrix4x4.Decompose(mat, out scale, out rot, out pos);
            }
            else
            {
                if (node->has_translation != 0) pos   = new Vector3(node->translation[0], node->translation[1], node->translation[2]);
                if (node->has_rotation    != 0) rot   = new Quaternion(node->rotation[0], node->rotation[1], node->rotation[2], node->rotation[3]);
                if (node->has_scale       != 0) scale = new Vector3(node->scale[0], node->scale[1], node->scale[2]);
            }

            string? nodeName = PtrToStr(node->name);
            bool isSkinned   = nodeName != null && SkinnedNodeNames.Contains(nodeName);

            var rn = new CGltfNode
            {
                Position  = pos,
                Rotation  = rot,
                Scale     = scale,
                MeshIndex = node->mesh != null ? 0 : -1,
                NodeName  = nodeName,
                IsSkinned = isSkinned,
                Parent    = parent,
                NodeIndex = Nodes.Count,
            };
            Nodes.Add(rn);

            for (int ci = 0; ci < (int)node->children_count; ci++)
                ProcessNode(node->children[ci], rn);
        }

        // ── ProcessAnimationsForCharacter (CGltfModel.cs:845) ─────────────────────
        private List<CGltfAnimation> ProcessAnimationsForCharacter(
            Dictionary<string, BoneInfo> boneInfoMap, cgltf_data* data)
        {
            var result = new List<CGltfAnimation>();
            var rootNode = BuildRootNodeHierarchy(data);

            for (int ai = 0; ai < (int)data->animations_count; ai++)
            {
                cgltf_animation* anim = &data->animations[ai];
                string animName = PtrToStr(anim->name) ?? $"Animation_{ai}";
                float duration  = ComputeAnimDuration(anim);

                var animation = new CGltfAnimation(duration, 1.0f, rootNode, boneInfoMap) { Name = animName };

                for (int ci = 0; ci < (int)anim->channels_count; ci++)
                {
                    cgltf_animation_channel* ch = &anim->channels[ci];
                    if (ch->target_node == null) continue; // pointer-based channel (material anim) — skipped

                    string? targetName = PtrToStr(ch->target_node->name);
                    if (targetName == null) continue;

                    if (ch->target_path == cgltf_animation_path_type.cgltf_animation_path_type_weights)
                    {
                        ParseMorphWeightChannel(ch, targetName, animation);
                        continue;
                    }

                    bool isBone     = boneInfoMap.ContainsKey(targetName);
                    bool isNodeAnim = !SkinnedNodeNames.Contains(targetName);
                    if (!isBone && !isNodeAnim) continue;

                    int boneId = isBone ? boneInfoMap[targetName].Id : -1;

                    var bone = animation.FindBone(targetName);
                    if (bone == null)
                    {
                        bone = new CGltfBone(targetName, boneId);
                        SetRest(bone, ch->target_node);
                        animation.AddBone(bone);
                    }

                    ParseBoneChannel(ch, bone, ConvertInterpolation(ch->sampler->interpolation));
                }

                if (animation.GetBones().Count > 0 || animation.MorphAnimations.Count > 0)
                    result.Add(animation);
            }
            return result;
        }

        // ── ProcessNodeAnimations (CGltfModel.cs:918) ─────────────────────────────
        private List<CGltfAnimation> ProcessNodeAnimations(cgltf_data* data)
        {
            var result   = new List<CGltfAnimation>();
            var emptyBim = new Dictionary<string, BoneInfo>();
            var rootNode = BuildRootNodeHierarchy(data);

            for (int ai = 0; ai < (int)data->animations_count; ai++)
            {
                cgltf_animation* anim = &data->animations[ai];
                string animName = PtrToStr(anim->name) ?? $"Animation_{ai}";
                float duration  = ComputeAnimDuration(anim);

                var animation = new CGltfAnimation(duration, 1.0f, rootNode, emptyBim) { Name = animName };

                for (int ci = 0; ci < (int)anim->channels_count; ci++)
                {
                    cgltf_animation_channel* ch = &anim->channels[ci];
                    if (ch->target_node == null) continue;

                    string? targetName = PtrToStr(ch->target_node->name);
                    if (targetName == null) continue;

                    if (ch->target_path == cgltf_animation_path_type.cgltf_animation_path_type_weights)
                    {
                        ParseMorphWeightChannel(ch, targetName, animation);
                        continue;
                    }

                    var bone = animation.FindBone(targetName);
                    if (bone == null)
                    {
                        bone = new CGltfBone(targetName, -1);
                        SetRest(bone, ch->target_node);
                        animation.AddBone(bone);
                    }
                    ParseBoneChannel(ch, bone, ConvertInterpolation(ch->sampler->interpolation));
                }

                if (animation.GetBones().Count > 0 || animation.MorphAnimations.Count > 0)
                    result.Add(animation);
            }
            return result;
        }

        // A bone starts at its node's own transform: the channels the clip lacks keep it.
        private static void SetRest(CGltfBone bone, cgltf_node* n)
        {
            Vector3 t = Vector3.Zero, s = Vector3.One;
            Quaternion r = Quaternion.Identity;
            if (n->has_matrix != 0)
            {
                var m = new Matrix4x4(
                    n->matrix[0],  n->matrix[1],  n->matrix[2],  n->matrix[3],
                    n->matrix[4],  n->matrix[5],  n->matrix[6],  n->matrix[7],
                    n->matrix[8],  n->matrix[9],  n->matrix[10], n->matrix[11],
                    n->matrix[12], n->matrix[13], n->matrix[14], n->matrix[15]);
                Matrix4x4.Decompose(m, out s, out r, out t);
            }
            else
            {
                if (n->has_translation != 0) t = new Vector3(n->translation[0], n->translation[1], n->translation[2]);
                if (n->has_rotation    != 0) r = new Quaternion(n->rotation[0], n->rotation[1], n->rotation[2], n->rotation[3]);
                if (n->has_scale       != 0) s = new Vector3(n->scale[0], n->scale[1], n->scale[2]);
            }
            bone.SetRest(t, r, s);
        }

        // ── ParseBoneChannel (CGltfModel.cs:973) ──────────────────────────────────
        private void ParseBoneChannel(cgltf_animation_channel* ch, CGltfBone bone, CGltfInterpolation interp)
        {
            cgltf_animation_sampler* samp = ch->sampler;
            int nKeys = (int)samp->input->count;
            if (nKeys == 0) return;

            float[] times = UnpackAccessorFloats(samp->input, nKeys, 1);

            switch (ch->target_path)
            {
                case cgltf_animation_path_type.cgltf_animation_path_type_translation:
                {
                    int valCount = interp == CGltfInterpolation.CubicSpline ? nKeys * 3 : nKeys;
                    float[] raw = UnpackAccessorFloats(samp->output, valCount * 3, 3);
                    var vals = new Vector3[valCount];
                    for (int i = 0; i < valCount; i++)
                        vals[i] = new Vector3(raw[i*3], raw[i*3+1], raw[i*3+2]);
                    bone.SetTranslationKeys(times, vals, interp);
                    break;
                }
                case cgltf_animation_path_type.cgltf_animation_path_type_rotation:
                {
                    int valCount = interp == CGltfInterpolation.CubicSpline ? nKeys * 3 : nKeys;
                    float[] raw = UnpackAccessorFloats(samp->output, valCount * 4, 4);
                    var vals = new Quaternion[valCount];
                    for (int i = 0; i < valCount; i++)
                        vals[i] = new Quaternion(raw[i*4], raw[i*4+1], raw[i*4+2], raw[i*4+3]);
                    bone.SetRotationKeys(times, vals, interp);
                    break;
                }
                case cgltf_animation_path_type.cgltf_animation_path_type_scale:
                {
                    int valCount = interp == CGltfInterpolation.CubicSpline ? nKeys * 3 : nKeys;
                    float[] raw = UnpackAccessorFloats(samp->output, valCount * 3, 3);
                    var vals = new Vector3[valCount];
                    for (int i = 0; i < valCount; i++)
                        vals[i] = new Vector3(raw[i*3], raw[i*3+1], raw[i*3+2]);
                    bone.SetScaleKeys(times, vals, interp);
                    break;
                }
            }
        }

        // ── ParseMorphWeightChannel (CGltfModel.cs:1019) ──────────────────────────
        private void ParseMorphWeightChannel(cgltf_animation_channel* ch, string targetName, CGltfAnimation animation)
        {
            cgltf_animation_sampler* samp = ch->sampler;
            int nKeys = (int)samp->input->count;
            if (nKeys == 0) return;

            int nodeIdx = -1;
            for (int ni = 0; ni < Nodes.Count; ni++)
                if (Nodes[ni].NodeName == targetName) { nodeIdx = ni; break; }

            int totalOutputVals = (int)samp->output->count;
            int numTargets = nKeys > 0 ? totalOutputVals / nKeys : 0;
            if (numTargets == 0) return;

            float[] times  = UnpackAccessorFloats(samp->input,  nKeys, 1);
            float[] values = UnpackAccessorFloats(samp->output, totalOutputVals, 1);

            var morphAnim = new MorphWeightAnimation { NodeIndex = nodeIdx, NodeName = targetName };
            for (int ki = 0; ki < nKeys; ki++)
            {
                var w = new float[numTargets];
                for (int ti = 0; ti < numTargets; ti++)
                    w[ti] = values[ki * numTargets + ti];
                morphAnim.Keyframes.Add((times[ki], w));
            }
            animation.MorphAnimations.Add(morphAnim);
        }

        // ── BuildRootNodeHierarchy / BuildNodeData (CGltfModel.cs:1059) ────────────
        private CGltfNodeData BuildRootNodeHierarchy(cgltf_data* data)
        {
            var root = new CGltfNodeData
            {
                Name = "SceneRoot",
                Transformation = Matrix4x4.Identity,
                Children = new List<CGltfNodeData>(),
                ChildrenCount = 0
            };

            cgltf_scene* scene = data->scene != null ? data->scene
                               : data->scenes_count > 0 ? data->scenes : null;
            if (scene != null)
            {
                for (int ni = 0; ni < (int)scene->nodes_count; ni++)
                {
                    root.Children.Add(BuildNodeData(scene->nodes[ni]));
                    root.ChildrenCount++;
                }
            }
            return root;
        }

        private CGltfNodeData BuildNodeData(cgltf_node* node)
        {
            float* m16 = stackalloc float[16];
            cgltf_node_transform_local(*node, m16);
            var localMatrix = new Matrix4x4(
                m16[0], m16[1], m16[2],  m16[3],
                m16[4], m16[5], m16[6],  m16[7],
                m16[8], m16[9], m16[10], m16[11],
                m16[12],m16[13],m16[14], m16[15]);

            string name = PtrToStr(node->name) ?? "";
            var data = new CGltfNodeData
            {
                Name = name,
                Transformation = localMatrix,
                Children = new List<CGltfNodeData>(),
                ChildrenCount = (int)node->children_count
            };
            for (int ci = 0; ci < (int)node->children_count; ci++)
                data.Children.Add(BuildNodeData(node->children[ci]));
            return data;
        }

        // ── helpers (CGltfModel.cs) ───────────────────────────────────────────────
        private static float ComputeAnimDuration(cgltf_animation* anim)
        {
            float maxTime = 0f;
            for (int ci = 0; ci < (int)anim->channels_count; ci++)
            {
                var ch = &anim->channels[ci];
                if (ch->sampler == null || ch->sampler->input == null) continue;
                int cnt = (int)ch->sampler->input->count;
                if (cnt == 0) continue;
                float* last = stackalloc float[1];
                cgltf_accessor_read_float(*ch->sampler->input, (nuint)(cnt - 1), last, 1);
                if (last[0] > maxTime) maxTime = last[0];
            }
            return maxTime > 0f ? maxTime : 1.0f;
        }

        private static CGltfInterpolation ConvertInterpolation(cgltf_interpolation_type t) => t switch
        {
            cgltf_interpolation_type.cgltf_interpolation_type_step         => CGltfInterpolation.Step,
            cgltf_interpolation_type.cgltf_interpolation_type_cubic_spline => CGltfInterpolation.CubicSpline,
            _                                                              => CGltfInterpolation.Linear
        };

        internal static float[] UnpackAccessorFloats(cgltf_accessor* acc, int totalFloats, int elemSize)
        {
            if (acc == null || totalFloats <= 0) return Array.Empty<float>();
            var buf = new float[totalFloats];
            fixed (float* pBuf = buf)
                cgltf_accessor_unpack_floats(*acc, pBuf, (nuint)totalFloats);
            return buf;
        }

        private static string? PtrToStr(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
    }
}
