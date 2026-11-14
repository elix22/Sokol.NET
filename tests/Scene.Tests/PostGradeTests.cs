using System.Numerics;
using System.Text;
using GameEditor.Framework.Core;
using GameEditor.Framework.Renderer.Server;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The post effects' Vignette and Color Adjustments (G8.9b, examples/GameEditor/docs/POST_EFFECTS_DESIGN.md): off by
    /// default, the uber pass's values (the reference editor's scales), config.json written and read back.</summary>
    [Collection("Sequential")]
    public sealed class PostGradeTests : IDisposable
    {
        public void Dispose() => PostEffects.Reset();

        [Fact]
        public void Vignette_and_color_adjustments_are_off_by_default_and_each_turns_the_post_pass_on()
        {
            PostEffects.Reset();
            Assert.False(PostEffects.Vignette.On);
            Assert.False(PostEffects.ColorAdjustments.On);
            Assert.False(PostEffects.Any);
            var v = PostEffects.Vignette;
            Assert.Equal((0f, 0.2f, new Vector2(0.5f, 0.5f), Vector3.Zero, false), (v.Intensity, v.Smoothness, v.Center, v.Color, v.Rounded));
            Assert.Equal(Vector3.One, PostEffects.ColorAdjustments.ColorFilter);
            PostEffects.Vignette.Intensity = 0.3f;
            Assert.True(PostEffects.Any);
            PostEffects.Reset();
            PostEffects.ColorAdjustments.Saturation = -100f;
            Assert.True(PostEffects.Any);
            PostEffects.Reset();
            PostEffects.ColorAdjustments.ColorFilter = new Vector3(1f, 0.5f, 0.5f);
            Assert.True(PostEffects.Any);
        }

        [Fact]
        public void The_uber_values_are_the_reference_editors_scales()
        {
            PostEffects.Reset();
            var (color, vignette, grade, filter) = PostEffects.UberValues(16f / 9f);
            Assert.Equal(0f, vignette.Z);                       // no vignette
            Assert.Equal(0f, grade.W);                          // no grading
            Assert.Equal(new Vector4(1f, 1f, 0f, 0f), grade);   // contrast and saturation 1 = none
            PostEffects.Vignette = new PostEffects.VignetteSettings { Intensity = 0.5f, Smoothness = 0.4f, Center = new Vector2(0.2f, 0.7f), Color = new Vector3(1f, 0f, 0f), Rounded = true };
            PostEffects.ColorAdjustments = new PostEffects.ColorAdjustmentSettings { Contrast = 50f, Saturation = -100f, HueShift = 90f, ColorFilter = new Vector3(0.5f, 1f, 1f) };
            (color, vignette, grade, filter) = PostEffects.UberValues(16f / 9f);
            Assert.Equal(new Vector4(1f, 0f, 0f, 16f / 9f), color);           // Rounded: the picture's aspect
            Assert.Equal(new Vector4(0.2f, 0.7f, 1.5f, 2f), vignette);        // intensity x 3, smoothness x 5
            Assert.Equal(new Vector4(1.5f, 0f, 0.25f, 1f), grade);            // contrast / 100 + 1, saturation / 100 + 1, turns
            Assert.Equal(new Vector4(0.5f, 1f, 1f, 0f), filter);
            PostEffects.Vignette.Rounded = false;
            Assert.Equal(1f, PostEffects.UberValues(16f / 9f).color.W);
        }

        [Fact]
        public void Vignette_and_color_adjustments_are_written_to_config_and_read_back_clamped()
        {
            var cfg = new ProjectConfig
            {
                EnvironmentVignetteIntensity = 0.6f, EnvironmentVignetteSmoothness = 0.3f, EnvironmentVignetteCenter = PostEffects.PointText(new Vector2(0.25f, 0.75f)),
                EnvironmentVignetteColor = PostEffects.TintText(new Vector3(0f, 0f, 1f)), EnvironmentVignetteRounded = true,
                EnvironmentContrast = 40f, EnvironmentColorFilter = "1 0.5 0.25", EnvironmentHueShift = -90f, EnvironmentSaturation = 30f,
            };
            PostEffects.Load(ConfigManager.LoadFromMemory(Encoding.UTF8.GetBytes(ConfigManager.Serialize(cfg)))!);
            var v = PostEffects.Vignette;
            Assert.Equal((0.6f, 0.3f, new Vector2(0.25f, 0.75f), new Vector3(0f, 0f, 1f), true), (v.Intensity, v.Smoothness, v.Center, v.Color, v.Rounded));
            var g = PostEffects.ColorAdjustments;
            Assert.Equal((40f, new Vector3(1f, 0.5f, 0.25f), -90f, 30f), (g.Contrast, g.ColorFilter, g.HueShift, g.Saturation));
            // Out of range in the file: clamped as the Inspector's fields are.
            string wild = "{ \"projectName\": \"W\", \"environmentVignetteIntensity\": 7, \"environmentSaturation\": -500, \"environmentHueShift\": 400 }";
            PostEffects.Load(ConfigManager.LoadFromMemory(Encoding.UTF8.GetBytes(wild))!);
            Assert.Equal((1f, -100f, 180f), (PostEffects.Vignette.Intensity, PostEffects.ColorAdjustments.Saturation, PostEffects.ColorAdjustments.HueShift));
            // A config of before: none of the keys - all off.
            PostEffects.Load(ConfigManager.LoadFromMemory(Encoding.UTF8.GetBytes("{ \"projectName\": \"Old\" }"))!);
            Assert.False(PostEffects.Any);
        }
    }
}
