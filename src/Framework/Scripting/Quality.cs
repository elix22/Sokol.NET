// Quality.cs — G8.5 (examples/GameEditor/docs/QUALITY_TIERS_DESIGN.md): the project's quality levels at run time — the
// reference editor's QualitySettings (names, the level, set it from an options menu). A built game starts at its
// target's default level; an editor draws at the level chosen in its Project Settings.

using System;
using System.Collections.Generic;
using GameEditor.Framework.Core;
using GameEditor.Framework.Renderer.Server;

namespace GameEditor.Framework.Scripting
{
    public static class Quality
    {
        private static List<QualityLevel> _levels = QualityLevel.Defaults();
        private static int _level;

        /// <summary>The levels' names, in the order of the project's table.</summary>
        public static string[] Names
        {
            get
            {
                var n = new string[_levels.Count];
                for (int i = 0; i < n.Length; i++) n[i] = _levels[i].Name;
                return n;
            }
        }

        /// <summary>The level drawn now: its index in <see cref="Names"/>.</summary>
        public static int Level => _level;

        /// <summary>The level drawn now.</summary>
        public static QualityLevel Current => _levels[_level];

        /// <summary>Draws at the level of this index from the next frame; false when there is none.</summary>
        public static bool Set(int index)
        {
            if ((uint)index >= (uint)_levels.Count) return false;
            _level = index;
            Apply();
            return true;
        }

        /// <summary>Draws at the level of this name (any case) from the next frame; false when there is none.</summary>
        public static bool Set(string name) => Set(IndexOf(_levels, name));

        /// <summary>The target this app was built for.</summary>
        public static QualityTarget ThisTarget
        {
            get
            {
#if WEB
                return QualityTarget.Web;
#else
                if (OperatingSystem.IsBrowser()) return QualityTarget.Web;
                // An Android build is NativeAOT for linux-bionic, where OperatingSystem.IsAndroid() is false.
                if (GameApplication.IsAndroid) return QualityTarget.Android;
                if (OperatingSystem.IsIOS()) return QualityTarget.Ios;
                return QualityTarget.Desktop;
#endif
            }
        }

        /// <summary>
        /// Takes a project's levels (the config's own list: an editor changing a value calls <see cref="Apply"/>) and draws
        /// at <paramref name="levelName"/> — or, when it names none, the first level.
        /// </summary>
        public static void Load(ProjectConfig config, string? levelName)
        {
            if (config.QualityLevels.Count == 0) config.QualityLevels = QualityLevel.Defaults();
            _levels = config.QualityLevels;
            _level = Math.Max(0, IndexOf(_levels, levelName));
            Apply();
        }

        /// <summary>The level of a target in a config (its default), or the first level when it names none.</summary>
        public static string DefaultOf(ProjectConfig config, QualityTarget t)
        {
            string name = config.QualityDefaults.TryGetValue(t, out var n) ? n : QualityLevel.DefaultFor(t);
            return IndexOf(config.QualityLevels, name) >= 0 ? name : (config.QualityLevels.Count > 0 ? config.QualityLevels[0].Name : name);
        }

        /// <summary>What a level sets in the renderer (a plain struct: NativeAOT).</summary>
        public struct RendererValues
        {
            public RenderingPath Path;
            public int MaxLights;
            public bool Csm4;
            public int ShadowResolution;
            public bool PunctualShadows;
            public int ShadowQuality;     // RenderingServer's: 0 = 1 tap, 1 = 9, 2 = 25
            public bool Hdr;
            public float RenderScale;
        }

        /// <summary>The renderer's values of a level.</summary>
        public static RendererValues ValuesOf(QualityLevel l) => new()
        {
            // RenderingServer.PathOf's reading, without touching RenderingServer (its type needs the native library).
            Path             = string.Equals(l.RenderingPath?.Trim(), "forward", StringComparison.OrdinalIgnoreCase) ? RenderingPath.Forward : RenderingPath.ForwardPlus,
            MaxLights        = Math.Clamp(l.MaxLights, 1, 256),
            Csm4             = l.Cascades >= 4,
            ShadowResolution = Math.Clamp(l.ShadowResolution, 256, 4096),
            PunctualShadows  = l.PunctualShadows,
            ShadowQuality    = l.SoftShadows >= 25 ? 2 : (l.SoftShadows >= 9 ? 1 : 0),
            Hdr              = l.Hdr,
            RenderScale      = Math.Clamp(l.RenderScale, 0.5f, 1f),
        };

        /// <summary>The current level's values given to the renderer (again: after a value of it changed).</summary>
        public static void Apply()
        {
            if (_level >= _levels.Count) _level = 0;
            if (_levels.Count == 0) return;
            var v = ValuesOf(_levels[_level]);
            RenderingServer.Path             = v.Path;
            RenderingServer.MaxLights        = v.MaxLights;
            RenderingServer.EnableCsm4       = v.Csm4;
            RenderingServer.ShadowResolution = v.ShadowResolution;
            RenderingServer.PunctualShadows  = v.PunctualShadows;
            RenderingServer.ShadowQuality    = v.ShadowQuality;
            RenderingServer.HdrAllowed       = v.Hdr;
            RenderingServer.RenderScale      = v.RenderScale;
        }

        private static int IndexOf(List<QualityLevel> levels, string? name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            for (int i = 0; i < levels.Count; i++)
                if (string.Equals(levels[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
