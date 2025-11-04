using System;
using System.Collections.Generic;
using System.Numerics;
using static Sokol.SG;
using static Sokol.Utils;
using SharpGLTF.Schema2;
using static cgltf_sapp_shader_cs_cgltf.Shaders;

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
        public BoundingBox Bounds;

        // Material properties
        public Vector4 BaseColorFactor = Vector4.One;
        public float MetallicFactor = 1.0f;
        public float RoughnessFactor = 1.0f;
        public Vector3 EmissiveFactor = Vector3.Zero;
        public float EmissiveStrength = 1.0f;  // KHR_materials_emissive_strength extension
        
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

        // Clearcoat properties (KHR_materials_clearcoat)
        public float ClearcoatFactor = 0.0f;  // 0.0 = no clearcoat, 1.0 = full clearcoat
        public float ClearcoatRoughness = 0.0f;  // Roughness of clearcoat layer (usually very low for glossy coating)

        // Texture transform for normal map (KHR_texture_transform)
        // Allows tiling the normal texture to increase detail
        public Vector2 NormalTexOffset = Vector2.Zero;
        public float NormalTexRotation = 0.0f;  // Rotation in radians
        public Vector2 NormalTexScale = Vector2.One;
        
        // Normal map scale (strength of normal perturbation)
        public float NormalMapScale = 1.0f;  // 1.0 = full strength, 0.2 = subtle (like car paint)

        private static Texture? _defaultWhiteTexture;
        private static Texture? _defaultNormalTexture;
        private static Texture? _defaultBlackTexture;
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
                _defaultWhiteTexture = new Texture(whitePixel, 1, 1, "default-white-texture");
            }
            return _defaultWhiteTexture;
        }

        private static unsafe Texture GetDefaultNormalTexture()
        {
            if (_defaultNormalTexture == null)
            {
                // Normal map default: (0.5, 0.5, 1.0) in RGB = (128, 128, 255, 255)
                byte* normalPixel = stackalloc byte[4] { 128, 128, 255, 255 };
                _defaultNormalTexture = new Texture(normalPixel, 1, 1, "default-normal-texture");
            }
            return _defaultNormalTexture;
        }

        private static unsafe Texture GetDefaultBlackTexture()
        {
            if (_defaultBlackTexture == null)
            {
                byte* blackPixel = stackalloc byte[4] { 0, 0, 0, 255 };
                _defaultBlackTexture = new Texture(blackPixel, 1, 1, "default-black-texture");
            }
            return _defaultBlackTexture;
        }

        public void Draw(sg_pipeline pipeline, sg_view screenView = default, sg_sampler screenSampler = default, cgltf_transmission_params_t? transmissionParams = null)
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

            // 5: screen_tex (for refraction in transmission materials)
            // Use pre-created view if provided, otherwise bind default texture
            if (screenView.id != 0 && screenSampler.id != 0)
            {
                bind.views[5] = screenView;  // Use pre-created view (created once in Init)
                bind.samplers[5] = screenSampler;
            }
            else
            {
                // Use default black texture when screen view not provided
                var defaultTex = GetDefaultBlackTexture();
                bind.views[5] = defaultTex.View;
                bind.samplers[5] = defaultTex.Sampler;
            }

            sg_apply_bindings(bind);
            
            // Apply transmission uniforms AFTER bindings but BEFORE draw (sokol validation requirement)
            if (transmissionParams.HasValue)
            {
                cgltf_transmission_params_t tp = transmissionParams.Value;
                sg_apply_uniforms(UB_cgltf_transmission_params, SG_RANGE(ref tp));
            }
            
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
