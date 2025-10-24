using System;
using System.IO;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using Assimp.Configs;
using static Sokol.SFetch;
using static Sokol.SDebugText;
using Assimp;

namespace Sokol
{
    public class Mesh
    {
        // Lazy-initialized default white texture for meshes without textures
        private static Texture? _defaultTexture = null;

        public Mesh( Vertex[] vertices, UInt16[] indices, List<Texture> textures)
        {

            Textures = textures;

            VertexBuffer = sg_make_buffer(new sg_buffer_desc()
            {
                data = SG_RANGE(vertices),
                label = "assimp-simple-vertex-buffer"
            });
            IndexBuffer = sg_make_buffer(new sg_buffer_desc()
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(indices),
                label = "assimp-simple-index-buffer"
            });

            VertexCount = vertices.Length;
            IndexCount = indices.Length;
        }       

        private static unsafe Texture GetDefaultTexture()
        {
            if (_defaultTexture == null)
            {
                // Create a 1x1 white texture as fallback for meshes without textures
                byte* whitePixel = stackalloc byte[4] { 255, 255, 255, 255 };
                _defaultTexture = new Texture(whitePixel, 1, 1, "default-white-texture");
            }
            return _defaultTexture;
        }

        public void Draw()
        {
            // Skip meshes with no indices
            if (IndexCount == 0)
            {
                return;
            }

            // Use default white texture if mesh has no textures
            Texture textureToUse;
            if (Textures.Count == 0 || Textures[0] == null || !Textures[0].IsValid)
            {
                textureToUse = GetDefaultTexture();
            }
            else
            {
                textureToUse = Textures[0];
            }

            sg_bindings bind = default;
            bind.vertex_buffers[0] = VertexBuffer;
            bind.index_buffer = IndexBuffer;
            bind.views[0] = textureToUse.View;
            bind.samplers[0] = textureToUse.Sampler;
            sg_apply_bindings(bind);
            sg_draw(0, (uint)IndexCount, 1);
        }



        public sg_buffer VertexBuffer;
        public sg_buffer IndexBuffer;

        public int VertexCount;
        public int IndexCount;

        public List<Texture> Textures = new List<Texture>();
    }
}


