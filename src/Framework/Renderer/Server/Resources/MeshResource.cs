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
        public string    MaterialName = "";
        /// <summary>Pre-computed registry key ("mtllib#materialName"). Empty when either part is absent.
        /// Read by RenderingServer.ResolveSubMeshMaterial — avoids per-frame string allocation.</summary>
        public string    MaterialKey  = "";

        public void Destroy()
        {
            if (VertexBuffer.id != 0) { sg_destroy_buffer(VertexBuffer); VertexBuffer = default; }
            if (IndexBuffer.id  != 0) { sg_destroy_buffer(IndexBuffer);  IndexBuffer  = default; }
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
