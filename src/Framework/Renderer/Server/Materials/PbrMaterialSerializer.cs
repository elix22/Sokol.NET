// PbrMaterialSerializer.cs — read/write the `.pbrmat` material-asset format.
//
// `.pbrmat` is a small, AOT-safe JSON file (manual Utf8JsonWriter / JsonDocument — no
// reflection) describing a PBR metallic-roughness material so Inspector edits survive a
// scene/asset reload.
//
// Factors + flags are always persisted. Texture-map paths are persisted ONLY when they are
// real Assets-relative files (picker-assigned). glTF-imported maps carry a synthetic cache
// key (e.g. "model.glb#img0") — those contain '#' and are NOT files, so they are skipped
// (the material reloads factor-only until a real texture file is assigned via the picker).

using System;
using System.Buffers;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace GameEditor.Framework.Renderer.Server.Materials
{
    /// <summary>
    /// Value snapshot of a <see cref="PbrMaterial"/>'s scalar/colour/flag fields (no textures).
    /// Shared by the serializer and the Inspector's undo path (capture → compare → re-apply).
    /// </summary>
    public struct PbrMatSnapshot : IEquatable<PbrMatSnapshot>
    {
        public Vector4 BaseColorFactor;
        public float   MetallicFactor;
        public float   RoughnessFactor;
        public Vector3 EmissiveFactor;
        public float   EmissiveStrength;
        public float   OcclusionStrength;
        public float   NormalMapScale;
        public int     AlphaMode;
        public float   AlphaCutoff;
        public bool    DoubleSided;

        public static PbrMatSnapshot Capture(PbrMaterial m) => new()
        {
            BaseColorFactor   = m.BaseColorFactor,
            MetallicFactor    = m.MetallicFactor,
            RoughnessFactor   = m.RoughnessFactor,
            EmissiveFactor    = m.EmissiveFactor,
            EmissiveStrength  = m.EmissiveStrength,
            OcclusionStrength = m.OcclusionStrength,
            NormalMapScale    = m.NormalMapScale,
            AlphaMode         = m.AlphaMode,
            AlphaCutoff       = m.AlphaCutoff,
            DoubleSided       = m.DoubleSided,
        };

        public readonly void Apply(PbrMaterial m)
        {
            m.BaseColorFactor   = BaseColorFactor;
            m.MetallicFactor    = MetallicFactor;
            m.RoughnessFactor   = RoughnessFactor;
            m.EmissiveFactor    = EmissiveFactor;
            m.EmissiveStrength  = EmissiveStrength;
            m.OcclusionStrength = OcclusionStrength;
            m.NormalMapScale    = NormalMapScale;
            m.AlphaMode         = AlphaMode;
            m.AlphaCutoff       = AlphaCutoff;
            m.DoubleSided       = DoubleSided;
        }

        public readonly bool Equals(PbrMatSnapshot o) =>
            BaseColorFactor == o.BaseColorFactor &&
            MetallicFactor == o.MetallicFactor &&
            RoughnessFactor == o.RoughnessFactor &&
            EmissiveFactor == o.EmissiveFactor &&
            EmissiveStrength == o.EmissiveStrength &&
            OcclusionStrength == o.OcclusionStrength &&
            NormalMapScale == o.NormalMapScale &&
            AlphaMode == o.AlphaMode &&
            AlphaCutoff == o.AlphaCutoff &&
            DoubleSided == o.DoubleSided;

        public override readonly bool Equals(object? obj) => obj is PbrMatSnapshot s && Equals(s);
        public override readonly int GetHashCode() => HashCode.Combine(
            BaseColorFactor, MetallicFactor, RoughnessFactor, EmissiveFactor,
            EmissiveStrength, OcclusionStrength, NormalMapScale,
            HashCode.Combine(AlphaMode, AlphaCutoff, DoubleSided));
    }

    public static class PbrMaterialSerializer
    {
        public const string Extension = ".pbrmat";
        private const int Version = 1;

        /// <summary>A map path is persistable only when it is a real Assets file (not a glTF
        /// synthetic cache key, which contains '#').</summary>
        private static string PersistablePath(string? p) =>
            !string.IsNullOrEmpty(p) && !p.Contains('#') ? GameEditor.Framework.Assets.AssetDatabase.Current(p) : "";   // where the file is now, if it was moved since the material was loaded

        public static string Serialize(PbrMaterial m)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteNumber("version", Version);
                if (!string.IsNullOrEmpty(m.Name)) w.WriteString("name", m.Name);

                WriteVec4(w, "baseColor", m.BaseColorFactor);
                w.WriteNumber("metallic",          m.MetallicFactor);
                w.WriteNumber("roughness",         m.RoughnessFactor);
                WriteVec3(w, "emissive", m.EmissiveFactor);
                w.WriteNumber("emissiveStrength",  m.EmissiveStrength);
                w.WriteNumber("occlusionStrength", m.OcclusionStrength);
                w.WriteNumber("normalScale",       m.NormalMapScale);
                w.WriteNumber("alphaMode",         m.AlphaMode);
                w.WriteNumber("alphaCutoff",       m.AlphaCutoff);
                w.WriteBoolean("doubleSided",      m.DoubleSided);

                w.WriteString("baseColorMap",         PersistablePath(m.BaseColorMapPath));
                w.WriteString("metallicRoughnessMap", PersistablePath(m.MetallicRoughnessMapPath));
                w.WriteString("normalMap",            PersistablePath(m.NormalMapPath));
                w.WriteString("occlusionMap",         PersistablePath(m.OcclusionMapPath));
                w.WriteString("emissiveMap",          PersistablePath(m.EmissiveMapPath));
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        /// <summary>
        /// Parse a `.pbrmat` document into a fresh <see cref="PbrMaterial"/> with factors/flags
        /// set and map *paths* populated. GPU texture views are loaded separately by the caller
        /// (SceneManager) from those paths.
        /// </summary>
        public static PbrMaterial Deserialize(ReadOnlySpan<byte> json, string fallbackName)
        {
            var m = new PbrMaterial { Name = fallbackName };
            // The texture maps it names, where they are now.
            using var doc = JsonDocument.Parse(GameEditor.Framework.Assets.AssetRefs.Resolve(Encoding.UTF8.GetString(json)));
            JsonElement r = doc.RootElement;

            if (r.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String)
                m.Name = nm.GetString() ?? fallbackName;

            m.BaseColorFactor   = ReadVec4(r, "baseColor", Vector4.One);
            m.MetallicFactor    = ReadFloat(r, "metallic", 1f);
            m.RoughnessFactor   = ReadFloat(r, "roughness", 1f);
            m.EmissiveFactor    = ReadVec3(r, "emissive", Vector3.Zero);
            m.EmissiveStrength  = ReadFloat(r, "emissiveStrength", 1f);
            m.OcclusionStrength = ReadFloat(r, "occlusionStrength", 1f);
            m.NormalMapScale    = ReadFloat(r, "normalScale", 1f);
            m.AlphaMode         = ReadInt(r, "alphaMode", 0);
            m.AlphaCutoff       = ReadFloat(r, "alphaCutoff", 0.5f);
            m.DoubleSided       = ReadBool(r, "doubleSided", false);

            m.BaseColorMapPath         = ReadString(r, "baseColorMap");
            m.MetallicRoughnessMapPath = ReadString(r, "metallicRoughnessMap");
            m.NormalMapPath            = ReadString(r, "normalMap");
            m.OcclusionMapPath         = ReadString(r, "occlusionMap");
            m.EmissiveMapPath          = ReadString(r, "emissiveMap");
            return m;
        }

        // ── write helpers ─────────────────────────────────────────────────────────────
        private static void WriteVec3(Utf8JsonWriter w, string name, Vector3 v)
        {
            w.WriteStartArray(name); w.WriteNumberValue(v.X); w.WriteNumberValue(v.Y); w.WriteNumberValue(v.Z); w.WriteEndArray();
        }
        private static void WriteVec4(Utf8JsonWriter w, string name, Vector4 v)
        {
            w.WriteStartArray(name); w.WriteNumberValue(v.X); w.WriteNumberValue(v.Y); w.WriteNumberValue(v.Z); w.WriteNumberValue(v.W); w.WriteEndArray();
        }

        // ── read helpers ──────────────────────────────────────────────────────────────
        private static float ReadFloat(JsonElement r, string n, float dflt)
            => r.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.Number ? e.GetSingle() : dflt;
        private static int ReadInt(JsonElement r, string n, int dflt)
            => r.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : dflt;
        private static bool ReadBool(JsonElement r, string n, bool dflt)
            => r.TryGetProperty(n, out var e) && (e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False) ? e.GetBoolean() : dflt;
        private static string ReadString(JsonElement r, string n)
            => r.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.String ? (e.GetString() ?? "") : "";

        private static Vector3 ReadVec3(JsonElement r, string n, Vector3 dflt)
        {
            if (!r.TryGetProperty(n, out var e) || e.ValueKind != JsonValueKind.Array || e.GetArrayLength() < 3) return dflt;
            return new Vector3(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
        }
        private static Vector4 ReadVec4(JsonElement r, string n, Vector4 dflt)
        {
            if (!r.TryGetProperty(n, out var e) || e.ValueKind != JsonValueKind.Array || e.GetArrayLength() < 4) return dflt;
            return new Vector4(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle(), e[3].GetSingle());
        }
    }
}
