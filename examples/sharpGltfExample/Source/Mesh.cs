using System;
using System.Collections.Generic;
using System.Numerics;
using static Sokol.SG;
using static Sokol.Utils;

namespace Sokol
{
    public class Mesh
    {
        public sg_buffer VertexBuffer;
        public sg_buffer IndexBuffer;
        public int VertexCount;
        public int IndexCount;
        public List<Texture?> Textures = new List<Texture?>();
        public bool HasSkinning;

        // Material properties
        public Vector4 BaseColorFactor = Vector4.One;
        public float MetallicFactor = 1.0f;
        public float RoughnessFactor = 1.0f;
        public Vector3 EmissiveFactor = Vector3.Zero;

        private static Texture? _defaultWhiteTexture;
        private static Texture? _defaultNormalTexture;
        private static Texture? _defaultBlackTexture;
        private static bool _firstDrawCall = true;  // Debug flag

        public Mesh(Vertex[] vertices, ushort[] indices, bool hasSkinning = false)
        {
            HasSkinning = hasSkinning;
            VertexCount = vertices.Length;
            IndexCount = indices.Length;

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

        public void Draw(sg_pipeline pipeline)
        {
            if (IndexCount == 0)
            {
                Console.WriteLine("[Mesh] Draw() called but IndexCount is 0, skipping");
                return;
            }
            
            // Debug output on first call
            if (_firstDrawCall)
            {
                Console.WriteLine($"[Mesh] Draw() called: VertexCount={VertexCount}, IndexCount={IndexCount}");
                Console.WriteLine($"[Mesh] VertexBuffer.id={VertexBuffer.id}, IndexBuffer.id={IndexBuffer.id}");
                _firstDrawCall = false;
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

            sg_apply_bindings(bind);
            sg_draw(0, (uint)IndexCount, 1);
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
