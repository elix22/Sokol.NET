using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace GameEditor.Framework.Renderer.Server.GI
{
    /// <summary>Lightmaps with or without the dominant light direction (the reference editor's Directional Mode).</summary>
    public enum DirectionalMode : byte { Directional, NonDirectional }

    /// <summary>
    /// A scene's baked-lighting settings (M8a, docs BAKED_GI_DESIGN.md §3.1): the reference editor's Lighting window, with
    /// its defaults. Kept in the scene file's "lighting" block (<see cref="Scene.Scene.Blocks"/>), only the values that
    /// differ from the defaults written; read with JsonDocument (no reflection).
    /// </summary>
    public sealed class LightingSettings
    {
        public const string BlockKey = "lighting";

        public bool BakedGI = true;
        public int MaxBounces = 2;
        public int DirectSamples = 32;
        public int IndirectSamples = 512;
        public int EnvironmentSamples = 256;
        /// <summary>Lightmap texels per world unit.</summary>
        public float Resolution = 40f;
        /// <summary>Texels between charts.</summary>
        public int Padding = 2;
        public int MaxLightmapSize = 1024;
        public DirectionalMode DirectionalMode = DirectionalMode.Directional;
        public float IndirectIntensity = 1f;
        public float AlbedoBoost = 1f;
        public bool Filtering = true;

        private static readonly LightingSettings Defaults = new();

        /// <summary>The scene's settings (the defaults when its file has none).</summary>
        public static LightingSettings Of(Scene.Scene scene)
            => scene.Blocks.TryGetValue(BlockKey, out var json) ? Parse(json) : new LightingSettings();

        /// <summary>Puts the settings into the scene (removes the block when all are the defaults).</summary>
        public void ApplyTo(Scene.Scene scene)
        {
            string json = ToJson();
            if (json == "{}") scene.Blocks.Remove(BlockKey);
            else scene.Blocks[BlockKey] = json;
        }

        public static LightingSettings Parse(string json)
        {
            var s = new LightingSettings();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                if (r.ValueKind != JsonValueKind.Object) return s;
                if (r.TryGetProperty("bakedGI", out var e) && (e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False)) s.BakedGI = e.GetBoolean();
                s.MaxBounces = Int(r, "maxBounces", s.MaxBounces, 0, 16);
                s.DirectSamples = Int(r, "directSamples", s.DirectSamples, 1, 4096);
                s.IndirectSamples = Int(r, "indirectSamples", s.IndirectSamples, 1, 65536);
                s.EnvironmentSamples = Int(r, "environmentSamples", s.EnvironmentSamples, 1, 65536);
                s.Resolution = Float(r, "resolution", s.Resolution, 0.01f, 1000f);
                s.Padding = Int(r, "padding", s.Padding, 0, 64);
                s.MaxLightmapSize = Int(r, "maxLightmapSize", s.MaxLightmapSize, 32, 4096);
                if (r.TryGetProperty("directionalMode", out e) && e.ValueKind == JsonValueKind.String)
                    s.DirectionalMode = string.Equals(e.GetString(), "NonDirectional", StringComparison.OrdinalIgnoreCase) ? DirectionalMode.NonDirectional : DirectionalMode.Directional;
                s.IndirectIntensity = Float(r, "indirectIntensity", s.IndirectIntensity, 0f, 100f);
                s.AlbedoBoost = Float(r, "albedoBoost", s.AlbedoBoost, 1f, 10f);
                if (r.TryGetProperty("filtering", out e) && (e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False)) s.Filtering = e.GetBoolean();
            }
            catch (JsonException) { }
            return s;
        }

        public string ToJson()
        {
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms))
            {
                w.WriteStartObject();
                if (BakedGI != Defaults.BakedGI) w.WriteBoolean("bakedGI", BakedGI);
                if (MaxBounces != Defaults.MaxBounces) w.WriteNumber("maxBounces", MaxBounces);
                if (DirectSamples != Defaults.DirectSamples) w.WriteNumber("directSamples", DirectSamples);
                if (IndirectSamples != Defaults.IndirectSamples) w.WriteNumber("indirectSamples", IndirectSamples);
                if (EnvironmentSamples != Defaults.EnvironmentSamples) w.WriteNumber("environmentSamples", EnvironmentSamples);
                if (Resolution != Defaults.Resolution) w.WriteNumber("resolution", Resolution);
                if (Padding != Defaults.Padding) w.WriteNumber("padding", Padding);
                if (MaxLightmapSize != Defaults.MaxLightmapSize) w.WriteNumber("maxLightmapSize", MaxLightmapSize);
                if (DirectionalMode != Defaults.DirectionalMode) w.WriteString("directionalMode", DirectionalMode.ToString());
                if (IndirectIntensity != Defaults.IndirectIntensity) w.WriteNumber("indirectIntensity", IndirectIntensity);
                if (AlbedoBoost != Defaults.AlbedoBoost) w.WriteNumber("albedoBoost", AlbedoBoost);
                if (Filtering != Defaults.Filtering) w.WriteBoolean("filtering", Filtering);
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static int Int(JsonElement r, string name, int fallback, int min, int max)
            => r.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int v) ? Math.Clamp(v, min, max) : fallback;

        private static float Float(JsonElement r, string name, float fallback, float min, float max)
            => r.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number ? Math.Clamp(e.GetSingle(), min, max) : fallback;
    }
}
