using System;
using System.Numerics;
using static Sokol.SG;
using static Sokol.Utils;

namespace GameEditor.Framework.Renderer.Server.Resources
{
    /// <summary>
    /// 80 B per-vertex layout for the <c>pbr_skinning</c> pipeline (vertex buffer slot 0):
    /// position(12) + normal(12) + uv(8) + tangent(16) + joints(16) + weights(16).
    /// Joint indices are stored as floats — the shader reads them via <c>int(joints_0.x)</c>.
    /// </summary>
    public struct SkinnedVertex
    {
        public Vector3 Position; // @0
        public Vector3 Normal;   // @12
        public Vector2 Uv;       // @24  (TEXCOORD_0)
        public Vector4 Tangent;  // @32
        public Vector4 Joints;   // @48  (4 joint indices, as floats)
        public Vector4 Weights;  // @64  (4 blend weights)
        public Vector2 Uv1;      // @80  (TEXCOORD_1 — 2nd UV set; (0,0) if absent)

        public const int SizeBytes = 12 + 12 + 8 + 16 + 16 + 16 + 8; // 88 B
    }

    /// <summary>
    /// GPU buffers for one skinned primitive. Unlike <see cref="MeshResource"/> (instanced,
    /// 48 B, ref-counted), a skinned mesh is drawn directly per character with its own bone
    /// matrices, so it is owned 1:1 by its renderer and not shared through MeshRegistry.
    /// </summary>
    public sealed class SkinnedMesh
    {
        public sg_buffer VertexBuffer;
        public sg_buffer IndexBuffer;
        public int       IndexCount;
        public Aabb      LocalBounds;

        // ── Morph targets (blend shapes) ───────────────────────────────────────────────────────
        // Set by the glTF importer when this primitive has morph targets. The displacement texture is
        // immutable (built once at load); only the per-frame weights vary. MorphNodeIndex keys the
        // animator's per-node weight lookup; StaticMorphWeights is the fallback when no animation drives
        // them. A morph-only mesh (no skin) rides this same skinned draw path with identity skinning.
        public sg_image  MorphImage;
        public sg_view   MorphView;
        public int       MorphTargetCount;          // active targets (≤ 8)
        public int       MorphNodeIndex = -1;       // CGltfSkinExtractor node index (for animated weights)
        public float[]?  StaticMorphWeights;        // node/mesh static weights when no animation

        /// <summary>True when this primitive carries morph-target displacement data.</summary>
        public bool HasMorph => MorphView.id != 0 && MorphTargetCount > 0;

        // ── Index LODs: each level a range of the index buffer, over the same vertices ──────────
        /// <summary>Level k+1's first index and count in <see cref="IndexBuffer"/>, and its error in the mesh's units.</summary>
        public int[] LodStarts = Array.Empty<int>();
        public int[] LodCounts = Array.Empty<int>();
        public float[] LodErrors = Array.Empty<float>();
        /// <summary>The range the next draw takes (the renderer chooses the level just before it draws).</summary>
        public int DrawStart;
        public int DrawCount => _drawCount > 0 ? _drawCount : IndexCount;
        private int _drawCount;

        /// <summary>Draws level <paramref name="level"/> from now on (0: the full mesh).</summary>
        public void UseLevel(int level)
        {
            if (level <= 0 || level > LodStarts.Length) { DrawStart = 0; _drawCount = 0; return; }
            DrawStart = LodStarts[level - 1];
            _drawCount = LodCounts[level - 1];
        }

        public static unsafe SkinnedMesh Create(SkinnedVertex[] vertices, uint[] indices, in Aabb bounds, string label, MeshLods? lods = null)
        {
            var m = new SkinnedMesh { IndexCount = indices.Length, LocalBounds = bounds };
            if (lods is { Indices.Length: > 0 })
            {
                int total = indices.Length;
                foreach (var l in lods.Indices) total += l.Length;
                var all = new uint[total];
                indices.CopyTo(all, 0);
                int at = indices.Length;
                m.LodStarts = new int[lods.Indices.Length];
                m.LodCounts = new int[lods.Indices.Length];
                m.LodErrors = new float[lods.Indices.Length];
                for (int k = 0; k < lods.Indices.Length; k++)
                {
                    lods.Indices[k].CopyTo(all, at);
                    m.LodStarts[k] = at; m.LodCounts[k] = lods.Indices[k].Length;
                    m.LodErrors[k] = k < lods.Errors.Length ? lods.Errors[k] : 0f;
                    at += lods.Indices[k].Length;
                }
                indices = all;
            }
            m.VertexBuffer = sg_make_buffer(new sg_buffer_desc
            {
                data  = SG_RANGE(vertices),
                label = label + "-skin-vbuf",
            });
            m.IndexBuffer = sg_make_buffer(new sg_buffer_desc
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data  = SG_RANGE(indices),
                label = label + "-skin-ibuf",
            });
            return m;
        }

        public void Destroy()
        {
            if (VertexBuffer.id != 0) { sg_destroy_buffer(VertexBuffer); VertexBuffer = default; }
            if (IndexBuffer.id  != 0) { sg_destroy_buffer(IndexBuffer);  IndexBuffer  = default; }
            if (MorphView.id    != 0) { sg_destroy_view(MorphView);      MorphView    = default; }
            if (MorphImage.id   != 0) { sg_destroy_image(MorphImage);    MorphImage   = default; }
        }
    }
}
