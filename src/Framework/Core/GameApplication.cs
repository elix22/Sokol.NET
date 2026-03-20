using System;
using System.IO;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using GameEditor.Framework.ECS;

namespace GameEditor.Framework.Core
{
    /// <summary>
    /// Startup helper for deployed game projects built from the Sokol.NET template.
    ///
    /// Typical usage in the generated &lt;GameName&gt;-app.cs:
    /// <code>
    /// [UnmanagedCallersOnly] static void Init()
    /// {
    ///     // Set up Sokol (sg_setup, simgui_setup, …);
    ///     GameFileSystem.Instance.Initialize();
    ///
    ///     // Register all your GameBehaviour subclasses:
    ///     ScriptSystem.RegisterType&lt;MyBehaviour&gt;();
    ///
    ///     // Load config.json + default scene, wire Logger to console:
    ///     GameApplication.Init();
    ///     GameApplication.StartPlay();
    /// }
    ///
    /// [UnmanagedCallersOnly] static void Frame()
    /// {
    ///     GameFileSystem.Instance.Update();          // pump sokol_fetch
    ///     GameApplication.Update(sapp_frame_duration());
    ///     // render …
    /// }
    ///
    /// [UnmanagedCallersOnly] static void Cleanup()
    /// {
    ///     GameApplication.Cleanup();
    ///     // sg_shutdown(), etc.
    /// }
    /// </code>
    /// </summary>
    public static class GameApplication
    {
        /// <summary>Resolved project config (null until <see cref="Init"/> succeeds).</summary>
        public static ProjectConfig? Config { get; private set; }

        /// <summary>
        /// Override to specify the folder that contains config.json.
        /// Defaults to <see cref="AppContext.BaseDirectory"/> when null.
        /// </summary>
        public static string? ProjectFolder { get; set; }

        // ── Init ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads config.json, loads the default scene and wires the Logger to stdout.
        /// Call from your Sokol Init() callback after <see cref="GameFileSystem.Instance.Initialize"/>.
        /// </summary>
        public static void Init(ProjectConfig? projectConfig = null,bool LoadFromassets = false)
        {
            // Wire logger to stdout so the user sees messages in the console
            Logger.OnLog -= ConsoleLog;
            Logger.OnLog += ConsoleLog;

            string folder = ProjectFolder ?? AppContext.BaseDirectory;

            var cfg = projectConfig ?? ConfigManager.Load(folder);
            if (cfg == null)
            {
                Logger.Warning("[GameApplication] config.json not found — using defaults.");
                return;
            }

            Config = cfg;
            Logger.Info($"[GameApplication] Project '{cfg.ProjectName}' loaded.");

            // Load default scene
            if (!string.IsNullOrEmpty(cfg.DefaultScene))
            {
                if(LoadFromassets)
                {
                    SceneManager.LoadSceneFromAssetsAsync(cfg.DefaultScene);
                    return;
                }
                string scenePath = Path.IsPathRooted(cfg.DefaultScene)
                    ? cfg.DefaultScene
                    : Path.Combine(folder, cfg.DefaultScene);

                if (File.Exists(scenePath))
                {
                    SceneManager.LoadScene(scenePath);
                    Logger.Info($"[GameApplication] Default scene loaded: {scenePath}");
                }
                else
                {
                    Logger.Warning($"[GameApplication] Default scene not found: {scenePath}");
                }
            }
        }

        // ── Play ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Populates the <see cref="ScriptSystem"/> from the current scene and starts play mode.
        /// Call after <see cref="Init"/> and after all script types are registered.
        /// </summary>
        public static void StartPlay()
        {
            if (SceneManager.ActiveScene == null)
            {
                Logger.Warning("[GameApplication] No active scene to play.");
                return;
            }

            // SceneManager.Play() will call ScriptSystem.PopulateFromScene + StartAll
            SceneManager.Play();
        }

        // ── Frame ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Ticks the script system.  Call every frame from your Sokol Frame() callback.
        /// </summary>
        public static void Update(float deltaTime)
        {
            if (SceneManager.PlayMode == PlayModeState.Playing)
                ScriptSystem.UpdateAll(deltaTime);
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        /// <summary>
        /// Gracefully stops all scripts.  Call once from your Sokol Cleanup() callback.
        /// </summary>
        public static void Cleanup()
        {
            ScriptSystem.StopAll();
            Logger.Info("[GameApplication] Shutdown complete.");
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private static void ConsoleLog(LogLevel level, string msg)
        {
            string pfx = level switch
            {
                LogLevel.Warning => "[WARN]  ",
                LogLevel.Error   => "[ERROR] ",
                _                => "[INFO]  "
            };
            Console.WriteLine(pfx + msg);
        }
    }
}
