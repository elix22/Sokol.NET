using System;
using System.Diagnostics;
using System.Numerics;
using SharpGLTF.Schema2;
using static Sokol.SG;
using static Sokol.SLog;

namespace Sokol
{
    public class SharpGltfNode
    {
        public Matrix4x4 Transform;
        public int MeshIndex = -1;  // Index into SharpGltfModel.Meshes
        public string? NodeName = null;  // Name of the original glTF node (for matching with animations)
        public SharpGLTF.Schema2.Node? CachedGltfNode = null;  // Cached reference to glTF node (for animation optimization)
        public bool HasAnimation = false;  // Pre-calculated flag to avoid expensive LINQ calls
    }
}