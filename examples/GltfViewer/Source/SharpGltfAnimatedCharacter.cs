using System;
using System.Collections.Generic;
using System.Numerics;
using static Sokol.SG;
using static Sokol.SLog;

namespace Sokol
{
    /// <summary>
    /// Represents a single animated character (skinned mesh) in a glTF scene.
    /// Each character has its own skeleton, animation, and animator.
    /// This allows multiple independent characters to coexist in one glTF file
    /// without animation cross-contamination.
    /// </summary>
    public class AnimatedCharacter
    {
        /// <summary>
        /// The skin index this character uses from the glTF file
        /// </summary>
        public int SkinIndex { get; }
        
        /// <summary>
        /// The character's name (from glTF skin name or auto-generated)
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// All animations available for this character
        /// </summary>
        private List<SharpGltfAnimation> Animations { get; }
        
        /// <summary>
        /// Number of animations available for this character
        /// </summary>
        public int AnimationCount => Animations.Count;
        
        /// <summary>
        /// Current animation index
        /// </summary>
        public int CurrentAnimationIndex { get; private set; }
        
        /// <summary>
        /// The current animation for this character
        /// </summary>
        public SharpGltfAnimation Animation
        {
            get
            {
                if (Animations.Count == 0)
                {
                    Error($"Character '{Name}' has no animations.");
                    return null;
                }
                
                return Animations[CurrentAnimationIndex];
            }
        }
        
        /// <summary>
        /// The animator that updates this character's bones
        /// </summary>
        public SharpGltfAnimator Animator { get; }
        
        /// <summary>
        /// All meshes that belong to this character
        /// </summary>
        public List<Mesh> Meshes { get; }
        
        /// <summary>
        /// Number of bones in this character's skeleton
        /// </summary>
        public int BoneCount { get; }
        
        /// <summary>
        /// Whether this character uses texture-based skinning
        /// (automatically true if BoneCount >= 100, or can be manually set)
        /// </summary>
        public bool UsesTextureSkinning { get; private set; }
        
        /// <summary>
        /// Set the skinning mode for this character (only if BoneCount < MAX_BONES)
        /// </summary>
        public void SetSkinningMode(SkinningMode mode)
        {
            // If bone count >= MAX_BONES, texture skinning is mandatory
            if (BoneCount >= AnimationConstants.MAX_BONES)
            {
                UsesTextureSkinning = true;
                return;
            }
            
            bool newMode = (mode == SkinningMode.TextureBased);
            
            // If switching to texture-based and we don't have texture yet, create it
            if (newMode && !UsesTextureSkinning && JointMatrixTexture.id == 0)
            {
                UsesTextureSkinning = true;
                CreateJointMatrixTexture();
            }
            // If switching from texture to uniform, just update the flag
            // (we keep the texture allocated in case user switches back)
            else if (!newMode && UsesTextureSkinning)
            {
                UsesTextureSkinning = false;
            }
            else
            {
                UsesTextureSkinning = newMode;
            }
        }
        
        /// <summary>
        /// Joint matrix texture for this character (only created if UsesTextureSkinning is true)
        /// </summary>
        public sg_image JointMatrixTexture { get; private set; }
        
        /// <summary>
        /// Joint matrix texture view for shader binding
        /// </summary>
        public sg_view JointMatrixView { get; private set; }
        
        /// <summary>
        /// Joint matrix sampler for shader binding
        /// </summary>
        public sg_sampler JointMatrixSampler { get; private set; }
        
        /// <summary>
        /// Texture width for joint matrix storage
        /// </summary>
        public int JointTextureWidth { get; private set; }
        
        /// <summary>
        /// Cached texture data array for GPU upload
        /// </summary>
        private float[]? jointTextureData;
        
        /// <summary>
        /// Tracks if texture has been updated this frame (prevents multiple updates per frame)
        /// </summary>
        private bool textureUpdatedThisFrame;
        
