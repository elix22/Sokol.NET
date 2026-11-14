using System.Collections.Generic;
using System.Numerics;

namespace GameEditor.Framework.ECS.Components
{
    /// <summary>
    /// A Light Probe Group (M8b, docs BAKED_GI_DESIGN.md §4; the reference editor's): probe positions, local to the entity.
    /// Generate Lighting bakes the light at each; objects that are not lightmapped take their ambient from them. Block
    /// "LightProbeGroup" {"P": [[x, y, z], ...]}.
    /// </summary>
    public struct LightProbeGroup
    {
        public List<Vector3>? Positions;

        /// <summary>A new group: a cube of 8 probes, 1 m apart (the reference editor's).</summary>
        public static LightProbeGroup Default()
        {
            var p = new List<Vector3>(8);
            for (int i = 0; i < 8; i++) p.Add(new Vector3((i & 1) != 0 ? 0.5f : -0.5f, (i & 2) != 0 ? 0.5f : -0.5f, (i & 4) != 0 ? 0.5f : -0.5f));
            return new LightProbeGroup { Positions = p };
        }
    }
}
