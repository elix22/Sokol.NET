using System;
using System.Globalization;
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// The scene's post effects (G8.9, examples/GameEditor/docs/POST_EFFECTS_DESIGN.md): the Environment's, read from
    /// config.json at the start and changeable by a game's scripts at any time (<c>PostEffects.Bloom.Intensity = 2f;</c>).
    /// Each is off at its default and costs nothing then.
    /// </summary>
    public static class PostEffects
    {
        /// <summary>The reference editor's Bloom: bright light bleeds into its surroundings.</summary>
        public struct BloomSettings
        {
            /// <summary>How strong the glow is; 0 = off.</summary>
            public float Intensity;
            /// <summary>The brightness (gamma space, as the reference editor's) under which a pixel does not glow.</summary>
            public float Threshold;
            /// <summary>How far the glow spreads, 0..1.</summary>
            public float Scatter;
            /// <summary>The glow's colour (its brightness taken out: only the hue counts).</summary>
            public Vector3 Tint;
            /// <summary>The brightest a pixel is taken as (a single very bright pixel does not flood the picture).</summary>
            public float Clamp;
            /// <summary>13 taps a step down instead of 4: a smoother glow, at a cost.</summary>
            public bool HighQuality;

            public static BloomSettings Default => new() { Intensity = 0f, Threshold = 0.9f, Scatter = 0.7f, Tint = Vector3.One, Clamp = 65472f };

            public readonly bool On => Intensity > 0f;
        }

        /// <summary>The reference editor's Vignette: the picture's edges darkened (or coloured) towards its corners.</summary>
        public struct VignetteSettings
        {
            /// <summary>How strong; 0 = off.</summary>
            public float Intensity;
            /// <summary>How soft its edge is, 0.01..1.</summary>
            public float Smoothness;
            /// <summary>Its centre on the picture, 0..1 (0, 0 the top left).</summary>
            public Vector2 Center;
            /// <summary>The colour the edges go to (black by default).</summary>
            public Vector3 Color;
            /// <summary>Round on any picture (else an ellipse of the picture's shape).</summary>
            public bool Rounded;

            public static VignetteSettings Default => new() { Intensity = 0f, Smoothness = 0.2f, Center = new Vector2(0.5f, 0.5f), Color = Vector3.Zero };

            public readonly bool On => Intensity > 0f;
        }

        /// <summary>The reference editor's Color Adjustments (its Post Exposure is the Environment's Exposure).</summary>
        public struct ColorAdjustmentSettings
        {
            /// <summary>-100..100; 0 = none.</summary>
            public float Contrast;
            /// <summary>The picture multiplied by it (white = none).</summary>
            public Vector3 ColorFilter;
            /// <summary>Degrees, -180..180.</summary>
            public float HueShift;
            /// <summary>-100 (grey)..100; 0 = none.</summary>
            public float Saturation;

            public static ColorAdjustmentSettings Default => new() { ColorFilter = Vector3.One };

            public readonly bool On => Contrast != 0f || HueShift != 0f || Saturation != 0f || ColorFilter != Vector3.One;
        }

        /// <summary>The bloom; off (Intensity 0) by default.</summary>
        public static BloomSettings Bloom = BloomSettings.Default;

        /// <summary>The vignette; off (Intensity 0) by default.</summary>
        public static VignetteSettings Vignette = VignetteSettings.Default;

        /// <summary>The colour adjustments; none by default.</summary>
        public static ColorAdjustmentSettings ColorAdjustments = ColorAdjustmentSettings.Default;

        /// <summary>Any post effect is on: the scene is drawn through a target and the post pass.</summary>
        public static bool Any => Bloom.On || Vignette.On || ColorAdjustments.On;

        /// <summary>Every effect back to its default (off).</summary>
        public static void Reset()
        {
            Bloom = BloomSettings.Default;
            Vignette = VignetteSettings.Default;
            ColorAdjustments = ColorAdjustmentSettings.Default;
        }

        /// <summary>The project's post effects (config.json's environmentBloom… keys): at a game's start, in the editor when
        /// they are edited and when Play stops (a script's changes last until then).</summary>
        public static void Load(GameEditor.Framework.Core.ProjectConfig c)
        {
            Bloom = new BloomSettings
            {
                Intensity = MathF.Max(0f, c.EnvironmentBloomIntensity),
                Threshold = MathF.Max(0f, c.EnvironmentBloomThreshold),
                Scatter = Math.Clamp(c.EnvironmentBloomScatter, 0f, 1f),
                Tint = ParseTint(c.EnvironmentBloomTint),
                Clamp = MathF.Max(0f, c.EnvironmentBloomClamp),
                HighQuality = c.EnvironmentBloomHq,
            };
            Vector3 centre = ParseVector(c.EnvironmentVignetteCenter + " 0", new Vector3(0.5f, 0.5f, 0f));
            Vignette = new VignetteSettings
            {
                Intensity = Math.Clamp(c.EnvironmentVignetteIntensity, 0f, 1f),
                Smoothness = Math.Clamp(c.EnvironmentVignetteSmoothness, 0.01f, 1f),
                Center = new Vector2(centre.X, centre.Y),
                Color = ParseVector(c.EnvironmentVignetteColor, Vector3.Zero),
                Rounded = c.EnvironmentVignetteRounded,
            };
            ColorAdjustments = new ColorAdjustmentSettings
            {
                Contrast = Math.Clamp(c.EnvironmentContrast, -100f, 100f),
                ColorFilter = ParseVector(c.EnvironmentColorFilter, Vector3.One),
                HueShift = Math.Clamp(c.EnvironmentHueShift, -180f, 180f),
                Saturation = Math.Clamp(c.EnvironmentSaturation, -100f, 100f),
            };
        }

        /// <summary>A tint as config.json keeps it: "r g b".</summary>
        public static string TintText(Vector3 t) => string.Create(CultureInfo.InvariantCulture, $"{t.X} {t.Y} {t.Z}");

        /// <summary>A centre as config.json keeps it: "x y".</summary>
        public static string PointText(Vector2 p) => string.Create(CultureInfo.InvariantCulture, $"{p.X} {p.Y}");

        /// <summary>The uber pass's vignette and grading values (the reference editor's: intensity × 3, smoothness × 5, the
        /// roundness the picture's aspect when Rounded; contrast and saturation / 100 + 1, the hue shift in turns).</summary>
        public static (Vector4 color, Vector4 vignette, Vector4 grade, Vector4 filter) UberValues(float aspect)
        {
            var v = Vignette;
            var g = ColorAdjustments;
            return (new Vector4(v.Color, v.Rounded ? aspect : 1f),
                    new Vector4(v.Center, v.On ? v.Intensity * 3f : 0f, v.Smoothness * 5f),
                    new Vector4(g.Contrast / 100f + 1f, g.Saturation / 100f + 1f, g.HueShift / 360f, g.On ? 1f : 0f),
                    new Vector4(g.ColorFilter, 0f));
        }

        private static Vector3 ParseTint(string? text) => ParseVector(text, Vector3.One);

        private static Vector3 ParseVector(string? text, Vector3 otherwise)
        {
            var parts = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g)
                && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
                return new Vector3(MathF.Max(0f, r), MathF.Max(0f, g), MathF.Max(0f, b));
            return otherwise;
        }

        /// <summary>The reference editor's: its threshold is in gamma space, the bloom works on linear light.</summary>
        public static float LinearThreshold(float gamma) => MathF.Pow(MathF.Max(0f, gamma), 2.2f);

        /// <summary>The reference editor's: the tint's luminance taken out, so it colours the glow without making it brighter.</summary>
        public static Vector3 NormalisedTint(Vector3 tint)
        {
            float luma = Vector3.Dot(tint, new Vector3(0.2126729f, 0.7151522f, 0.0721750f));
            return luma > 0f ? tint / luma : Vector3.One;
        }

        /// <summary>The reference editor's: Scatter 0..1 is a mix of 0.05..0.95 between a level and the one under it.</summary>
        public static float ScatterMix(float scatter) => 0.05f + 0.9f * Math.Clamp(scatter, 0f, 1f);

        /// <summary>The number of levels of the chain for a picture of <paramref name="width"/> × <paramref name="height"/>
        /// (the reference editor's: from half size, halving until about 4 px, at most 6).</summary>
        public static int BloomLevels(int width, int height)
        {
            int tw = Math.Max(1, width / 2), th = Math.Max(1, height / 2);
            int n = (int)MathF.Floor(MathF.Log2(Math.Max(tw, th)) - 1f);
            return Math.Clamp(n, 1, 6);
        }
    }
}
