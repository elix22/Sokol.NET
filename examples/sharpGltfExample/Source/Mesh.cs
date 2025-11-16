
using System.Numerics;
using static Sokol.SG;
using static Sokol.Utils;
using SharpGLTF.Schema2;

namespace Sokol
{
    public class Mesh
    {
        public sg_buffer VertexBuffer;
        public sg_buffer IndexBuffer;
        public int VertexCount;
        public int IndexCount;
        public sg_index_type IndexType;  // Track whether we're using 16-bit or 32-bit indices
        public List<Texture?> Textures = new List<Texture?>();
        public bool HasSkinning;
        public int SkinIndex = -1;  // Which skin this mesh belongs to (-1 = no skin/static)
        public BoundingBox Bounds;

        // Morph target properties
        public bool HasMorphTargets = false;
        public MeshPrimitive? GltfPrimitive = null;  // Keep reference to glTF primitive for morph target access
        public int MorphTargetCount = 0;

        // Material properties
        public Vector4 BaseColorFactor = Vector4.One;
        public float MetallicFactor = 1.0f;
        public float RoughnessFactor = 1.0f;
        public Vector3 EmissiveFactor = Vector3.Zero;
        public float EmissiveStrength = 1.0f;  // KHR_materials_emissive_strength extension
        public float OcclusionStrength = 1.0f;  // Occlusion texture strength (1.0 = full effect, 0.0 = no effect)
        
        // Alpha properties
        public AlphaMode AlphaMode = AlphaMode.OPAQUE;
        public float AlphaCutoff = 0.5f;
        
        // Double-sided rendering (glTF doubleSided property)
        public bool DoubleSided = false;

        // Glass material properties
        public float IOR = 1.5f;  // Index of Refraction (KHR_materials_ior, default: 1.5 for glass)
        
        // Transmission properties (KHR_materials_transmission)
        public float TransmissionFactor = 0.0f;  // 0.0 = opaque, 1.0 = fully transparent/refractive
        public int TransmissionTextureIndex = -1;  // Index into Textures list, -1 = no texture

        // Volume properties (KHR_materials_volume) - Beer's Law absorption
        public Vector3 AttenuationColor = new Vector3(1.0f, 1.0f, 1.0f);  // RGB color, white = no tint
        public float AttenuationDistance = float.MaxValue;  // Distance at which color reaches AttenuationColor
        public float ThicknessFactor = 0.0f;  // Thickness of volume in world units
        public int ThicknessTextureIndex = -1;  // Index into Textures list, -1 = no texture
        public int ThicknessTexCoord = 0;  // Which UV channel to use (TEXCOORD_0 or TEXCOORD_1)
        
        // Runtime-determined binding slot for thickness texture (calculated in Draw())
        // This allows thickness to use any free slot (0-11) to avoid conflicts with other textures
        public int ThicknessBindingSlot = -1;  // -1 = not bound, 0-11 = actual slot

        // Clearcoat properties (KHR_materials_clearcoat)
        public float ClearcoatFactor = 0.0f;  // 0.0 = no clearcoat, 1.0 = full clearcoat
        public float ClearcoatRoughness = 0.0f;  // Roughness of clearcoat layer (usually very low for glossy coating)

        // Texture transforms (KHR_texture_transform) for each texture type
        // Allows tiling/offsetting/rotating textures independently
        public Vector2 BaseColorTexOffset = Vector2.Zero;
        public float BaseColorTexRotation = 0.0f;
        public Vector2 BaseColorTexScale = Vector2.One;
        public bool HasBaseColorTexTransform = false;  // Optimization: skip transform if identity
        
        public Vector2 MetallicRoughnessTexOffset = Vector2.Zero;
        public float MetallicRoughnessTexRotation = 0.0f;
        public Vector2 MetallicRoughnessTexScale = Vector2.One;
        public bool HasMetallicRoughnessTexTransform = false;  // Optimization: skip transform if identity
        
        public Vector2 NormalTexOffset = Vector2.Zero;
        public float NormalTexRotation = 0.0f;  // Rotation in radians
        public Vector2 NormalTexScale = Vector2.One;
        public bool HasNormalTexTransform = false;  // Optimization: skip transform if identity
        
