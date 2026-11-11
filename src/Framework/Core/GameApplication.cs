using System;
using System.IO;
using Sokol;
using Sokol.GUI;
using static Sokol.SApp;
using static Sokol.NanoVG;
using static Sokol.STM;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using GameEditor.Framework.ECS;
using GameEditor.Framework.Renderer.Server;
using GameEditor.Framework.Renderer.Server.Lighting;

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
        static IntPtr _nvgCtx = IntPtr.Zero;
        static Screen? _screen;

        /// <summary>Resolved project config (null until <see cref="Init"/> succeeds).</summary>
        public static ProjectConfig? Config { get; private set; }

        /// <summary>
        /// Override to specify the folder that contains config.json.
        /// Defaults to <see cref="AppContext.BaseDirectory"/> when null.
        /// </summary>
        public static string? ProjectFolder { get; set; }


        // A project whose files were moved or renamed after its scenes were last saved comes with an index: where
        // those files are now. It is one of the app's embedded files, so it is there before anything is read.
        static void LoadAssetIndex()
        {
            string? index = EmbeddedFiles.ReadText(Assets.AssetDatabase.IndexFileName);
            if (index == null) return;
            if (Assets.AssetDatabase.LoadIndex(index)) Logger.Info("[GameApplication] Asset index loaded: files moved since the scenes were saved are found.");
            else Logger.Warning("[GameApplication] The asset index is not one this version reads: files are found by the names the scenes have.");
        }

        static void RegisterAvailableGameBehaviours()
        {
#if !GAME_EDITOR && ! __ANDROID__ && !__IOS__
            // GameBehaviour is now in GameEditor.Framework.dll (a separate assembly).
            // Game script types live in the entry/calling assembly, so scan all loaded assemblies.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type == typeof(GameBehaviour)) continue;
                    if (!type.IsSubclassOf(typeof(GameBehaviour))) continue;
                    var captured = type;
                    ScriptSystem.RegisterType(type.Name, () =>
                        (GameBehaviour)(Activator.CreateInstance(captured)
                            ?? throw new InvalidOperationException($"Cannot create {captured.Name}")));
                }
            }
#endif
        }
        // ── Init ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads config.json, loads the default scene and wires the Logger to stdout.
        /// Call from your Sokol Init() callback; it initialises the file system internally.
        /// </summary>
        public static void Init(ProjectConfig? projectConfig = null,bool LoadFromassets = false)
        {

            RegisterAvailableGameBehaviours();

            SFilesystem.Initialize();
            Logger.OnLog -= ConsoleLog;
            Logger.OnLog += ConsoleLog;

            string folder = ProjectFolder ?? AppContext.BaseDirectory;

            LoadAssetIndex();
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

/// <summary>
        /// Async-safe initialisation for browser/WASM builds.
        /// Fetches config.json via sokol_fetch, then fetches the default scene,
        /// then fires <see cref="EventBus.SceneLoaded"/> so the caller can start play.
        /// Call from your Sokol Init() callback after
        /// Call from your Sokol Init() callback; it initialises the file system and
        /// all script types registered before this call.
        /// </summary>
        public static void InitFromAssetsAsync()
        {

            RegisterAvailableGameBehaviours();

            SFilesystem.Initialize();

            if (_screen == null)
            {
                stm_setup();
                _nvgCtx = nvgCreateSokol(NVG_ANTIALIAS | NVG_STENCIL_STROKES);
                _screen = Screen.Initialize(_nvgCtx);
            }

            Logger.OnLog -= ConsoleLog;
            Logger.OnLog += ConsoleLog;

            LoadAssetIndex();
            SFilesystem.LoadFileAsync(ConfigManager.ConfigFileName, (_, buffer, status) =>
            {
                if (status != SFileLoadStatus.Success)
                {
                    Logger.Warning("[GameApplication] config.json not found in assets — using defaults.");
                    return;
                }

                var cfg = ConfigManager.LoadFromMemory(buffer);
                if (cfg == null)
                {
                    Logger.Warning("[GameApplication] Failed to parse config.json from assets.");
                    return;
                }

                Config = cfg;
                Logger.Info($"[GameApplication] Project '{cfg.ProjectName}' loaded from assets.");

                if (!string.IsNullOrEmpty(cfg.DefaultScene))
                    SceneManager.LoadSceneFromAssetsAsync(cfg.DefaultScene);
            });
        }
        
        /// <summary>
        /// Applies the project's persisted environment (IBL/skybox, Shadow→Ambient, CSM4) to the
        /// <see cref="RenderingServer"/>. Standalone/game builds have no Environment panel, so call
        /// this once after the config is loaded (e.g. from your SceneLoaded handler) to match what
        /// the editor's Scene view shows. No-op if no config is loaded.
        /// </summary>
        public static void ApplyPersistedEnvironment()
        {
            var c = Config;
            if (c == null) return;

            var s = new EnvironmentSettings();
            EnvironmentSettings.LoadInto(s, c);
            RenderingServer.ApplyEnvironment(s);
            RenderingServer.ShadowAmbientWeight = c.EnvironmentShadowAmbient;
            RenderingServer.EnableCsm4          = c.EnvironmentCsm4;
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

        public static void Pause()
        {
            SceneManager.Pause();
        }

        // ── Frame ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Ticks the script system.  Call every frame from your Sokol Frame() callback.
        /// </summary>
        public static void Update(float deltaTime)
        {
            SFilesystem.Update();

            if (_screen != null)
            {
                float dpi = sapp_dpi_scale();
                float logicalWidth = sapp_width() / MathF.Max(1f, dpi);
                float logicalHeight = sapp_height() / MathF.Max(1f, dpi);
                _screen.Update(logicalWidth, logicalHeight, dpi);
            }

            if (SceneManager.PlayMode == PlayModeState.Playing)
            {
                SceneManager.UpdatePhysics(deltaTime);
                ScriptSystem.UpdateAll(deltaTime);
            }
        }

        public static void Render()
        {
            if (_screen == null)
                return;

            float dpi = sapp_dpi_scale();
            float logicalWidth = sapp_width() / MathF.Max(1f, dpi);
            float logicalHeight = sapp_height() / MathF.Max(1f, dpi);
            _screen.Draw(logicalWidth, logicalHeight, dpi);
        }

        public static unsafe void DispatchEvent(sapp_event* e)
        {
            _screen?.DispatchEvent(e);
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        /// <summary>
        /// Gracefully stops all scripts.  Call once from your Sokol Cleanup() callback.
        /// </summary>
        public static void Cleanup()
        {
            SFilesystem.Shutdown();
            ScriptSystem.StopAll();
            Screen.Shutdown();
            if (_nvgCtx != IntPtr.Zero)
            {
                nvgDeleteSokol(_nvgCtx);
                _nvgCtx = IntPtr.Zero;
            }
            _screen = null;
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
