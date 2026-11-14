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
                string json = Assets.AssetRefs.Resolve(SokolFile.ReadAllText(path));   // the start scene, where it is now
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
                    if (phys.TryGetProperty("gravity2D", out var g2) && g2.ValueKind == JsonValueKind.Array && g2.GetArrayLength() == 2)
                        cfg.Gravity2D = new System.Numerics.Vector2(g2[0].GetSingle(), g2[1].GetSingle());
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
                string json = Assets.AssetRefs.Resolve(Encoding.UTF8.GetString(data));
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
                    if (phys.TryGetProperty("gravity2D", out var g2) && g2.ValueKind == JsonValueKind.Array && g2.GetArrayLength() == 2)
                        cfg.Gravity2D = new System.Numerics.Vector2(g2[0].GetSingle(), g2[1].GetSingle());
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
                string json = Serialize(config);

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

        /// <summary>The text of <c>config.json</c> for a config, with the id of each file it names.</summary>
        public static string Serialize(ProjectConfig config)
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
            sb.Append($"    \"engine2D\": \"{Esc(config.Physics2D)}\",\n");
            sb.Append("    \"gravity2D\": [").Append(config.Gravity2D.X.ToString("G", System.Globalization.CultureInfo.InvariantCulture)).Append(", ")
              .Append(config.Gravity2D.Y.ToString("G", System.Globalization.CultureInfo.InvariantCulture)).Append("]\n");
            sb.Append("  },\n");
            sb.Append($"  \"environmentMode\": \"{Esc(config.EnvironmentMode)}\",\n");
            sb.Append($"  \"environmentFolder\": \"{Esc(config.EnvironmentFolder)}\",\n");
            sb.Append($"  \"environmentFaces\": \"{Esc(config.EnvironmentFaces)}\",\n");
            sb.Append($"  \"environmentIntensity\": {config.EnvironmentIntensity.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentRotation\": {config.EnvironmentRotation.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentShowSkybox\": {(config.EnvironmentShowSkybox ? "true" : "false")},\n");
            sb.Append($"  \"environmentShadowAmbient\": {config.EnvironmentShadowAmbient.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentAmbientSource\": \"{Esc(config.EnvironmentAmbientSource)}\",\n");
            sb.Append($"  \"environmentAmbientColor\": \"{Esc(config.EnvironmentAmbientColor)}\",\n");
            sb.Append($"  \"environmentAmbientEnergy\": {config.EnvironmentAmbientEnergy.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentTonemapping\": \"{Esc(config.EnvironmentTonemapping)}\",\n");
            sb.Append($"  \"environmentExposure\": {config.EnvironmentExposure.ToString(CultureInfo.InvariantCulture)},\n");
            WriteQuality(sb, config);
            sb.Append('}');
            return Assets.AssetRefs.Stamp(sb.ToString());
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
            if (root.TryGetProperty("environmentTonemapping", out var etm)) cfg.EnvironmentTonemapping = etm.GetString() ?? cfg.EnvironmentTonemapping;
            if (root.TryGetProperty("environmentExposure", out var eex) && eex.TryGetSingle(out var exv)) cfg.EnvironmentExposure = exv;
            if (root.TryGetProperty("quality", out var q) && q.ValueKind == JsonValueKind.Object) ReadQuality(q, cfg);
            if (root.TryGetProperty("environmentMode", out var em))        cfg.EnvironmentMode   = em.GetString()  ?? cfg.EnvironmentMode;
            if (root.TryGetProperty("environmentFolder", out var ef))      cfg.EnvironmentFolder = ef.GetString()  ?? cfg.EnvironmentFolder;
            if (root.TryGetProperty("environmentFaces", out var efa))      cfg.EnvironmentFaces  = efa.GetString() ?? cfg.EnvironmentFaces;
            if (root.TryGetProperty("environmentIntensity", out var ei) && ei.TryGetSingle(out var iv)) cfg.EnvironmentIntensity = iv;
            if (root.TryGetProperty("environmentRotation", out var er)  && er.TryGetSingle(out var rv)) cfg.EnvironmentRotation  = rv;
            if (root.TryGetProperty("environmentShowSkybox", out var es) && (es.ValueKind == JsonValueKind.True || es.ValueKind == JsonValueKind.False))
                cfg.EnvironmentShowSkybox = es.GetBoolean();
            if (root.TryGetProperty("environmentShadowAmbient", out var esa) && esa.TryGetSingle(out var sav)) cfg.EnvironmentShadowAmbient = sav;
            if (root.TryGetProperty("environmentAmbientSource", out var eas)) cfg.EnvironmentAmbientSource = eas.GetString() ?? cfg.EnvironmentAmbientSource;
            if (root.TryGetProperty("environmentAmbientColor", out var eac))  cfg.EnvironmentAmbientColor  = eac.GetString() ?? cfg.EnvironmentAmbientColor;
            if (root.TryGetProperty("environmentAmbientEnergy", out var eae) && eae.TryGetSingle(out var aev)) cfg.EnvironmentAmbientEnergy = aev;
        }

        // ── G8.5: "quality" ─────────────────────────────────────────────────────────────
        private static readonly (QualityTarget Target, string Key)[] QualityTargets =
            { (QualityTarget.Desktop, "desktop"), (QualityTarget.Android, "android"), (QualityTarget.Ios, "ios"), (QualityTarget.Web, "web") };

        private static void WriteQuality(StringBuilder sb, ProjectConfig c)
        {
            var inv = CultureInfo.InvariantCulture;
            sb.Append("  \"quality\": {\n    \"levels\": [\n");
            for (int i = 0; i < c.QualityLevels.Count; i++)
            {
                var l = c.QualityLevels[i];
                sb.Append($"      {{ \"name\": \"{Esc(l.Name)}\", \"renderingPath\": \"{Esc(l.RenderingPath)}\", \"maxLights\": {l.MaxLights}, ");
                sb.Append($"\"cascades\": {l.Cascades}, \"shadowResolution\": {l.ShadowResolution}, \"punctualShadows\": {(l.PunctualShadows ? "true" : "false")}, ");
                sb.Append($"\"softShadows\": {l.SoftShadows}, \"hdr\": {(l.Hdr ? "true" : "false")}, \"renderScale\": {l.RenderScale.ToString(inv)} }}");
                sb.Append(i + 1 < c.QualityLevels.Count ? ",\n" : "\n");
            }
            sb.Append("    ],\n    \"defaults\": { ");
            for (int i = 0; i < QualityTargets.Length; i++)
            {
                var (t, key) = QualityTargets[i];
                string name = c.QualityDefaults.TryGetValue(t, out var n) ? n : QualityLevel.DefaultFor(t);
                sb.Append($"\"{key}\": \"{Esc(name)}\"").Append(i + 1 < QualityTargets.Length ? ", " : " },\n");
            }
            sb.Append($"    \"editor\": \"{Esc(c.QualityEditor)}\"\n  }}\n");
        }

        private static void ReadQuality(JsonElement q, ProjectConfig cfg)
        {
            if (q.TryGetProperty("levels", out var levels) && levels.ValueKind == JsonValueKind.Array)
            {
                var list = new System.Collections.Generic.List<QualityLevel>();
                foreach (var e in levels.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object) continue;
                    var l = new QualityLevel();
                    if (e.TryGetProperty("name", out var v) && v.ValueKind == JsonValueKind.String) l.Name = v.GetString()!;
                    if (e.TryGetProperty("renderingPath", out v) && v.ValueKind == JsonValueKind.String) l.RenderingPath = v.GetString()!;
                    if (e.TryGetProperty("maxLights", out v) && v.TryGetInt32(out int iv)) l.MaxLights = iv;
                    if (e.TryGetProperty("cascades", out v) && v.TryGetInt32(out iv)) l.Cascades = iv >= 4 ? 4 : 1;
                    if (e.TryGetProperty("shadowResolution", out v) && v.TryGetInt32(out iv)) l.ShadowResolution = iv;
                    if (e.TryGetProperty("punctualShadows", out v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)) l.PunctualShadows = v.GetBoolean();
                    if (e.TryGetProperty("softShadows", out v) && v.TryGetInt32(out iv)) l.SoftShadows = iv;
                    if (e.TryGetProperty("hdr", out v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)) l.Hdr = v.GetBoolean();
                    if (e.TryGetProperty("renderScale", out v) && v.TryGetSingle(out float fv)) l.RenderScale = fv;
                    list.Add(l);
                }
                if (list.Count > 0) cfg.QualityLevels = list;
            }
            if (q.TryGetProperty("defaults", out var d) && d.ValueKind == JsonValueKind.Object)
                foreach (var (t, key) in QualityTargets)
                    if (d.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String) cfg.QualityDefaults[t] = v.GetString()!;
            if (q.TryGetProperty("editor", out var ed) && ed.ValueKind == JsonValueKind.String) cfg.QualityEditor = ed.GetString()!;
        }

        private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