        public Vector2 OcclusionTexOffset = Vector2.Zero;
        public float OcclusionTexRotation = 0.0f;
        public Vector2 OcclusionTexScale = Vector2.One;
        public bool HasOcclusionTexTransform = false;  // Optimization: skip transform if identity
        
        public Vector2 EmissiveTexOffset = Vector2.Zero;
        public float EmissiveTexRotation = 0.0f;
        public Vector2 EmissiveTexScale = Vector2.One;
        public bool HasEmissiveTexTransform = false;  // Optimization: skip transform if identity
        
        // Normal map scale (strength of normal perturbation)
        public float NormalMapScale = 1.0f;  // 1.0 = full strength, 0.2 = subtle (like car paint)

        // Texture coordinate set indices (which UV set each texture uses)
        public int BaseColorTexCoord = 0;  // TEXCOORD_0 by default
        public int MetallicRoughnessTexCoord = 0;
        public int NormalTexCoord = 0;
        public int OcclusionTexCoord = 0;
        public int EmissiveTexCoord = 0;
        public int TransmissionTexCoord = 0;  // Which UV channel transmission texture uses

        private static Texture? _defaultWhiteTexture;
        private static Texture? _defaultNormalTexture;
        private static Texture? _defaultBlackTexture;
        private static sg_view _defaultWhiteCubemapView;
        private static sg_sampler _defaultCubemapSampler;
        private static bool _firstDrawCall = true;  // Debug flag

        // Constructor for 16-bit indices (up to 65535 vertices)
        public Mesh(Vertex[] vertices, ushort[] indices, bool hasSkinning = false)
        {
            HasSkinning = hasSkinning;
            VertexCount = vertices.Length;
            IndexCount = indices.Length;
            IndexType = sg_index_type.SG_INDEXTYPE_UINT16;

            CalculateBounds(vertices);

            // Create vertex buffer
            VertexBuffer = sg_make_buffer(new sg_buffer_desc
            {
                data = SG_RANGE(vertices),
                label = "mesh-vertex-buffer"
            });

            // Create index buffer
            IndexBuffer = sg_make_buffer(new sg_buffer_desc
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(indices),
                label = "mesh-index-buffer"
            });
        }

