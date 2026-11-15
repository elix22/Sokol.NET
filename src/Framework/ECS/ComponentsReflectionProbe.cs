using System.Numerics;

namespace GameEditor.Framework.ECS.Components
{
    /// <summary>
    /// A Reflection Probe (M8c, docs BAKED_GI_DESIGN.md §4.4; the reference editor's): a cube of what is around it, captured by
    /// Generate Lighting from the entity's position; objects whose bounds' centre is inside its box reflect it instead of
    /// the sky. Box Projection: the reflection corrected against the box. Block "ReflectionProbe".
    /// </summary>
    public struct ReflectionProbe
    {
        public Vector3 Size;
        public Vector3 Offset;
        public bool BoxProjection;
        public float Intensity;
        public float Importance;
        public int Resolution;

        public static ReflectionProbe Default => new() { Size = new Vector3(10f), Intensity = 1f, Importance = 1f, Resolution = 128 };
    }
}