        public AnimatedCharacter(
            int skinIndex,
            string name,
            List<SharpGltfAnimation> animations,
            List<Mesh> meshes,
            Dictionary<int, List<Mesh>> materialToMeshMap,
            List<SharpGltfNode> nodes,
            int boneCount,
            Dictionary<string, BoneInfo> boneInfoMap)
        {
            SkinIndex = skinIndex;
            Name = name;
            Animations = animations;
            CurrentAnimationIndex = 0;
            Meshes = meshes;
            BoneCount = boneCount;
            
            // Default: use texture-based skinning if bone count >= MAX_BONES
            UsesTextureSkinning = (boneCount >= AnimationConstants.MAX_BONES);
            
            // Each character gets its own animator instance
            // If character has animations, use the first one; otherwise use null for bind pose
            Animator = new SharpGltfAnimator(
                animations.Count > 0 ? animations[0] : null, 
                materialToMeshMap, 
                nodes, 
                boneCount,
                boneInfoMap  // Pass character-specific bone info map
            );
            
            // Log character initialization
            if (animations.Count > 0)
            {
                Info($"[Character] '{name}' initialized with animation '{animations[0].Name}' (Index 0/{animations.Count})", "Animation");
            }
            else
            {
                Info($"[Character] '{name}' initialized in bind pose (no animations)", "Animation");
            }
            
            // Create joint matrix texture if this character uses texture-based skinning
            if (UsesTextureSkinning)
            {
                CreateJointMatrixTexture();
            }
        }
        
        /// <summary>
        /// Update this character's animation
        /// </summary>
        public void Update(float dt)
        {
            Info($"[Character] '{Name}' Update called: dt={dt:F4}", "Animation");
            Animator.UpdateAnimation(dt);
            
            // Reset the texture update flag at the start of each frame
            textureUpdatedThisFrame = false;
        }
        
        /// <summary>
        /// Change this character's animation
        /// </summary>
        public void SetAnimation(SharpGltfAnimation animation)
        {
            Animator.SetAnimation(animation);
        }
        
        /// <summary>
        /// Get the number of animations for this character
        /// </summary>
        public int GetAnimationCount()
        {
            return Animations.Count;
        }
        
        /// <summary>
        /// Switch to the next animation
        /// </summary>
        public void NextAnimation()
        {
            if (Animations.Count == 0) return;
            
            CurrentAnimationIndex++;
            if (CurrentAnimationIndex >= Animations.Count)
                CurrentAnimationIndex = 0;
            
            Animator.SetAnimation(Animation);
        }
        
        /// <summary>
        /// Switch to the previous animation
        /// </summary>
        public void PreviousAnimation()
        {
            if (Animations.Count == 0) return;
            
            CurrentAnimationIndex--;
            if (CurrentAnimationIndex < 0)
                CurrentAnimationIndex = Animations.Count - 1;
            
            Animator.SetAnimation(Animation);
        }
        
        /// <summary>
        /// Get the bone matrices for rendering
        /// </summary>
        public Matrix4x4[] GetBoneMatrices()
        {
            return Animator.GetFinalBoneMatrices();
        }
        
        /// <summary>
        /// Create the joint matrix texture for this character.
        /// Each joint stores 2 matrices (transform + normal) = 32 floats = 8 vec4 (RGBA)
        /// </summary>
        private void CreateJointMatrixTexture()
        {
            if (BoneCount <= 0)
                return;

            // Calculate texture size to hold all joint matrices
            // Each joint needs 2 mat4 (transform + normal) = 32 floats = 8 vec4 (RGBA)
            int width = (int)Math.Ceiling(Math.Sqrt(BoneCount * 8));
            JointTextureWidth = width;

            // Create sampler with NEAREST filtering and CLAMP_TO_EDGE wrapping
            JointMatrixSampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = sg_filter.SG_FILTER_NEAREST,
                mag_filter = sg_filter.SG_FILTER_NEAREST,
                wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                label = $"joint-matrix-sampler-{Name}"
            });

