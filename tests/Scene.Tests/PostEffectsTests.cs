using System.Numerics;
using System.Text;
using GameEditor.Framework.Core;
using GameEditor.Framework.Renderer.Server;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The post effects' values (G8.9, examples/GameEditor/docs/POST_EFFECTS_DESIGN.md): the bloom's defaults (off),
    /// the reference editor's threshold, tint and scatter conversions, the chain's levels, config.json written and read back.</summary>
    [Collection("Sequential")]
    public sealed class PostEffectsTests : IDisposable
    {
        public void Dispose() => PostEffects.Reset();

        [Fact]
        public void Bloom_is_off_by_default_with_the_reference_editors_values()
        {
            PostEffects.Reset();
            var b = PostEffects.Bloom;
            Assert.False(b.On);
            Assert.False(PostEffects.Any);
            Assert.Equal((0f, 0.9f, 0.7f, Vector3.One, 65472f, false), (b.Intensity, b.Threshold, b.Scatter, b.Tint, b.Clamp, b.HighQuality));
            PostEffects.Bloom.Intensity = 1f;
            Assert.True(PostEffects.Any);
        }

        [Fact]
        public void The_threshold_is_gamma_the_tint_keeps_its_hue_not_its_brightness_and_scatter_is_a_mix_of_005_to_095()
        {
            Assert.InRange(PostEffects.LinearThreshold(0.9f), 0.78f, 0.80f);
            Assert.Equal(1f, PostEffects.LinearThreshold(1f), 4);
            Assert.True(Vector3.Distance(Vector3.One, PostEffects.NormalisedTint(Vector3.One * 0.3f)) < 1e-4f);
            Vector3 red = PostEffects.NormalisedTint(new Vector3(1f, 0f, 0f));
            Assert.Equal(1f, Vector3.Dot(red, new Vector3(0.2126729f, 0.7151522f, 0.0721750f)), 4);
            Assert.Equal(Vector3.One, PostEffects.NormalisedTint(Vector3.Zero));
            Assert.Equal(0.05f, PostEffects.ScatterMix(0f), 5);
            Assert.Equal(0.95f, PostEffects.ScatterMix(1f), 5);
            Assert.Equal(0.95f, PostEffects.ScatterMix(3f), 5);
        }

        [Theory]
        [InlineData(1920, 1080, 6)]
        [InlineData(256, 256, 6)]
        [InlineData(64, 64, 4)]
        [InlineData(16, 16, 2)]
        [InlineData(4, 4, 1)]
        public void The_chain_halves_from_half_size_to_about_4_pixels_at_most_6_levels(int w, int h, int levels)
            => Assert.Equal(levels, PostEffects.BloomLevels(w, h));

        [Fact]
        public void The_bloom_is_written_to_config_and_read_back_and_loaded()
        {
            var cfg = new ProjectConfig
            {
                EnvironmentBloomIntensity = 2.5f, EnvironmentBloomThreshold = 1.2f, EnvironmentBloomScatter = 0.4f,
                EnvironmentBloomTint = PostEffects.TintText(new Vector3(1f, 0.5f, 0.25f)), EnvironmentBloomClamp = 100f, EnvironmentBloomHq = true,
            };
            var read = ConfigManager.LoadFromMemory(Encoding.UTF8.GetBytes(ConfigManager.Serialize(cfg)))!;
            PostEffects.Load(read);
            var b = PostEffects.Bloom;
            Assert.Equal((2.5f, 1.2f, 0.4f, new Vector3(1f, 0.5f, 0.25f), 100f, true), (b.Intensity, b.Threshold, b.Scatter, b.Tint, b.Clamp, b.HighQuality));
            // A config of before has none of the keys: bloom off, the defaults.
            PostEffects.Load(ConfigManager.LoadFromMemory(Encoding.UTF8.GetBytes("{ \"projectName\": \"Old\" }"))!);
            Assert.False(PostEffects.Bloom.On);
            Assert.Equal(Vector3.One, PostEffects.Bloom.Tint);
        }
    }
}
