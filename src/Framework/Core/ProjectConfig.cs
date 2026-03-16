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
    }
}
