using System.Numerics;
using System.Runtime.InteropServices;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    /// <summary>
    /// Discriminates light types. Values must match the integer stored in GpuLight.PositionType.w.
    /// </summary>
    public enum LightType : int
    {
        Directional = 0,
        Point       = 1,
        Spot        = 2,
    }

    /// <summary>
    /// CPU-side light value — mirrors <c>LightComponent</c> enriched with world-space position/direction.
    /// Collected per frame by gathering ECS entities; written into LightBuffer before SubmitView.
    /// </summary>
    public readonly struct Light
    {
        public readonly LightType Type;
        public readonly Vector3   Position;     // world-space origin (ignored for directional)
        public readonly Vector3   Direction;    // world-space unit direction (toward the light)
        public readonly Vector3   Color;        // linear RGB
        public readonly float     Intensity;
        public readonly float     Range;        // attenuation radius
        public readonly float     InnerAngle;   // spot: inner cone half-angle (radians)
        public readonly float     OuterAngle;   // spot: outer cone half-angle (radians)
        public readonly int       ShadowIndex;  // -1 = no shadow, otherwise atlas/cube slot

        public Light(LightType type, Vector3 position, Vector3 direction,
                     Vector3 color, float intensity, float range,
                     float innerAngle = 0f, float outerAngle = 0f,
                     int shadowIndex = -1)
        {
            Type       = type;
            Position   = position;
            Direction  = direction;
            Color      = color;
            Intensity  = intensity;
            Range      = range;
            InnerAngle = innerAngle;
            OuterAngle = outerAngle;
            ShadowIndex = shadowIndex;
        }
    }

    /// <summary>
    /// 64-byte GPU light record aligned to std140 rules.
    /// The layout here must match the <c>GpuLight</c> struct in <c>blinn_phong.glsl</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GpuLight
    {
        public readonly Vector4 PositionType;   // xyz = position; w = (float)LightType
        public readonly Vector4 DirectionRange; // xyz = direction; w = range
        public readonly Vector4 ColorIntensity; // xyz = color; w = intensity
        public readonly Vector4 SpotShadow;     // x = cos(innerAngle); y = cos(outerAngle);
                                                // z = shadowIndex (-1 = no shadow); w = 1: the shadowmask's sun

        private GpuLight(Vector4 positionType, Vector4 directionRange, Vector4 colorIntensity, Vector4 spotShadow)
        {
            PositionType = positionType; DirectionRange = directionRange; ColorIntensity = colorIntensity; SpotShadow = spotShadow;
        }

        /// <summary>This light marked as the sun whose shadow on lightmapped surfaces the bake's shadowmask holds.</summary>
        public GpuLight WithShadowmask() => new(PositionType, DirectionRange, ColorIntensity, new Vector4(SpotShadow.X, SpotShadow.Y, SpotShadow.Z, 1f));

        public GpuLight(in Light l)
        {
            PositionType   = new Vector4(l.Position,  (float)l.Type);
            DirectionRange = new Vector4(l.Direction, l.Range);
            ColorIntensity = new Vector4(l.Color,     l.Intensity);
            SpotShadow     = new Vector4(
                MathF.Cos(l.InnerAngle),
                MathF.Cos(l.OuterAngle),
                l.ShadowIndex,
                0f);
        }
    }
}
