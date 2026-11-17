using System.Collections.Generic;
namespace GameEditor.Framework.Core
{
    /// <summary>
    /// Data loaded from / saved to a project's config.json.
    /// </summary>
    public class ProjectConfig
    {
        public string Version       { get; set; } = "1.0";
        public string ProjectName   { get; set; } = "MyGame";
        public string DefaultScene  { get; set; } = "Scenes/Main.scene.json";
        public string DefaultCamera { get; set; } = "MainCamera";
        public int    ScreenWidth   { get; set; } = 1280;
        public int    ScreenHeight  { get; set; } = 720;
        public string Physics3D     { get; set; } = "jolt";
        public string Physics2D     { get; set; } = "box2d";
        /// <summary>The 2D world's gravity, in units per second squared ("physics": { "gravity2D": [0, -9.81] }).</summary>
        public System.Numerics.Vector2 Gravity2D { get; set; } = new(0f, -9.81f);

        // ── Environment / IBL (authored in the editor's Environment panel) ──────────────
        public string EnvironmentMode      { get; set; } = "cubemap";          // "procedural" | "cubemap"
        public string EnvironmentFolder    { get; set; } = "skyboxes/skybox";  // Assets-relative cubemap folder
        public string EnvironmentFaces     { get; set; } = "";                 // 6 per-face overrides, '|'-joined ("" = use folder)
        public float  EnvironmentIntensity { get; set; } = 1f;
        public float  EnvironmentRotation  { get; set; } = 0f;                 // degrees about Y
        public bool   EnvironmentShowSkybox{ get; set; } = true;
        public float  EnvironmentShadowAmbient { get; set; } = 0.4f;           // 0=physical, 1=shadows fully darken IBL ambient
        public float  FixedTimestep { get; set; } = 0.02f;                     // G10: Time.FixedDeltaTime (Project Settings ▸ Time)
        public bool   DevelopmentBuild { get; set; }                           // G11.1: a built game opens the player link (Build & Deploy)
        /// <summary>Project Settings ▸ High DPI: the game drawn at the screen's full resolution (a Retina Mac, a phone, a web page on
        /// a high-density screen); off: at its points, cheaper and softer. Written into the app file's sokol_main by the editor.</summary>
        public bool   HighDpi { get; set; } = true;
        public List<string> Services { get; set; } = new();                    // G9: the game services added (speech, share, ...)
        public Dictionary<string, string> ServiceSettings { get; set; } = new(); // G9: their settings (AdMob ids, product ids, ...)
        public string EnvironmentAmbientSource { get; set; } = "sky";         // "sky" | "color" | "disabled"
        public string EnvironmentAmbientColor  { get; set; } = "0.1 0.1 0.1"; // "r g b", used by "color"
        public float  EnvironmentAmbientEnergy { get; set; } = 1f;
        // G8.3: the scene's tone curve ("none" | "neutral" | "aces") and exposure in EV (0 = none).
        public string EnvironmentTonemapping { get; set; } = "none";
        public float  EnvironmentExposure    { get; set; } = 0f;
        // G8.9a: the post effects' bloom (Intensity 0 = off), the reference editor's defaults.
        public float  EnvironmentBloomIntensity { get; set; } = 0f;
        public float  EnvironmentBloomThreshold { get; set; } = 0.9f;
        public float  EnvironmentBloomScatter   { get; set; } = 0.7f;
        public string EnvironmentBloomTint      { get; set; } = "1 1 1";   // "r g b"
        public float  EnvironmentBloomClamp     { get; set; } = 65472f;
        public bool   EnvironmentBloomHq        { get; set; } = false;
        // G8.9b: the vignette (Intensity 0 = off) and the colour adjustments (0 / white = none).
        public float  EnvironmentVignetteIntensity  { get; set; } = 0f;
        public float  EnvironmentVignetteSmoothness { get; set; } = 0.2f;
        public string EnvironmentVignetteCenter     { get; set; } = "0.5 0.5";   // "x y", 0..1, the top left 0 0
        public string EnvironmentVignetteColor      { get; set; } = "0 0 0";     // "r g b"
        // Phase H E7: the haze - far things fade into a colour (exp2 of distance x density; 0 = none). Colour "" = the sky's horizon.
        public float  EnvironmentFogDensity         { get; set; } = 0f;
        public string EnvironmentFogColor           { get; set; } = "";          // "r g b" (sRGB 0..1) or "" for the sky's horizon
        public bool   EnvironmentVignetteRounded    { get; set; } = false;
        public float  EnvironmentContrast           { get; set; } = 0f;
        public string EnvironmentColorFilter        { get; set; } = "1 1 1";
        public float  EnvironmentHueShift           { get; set; } = 0f;
        public float  EnvironmentSaturation         { get; set; } = 0f;
        // G8.5: the quality levels (config.json "quality": levels, the default per target, the editor's level). The
        // Rendering Path (G8.2) and the cascades are a level's values.
        public System.Collections.Generic.List<QualityLevel> QualityLevels { get; set; } = QualityLevel.Defaults();
        public System.Collections.Generic.Dictionary<QualityTarget, string> QualityDefaults { get; set; } = new()
        {
            [QualityTarget.Desktop] = QualityLevel.DefaultFor(QualityTarget.Desktop),
            [QualityTarget.Android] = QualityLevel.DefaultFor(QualityTarget.Android),
            [QualityTarget.Ios]     = QualityLevel.DefaultFor(QualityTarget.Ios),
            [QualityTarget.Web]     = QualityLevel.DefaultFor(QualityTarget.Web),
        };
        /// <summary>The level an editor draws its views at (the reference editor's current level); a game ignores it.</summary>
        public string QualityEditor { get; set; } = QualityLevel.DefaultFor(QualityTarget.Desktop);
    }
}