            // Create empty stream texture (no initial data allowed with stream_update)
            JointMatrixTexture = sg_make_image(new sg_image_desc
            {
                width = width,
                height = width,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA32F,
                usage = new sg_image_usage { stream_update = true }, // Allow per-frame updates
                label = $"joint-matrix-texture-{Name}"
            });
            
            // Create view once for the joint texture
            JointMatrixView = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = JointMatrixTexture },
                label = $"joint-matrix-view-{Name}"
            });
        }
        
        /// <summary>
        /// Update this character's joint matrix texture with current bone matrices.
        /// Packs transform and normal matrices for each joint into RGBA32F format.
        /// Only uploads ONCE per frame (subsequent calls in the same frame are no-ops).
        /// </summary>
        public unsafe void UpdateJointMatrixTexture()
        {
            if (!UsesTextureSkinning || JointMatrixTexture.id == 0)
                return;
            
            // CRITICAL: Only upload once per frame (Sokol validation rule)
            if (textureUpdatedThisFrame)
                return;
            
            textureUpdatedThisFrame = true;

            var boneMatrices = GetBoneMatrices();
            int width = JointTextureWidth;

            // Allocate float array: width² × 4 (RGBA)
            int texelCount = width * width;

            if (jointTextureData == null || jointTextureData.Length != texelCount * 4)
            {
                jointTextureData = new float[texelCount * 4];
            }

            // Initialize to zero
            Array.Clear(jointTextureData, 0, jointTextureData.Length);
            
            // Only update as many joints as we have space for
            int maxJoints = Math.Min(BoneCount, texelCount / 8);
            for (int i = 0; i < maxJoints; i++)
            {
                Matrix4x4 jointMatrix = boneMatrices[i];
                
                // Store transform matrix at offset i*32 (4 vec4 = 16 floats)
                CopyMatrix4x4ToFloatArray(jointMatrix, jointTextureData, i * 32);
                
                // Store same matrix for normals at offset i*32 + 16
                CopyMatrix4x4ToFloatArray(jointMatrix, jointTextureData, i * 32 + 16);
            }
            
            // Upload to GPU
            fixed (float* ptr = jointTextureData)
            {
                var imageData = new sg_image_data();
                imageData.mip_levels[0].ptr = ptr;
                imageData.mip_levels[0].size = (nuint)(jointTextureData.Length * sizeof(float));
                
                sg_update_image(JointMatrixTexture, in imageData);
            }
        }
        
        /// <summary>
        /// Copies a Matrix4x4 into a float array in ROW-MAJOR order for texture storage.
        /// </summary>
        private static void CopyMatrix4x4ToFloatArray(Matrix4x4 mat, float[] arr, int offset)
        {
            // Row-major order (don't transpose) - texelFetch reads vec4 as matrix rows
            arr[offset + 0] = mat.M11; arr[offset + 1] = mat.M12; arr[offset + 2] = mat.M13; arr[offset + 3] = mat.M14;
            arr[offset + 4] = mat.M21; arr[offset + 5] = mat.M22; arr[offset + 6] = mat.M23; arr[offset + 7] = mat.M24;
            arr[offset + 8] = mat.M31; arr[offset + 9] = mat.M32; arr[offset + 10] = mat.M33; arr[offset + 11] = mat.M34;
            arr[offset + 12] = mat.M41; arr[offset + 13] = mat.M42; arr[offset + 14] = mat.M43; arr[offset + 15] = mat.M44;
        }
        
        /// <summary>
        /// Cleanup GPU resources
        /// </summary>
        public void Dispose()
        {
            if (JointMatrixTexture.id != 0)
            {
                sg_destroy_image(JointMatrixTexture);
                JointMatrixTexture = default;
            }
            
            if (JointMatrixView.id != 0)
            {
                sg_destroy_view(JointMatrixView);
                JointMatrixView = default;
            }
            
            if (JointMatrixSampler.id != 0)
            {
                sg_destroy_sampler(JointMatrixSampler);
                JointMatrixSampler = default;
            }
        }
    }
}
