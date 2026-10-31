// EnvironmentSettings.cs — project-global IBL/skybox configuration authored in the editor.
//
// Drives RenderingServer.ApplyEnvironment. Persisted in the project config so the chosen
// sky survives restarts. Kept deliberately small (no full Godot sun/fog/GI setup).

using System;
using System.Globalization;
using System.Numerics;
using GameEditor.Framework.Core;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    public enum EnvironmentMode
    {
        Procedural = 0,  // analytic sky/sun baker (cross-platform, no assets)
        Cubemap    = 1,  // 6 pre-baked face images from the project's Assets
    }

    /// <summary>Where diffuse ambient light comes from (Godot's Environment "Ambient Light > Source").</summary>
    public enum AmbientSource
    {
        Sky      = 0,  // the environment's sky irradiance (PBR: the diffuse IBL cube; others: its SH)
        Color    = 1,  // a constant colour
        Disabled = 2,  // no ambient light
    }

    public sealed class EnvironmentSettings
    {
        /// <summary>Number of cubemap faces (+X,-X,+Y,-Y,+Z,-Z).</summary>
        public const int FaceCount = 6;

        public EnvironmentMode Mode = EnvironmentMode.Cubemap;

        /// <summary>Assets-relative folder holding skybox_{px,nx,py,ny,pz,nz}.jpg.</summary>
        public string CubemapFolder = "skyboxes/skybox";

        /// <summary>
        /// Optional per-face overrides (Assets-relative paths). An empty entry falls back to
        /// the folder's default skybox_*.jpg name. Order: +X,-X,+Y,-Y,+Z,-Z.
        /// </summary>
        public readonly string[] FaceOverrides = new string[FaceCount];

        public float Intensity      = 1f;
        public float RotationDegrees = 0f;
        public bool  ShowSkybox     = true;

        public AmbientSource Ambient       = AmbientSource.Sky;
        /// <summary>Ambient colour for <see cref="AmbientSource.Color"/>; (0.1, 0.1, 0.1) is the old fixed Blinn-Phong ambient.</summary>
        public Vector3       AmbientColor  = new(0.1f, 0.1f, 0.1f);
        /// <summary>Multiplies the ambient of either source.</summary>
        public float         AmbientEnergy = 1f;

        /// <summary>
        /// Populates <paramref name="s"/> from the project's persisted environment fields. Shared by
        /// the editor's Environment panel and standalone/game builds (which have no panel), so the
        /// config→settings mapping lives in exactly one place.
        /// </summary>
        public static void LoadInto(EnvironmentSettings s, ProjectConfig c)
        {
            s.Mode            = string.Equals(c.EnvironmentMode, "procedural", StringComparison.OrdinalIgnoreCase)
                                ? EnvironmentMode.Procedural : EnvironmentMode.Cubemap;
            s.CubemapFolder   = string.IsNullOrWhiteSpace(c.EnvironmentFolder) ? "skyboxes/skybox" : c.EnvironmentFolder;
            s.Intensity       = c.EnvironmentIntensity;
            s.RotationDegrees = c.EnvironmentRotation;
            s.ShowSkybox      = c.EnvironmentShowSkybox;
            s.Ambient         = c.EnvironmentAmbientSource?.ToLowerInvariant() switch
            {
                "color"    => AmbientSource.Color,
                "disabled" => AmbientSource.Disabled,
                _          => AmbientSource.Sky,
            };
            s.AmbientColor    = ParseColor(c.EnvironmentAmbientColor, s.AmbientColor);
            s.AmbientEnergy   = c.EnvironmentAmbientEnergy;
            string[] parts = (c.EnvironmentFaces ?? "").Split('|');
            for (int i = 0; i < FaceCount; i++)
                s.FaceOverrides[i] = i < parts.Length ? parts[i] : "";
        }

        public EnvironmentSettings Clone()
        {
            var c = new EnvironmentSettings
            {
                Mode            = Mode,
                CubemapFolder   = CubemapFolder,
                Intensity       = Intensity,
                RotationDegrees = RotationDegrees,
                ShowSkybox      = ShowSkybox,
                Ambient         = Ambient,
                AmbientColor    = AmbientColor,
                AmbientEnergy   = AmbientEnergy,
            };
            Array.Copy(FaceOverrides, c.FaceOverrides, FaceCount);
            return c;
        }

        /// <summary>The config spelling of <see cref="Ambient"/> ("sky" | "color" | "disabled").</summary>
        public string AmbientSourceName => Ambient switch
        {
            AmbientSource.Color    => "color",
            AmbientSource.Disabled => "disabled",
            _                      => "sky",
        };

        /// <summary>The config spelling of <see cref="AmbientColor"/>: "r g b".</summary>
        public string AmbientColorText => string.Create(CultureInfo.InvariantCulture,
            $"{AmbientColor.X} {AmbientColor.Y} {AmbientColor.Z}");

        private static Vector3 ParseColor(string? text, Vector3 fallback)
        {
            var parts = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return fallback;
            var v = new float[3];
            for (int i = 0; i < 3; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return fallback;
            return new Vector3(v[0], v[1], v[2]);
        }

        /// <summary>True when the env must be rebuilt (vs. only intensity/rotation/show tweaked).</summary>
        public bool RequiresRebuild(EnvironmentSettings other)
        {
            if (Mode != other.Mode) return true;
            if (Mode == EnvironmentMode.Cubemap)
            {
                if (!string.Equals(CubemapFolder, other.CubemapFolder, StringComparison.Ordinal)) return true;
                for (int i = 0; i < FaceCount; i++)
                    if (!string.Equals(FaceOverrides[i] ?? "", other.FaceOverrides[i] ?? "", StringComparison.Ordinal))
                        return true;
            }
            return false;
        }
    }
}
