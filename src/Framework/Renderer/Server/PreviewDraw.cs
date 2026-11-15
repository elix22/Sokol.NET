using System.Numerics;

namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>One mesh of a preview (RenderingServer.SubmitPreview, §9.2): a mesh by its key ("model.glb#m0p0",
    /// "prim:sphere"), the material it is drawn with (null: its sub-meshes' own) and where.</summary>
    public readonly struct PreviewDraw
    {
        public readonly string Mesh;
        public readonly string? Material;
        public readonly Matrix4x4 Model;
        public PreviewDraw(string mesh, string? material, in Matrix4x4 model) { Mesh = mesh; Material = material; Model = model; }
    }
}
