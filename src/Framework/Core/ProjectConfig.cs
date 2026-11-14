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
        public bool   EnvironmentCsm4       { get; set; } = false;            // 4-cascade directional shadows (else single-fit CSM1)
        public string EnvironmentAmbientSource { get; set; } = "sky";         // "sky" | "color" | "disabled"
        public string EnvironmentAmbientColor  { get; set; } = "0.1 0.1 0.1"; // "r g b", used by "color"
        public float  EnvironmentAmbientEnergy { get; set; } = 1f;
        // G8.2: the Rendering Path — "forward+" (the cluster grid; a new project's) or "forward" (every light on every pixel).
        public string RenderingPath { get; set; } = "forward+";
        // G8.3: the scene's tone curve ("none" | "neutral" | "aces") and exposure in EV (0 = none).
        public string EnvironmentTonemapping { get; set; } = "none";
        public float  EnvironmentExposure    { get; set; } = 0f;
    }
}
