using System;
using System.Numerics;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.Resources
{
    /// <summary>
    /// Per-sub-mesh GPU buffers and metadata.
    /// Ref-counted by MeshRegistry; destroyed when ref-count reaches zero.
    /// </summary>
    public sealed class MeshSubResource
    {
        public sg_buffer VertexBuffer;
        public sg_buffer IndexBuffer;
        public int       IndexCount;
        /// <summary>Its first index in <see cref="IndexBuffer"/>: 0, or its place in buffers it shares with other meshes
        /// (<see cref="SharedBuffers"/>: a bake's lightmapped meshes, one buffer pair for all).</summary>
        public int       IndexStart;
        /// <summary>The buffers are shared (their owner frees them, not this sub-mesh).</summary>
        public bool      SharedBuffers;
        /// <summary>
        /// The index buffer the shadow passes draw with: each vertex replaced by the first
        /// vertex at its position, so a seam's copies are one vertex to a shadow and the GPU's vertex cache reuses them.
        /// Same length and ranges as <see cref="IndexBuffer"/>; 0: the shadows draw with <see cref="IndexBuffer"/>.
        /// </summary>
        public sg_buffer ShadowIndexBuffer;
        /// <summary>The vertex buffer holds <see cref="CompactVertex"/> (28 B), not <see cref="ObjVertex"/> (56 B) .</summary>
        public bool      Compact;
        public string    MaterialName = "";
        /// <summary>Pre-computed registry key ("mtllib#materialName"). Empty when either part is absent.
        /// Read by RenderingServer.ResolveSubMeshMaterial — avoids per-frame string allocation.</summary>
        public string    MaterialKey  = "";

        public void Destroy()
        {
            if (SharedBuffers) { VertexBuffer = default; IndexBuffer = default; ShadowIndexBuffer = default; return; }
            if (VertexBuffer.id != 0) { sg_destroy_buffer(VertexBuffer); VertexBuffer = default; }
            if (IndexBuffer.id  != 0) { sg_destroy_buffer(IndexBuffer);  IndexBuffer  = default; }
            if (ShadowIndexBuffer.id != 0) { sg_destroy_buffer(ShadowIndexBuffer); ShadowIndexBuffer = default; }
        }
    }

    /// <summary>
    /// Loaded mesh with 1..N sub-meshes, an AABB, and a registry-assigned numeric id.
    /// </summary>
    public sealed class MeshResource
    {
        /// <summary>16-bit id assigned by MeshRegistry (0 = invalid).</summary>
        public ushort Id;

        /// <summary>Absolute or asset-relative file path (used as registry key).</summary>
        public string SourcePath = "";

        /// <summary>MTL library referenced by the OBJ (may be empty).</summary>
        public string MtlLib = "";

        /// <summary>One entry per usemtl group in the OBJ.</summary>
        public MeshSubResource[] SubMeshes = Array.Empty<MeshSubResource>();

        /// <summary>Local-space AABB computed during load.</summary>
        public Aabb LocalBounds;

        /// <summary>
        /// Optional LOD ladder — additional mesh levels ordered coarsest-to-finest
        /// starting at LOD 1.  <c>LodSlots[0]</c> = LOD 1 (one step coarser than this
        /// mesh), <c>LodSlots[1]</c> = LOD 2, etc.  Null or empty means no additional
        /// LOD levels exist; the draw walk falls back to the base mesh for any index &gt; 0.
        ///
        /// Populated by the glTF importer (M4).  The OBJ loader leaves this empty.
        /// </summary>
        public MeshResource?[] LodSlots = Array.Empty<MeshResource?>();

        /// <summary>
        /// A level of an index LOD: how far its surface is from the full mesh's, in the
        /// mesh's own units (0 for the full mesh). A <see cref="LodSlots"/> entry made from the file's levels shares the full
        /// mesh's buffers (its sub-meshes' <see cref="MeshSubResource.SharedBuffers"/>, a range of the index buffer).
        /// </summary>
        public float LodError;

        /// <summary>The mesh's levels came from its file (index LODs): the renderer chooses among them by their error when the
        /// entity has no LOD Group.</summary>
        public bool HasIndexLods;

        /// <summary>Its vertex buffers hold <see cref="CompactVertex"/>: its pipelines are the compact ones.</summary>
        public bool Compact => SubMeshes.Length > 0 && SubMeshes[0].Compact;

        /// <summary>The sub-meshes' vertices and indices as uploaded, kept only while <see cref="MeshRegistry.KeepCpuCopies"/>
        /// is on (the editor: the lightmap baker reads them, M8a); null in a game.</summary>
        public ObjSubMesh[]? Cpu;

        /// <summary>Reference count maintained by MeshRegistry.</summary>
        public int RefCount;

        public void Destroy()
        {
            foreach (var sm in SubMeshes) sm.Destroy();
            SubMeshes = Array.Empty<MeshSubResource>();
        }
    }
}
