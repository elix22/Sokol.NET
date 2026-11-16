using System.Text;
using GameEditor.Framework.Core;
using GameEditor.Framework.Renderer.Server;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>Quality levels (G8.5): a new project's three levels and their defaults per target, config.json's "quality"
    /// block written and read back, and a level's values reaching the renderer.</summary>
    [Collection("Sequential")]
    public sealed class QualityTests
    {
        [Fact]
        public void A_new_project_has_High_Medium_Mobile_Low_and_a_default_per_target()
        {
            var cfg = new ProjectConfig();
            Assert.Equal(new[] { "High", "Medium", "Mobile", "Low" }, cfg.QualityLevels.ConvertAll(l => l.Name));
            Assert.Equal("High", Quality.DefaultOf(cfg, QualityTarget.Desktop));
            Assert.Equal("Mobile", Quality.DefaultOf(cfg, QualityTarget.Android));
            Assert.Equal("Medium", Quality.DefaultOf(cfg, QualityTarget.Ios));
            Assert.Equal("Medium", Quality.DefaultOf(cfg, QualityTarget.Web));
            // High: Forward+, 256 lights, 4 cascades to 150 m (as the reference editor's top level), 2048, 9 taps, HDR, full scale.
            var high = cfg.QualityLevels[0];
            Assert.Equal(("forward+", 256, 4, 150f, 2048, true, 9, true, 1f),
                (high.RenderingPath, high.MaxLights, high.Cascades, high.ShadowDistance, high.ShadowResolution, high.PunctualShadows, high.SoftShadows, high.Hdr, high.RenderScale));
            // Medium (phones, the web) and Low: one map around the casters; their distances for when they are set to 4.
            Assert.Equal((1, 100f), (cfg.QualityLevels[1].Cascades, cfg.QualityLevels[1].ShadowDistance));
            Assert.Equal((1, 50f), (cfg.QualityLevels[3].Cascades, cfg.QualityLevels[3].ShadowDistance));
            // Mobile (Android): Medium's path, lights, cascade, distance, map, spot / point shadows and HDR - one shadow tap, half
            // the 3D's pixels (a Galaxy A71: a full-screen shadowed surface 117 -> 25 ms a frame, 40 lamps 76 -> 23 ms).
            var mobile = cfg.QualityLevels[2];
            Assert.Equal(("forward+", 32, 1, 100f, 2048, true, 1, true, 0.5f),
                (mobile.RenderingPath, mobile.MaxLights, mobile.Cascades, mobile.ShadowDistance, mobile.ShadowResolution, mobile.PunctualShadows,
                 mobile.SoftShadows, mobile.Hdr, mobile.RenderScale));
            var low = cfg.QualityLevels[3];
            Assert.Equal(("forward", 8, 1024, false, 1, false, 0.75f),
                (low.RenderingPath, low.MaxLights, low.ShadowResolution, low.PunctualShadows, low.SoftShadows, low.Hdr, low.RenderScale));
        }

        [Fact]
        public void The_quality_block_is_written_and_read_back()
        {
            var cfg = new ProjectConfig();
            cfg.QualityLevels[1].Cascades = 4;
            cfg.QualityLevels[1].RenderScale = 0.85f;
            cfg.QualityLevels[1].ShadowDistance = 80f;
            cfg.QualityLevels.Add(new QualityLevel { Name = "Ultra", SoftShadows = 25, ShadowResolution = 4096 });
            cfg.QualityDefaults[QualityTarget.Web] = "Low";
            cfg.QualityEditor = "Ultra";
            var read = ConfigManager.LoadFromMemory(Encoding.UTF8.GetBytes(ConfigManager.Serialize(cfg)))!;
            Assert.Equal(new[] { "High", "Medium", "Mobile", "Low", "Ultra" }, read.QualityLevels.ConvertAll(l => l.Name));
            Assert.Equal(4, read.QualityLevels[1].Cascades);
            Assert.Equal(0.85f, read.QualityLevels[1].RenderScale, 3);
            Assert.Equal(80f, read.QualityLevels[1].ShadowDistance, 3);
            Assert.Equal((25, 4096), (read.QualityLevels[4].SoftShadows, read.QualityLevels[4].ShadowResolution));
            Assert.Equal("Low", Quality.DefaultOf(read, QualityTarget.Web));
            Assert.Equal("High", Quality.DefaultOf(read, QualityTarget.Desktop));
            Assert.Equal("Ultra", read.QualityEditor);
            // A config without the block: the four levels.
            var old = ConfigManager.LoadFromMemory(Encoding.UTF8.GetBytes("{ \"projectName\": \"Old\" }"))!;
            Assert.Equal(4, old.QualityLevels.Count);
            // A table of before the Mobile level that names no defaults: Android at its Medium (not its first level, High).
            var three = ConfigManager.LoadFromMemory(Encoding.UTF8.GetBytes(
                "{ \"quality\": { \"levels\": [ { \"name\": \"High\" }, { \"name\": \"Medium\" }, { \"name\": \"Low\" } ] } }"))!;
            Assert.Equal("Medium", Quality.DefaultOf(three, QualityTarget.Android));
            Assert.Equal("High", Quality.DefaultOf(three, QualityTarget.Desktop));
            // Levels written before the shadow distance: a default level's name takes that level's new cascades and distance
            // (one cascade was coarse then); a level of the user's own keeps its cascades, at the default distance.
            var before = ConfigManager.LoadFromMemory(Encoding.UTF8.GetBytes(
                "{ \"quality\": { \"levels\": [ { \"name\": \"High\", \"cascades\": 1 }, { \"name\": \"Mine\", \"cascades\": 1 } ] } }"))!;
            Assert.Equal((4, 150f), (before.QualityLevels[0].Cascades, before.QualityLevels[0].ShadowDistance));
            Assert.Equal((1, 150f), (before.QualityLevels[1].Cascades, before.QualityLevels[1].ShadowDistance));
            // A default naming no level: the first level.
            read.QualityDefaults[QualityTarget.Android] = "Gone";
            Assert.Equal("High", Quality.DefaultOf(read, QualityTarget.Android));
        }

        [Fact]
        public void A_levels_values_for_the_renderer()
        {
            // The renderer's own settings are set from these by Quality.Apply (seen in the editor's check: RenderingServer
            // needs the native library, not loaded in these tests).
            var levels = QualityLevel.Defaults();
            var mobile = Quality.ValuesOf(levels[2]);
            Assert.Equal((RenderingPath.ForwardPlus, 32, false, 2048, true, 0, true, 0.5f),
                (mobile.Path, mobile.MaxLights, mobile.Csm4, mobile.ShadowResolution, mobile.PunctualShadows, mobile.ShadowQuality, mobile.Hdr, mobile.RenderScale));
            var low = Quality.ValuesOf(levels[3]);
            Assert.Equal((RenderingPath.Forward, 8, false, 1024, false, 0, false, 0.75f),
                (low.Path, low.MaxLights, low.Csm4, low.ShadowResolution, low.PunctualShadows, low.ShadowQuality, low.Hdr, low.RenderScale));
            var high = Quality.ValuesOf(levels[0]);
            Assert.Equal((RenderingPath.ForwardPlus, 256, true, 150f, 2048, true, 1, true, 1f),
                (high.Path, high.MaxLights, high.Csm4, high.ShadowDistance, high.ShadowResolution, high.PunctualShadows, high.ShadowQuality, high.Hdr, high.RenderScale));
            Assert.Equal(50f, low.ShadowDistance);
            Assert.Equal(32, Quality.ValuesOf(levels[1]).MaxLights);
            // Out of range: clamped.
            var odd = Quality.ValuesOf(new QualityLevel { MaxLights = 0, Cascades = 4, ShadowDistance = 1f, ShadowResolution = 99999, SoftShadows = 25, RenderScale = 0.1f });
            Assert.Equal((1, true, 5f, 4096, 2, 0.5f), (odd.MaxLights, odd.Csm4, odd.ShadowDistance, odd.ShadowResolution, odd.ShadowQuality, odd.RenderScale));
        }
    }
}
