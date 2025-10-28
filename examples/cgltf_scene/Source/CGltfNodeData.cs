using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sokol
{
    /// <summary>
    /// Node hierarchy data for CGLTF animation
    /// </summary>
    public struct CGltfNodeData
    {
        public Matrix4x4 Transformation;
        public string Name;
        public int ChildrenCount;
        public List<CGltfNodeData> Children;
    }
}
