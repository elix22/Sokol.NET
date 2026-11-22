// SkinnedCharacterRegistry.cs — persistent home for the live GPU + animator objects of imported
// skinned characters, keyed by source glTF path. The model file's entry holds what every copy of the character shares
// (GPU meshes, clips, skeleton); each copy in the scene - the entity its skinned meshes sit under - gets an entry of
// its own with its own animator, joint texture and Driven flag (Phase H E1), so two people made from one .glb no
// longer share one pose. Mirrors MeshRegistry's role for static meshes:
// the SkinnedMeshRenderer component stores only serializable keys (path + primitive index), and the
// live SkinnedMesh / CGltfAnimator are resolved from here at draw time. Because the registry is a
// static that outlives the play/stop scene-snapshot round-trip, skinned characters survive Play→Stop
// (the deserialized component re-resolves the same registry entry).
using System;
using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Resources;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.Animation
{
    public static class SkinnedCharacterRegistry
    {
        public sealed class Entry
        {
            /// <summary>The character's animator (drives every primitive's bones). Each copy has its own; the first copy
            /// takes the file entry's, so whoever asks without naming a copy (a script, a check) sees a live character.</summary>
            public CGltfAnimator? Animator;
            public int BoneCount;

            /// <summary>The skeleton's rest pose (the file's, by bone id): drawn when there is no animator. Zero matrices would
            /// leave the shader's skinning off - right for a file whose positions are already in model space, but a quantized
            /// one (KHR_mesh_quantization) is only placed by its skin.</summary>
            public Matrix4x4[]? RestBones;

            /// <summary>The bone matrices to draw with: the animator's pose, else the rest pose.</summary>
            public Matrix4x4[]? Bones => Animator?.GetFinalBoneMatrices() ?? RestBones;

            /// <summary>The model file's entry this copy belongs to; null on the file's entry itself.</summary>
            public Entry? File { get; private set; }

            // The skeleton a copy's animator is made from (set by the importer on the file's entry).
            internal List<CGltfNode>? Nodes;
            internal Dictionary<string, BoneInfo>? BoneInfoMap;
            // The file entry's animator is a copy's (the first one's) while that copy lives.
            internal bool AnimatorTaken;

            public Entry() { Clips = new(); Meshes = new(); }

            // A copy: the file's meshes and clips shared, an animator of its own playing the first clip.
            internal Entry(Entry file)
            {
                File = file;
                Clips = file.Clips;
                Meshes = file.Meshes;
                BoneCount = file.BoneCount;
                RestBones = file.RestBones;
                if (file.Animator != null && !file.AnimatorTaken) { Animator = file.Animator; file.AnimatorTaken = true; }
                else if (file.Animator != null && file.Nodes != null && file.Clips.Count > 0)
                    Animator = new CGltfAnimator(file.Clips[0].Clip, file.Nodes, file.BoneCount, file.BoneInfoMap);
            }

            /// <summary>
            /// Every clip of the character's file, in the file's order, by a name that does not move between imports (G6 A0):
            /// its own, or <c>clip&lt;N&gt;</c> by its index when it has none; a repeated name gets <c>~2</c>, <c>~3</c>.
            /// Scenes store these names (<see cref="SkinnedMeshRenderer.Clip"/>).
            /// </summary>
            public readonly List<(string Name, CGltfAnimation Clip)> Clips;

            /// <summary>The clips' names, in the file's order.</summary>
            public IEnumerable<string> ClipNames { get { foreach (var c in Clips) yield return c.Name; } }

            /// <summary>A clip by its name, or null.</summary>
            public CGltfAnimation? FindClip(string name)
            {
                foreach (var c in Clips) if (c.Name == name) return c.Clip;
                return null;
            }

            /// <summary>The name of the clip the animator plays now ("" for none).</summary>
            public string CurrentClipName
            {
                get
                {
                    var now = Animator?.GetCurrentAnimation();
                    foreach (var c in Clips) if (ReferenceEquals(c.Clip, now)) return c.Name;
                    return "";
                }
            }

            /// <summary>
            /// Names the file's clips (unnamed → clip&lt;N&gt;, repeated → name~2 …) and keeps them, the first one playing.
            /// </summary>
            public void SetClips(IReadOnlyList<CGltfAnimation> clips)
            {
                Clips.Clear();
                var seen = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < clips.Count; i++)
                {
                    string name = string.IsNullOrEmpty(clips[i].Name) ? $"clip{i}" : clips[i].Name;
                    if (seen.TryGetValue(name, out int n)) { seen[name] = n + 1; name = $"{name}~{n + 1}"; }
                    else seen[name] = 1;
                    Clips.Add((name, clips[i]));
                }
            }

            // The settings last applied from the character's SkinnedMeshRenderer (Apply): a clip asked for that is not one of
            // its clips is said once.
            private string? _askedClip;

            /// <summary>
            /// Brings the animator in line with a SkinnedMeshRenderer's playback settings (once a frame, before it is advanced):
            /// another clip plays from its start ("" = the first), the speed, the loop. False when it is not to move this
            /// frame (not playing: the pose holds).
            /// </summary>
            /// <summary>A controller poses this character (G6 A7): its skinned meshes' own clip settings wait.</summary>
            public bool Driven;

            /// <summary>A clip's length in seconds, by name (0 when the character has none of that name).</summary>
            public float ClipSeconds(string name)
                => FindClip(name) is { } c ? c.GetDuration() / MathF.Max(c.GetTicksPerSecond(), 1e-6f) : 0f;

            public bool Apply(in SkinnedMeshRenderer smr)
            {
                var anim = Animator;
                if (anim == null) return false;
                string want = string.IsNullOrEmpty(smr.Clip) ? (Clips.Count > 0 ? Clips[0].Name : "") : smr.Clip;
                if (want.Length > 0 && want != CurrentClipName)
                {
                    if (FindClip(want) is { } clip)
                    {
                        if (CurrentClipName.Length > 0 && smr.Fade > 0f) anim.CrossFade(clip, smr.Fade);   // a clip changed: faded in
                        else anim.PlayAnimation(clip);
                    }
                    else if (_askedClip != want) Core.Logger.Warning($"[Animation] '{smr.CharacterKey}' has no clip '{want}' (it has {string.Join(", ", ClipNames)}).");
                    _askedClip = want;
                }
                anim.PlaybackSpeed = smr.Speed;
                anim.Loop = smr.Loop;
                return smr.Playing;
            }
            /// <summary>Skinned GPU mesh per running primitive index.</summary>
            public readonly Dictionary<int, SkinnedMesh> Meshes;

            // ── >128-bone joint-matrix texture path ──────────────────────────────────────────────
            // The uniform path packs bones into finalBonesMatrices[MAX_BONES] (a fixed UBO array).
            // A skeleton with more joints than that can't fit, so its bones ride in a per-character
            // RGBA32F texture instead and the shader is told to fetch them (use_uniform_skinning=0).
            // Layout matches what animation.glsl's getMatrixFromTexture expects (Khronos glTF-Sample-
            // Viewer convention): each joint occupies 8 RGBA texels = 2 mat4 — transform at i*32 and a
            // normal matrix at i*32+16. We store the same matrix in both (mirroring the uniform path,
            // which also drives positions and normals from one matrix).

            /// <summary>True when the skeleton is too large for the uniform array → use the joint texture.</summary>
            // A LOW_TIER GPU's skinned shaders have no bone array, and a GPU with few vertex uniforms cannot index all of it:
            // every skeleton rides in the joint texture there.
            public bool UsesTextureSkinning => BoneCount > AnimationConstants.MAX_BONES || ShaderVariantCache.TextureSkinning;

            public sg_image JointTexture;
            public sg_view  JointTextureView;
            private int      _jointTexWidth;
            private float[]? _jointScratch;
            private int      _jointTexFrame = -1;   // last frame the texture was uploaded (single-upload guard)

            /// <summary>Lazily creates the square RGBA32F stream texture sized to hold every joint's 8
            /// texels. Render-thread only; no-op if already created or the skeleton fits the uniform path.</summary>
            public void EnsureJointTexture()
            {
                if (JointTexture.id != 0 || !UsesTextureSkinning || BoneCount <= 0) return;

                // ceil(sqrt(boneCount * 8)) texels per side (8 RGBA texels = 2 mat4 per joint).
                _jointTexWidth = (int)Math.Ceiling(Math.Sqrt(BoneCount * 8));

                JointTexture = sg_make_image(new sg_image_desc
                {
                    width        = _jointTexWidth,
                    height       = _jointTexWidth,
                    pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA32F,
                    usage        = new sg_image_usage { stream_update = true },
                    label        = "skinned-joint-matrix-texture",
                });
                JointTextureView = sg_make_view(new sg_view_desc
                {
                    texture = new sg_texture_view_desc { image = JointTexture },
                    label   = "skinned-joint-matrix-view",
                });
            }

            /// <summary>Packs the current bone matrices into the joint texture and uploads it. Frame-
            /// guarded so a character shared across primitives + views uploads at most once per frame.
            /// A null animator uploads the rest pose (none: cleared → zero matrices → the shader's no-skin fallback).
            /// Render-thread only.</summary>
            public unsafe void UploadJointMatrices(int frame)
            {
                if (JointTexture.id == 0 || _jointTexFrame == frame) return;
                _jointTexFrame = frame;

                int texelCount = _jointTexWidth * _jointTexWidth;
                if (_jointScratch == null || _jointScratch.Length != texelCount * 4)
                    _jointScratch = new float[texelCount * 4];
                Array.Clear(_jointScratch, 0, _jointScratch.Length);

                var bones = Bones;
                if (bones != null)
                {
                    int maxJoints = Math.Min(Math.Min(BoneCount, bones.Length), texelCount / 8);
                    for (int i = 0; i < maxJoints; i++)
                    {
                        CopyMatrix(bones[i], _jointScratch, i * 32);       // transform matrix
                        CopyMatrix(bones[i], _jointScratch, i * 32 + 16);  // normal matrix (same matrix)
                    }
                }

                fixed (float* ptr = _jointScratch)
                {
                    var data = new sg_image_data();
                    data.mip_levels[0].ptr  = ptr;
                    data.mip_levels[0].size = (nuint)(_jointScratch.Length * sizeof(float));
                    sg_update_image(JointTexture, in data);
                }
            }

            private static void CopyMatrix(Matrix4x4 m, float[] dst, int o)
            {
                dst[o +  0] = m.M11; dst[o +  1] = m.M12; dst[o +  2] = m.M13; dst[o +  3] = m.M14;
                dst[o +  4] = m.M21; dst[o +  5] = m.M22; dst[o +  6] = m.M23; dst[o +  7] = m.M24;
                dst[o +  8] = m.M31; dst[o +  9] = m.M32; dst[o + 10] = m.M33; dst[o + 11] = m.M34;
                dst[o + 12] = m.M41; dst[o + 13] = m.M42; dst[o + 14] = m.M43; dst[o + 15] = m.M44;
            }

            /// <summary>Frees the GPU meshes AND the joint texture (the full per-character GPU teardown,
            /// used on re-import and registry reset).</summary>
            public void DestroyMeshes()
            {
                foreach (var m in Meshes.Values) m.Destroy();
                Meshes.Clear();
                DestroyJointTexture();
            }

            /// <summary>Frees the joint texture only (a copy's own GPU object; its meshes are the file's).</summary>
            internal void DestroyJointTexture()
            {
                if (JointTextureView.id != 0) { sg_destroy_view(JointTextureView); JointTextureView = default; }
                if (JointTexture.id     != 0) { sg_destroy_image(JointTexture);    JointTexture     = default; }
                _jointTexFrame = -1;
            }
        }

        private static readonly Dictionary<string, Entry> _entries = new();
        private static readonly Dictionary<(string Key, Entity Owner), Entry> _copies = new();
        private static readonly List<(string Key, Entity Owner)> _gone = new();

        /// <summary>Gets the entry for <paramref name="key"/>, creating it (and freeing any prior
        /// GPU meshes if it already existed, e.g. on re-import) so the caller can repopulate it.
        /// The copies of a re-imported file are dropped: they are made again from the new clips.</summary>
        public static Entry GetOrCreateFresh(string key)
        {
            if (_entries.TryGetValue(key, out var e)) { e.DestroyMeshes(); e.Animator = null; e.Nodes = null; e.BoneInfoMap = null; e.AnimatorTaken = false; }
            else _entries[key] = e = new Entry();
            DropCopies(k => k == key);
            return e;
        }

        /// <summary>The model file's entry (shared meshes and clips; its own animator is no copy's).</summary>
        public static bool TryGet(string key, out Entry entry) => _entries.TryGetValue(key, out entry!);

        /// <summary>
        /// The entry of one copy of a character: <paramref name="owner"/> is the entity its skinned meshes sit under
        /// (<see cref="OwnerOf"/>). Made on first use, with its own animator; a file without clips has one entry for all.
        /// </summary>
        public static bool TryGet(string key, Entity owner, out Entry entry)
        {
            if (!_entries.TryGetValue(key, out var file)) { entry = null!; return false; }
            if (file.Animator == null || owner.IsNull) { entry = file; return true; }
            if (!_copies.TryGetValue((key, owner), out entry!)) _copies[(key, owner)] = entry = new Entry(file);
            return true;
        }

        /// <summary>The copy a skinned mesh belongs to: the entity it sits under (the model's root), or itself.</summary>
        public static Entity OwnerOf(ECSWorld world, Entity skinned)
            => world.TryGetComponent<Transform>(skinned, out var t) && t.Parent is { } p && !p.IsNull && p.IsAlive ? p : skinned;

        /// <summary>The copy a skinned mesh belongs to, resolved from its entity.</summary>
        public static bool TryGetFor(ECSWorld world, Entity skinned, string key, out Entry entry)
            => TryGet(key, OwnerOf(world, skinned), out entry);

        /// <summary>Frees the copies whose entity is gone (once a frame; a copy's joint texture is a GPU object).</summary>
        public static void ForgetGoneCopies()
        {
            if (_copies.Count == 0) return;
            _gone.Clear();
            foreach (var k in _copies.Keys) if (!k.Owner.IsAlive) _gone.Add(k);
            foreach (var k in _gone) Drop(k);
        }

        private static void DropCopies(Func<string, bool> which)
        {
            _gone.Clear();
            foreach (var k in _copies.Keys) if (which(k.Key)) _gone.Add(k);
            foreach (var k in _gone) Drop(k);
        }

        private static void Drop((string Key, Entity Owner) k)
        {
            var c = _copies[k];
            c.DestroyJointTexture();
            if (c.File is { } f && ReferenceEquals(c.Animator, f.Animator)) f.AnimatorTaken = false;   // the next copy takes it
            _copies.Remove(k);
        }

        /// <summary>The number of copies alive (a check reads it).</summary>
        public static int CopyCount => _copies.Count;

        /// <summary>Frees every character's GPU meshes and drops all entries (full reset).</summary>
        public static void Clear()
        {
            DropCopies(_ => true);
            foreach (var e in _entries.Values) e.DestroyMeshes();
            _entries.Clear();
        }
    }
}
