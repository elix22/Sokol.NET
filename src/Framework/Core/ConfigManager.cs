using System;
using System.Globalization;
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

        public static string? ProjectFolder { get; private set; }
        public static ProjectConfig? Config { get; private set; }

        public static bool HasProject => ProjectFolder != null && Config != null;

        /// <summary>
        /// Reads config.json from <paramref name="projectFolder"/>.
        /// Sets <see cref="ProjectFolder"/> and <see cref="Config"/> on success.
        /// Returns null on failure.
        /// </summary>
        public static ProjectConfig? Load(string projectFolder)
        {
            string path = Path.Combine(projectFolder, ConfigFileName);
            if (!SokolFile.Exists(path))
            {
                Logger.Error($"config.json not found in: {projectFolder}");
                return null;
            }

            try
            {
                string json = SokolFile.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var cfg = new ProjectConfig();
                if (root.TryGetProperty("version", out var v)) cfg.Version = v.GetString() ?? cfg.Version;
                if (root.TryGetProperty("projectName", out var pn)) cfg.ProjectName = pn.GetString() ?? cfg.ProjectName;
                if (root.TryGetProperty("defaultScene", out var ds)) cfg.DefaultScene = ds.GetString() ?? cfg.DefaultScene;
                if (root.TryGetProperty("defaultCamera", out var dc)) cfg.DefaultCamera = dc.GetString() ?? cfg.DefaultCamera;
                if (root.TryGetProperty("screenWidth", out var sw)) cfg.ScreenWidth = sw.GetInt32();
                if (root.TryGetProperty("screenHeight", out var sh)) cfg.ScreenHeight = sh.GetInt32();
                if (root.TryGetProperty("physics", out var phys))
                {
                    if (phys.TryGetProperty("engine3D", out var e3)) cfg.Physics3D = e3.GetString() ?? cfg.Physics3D;
                    if (phys.TryGetProperty("engine2D", out var e2)) cfg.Physics2D = e2.GetString() ?? cfg.Physics2D;
                }
                ReadEnvironment(root, cfg);

                ProjectFolder = projectFolder;
                Config = cfg;
                Logger.Info($"Project loaded: {cfg.ProjectName}  ({projectFolder})");
                return cfg;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse config.json: {ex.Message}");
                return null;
            }
        }

        public static ProjectConfig? LoadFromMemory(byte[]? data)
        {
            if (data == null) return null;
            try
            {
                string json = Encoding.UTF8.GetString(data);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var cfg = new ProjectConfig();
                if (root.TryGetProperty("version", out var v)) cfg.Version = v.GetString() ?? cfg.Version;
                if (root.TryGetProperty("projectName", out var pn)) cfg.ProjectName = pn.GetString() ?? cfg.ProjectName;
                if (root.TryGetProperty("defaultScene", out var ds)) cfg.DefaultScene = ds.GetString() ?? cfg.DefaultScene;
                if (root.TryGetProperty("defaultCamera", out var dc)) cfg.DefaultCamera = dc.GetString() ?? cfg.DefaultCamera;
                if (root.TryGetProperty("screenWidth", out var sw)) cfg.ScreenWidth = sw.GetInt32();
                if (root.TryGetProperty("screenHeight", out var sh)) cfg.ScreenHeight = sh.GetInt32();
                if (root.TryGetProperty("physics", out var phys))
                {
                    if (phys.TryGetProperty("engine3D", out var e3)) cfg.Physics3D = e3.GetString() ?? cfg.Physics3D;
                    if (phys.TryGetProperty("engine2D", out var e2)) cfg.Physics2D = e2.GetString() ?? cfg.Physics2D;
                }
                ReadEnvironment(root, cfg);

                Config = cfg;
                Logger.Info($"Project config loaded from buffer.");
                return cfg;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse config buffer: {ex.Message}");
                return null;
            }
        }

        /// <summary>Saves the current <see cref="Config"/> back to <see cref="ProjectFolder"/>.</summary>
        public static void Save()
        {
            if (ProjectFolder != null && Config != null)
                Save(ProjectFolder, Config);
        }

        /// <summary>Serializes <paramref name="config"/> to <c>config.json</c> inside <paramref name="projectFolder"/>
        /// and to <c>Assets/config.json</c> (the copy used at application startup).</summary>
        public static void Save(string projectFolder, ProjectConfig config)
        {
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
                sb.Append("  },\n");
                sb.Append($"  \"environmentMode\": \"{Esc(config.EnvironmentMode)}\",\n");
                sb.Append($"  \"environmentFolder\": \"{Esc(config.EnvironmentFolder)}\",\n");
                sb.Append($"  \"environmentFaces\": \"{Esc(config.EnvironmentFaces)}\",\n");
                sb.Append($"  \"environmentIntensity\": {config.EnvironmentIntensity.ToString(CultureInfo.InvariantCulture)},\n");
                sb.Append($"  \"environmentRotation\": {config.EnvironmentRotation.ToString(CultureInfo.InvariantCulture)},\n");
                sb.Append($"  \"environmentShowSkybox\": {(config.EnvironmentShowSkybox ? "true" : "false")},\n");
                sb.Append($"  \"environmentShadowAmbient\": {config.EnvironmentShadowAmbient.ToString(CultureInfo.InvariantCulture)},\n");
                sb.Append($"  \"environmentCsm4\": {(config.EnvironmentCsm4 ? "true" : "false")},\n");
                sb.Append($"  \"environmentAmbientSource\": \"{Esc(config.EnvironmentAmbientSource)}\",\n");
                sb.Append($"  \"environmentAmbientColor\": \"{Esc(config.EnvironmentAmbientColor)}\",\n");
                sb.Append($"  \"environmentAmbientEnergy\": {config.EnvironmentAmbientEnergy.ToString(CultureInfo.InvariantCulture)}\n");
                sb.Append('}');

                string json = sb.ToString();

                // Root copy
                SokolFile.WriteAllText(Path.Combine(projectFolder, ConfigFileName), json);

                // Assets/ copy — the one used by the application at startup
                string assetsDir = Path.Combine(projectFolder, "Assets");
                Directory.CreateDirectory(assetsDir);
                SokolFile.WriteAllText(Path.Combine(assetsDir, ConfigFileName), json);

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
        public static string GetSokolNetHome()
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(homeDir))
                homeDir = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;

            string sokolNetHomeFile = Path.Combine(homeDir, ".sokolnet_config", "sokolnet_home");
            return SokolFile.Exists(sokolNetHomeFile)
                ? SokolFile.ReadAllText(sokolNetHomeFile).Trim()
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        }

        public static string GetSokolAppBuilderPath()
            => Path.GetFullPath(Path.Combine(GetSokolNetHome(), "tools", "SokolApplicationBuilder"));

        private static void ReadEnvironment(JsonElement root, ProjectConfig cfg)
        {
            if (root.TryGetProperty("environmentMode", out var em))        cfg.EnvironmentMode   = em.GetString()  ?? cfg.EnvironmentMode;
            if (root.TryGetProperty("environmentFolder", out var ef))      cfg.EnvironmentFolder = ef.GetString()  ?? cfg.EnvironmentFolder;
            if (root.TryGetProperty("environmentFaces", out var efa))      cfg.EnvironmentFaces  = efa.GetString() ?? cfg.EnvironmentFaces;
            if (root.TryGetProperty("environmentIntensity", out var ei) && ei.TryGetSingle(out var iv)) cfg.EnvironmentIntensity = iv;
            if (root.TryGetProperty("environmentRotation", out var er)  && er.TryGetSingle(out var rv)) cfg.EnvironmentRotation  = rv;
            if (root.TryGetProperty("environmentShowSkybox", out var es) && (es.ValueKind == JsonValueKind.True || es.ValueKind == JsonValueKind.False))
                cfg.EnvironmentShowSkybox = es.GetBoolean();
            if (root.TryGetProperty("environmentShadowAmbient", out var esa) && esa.TryGetSingle(out var sav)) cfg.EnvironmentShadowAmbient = sav;
            if (root.TryGetProperty("environmentCsm4", out var c4) && (c4.ValueKind == JsonValueKind.True || c4.ValueKind == JsonValueKind.False))
                cfg.EnvironmentCsm4 = c4.GetBoolean();
            if (root.TryGetProperty("environmentAmbientSource", out var eas)) cfg.EnvironmentAmbientSource = eas.GetString() ?? cfg.EnvironmentAmbientSource;
            if (root.TryGetProperty("environmentAmbientColor", out var eac))  cfg.EnvironmentAmbientColor  = eac.GetString() ?? cfg.EnvironmentAmbientColor;
            if (root.TryGetProperty("environmentAmbientEnergy", out var eae) && eae.TryGetSingle(out var aev)) cfg.EnvironmentAmbientEnergy = aev;
        }

        private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
