using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace GameEditor.Framework.Core
{
    /// <summary>
    /// Loads and saves a project's config.json.
    /// AOT-safe: uses JsonDocument for reading and manual StringBuilder for writing,
    /// matching the pattern established in SceneSerializer.
    /// </summary>
    public static class ConfigManager
    {
        public const string ConfigFileName = "config.json";

        public static string?        ProjectFolder { get; private set; }
        public static ProjectConfig? Config        { get; private set; }

        public static bool HasProject => ProjectFolder != null && Config != null;

        /// <summary>
        /// Reads config.json from <paramref name="projectFolder"/>.
        /// Sets <see cref="ProjectFolder"/> and <see cref="Config"/> on success.
        /// Returns null on failure.
        /// </summary>
        public static ProjectConfig? Load(string projectFolder)
        {
            string path = Path.Combine(projectFolder, ConfigFileName);
            if (!File.Exists(path))
            {
                Logger.Error($"config.json not found in: {projectFolder}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var cfg = new ProjectConfig();
                if (root.TryGetProperty("version",       out var v))  cfg.Version       = v.GetString()  ?? cfg.Version;
                if (root.TryGetProperty("projectName",   out var pn)) cfg.ProjectName   = pn.GetString() ?? cfg.ProjectName;
                if (root.TryGetProperty("defaultScene",  out var ds)) cfg.DefaultScene  = ds.GetString() ?? cfg.DefaultScene;
                if (root.TryGetProperty("defaultCamera", out var dc)) cfg.DefaultCamera = dc.GetString() ?? cfg.DefaultCamera;
                if (root.TryGetProperty("screenWidth",   out var sw)) cfg.ScreenWidth   = sw.GetInt32();
                if (root.TryGetProperty("screenHeight",  out var sh)) cfg.ScreenHeight  = sh.GetInt32();
                if (root.TryGetProperty("physics", out var phys))
                {
                    if (phys.TryGetProperty("engine3D", out var e3)) cfg.Physics3D = e3.GetString() ?? cfg.Physics3D;
                    if (phys.TryGetProperty("engine2D", out var e2)) cfg.Physics2D = e2.GetString() ?? cfg.Physics2D;
                }

                ProjectFolder = projectFolder;
                Config        = cfg;
                Logger.Info($"Project loaded: {cfg.ProjectName}  ({projectFolder})");
                return cfg;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse config.json: {ex.Message}");
                return null;
            }
        }

        /// <summary>Saves the current <see cref="Config"/> back to <see cref="ProjectFolder"/>.</summary>
        public static void Save()
        {
            if (ProjectFolder != null && Config != null)
                Save(ProjectFolder, Config);
        }

        /// <summary>Serializes <paramref name="config"/> to <c>config.json</c> inside <paramref name="projectFolder"/>.</summary>
        public static void Save(string projectFolder, ProjectConfig config)
        {
            string path = Path.Combine(projectFolder, ConfigFileName);
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\n");
                sb.Append($"  \"version\": \"{Esc(config.Version)}\",\n");
                sb.Append($"  \"projectName\": \"{Esc(config.ProjectName)}\",\n");
                sb.Append($"  \"defaultScene\": \"{Esc(config.DefaultScene)}\",\n");
                sb.Append($"  \"defaultCamera\": \"{Esc(config.DefaultCamera)}\",\n");
                sb.Append($"  \"screenWidth\": {config.ScreenWidth},\n");
                sb.Append($"  \"screenHeight\": {config.ScreenHeight},\n");
                sb.Append("  \"physics\": {\n");
                sb.Append($"    \"engine3D\": \"{Esc(config.Physics3D)}\",\n");
                sb.Append($"    \"engine2D\": \"{Esc(config.Physics2D)}\"\n");
                sb.Append("  }\n");
                sb.Append('}');

                File.WriteAllText(path, sb.ToString());
                Logger.Info("Project config saved.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save config.json: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the filesystem path to the SokolApplicationBuilder project directory.
        /// Reads <c>~/.sokolnet_config/sokolnet_home</c> for the root; falls back to six
        /// directories up from the executable (matching the project's build-time convention).
        /// </summary>
        public static string GetSokolAppBuilderPath()
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(homeDir))
                homeDir = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;

            string sokolNetHomeFile = Path.Combine(homeDir, ".sokolnet_config", "sokolnet_home");
            string sokolNetHome = File.Exists(sokolNetHomeFile)
                ? File.ReadAllText(sokolNetHomeFile).Trim()
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

            return Path.GetFullPath(Path.Combine(sokolNetHome, "tools", "SokolApplicationBuilder"));
        }

        private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
