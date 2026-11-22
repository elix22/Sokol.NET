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
        /// <summary>The directional shadow's cascades: 1 (one map around the casters) or 4 (fitted to what the camera sees).</summary>
        public int    Cascades         { get; set; } = 4;
        /// <summary>How far the cascades reach from the camera, world units (the sun's shadow fades out over its last tenth).</summary>
        public float  ShadowDistance   { get; set; } = 150f;
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
        /// <summary>Forward: the most spot / point lights an object is lit by - those of the view's most important that reach
        /// it (directional lights always); 0: every light of the view on every pixel. Forward+ is not limited (a pixel is
        /// lit by its cluster's lights).</summary>
        public int    PerObjectLights  { get; set; } = 0;
        /// <summary>A baked Mixed sun's shadow on lightmapped (static) objects comes from the bake's shadowmask; its real-time
        /// map holds only the moving casters (an empty one is not sampled). Moving objects get no shadow of the static ones.
        /// Off: every caster in real time (a shadowmask is not used).</summary>
        public bool   Shadowmask       { get; set; }
        /// <summary>A weak phone's shading: every surface lit by its baked light (or the sky's diffuse light), each light's
        /// diffuse with the sun's shadow, its occlusion, glow and haze - no specular highlights, no reflections, no
        /// clearcoat, no spot / point shadow maps. A Galaxy Tab A8 (Mali-G52) spent its frame on the full PBR of every
        /// surface (2026-11-17: 16 fps whatever else was turned off).</summary>
        public bool   SimpleShading    { get; set; }
        /// <summary>A static mesh's triangles and vertices put in the GPU's best order when it loads (vertex cache, overdraw,
        /// vertex fetch). The picture is the same.</summary>
        public bool   OptimizeMeshes   { get; set; } = true;
        /// <summary>A mesh with index LODs draws the coarsest level whose error on the screen is under this many pixels (of a
        /// 1080-pixel-high picture;). 0: always the full mesh.</summary>
        public float  LodPixels        { get; set; } = 1f;
        /// <summary>Static meshes loaded from now on keep their vertices in 28 bytes instead of 56  when their UVs
        /// allow it: half the memory and bandwidth, normals at 8 bits.</summary>
        public bool   CompactVertices  { get; set; }

        public QualityLevel Clone() => (QualityLevel)MemberwiseClone();

        /// <summary>
        /// A new project's levels: High is the desktop picture of before the levels, Medium a phone's (Forward+ is limited
        /// to 32 lights on a phone and in a browser anyway), Mobile an Android phone's, Low draws less.
        /// Mobile (2026-11-16): a Galaxy A71 (Adreno 618) draws at its screen's 2309 × 1080 since High DPI (four times the
        /// pixels of before); at Medium a scene whose every pixel is a lit, shadowed surface took 117 ms a frame (8 fps) and
        /// 40 point lamps 76 ms, GPU-bound. Mobile - half the 3D's pixels (the count of before High DPI; the UI stays sharp)
        /// and one shadow tap - draws them in 25 and 23 ms (40 fps), with Medium's 32 lights, cascade, shadow distance and
        /// map, HDR and spot / point shadows. Forward (owner, the same day: a town with a sun and a few lamps ran much faster
        /// on the phones with it): Forward+ costs every pixel its cluster's lookups (a float texture) whatever the lights,
        /// and pays back only where many small lights overlap (40 lamps on one floor: Forward+ 46 against Forward 114 ms).
        /// </summary>
        public static List<QualityLevel> Defaults() => new()
        {
            new QualityLevel { Name = "High" },
            new QualityLevel { Name = "Medium", MaxLights = 32, Cascades = 1, ShadowDistance = 100f },
            new QualityLevel { Name = "Mobile", RenderingPath = "forward", MaxLights = 32, Cascades = 1, ShadowDistance = 100f,
                               SoftShadows = 1, RenderScale = 0.5f, PerObjectLights = 4,
                               Shadowmask = true, SimpleShading = true, LodPixels = 2f, CompactVertices = true },
            new QualityLevel { Name = "Low", RenderingPath = "forward", MaxLights = 8, Cascades = 1, ShadowDistance = 50f, ShadowResolution = 1024,
                               PunctualShadows = false, SoftShadows = 1, Hdr = false, RenderScale = 0.75f, SimpleShading = true,
                               LodPixels = 3f, CompactVertices = true },
        };

        /// <summary>A new project's default level per target: desktop High, Android Mobile, iOS and web Medium.</summary>
        public static string DefaultFor(QualityTarget t) =>
            t == QualityTarget.Desktop ? "High" : t == QualityTarget.Android ? "Mobile" : "Medium";
    }
}
