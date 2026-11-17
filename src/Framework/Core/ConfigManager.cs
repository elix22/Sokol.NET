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
            sb.Append($"  \"fixedTimestep\": {config.FixedTimestep.ToString(CultureInfo.InvariantCulture)},\n");
            if (config.DevelopmentBuild) sb.Append("  \"developmentBuild\": true,\n");
            if (!config.HighDpi) sb.Append("  \"highDpi\": false,\n");
            if (config.Services.Count > 0)
            {
                sb.Append("  \"services\": [");
                for (int i = 0; i < config.Services.Count; i++) sb.Append(i > 0 ? ", " : "").Append('"').Append(Esc(config.Services[i])).Append('"');
                sb.Append("],\n");
            }
            if (config.ServiceSettings.Count > 0)
            {
                sb.Append("  \"serviceSettings\": {");
                bool firstSetting = true;
                foreach (var kv in config.ServiceSettings)
                {
                    sb.Append(firstSetting ? "" : ", ").Append('"').Append(Esc(kv.Key)).Append("\": \"").Append(Esc(kv.Value)).Append('"');
                    firstSetting = false;
                }
                sb.Append("},\n");
            }
            sb.Append($"  \"environmentAmbientSource\": \"{Esc(config.EnvironmentAmbientSource)}\",\n");
            sb.Append($"  \"environmentAmbientColor\": \"{Esc(config.EnvironmentAmbientColor)}\",\n");
            sb.Append($"  \"environmentAmbientEnergy\": {config.EnvironmentAmbientEnergy.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentTonemapping\": \"{Esc(config.EnvironmentTonemapping)}\",\n");
            sb.Append($"  \"environmentExposure\": {config.EnvironmentExposure.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentBloomIntensity\": {config.EnvironmentBloomIntensity.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentBloomThreshold\": {config.EnvironmentBloomThreshold.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentBloomScatter\": {config.EnvironmentBloomScatter.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentBloomTint\": \"{Esc(config.EnvironmentBloomTint)}\",\n");
            sb.Append($"  \"environmentBloomClamp\": {config.EnvironmentBloomClamp.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentBloomHq\": {(config.EnvironmentBloomHq ? "true" : "false")},\n");
            sb.Append($"  \"environmentVignetteIntensity\": {config.EnvironmentVignetteIntensity.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentFogDensity\": {config.EnvironmentFogDensity.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentFogColor\": \"{Esc(config.EnvironmentFogColor)}\",\n");
            sb.Append($"  \"environmentVignetteSmoothness\": {config.EnvironmentVignetteSmoothness.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentContrast\": {config.EnvironmentContrast.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentHueShift\": {config.EnvironmentHueShift.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentSaturation\": {config.EnvironmentSaturation.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"environmentVignetteCenter\": \"{Esc(config.EnvironmentVignetteCenter)}\",\n");
            sb.Append($"  \"environmentVignetteColor\": \"{Esc(config.EnvironmentVignetteColor)}\",\n");
            sb.Append($"  \"environmentColorFilter\": \"{Esc(config.EnvironmentColorFilter)}\",\n");
            sb.Append($"  \"environmentVignetteRounded\": {(config.EnvironmentVignetteRounded ? "true" : "false")},\n");
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
            if (root.TryGetProperty("environmentBloomIntensity", out var bi) && bi.TryGetSingle(out var biv)) cfg.EnvironmentBloomIntensity = MathF.Max(0f, biv);
            if (root.TryGetProperty("environmentBloomThreshold", out var bt) && bt.TryGetSingle(out var btv)) cfg.EnvironmentBloomThreshold = MathF.Max(0f, btv);
            if (root.TryGetProperty("environmentBloomScatter", out var bs) && bs.TryGetSingle(out var bsv)) cfg.EnvironmentBloomScatter = Math.Clamp(bsv, 0f, 1f);
            if (root.TryGetProperty("environmentBloomTint", out var bc)) cfg.EnvironmentBloomTint = bc.GetString() ?? cfg.EnvironmentBloomTint;
            if (root.TryGetProperty("environmentBloomClamp", out var bm) && bm.TryGetSingle(out var bmv)) cfg.EnvironmentBloomClamp = MathF.Max(0f, bmv);
            if (root.TryGetProperty("environmentVignetteIntensity", out var vi) && vi.TryGetSingle(out var viv)) cfg.EnvironmentVignetteIntensity = Math.Clamp(viv, 0f, 1f);
            if (root.TryGetProperty("environmentFogDensity", out var fdn) && fdn.TryGetSingle(out var fdv)) cfg.EnvironmentFogDensity = Math.Clamp(fdv, 0f, 1f);
            if (root.TryGetProperty("environmentFogColor", out var fcl)) cfg.EnvironmentFogColor = fcl.GetString() ?? cfg.EnvironmentFogColor;
            if (root.TryGetProperty("environmentVignetteSmoothness", out var vs) && vs.TryGetSingle(out var vsv)) cfg.EnvironmentVignetteSmoothness = Math.Clamp(vsv, 0.01f, 1f);
            if (root.TryGetProperty("environmentVignetteCenter", out var vce)) cfg.EnvironmentVignetteCenter = vce.GetString() ?? cfg.EnvironmentVignetteCenter;
            if (root.TryGetProperty("environmentVignetteColor", out var vco)) cfg.EnvironmentVignetteColor = vco.GetString() ?? cfg.EnvironmentVignetteColor;
            if (root.TryGetProperty("environmentVignetteRounded", out var vr) && (vr.ValueKind == JsonValueKind.True || vr.ValueKind == JsonValueKind.False)) cfg.EnvironmentVignetteRounded = vr.GetBoolean();
            if (root.TryGetProperty("environmentContrast", out var gc) && gc.TryGetSingle(out var gcv)) cfg.EnvironmentContrast = Math.Clamp(gcv, -100f, 100f);
            if (root.TryGetProperty("environmentColorFilter", out var gf)) cfg.EnvironmentColorFilter = gf.GetString() ?? cfg.EnvironmentColorFilter;
            if (root.TryGetProperty("environmentHueShift", out var gh) && gh.TryGetSingle(out var ghv)) cfg.EnvironmentHueShift = Math.Clamp(ghv, -180f, 180f);
            if (root.TryGetProperty("environmentSaturation", out var gs) && gs.TryGetSingle(out var gsv)) cfg.EnvironmentSaturation = Math.Clamp(gsv, -100f, 100f);
            if (root.TryGetProperty("environmentBloomHq", out var bh) && (bh.ValueKind == JsonValueKind.True || bh.ValueKind == JsonValueKind.False)) cfg.EnvironmentBloomHq = bh.GetBoolean();
            if (root.TryGetProperty("quality", out var q) && q.ValueKind == JsonValueKind.Object) ReadQuality(q, cfg);
            if (root.TryGetProperty("environmentMode", out var em))        cfg.EnvironmentMode   = em.GetString()  ?? cfg.EnvironmentMode;
            if (root.TryGetProperty("environmentFolder", out var ef))      cfg.EnvironmentFolder = ef.GetString()  ?? cfg.EnvironmentFolder;
            if (root.TryGetProperty("environmentFaces", out var efa))      cfg.EnvironmentFaces  = efa.GetString() ?? cfg.EnvironmentFaces;
            if (root.TryGetProperty("environmentIntensity", out var ei) && ei.TryGetSingle(out var iv)) cfg.EnvironmentIntensity = iv;
            if (root.TryGetProperty("environmentRotation", out var er)  && er.TryGetSingle(out var rv)) cfg.EnvironmentRotation  = rv;
            if (root.TryGetProperty("environmentShowSkybox", out var es) && (es.ValueKind == JsonValueKind.True || es.ValueKind == JsonValueKind.False))
                cfg.EnvironmentShowSkybox = es.GetBoolean();
            if (root.TryGetProperty("environmentShadowAmbient", out var esa) && esa.TryGetSingle(out var sav)) cfg.EnvironmentShadowAmbient = sav;
            if (root.TryGetProperty("fixedTimestep", out var fts) && fts.TryGetSingle(out var ftv) && ftv > 0f) cfg.FixedTimestep = ftv;
            if (root.TryGetProperty("developmentBuild", out var dvb) && dvb.ValueKind == JsonValueKind.True) cfg.DevelopmentBuild = true;
            if (root.TryGetProperty("highDpi", out var hdp) && hdp.ValueKind == JsonValueKind.False) cfg.HighDpi = false;
            if (root.TryGetProperty("services", out var svc) && svc.ValueKind == JsonValueKind.Array)
                foreach (var e in svc.EnumerateArray()) if (e.GetString() is { Length: > 0 } id && !cfg.Services.Contains(id)) cfg.Services.Add(id);
            if (root.TryGetProperty("serviceSettings", out var sst) && sst.ValueKind == JsonValueKind.Object)
                foreach (var p in sst.EnumerateObject()) cfg.ServiceSettings[p.Name] = p.Value.GetString() ?? "";
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
                sb.Append($"\"cascades\": {l.Cascades}, \"shadowDistance\": {l.ShadowDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"shadowResolution\": {l.ShadowResolution}, \"punctualShadows\": {(l.PunctualShadows ? "true" : "false")}, ");
                sb.Append($"\"softShadows\": {l.SoftShadows}, \"hdr\": {(l.Hdr ? "true" : "false")}, \"renderScale\": {l.RenderScale.ToString(inv)}, \"perObjectLights\": {l.PerObjectLights}, \"shadowmask\": {(l.Shadowmask ? "true" : "false")} }}");
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
                    if (e.TryGetProperty("shadowDistance", out v) && v.TryGetSingle(out float sd)) l.ShadowDistance = Math.Clamp(sd, 5f, 2000f);
                    else if (Defaults(l.Name) is { } was)
                    {
                        // A level written before the shadow distance (2026-11-15): the cascades of that time were coarse and
                        // off by default - it takes the new default level's cascades and distance.
                        l.Cascades = was.Cascades;
                        l.ShadowDistance = was.ShadowDistance;
                    }
                    if (e.TryGetProperty("shadowResolution", out v) && v.TryGetInt32(out iv)) l.ShadowResolution = iv;
                    if (e.TryGetProperty("punctualShadows", out v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)) l.PunctualShadows = v.GetBoolean();
                    if (e.TryGetProperty("softShadows", out v) && v.TryGetInt32(out iv)) l.SoftShadows = iv;
                    if (e.TryGetProperty("hdr", out v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)) l.Hdr = v.GetBoolean();
                    if (e.TryGetProperty("renderScale", out v) && v.TryGetSingle(out float fv)) l.RenderScale = fv;
                    if (e.TryGetProperty("perObjectLights", out v) && v.TryGetInt32(out iv)) l.PerObjectLights = Math.Clamp(iv, 0, 8);
                    else if (Defaults(l.Name) is { } known) l.PerObjectLights = known.PerObjectLights;   // a level of before the value
                    if (e.TryGetProperty("shadowmask", out v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)) l.Shadowmask = v.ValueKind == JsonValueKind.True;
                    else if (Defaults(l.Name) is { } known2) l.Shadowmask = known2.Shadowmask;
                    list.Add(l);
                }
                if (list.Count > 0) cfg.QualityLevels = list;
            }
            if (q.TryGetProperty("defaults", out var d) && d.ValueKind == JsonValueKind.Object)
                foreach (var (t, key) in QualityTargets)
                    if (d.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String) cfg.QualityDefaults[t] = v.GetString()!;
            if (q.TryGetProperty("editor", out var ed) && ed.ValueKind == JsonValueKind.String) cfg.QualityEditor = ed.GetString()!;
        }

        private static QualityLevel? Defaults(string name) =>
            QualityLevel.Defaults().Find(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

        private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
