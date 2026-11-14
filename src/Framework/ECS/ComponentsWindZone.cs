using System.Numerics;

namespace GameEditor.Framework.ECS.Components
{
    /// <summary>A Wind Zone's kinds: wind along the entity's forward everywhere, or outward from its centre within its radius.</summary>
    public enum WindZoneMode : byte { Directional, Spherical }

    /// <summary>
    /// A Wind Zone (G8.8 Q7, examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §6.8; the reference editor's): wind that pushes
    /// the particles of systems with External Forces on. Block "WindZone". Directional: <see cref="Main"/> metres a second
    /// squared along the entity's forward (+Z). Spherical: outward from its position, fading to nothing at <see cref="Radius"/>.
    /// A pulse makes Main × (1 + <see cref="PulseMagnitude"/> × sin(2π × <see cref="PulseFrequency"/> × time)); turbulence adds a
    /// smooth noise of that strength.
    /// </summary>
    public struct WindZone
    {
        public WindZoneMode Mode;
        public float Radius;
        public float Main;
        public float Turbulence;
        public float PulseMagnitude;
        public float PulseFrequency;

        public static WindZone Default => new() { Mode = WindZoneMode.Directional, Radius = 20f, Main = 1f, Turbulence = 1f, PulseMagnitude = 0.5f, PulseFrequency = 0.01f };
    }
}
