// QualityLevel.cs — G8.5 (examples/GameEditor/docs/QUALITY_TIERS_DESIGN.md): a named quality level of a project, kept in
// config.json's "quality" block — the reference editor's quality levels. Its values set the one renderer (Quality.Apply).

using System.Collections.Generic;

namespace GameEditor.Framework.Core
{
    /// <summary>The targets a level is the default of.</summary>
    public enum QualityTarget { Desktop, Android, Ios, Web }

    public sealed class QualityLevel
    {
        public string Name             { get; set; } = "High";
        /// <summary>"forward+" or "forward" (RenderingServer.PathOf).</summary>
        public string RenderingPath    { get; set; } = "forward+";
        /// <summary>A view's light limit (Forward never over 32, Forward+ never over the GPU's).</summary>
        public int    MaxLights        { get; set; } = 256;
        /// <summary>The directional shadow's cascades: 1 or 4.</summary>
        public int    Cascades         { get; set; } = 1;
        /// <summary>The shadow atlas's slice, in pixels (a point light's cube faces: a quarter).</summary>
        public int    ShadowResolution { get; set; } = 2048;
        /// <summary>Spot and point lights cast shadows.</summary>
        public bool   PunctualShadows  { get; set; } = true;
        /// <summary>PCF taps: 1, 9 or 25.</summary>
        public int    SoftShadows      { get; set; } = 9;
        /// <summary>An HDR target when the Environment asks for a tone curve or an exposure (else the in-shader fallback).</summary>
        public bool   Hdr              { get; set; } = true;
        /// <summary>The 3D scene's pixels, a share of the view's: 0.5–1.</summary>
        public float  RenderScale      { get; set; } = 1f;

        public QualityLevel Clone() => (QualityLevel)MemberwiseClone();

        /// <summary>
        /// A new project's levels: High is the desktop picture of before the levels, Medium a phone's (Forward+ is limited
        /// to 32 lights on a phone and in a browser anyway), Low draws less.
        /// </summary>
        public static List<QualityLevel> Defaults() => new()
        {
            new QualityLevel { Name = "High" },
            new QualityLevel { Name = "Medium", MaxLights = 32 },
            new QualityLevel { Name = "Low", RenderingPath = "forward", MaxLights = 8, ShadowResolution = 1024,
                               PunctualShadows = false, SoftShadows = 1, Hdr = false, RenderScale = 0.75f },
        };

        /// <summary>A new project's default level per target (desktop, Android, iOS, web).</summary>
        public static string DefaultFor(QualityTarget t) => t == QualityTarget.Desktop ? "High" : "Medium";
    }
}