        // Constructor for 32-bit indices (for meshes with > 65535 vertices)
        public Mesh(Vertex[] vertices, uint[] indices, bool hasSkinning = false)
        {
            HasSkinning = hasSkinning;
            VertexCount = vertices.Length;
            IndexCount = indices.Length;
            IndexType = sg_index_type.SG_INDEXTYPE_UINT32;

            CalculateBounds(vertices);

            // Create vertex buffer
            VertexBuffer = sg_make_buffer(new sg_buffer_desc
            {
                data = SG_RANGE(vertices),
                label = "mesh-vertex-buffer"
            });

            // Create index buffer (32-bit)
            IndexBuffer = sg_make_buffer(new sg_buffer_desc
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(indices),
                label = "mesh-index-buffer"
            });
        }

        private void CalculateBounds(Vertex[] vertices)
        {
            // Calculate bounding box from vertices
            if (vertices.Length > 0)
            {
                Vector3 min = vertices[0].Position;
                Vector3 max = vertices[0].Position;
                
                foreach (var vertex in vertices)
                {
                    min = Vector3.Min(min, vertex.Position);
                    max = Vector3.Max(max, vertex.Position);
                }
                
                Bounds = new BoundingBox(min, max);
            }
            else
            {
                Bounds = new BoundingBox(Vector3.Zero, Vector3.Zero);
            }
        }

        private static unsafe Texture GetDefaultWhiteTexture()
        {
            if (_defaultWhiteTexture == null)
            {
                byte* whitePixel = stackalloc byte[4] { 255, 255, 255, 255 };
                _defaultWhiteTexture = new Texture(whitePixel, 1, 1, "default-white-texture", SG.sg_pixel_format.SG_PIXELFORMAT_RGBA8);
            }
            return _defaultWhiteTexture;
        }

        private static unsafe Texture GetDefaultNormalTexture()
        {
            if (_defaultNormalTexture == null)
            {
                // Normal map default: (0.5, 0.5, 1.0) in RGB = (128, 128, 255, 255)
                byte* normalPixel = stackalloc byte[4] { 128, 128, 255, 255 };
                _defaultNormalTexture = new Texture(normalPixel, 1, 1, "default-normal-texture", SG.sg_pixel_format.SG_PIXELFORMAT_RGBA8);
            }
            return _defaultNormalTexture;
        }

        private static unsafe Texture GetDefaultBlackTexture()
        {
            if (_defaultBlackTexture == null)
            {
                byte* blackPixel = stackalloc byte[4] { 0, 0, 0, 255 };
                _defaultBlackTexture = new Texture(blackPixel, 1, 1, "default-black-texture", SG.sg_pixel_format.SG_PIXELFORMAT_RGBA8);
            }
            return _defaultBlackTexture;
        }

        private static unsafe void EnsureDefaultCubemap()
        {
            if (_defaultWhiteCubemapView.id != 0)
            {
                return; // Already created
            }

            // Create a 1x1 white cubemap (all 6 faces)
            const int FACE_SIZE = 1;
            const int BYTES_PER_PIXEL = 4;
            const int FACE_NUM_BYTES = FACE_SIZE * FACE_SIZE * BYTES_PER_PIXEL;
            const int NUM_FACES = 6;
            
            byte[] pixels = new byte[NUM_FACES * FACE_NUM_BYTES];
            // Fill all pixels with white (255, 255, 255, 255)
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = 255;
            }

            fixed (byte* pixelPtr = pixels)
            {
                var img = sg_make_image(new sg_image_desc
                {
                    type = sg_image_type.SG_IMAGETYPE_CUBE,
                    width = FACE_SIZE,
                    height = FACE_SIZE,
                    pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                    data = {
                        mip_levels = {
                            [0] = new sg_range { ptr = pixelPtr, size = (nuint)pixels.Length }
                        }
                    },
                    label = "default-white-cubemap"
                });

                _defaultWhiteCubemapView = sg_make_view(new sg_view_desc
                {
                    texture = { image = img },
                    label = "default-white-cubemap-view"
                });

                _defaultCubemapSampler = sg_make_sampler(new sg_sampler_desc
                {
                    min_filter = sg_filter.SG_FILTER_LINEAR,
                    mag_filter = sg_filter.SG_FILTER_LINEAR,
                    wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                    wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                    wrap_w = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                    label = "default-cubemap-sampler"
                });
            }
        }

        public void Draw(sg_pipeline pipeline, EnvironmentMap? environmentMap = null, bool useIBL = true,
                         sg_view screenView = default, sg_sampler screenSampler = default, 
                         sg_view jointMatrixView = default, sg_sampler jointMatrixSampler = default,
                         sg_view morphTargetView = default, sg_sampler morphTargetSampler = default)
        {
            if (IndexCount == 0)
            {
                return;
            }

            // Note: sg_apply_pipeline() should be called by the caller before this method
            // Don't call it here as it would invalidate uniforms applied before Draw()

            // Prepare bindings
            sg_bindings bind = default;
            bind.vertex_buffers[0] = VertexBuffer;
            bind.index_buffer = IndexBuffer;

            // Bind textures (use defaults if not available)
            // 0: base_color_tex
            var baseColorTex = Textures.Count > 0 && Textures[0] != null ? Textures[0] : GetDefaultWhiteTexture();
            bind.views[0] = baseColorTex.View;
            bind.samplers[0] = baseColorTex.Sampler;

            // 1: metallic_roughness_tex
            var metallicRoughnessTex = Textures.Count > 1 && Textures[1] != null ? Textures[1] : GetDefaultWhiteTexture();
            bind.views[1] = metallicRoughnessTex.View;
            bind.samplers[1] = metallicRoughnessTex.Sampler;

            // 2: normal_tex
            var normalTex = Textures.Count > 2 && Textures[2] != null ? Textures[2] : GetDefaultNormalTexture();
            bind.views[2] = normalTex.View;
            bind.samplers[2] = normalTex.Sampler;

            // 3: occlusion_tex
            var occlusionTex = Textures.Count > 3 && Textures[3] != null ? Textures[3] : GetDefaultWhiteTexture();
            bind.views[3] = occlusionTex.View;
            bind.samplers[3] = occlusionTex.Sampler;

            // 4: emissive_tex
            var emissiveTex = Textures.Count > 4 && Textures[4] != null ? Textures[4] : GetDefaultBlackTexture();
            bind.views[4] = emissiveTex.View;
            bind.samplers[4] = emissiveTex.Sampler;

            // Track which slots are already in use for dynamic thickness texture binding
            bool[] slotUsed = new bool[12]; // Slots 0-11
            slotUsed[0] = true; // BaseColor
            slotUsed[1] = true; // MetallicRoughness
            slotUsed[2] = true; // Normal
            slotUsed[3] = true; // Occlusion
            slotUsed[4] = true; // Emissive
            slotUsed[5] = useIBL; // GGX env (only used if IBL is enabled)
            slotUsed[6] = useIBL; // Lambertian env (only used if IBL is enabled)
            slotUsed[7] = useIBL; // GGX LUT (only used if IBL is enabled)
            slotUsed[8] = false; // Charlie env or Transmission texture (will be set later)
            slotUsed[9] = morphTargetView.id != 0; // Charlie LUT or morph targets
            slotUsed[10] = screenView.id != 0; // Transmission framebuffer (only if transmission is used)
            slotUsed[11] = jointMatrixView.id != 0; // Joints (only if skinning is enabled)

            // 5-8: IBL cubemap textures (GGX and Lambertian environment maps)
            // 7: GGX LUT texture (2D)
            // 8: Charlie environment cubemap or Transmission texture
            // 9: Charlie LUT texture (2D) or Morph targets (vertex shader)
            // 10: Transmission framebuffer texture (2D)
            // 11: Joints (vertex shader)
            
            // Bind IBL textures from EnvironmentMap, or use defaults
            // Check both the useIBL flag AND if environment map is loaded
            if (useIBL && environmentMap != null && environmentMap.IsLoaded)
            {
                // Binding 5: GGX (specular) environment cubemap
                bind.views[5] = environmentMap.SpecularCubemapView;
                bind.samplers[5] = environmentMap.CubemapSampler;
                
                // Binding 6: Lambertian (diffuse) environment cubemap
                bind.views[6] = environmentMap.DiffuseCubemapView;
                bind.samplers[6] = environmentMap.CubemapSampler;
                
                // Binding 7: GGX BRDF LUT (2D texture)
                bind.views[7] = environmentMap.GGX_LUTView;
                bind.samplers[7] = environmentMap.LUTSampler;
                
                // Binding 8: Charlie environment cubemap (for sheen)
                if (environmentMap.SheenCubemapView.id != 0)
                {
                    bind.views[8] = environmentMap.SheenCubemapView;
                    bind.samplers[8] = environmentMap.CubemapSampler;
                }
                else
                {
                    EnsureDefaultCubemap();
                    bind.views[8] = _defaultWhiteCubemapView;
                    bind.samplers[8] = _defaultCubemapSampler;
                }
                
                // Binding 9: Charlie LUT (2D texture) - will be overridden by morph targets if needed
                if (morphTargetView.id != 0 && morphTargetSampler.id != 0)
                {
                    // Morphing has priority over Charlie LUT
                    bind.views[9] = morphTargetView;
                    bind.samplers[9] = morphTargetSampler;
                }
                else if (environmentMap.Charlie_LUTView.id != 0)
                {
                    bind.views[9] = environmentMap.Charlie_LUTView;
                    bind.samplers[9] = environmentMap.LUTSampler;
                }
                else
                {
                    bind.views[9] = GetDefaultWhiteTexture().View;
                    bind.samplers[9] = GetDefaultWhiteTexture().Sampler;
                }
            }
            else
            {
                // No environment map - use default white cubemaps
                EnsureDefaultCubemap();
                
                // Bindings 5-6: IBL cubemaps
                bind.views[5] = _defaultWhiteCubemapView;
                bind.samplers[5] = _defaultCubemapSampler;
                bind.views[6] = _defaultWhiteCubemapView;
                bind.samplers[6] = _defaultCubemapSampler;
                
                // Bindings 7: GGX LUT
                var defaultWhite = GetDefaultWhiteTexture();
                bind.views[7] = defaultWhite.View;
                bind.samplers[7] = defaultWhite.Sampler;
                
                // Binding 8: Charlie environment cubemap
                bind.views[8] = _defaultWhiteCubemapView;
                bind.samplers[8] = _defaultCubemapSampler;
                
                // Binding 9: Charlie LUT or morph targets
                if (morphTargetView.id != 0 && morphTargetSampler.id != 0)
                {
                    bind.views[9] = morphTargetView;
                    bind.samplers[9] = morphTargetSampler;
                }
                else
                {
                    bind.views[9] = defaultWhite.View;
                    bind.samplers[9] = defaultWhite.Sampler;
                }
            }
            
            
            // Binding 10: Transmission framebuffer or default
            if (screenView.id != 0 && screenSampler.id != 0)
            {
                bind.views[10] = screenView;
                bind.samplers[10] = screenSampler;
                slotUsed[10] = true;
            }
            else
            {
                var defaultWhite = GetDefaultWhiteTexture();
                bind.views[10] = defaultWhite.View;
                bind.samplers[10] = defaultWhite.Sampler;
            }

            // Mark slot 8 if transmission texture will be bound there (happens later in code)
            if (TransmissionTextureIndex >= 0 && TransmissionTextureIndex < Textures.Count && Textures[TransmissionTextureIndex] != null)
            {
                slotUsed[8] = true;  // Transmission texture uses slot 8
            }

            // Dynamic thickness texture binding - find first available slot
            // KHR_materials_volume: thickness texture for Beer's law volume attenuation
            ThicknessBindingSlot = -1;  // Reset
            if (ThicknessTextureIndex >= 0 && ThicknessTextureIndex < Textures.Count && Textures[ThicknessTextureIndex] != null)
            {
                // Find first free slot
                // IMPORTANT: Avoid slots 5-6 (cubemap slots), prefer 2D texture slots
                // Safe 2D texture slots: 7 (GGX LUT), 8 (Transmission/Charlie), 9 (Charlie LUT), 10 (Transmission FB), 0-4 (standard PBR)
                int[] preferredSlots = { 7, 8, 9, 10, 11, 0, 1, 2, 3, 4 };
                foreach (int slot in preferredSlots)
                {
                    if (!slotUsed[slot])
                    {
                        ThicknessBindingSlot = slot;
                        slotUsed[slot] = true;
                        break;
                    }
                }

                if (ThicknessBindingSlot >= 0)
                {
                    var thicknessTex = Textures[ThicknessTextureIndex]!;
                    bind.views[ThicknessBindingSlot] = thicknessTex.View;
                    bind.samplers[ThicknessBindingSlot] = thicknessTex.Sampler;
                    
                    // Debug logging - always print for first frame
                    // Console.WriteLine($"[THICKNESS DEBUG]");
                    // Console.WriteLine($"  Thickness texture index: {ThicknessTextureIndex}");
                    // Console.WriteLine($"  Assigned to slot: {ThicknessBindingSlot}");
                    // Console.WriteLine($"  Transmission texture index: {TransmissionTextureIndex}");
                    // Console.WriteLine($"  Has transmission texture: {(TransmissionTextureIndex >= 0 && TransmissionTextureIndex < Textures.Count && Textures[TransmissionTextureIndex] != null)}");
                    // Console.WriteLine($"  Attenuation distance: {AttenuationDistance}");
                    // Console.WriteLine($"  Attenuation color: {AttenuationColor}");
                }
            }

            // Binding 8: Transmission texture (per-pixel transmission mask, RED channel)
            // Used by KHR_materials_transmission for selective transparency
            // Uses binding 8 (shared with Charlie for sheen - rarely used together)
            // NOTE: If thickness texture is already bound to slot 8, don't overwrite it!
            if (TransmissionTextureIndex >= 0 && TransmissionTextureIndex < Textures.Count && Textures[TransmissionTextureIndex] != null)
            {
                // Only bind if slot 8 is not already used by thickness
                if (ThicknessBindingSlot != 8)
                {
                    var transmissionTex = Textures[TransmissionTextureIndex]!;
                    bind.views[8] = transmissionTex.View;
                    bind.samplers[8] = transmissionTex.Sampler;
                }
            }
            else if (ThicknessBindingSlot != 8 && (environmentMap == null || !environmentMap.IsLoaded || environmentMap.Charlie_LUTView.id == 0))
            {
                // No transmission texture and no Charlie LUT - use default
                // But only if thickness is not using slot 8
                var defaultWhite = GetDefaultWhiteTexture();
                bind.views[8] = defaultWhite.View;
                bind.samplers[8] = defaultWhite.Sampler;
            }

            // Binding 11: Joint matrix texture for skinning (u_jointsSampler)
            // This is REQUIRED for skinned meshes - the shader expects this binding
            if (jointMatrixView.id != 0 && jointMatrixSampler.id != 0)
            {
                bind.views[11] = jointMatrixView;
                bind.samplers[11] = jointMatrixSampler;
            }
            else
            {
                // Even if not skinned, provide a default to avoid validation errors
                var defaultWhite = GetDefaultWhiteTexture();
                bind.views[11] = defaultWhite.View;
                bind.samplers[11] = defaultWhite.Sampler;
            }

            // Binding 9: Morph target texture for morphing (u_MorphTargetsSampler)
            // This is REQUIRED for morphed meshes - the shader expects this binding
            // NOTE: Shares slot 9 with CharlieLUT (sheen) - rarely used together (morphing is animation, sheen is fabric)
            if (morphTargetView.id != 0 && morphTargetSampler.id != 0)
            {
                bind.views[9] = morphTargetView;
                bind.samplers[9] = morphTargetSampler;
            }
            else if (environmentMap == null || !environmentMap.IsLoaded || environmentMap.Charlie_LUTView.id == 0)
            {
                // No morph targets and no Charlie LUT - use default
                var defaultWhite = GetDefaultWhiteTexture();
                bind.views[9] = defaultWhite.View;
                bind.samplers[9] = defaultWhite.Sampler;
            }

            sg_apply_bindings(bind);
            
            sg_draw(0, (uint)IndexCount, 1);
        }

        // Check if bounding box is visible in camera frustum
        public bool IsVisible(Matrix4x4 worldTransform, Matrix4x4 viewProjection)
        {
            // Transform bounding box to world space
            BoundingBox worldBounds = Bounds.Transform(worldTransform);
            
            // Extract frustum planes from view-projection matrix
            Matrix4x4 vp = viewProjection;
            
            // Left plane
            Vector4 left = new Vector4(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41);
            // Right plane  
            Vector4 right = new Vector4(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41);
            // Bottom plane
            Vector4 bottom = new Vector4(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42);
            // Top plane
            Vector4 top = new Vector4(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42);
            // Near plane
            Vector4 near = new Vector4(vp.M14 + vp.M13, vp.M24 + vp.M23, vp.M34 + vp.M33, vp.M44 + vp.M43);
            // Far plane
            Vector4 far = new Vector4(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43);
            
            // Normalize planes
            left = NormalizePlane(left);
            right = NormalizePlane(right);
            bottom = NormalizePlane(bottom);
            top = NormalizePlane(top);
            near = NormalizePlane(near);
            far = NormalizePlane(far);
            
            Vector4[] planes = { left, right, bottom, top, near, far };
            
            // Test bounding box against each plane
            foreach (var plane in planes)
            {
                Vector3 planeNormal = new Vector3(plane.X, plane.Y, plane.Z);
                float planeDistance = plane.W;
                
                // Find the positive vertex (furthest in the direction of the plane normal)
                Vector3 positiveVertex = new Vector3(
                    planeNormal.X >= 0 ? worldBounds.Max.X : worldBounds.Min.X,
                    planeNormal.Y >= 0 ? worldBounds.Max.Y : worldBounds.Min.Y,
                    planeNormal.Z >= 0 ? worldBounds.Max.Z : worldBounds.Min.Z
                );
                
                float distance = Vector3.Dot(planeNormal, positiveVertex) + planeDistance;
                
                // If positive vertex is outside this plane, box is completely outside frustum
                if (distance < 0)
                    return false;
            }
            
            return true;
        }
        
        private static Vector4 NormalizePlane(Vector4 plane)
        {
            Vector3 normal = new Vector3(plane.X, plane.Y, plane.Z);
            float length = normal.Length();
            if (length > 0.0001f)
            {
                return new Vector4(
                    plane.X / length,
                    plane.Y / length,
                    plane.Z / length,
                    plane.W / length
                );
            }
            return plane;
        }

        public void Dispose()
        {
            if (VertexBuffer.id != 0)
                sg_destroy_buffer(VertexBuffer);
            if (IndexBuffer.id != 0)
                sg_destroy_buffer(IndexBuffer);

            foreach (var texture in Textures)
                texture?.Dispose();

            Textures.Clear();
        }
    }
}
