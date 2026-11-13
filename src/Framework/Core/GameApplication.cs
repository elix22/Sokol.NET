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
            // A game compiled ahead of time registers its scripts by generated code (RegisteredScripts.g.cs, a
            // module initializer that has run by now): nothing is searched for.
            if (ScriptSystem.HasGeneratedTypes) return;
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
                UI.GameUI.ShowOn(_screen);
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
                // The game's UI: the scene's UI entities as widgets over the whole picture.
                UI.GameUI.Sync(ECS.ECSWorld.Instance, logicalWidth, logicalHeight, _screen.SafeAreaInsets);
                _screen.Update(logicalWidth, logicalHeight, dpi);
            }

            if (SceneManager.PlayMode == PlayModeState.Playing)
            {
                SceneManager.UpdatePhysics(deltaTime);
                ScriptSystem.UpdateAll(deltaTime);
            }
        }

        // The 3D scene is drawn here when it has see-through materials: their pass must sample a copy of the
        // opaque picture, and the swapchain cannot be sampled.
        private static readonly Renderer.OffscreenTarget _sceneTarget = new();

        /// <summary>
        /// One whole frame of a game: input, scripts and physics, then the picture — the scene through its main
        /// camera (perspective or orthographic), meshes and sprites, and the game's UI on top — and the commit.
        /// A game's Frame callback is this one call, so what the engine learns to draw reaches every game.
        /// <paramref name="clear"/>: how the picture starts (the background colour).
        /// </summary>
        public static unsafe void RunFrame(Sokol.SG.sg_pass_action clear)
        {
            AutoScreenshots();
            Input.InputManager.BeginFrame();
            Update((float)sapp_frame_duration());

            RenderingServer.BeginFrame();

            int width = sapp_width(), height = sapp_height();
            System.Numerics.Matrix4x4 viewProj = System.Numerics.Matrix4x4.Identity;
            RenderingServer.CsmCamera? csm = null;
            if (SceneManager.GetMainCamera(width, height, out var view, out var proj, out var cam, out _))
            {
                viewProj = view * proj;
                // A perspective camera gets shadow cascades fitted to what it sees; an orthographic one a single fit.
                if (!cam.IsOrthographic) csm = new RenderingServer.CsmCamera(view, proj, cam.NearZ, cam.FarZ);
            }

            // Shadow maps go into their atlas BEFORE the scene's pass.
            RenderingServer.RenderShadowMaps(viewProj, csm);

            // Two paths, chosen per frame for the least cost:
            //   • nothing see-through → the scene goes STRAIGHT to the swapchain (no offscreen target, no
            //     full-screen copy): the common case, and the best one for a low-end phone;
            //   • see-through materials → the scene goes to an offscreen target, the refraction pass runs
            //     there, and the result is copied to the swapchain.
            if (RenderingServer.SceneNeedsTransmission)
            {
                if (width > 0 && height > 0)
                {
                    _sceneTarget.Resize(width, height);

                    Sokol.SG.sg_begin_pass(_sceneTarget.MakePass(clear.colors[0].clear_value));
                    RenderingServer.RenderSkybox(viewProj, offscreen: true);
                    RenderingServer.SubmitView(viewProj, PipelineFlags.OffscreenRt, allowShadowPassRendering: false, csm: csm);
                    Sokol.SG.sg_end_pass();

                    RenderingServer.RenderTransmission(_sceneTarget, viewProj);
                }

                Sokol.ScreenCapture.BeginFrame(clear);     // the swapchain - or, once after Screenshot(), a picture of the frame
                if (_sceneTarget.IsValid)
                    RenderingServer.BlitToSwapchain(_sceneTarget.TexView);
                Render();
                Sokol.SG.sg_end_pass();
            }
            else
            {
                Sokol.ScreenCapture.BeginFrame(clear);     // the swapchain - or, once after Screenshot(), a picture of the frame
                RenderingServer.RenderSkybox(viewProj, offscreen: false);
                RenderingServer.SubmitView(viewProj, PipelineFlags.None, allowShadowPassRendering: false, csm: csm);
                Render();
                Sokol.SG.sg_end_pass();
            }
            Sokol.ScreenCapture.Commit();
        }

        /// <summary>
        /// A picture of the next frame as it is drawn (the scene and the game's UI), written as a PNG named
        /// <paramref name="name"/> in the app's own folder (<see cref="SaveData.Folder"/>) - on a phone the only way to take one
        /// from the game itself. That frame is not shown (one black frame). Returns the file's path, or null when this
        /// platform or GPU cannot read a picture back, the app has no folder (a browser), or a picture is already on
        /// its way; <see cref="LastScreenshot"/> is the path once it is written. For a game built from the engine: in an
        /// editor it is the editor's whole window.
        /// </summary>
        public static string? Screenshot(string name)
        {
            string folder = ShotFolder();
            if (folder.Length == 0 || string.IsNullOrEmpty(name)) return null;
            // A picture the game asks for goes before a waiting automatic one (SOKOL_SCREENSHOTS), which is asked again after it.
            if (_shotAsked != null && Sokol.ScreenCapture.Pending)
            {
                Sokol.ScreenCapture.Cancel();
                if (_shotAsked.EndsWith("begin.png", StringComparison.Ordinal)) _shotBegun = false;
                else if (_shotAsked.EndsWith("end.png", StringComparison.Ordinal)) { _shotEnded = false; _shotEndIn = 2; }
                _shotAsked = null;
            }
            string path = Path.Combine(folder, name);
            return Sokol.ScreenCapture.Request(path) ? path : null;
        }

        /// <summary>The path of the last picture <see cref="Screenshot"/> wrote, null before one.</summary>
        public static string? LastScreenshot => Sokol.ScreenCapture.LastPath;

        // A game started with SOKOL_SCREENSHOTS=1 in its environment (a check launching it: `devicectl device process launch
        // --environment-variables`, or the desktop app's environment; on Android `adb shell setprop debug.sokol.screenshots 1`)
        // takes pictures of itself through ScreenCapture - the frame as drawn - in its folder, and logs "[Shot] <path>" for
        // each: begin.png 0.3 s after it starts, t004.png, t008.png, ... every 4 seconds, and end.png a few frames after a
        // script says its verdict (a line with " PASS", " FAIL" or " finished"). How a phone that cannot be photographed from
        // the computer (an iPhone on iOS 26) shows what it drew.
        private static int _shotMode = -1;            // -1: not read yet, 0: off, 1: on
        private static double _shotStart, _shotNext;
        private static bool _shotBegun, _shotEnded;
        private static int _shotEndIn;
        private static string? _shotAsked;

        // Where the pictures go: the app's own folder; on Android its external files folder
        // (/sdcard/Android/data/<package>/files), which adb can read from a release build (the inner one it cannot).
        private static string? _shotFolder;

        // An Android build is NativeAOT for linux-bionic, where OperatingSystem.IsAndroid() is false: sokol's native activity says it.
        private static unsafe bool IsAndroid => sapp_android_get_native_activity() != null;

        private static unsafe string ShotFolder()
        {
            // Not kept: the project's name (the folder's) is known only once its config is read, after the first frame.
            if (_shotFolder != null) return _shotFolder;
            if (IsAndroid)
            {
                // ANativeActivity: callbacks, vm, env, clazz, internalDataPath, externalDataPath, ...
                byte* activity = (byte*)sapp_android_get_native_activity();
                IntPtr external = *(IntPtr*)(activity + 5 * IntPtr.Size);
                string? path = external != IntPtr.Zero ? System.Runtime.InteropServices.Marshal.PtrToStringUTF8(external) : null;
                if (!string.IsNullOrEmpty(path)) return _shotFolder = path + "/";
            }
            return SaveData.Folder;
        }

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "__system_property_get")]
        private static extern unsafe int SystemPropertyGet(byte* name, byte* value);

        private static unsafe string? AndroidProperty(string name)
        {
            try
            {
                byte* value = stackalloc byte[92];                 // PROP_VALUE_MAX
                byte[] key = System.Text.Encoding.ASCII.GetBytes(name + "\0");
                fixed (byte* k = key)
                {
                    int n = SystemPropertyGet(k, value);
                    return n > 0 ? System.Text.Encoding.ASCII.GetString(value, n) : null;
                }
            }
            catch (Exception) { return null; }
        }

        private static void AutoScreenshots()
        {
            if (_shotMode < 0)
            {
                string? on = Environment.GetEnvironmentVariable("SOKOL_SCREENSHOTS");
                if (string.IsNullOrEmpty(on) && IsAndroid) on = AndroidProperty("debug.sokol.screenshots");
                _shotMode = !string.IsNullOrEmpty(on) && on != "0" ? 1 : 0;
                if (_shotMode == 0) return;
                _shotStart = stm_sec(stm_now());
                _shotNext = _shotStart + 4.0;
                Logger.OnLog += ShotOnVerdict;
                OpenLogFile();
                Logger.Info("[Shot] at the begin, every 4 s and at the end, in " + ShotFolder());
            }
            if (_shotMode == 0) return;
            if (_shotAsked != null)
            {
                if (Sokol.ScreenCapture.Pending) return;
                if (Sokol.ScreenCapture.LastPath == _shotAsked) Logger.Info("[Shot] " + _shotAsked);
                else Logger.Warning("[Shot] not taken: " + Sokol.ScreenCapture.LastError);
                _shotAsked = null;
            }
            double now = stm_sec(stm_now());
            if (!_shotBegun && now - _shotStart >= 0.3) { _shotBegun = true; _shotAsked = Screenshot("begin.png"); return; }
            if (_shotEndIn > 0 && --_shotEndIn == 0) { _shotEnded = true; _shotAsked = Screenshot("end.png"); return; }
            if (_shotBegun && !_shotEnded && _shotEndIn == 0 && now >= _shotNext)
            {
                int at = (int)Math.Round(_shotNext - _shotStart);
                _shotNext += 4.0;
                _shotAsked = Screenshot("t" + at.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".png");
            }
        }

        // The game's log lines in a file too, where a check can read them when it cannot read the app's output: an iPhone on
        // iOS 15 (launched detached: Documents/sokol_log.txt), an Android phone (its external files folder: sokol_log.txt).
        private static StreamWriter? _logFile;

        private static void OpenLogFile()
        {
            string? folder = IsAndroid ? ShotFolder() : OperatingSystem.IsIOS() ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : null;
            if (string.IsNullOrEmpty(folder)) return;
            try
            {
                _logFile = new StreamWriter(Path.Combine(folder, "sokol_log.txt"), false) { AutoFlush = true };
                Logger.OnLog += (level, msg) => { try { _logFile?.WriteLine(msg); } catch (Exception) { } };
            }
            catch (Exception) { _logFile = null; }
        }

        // A script's verdict ("[Ctl] PASS ...", "[Zip] finished place=1 ..."): the end picture, a few frames later (the
        // verdict's own picture or panel is on the screen by then).
        private static void ShotOnVerdict(LogLevel level, string msg)
        {
            if (_shotEnded || _shotEndIn > 0 || msg.StartsWith("[Shot]", StringComparison.Ordinal)) return;
            if (msg.Contains(" PASS", StringComparison.Ordinal) || msg.Contains(" FAIL", StringComparison.Ordinal) || msg.Contains(" finished", StringComparison.Ordinal))
                _shotEndIn = 4;
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
            // The app is about to be out of sight, or to end: what the game wanted kept is written now — a phone
            // may not give it another chance.
            if (e->type is sapp_event_type.SAPP_EVENTTYPE_SUSPENDED or sapp_event_type.SAPP_EVENTTYPE_QUIT_REQUESTED
                or sapp_event_type.SAPP_EVENTTYPE_UNFOCUSED)
                SaveData.Save();
            // Out of sight (a phone call, the home button): nothing is heard until the app is back.
            if (e->type == sapp_event_type.SAPP_EVENTTYPE_SUSPENDED) Sound.Audio.Pause();
            else if (e->type == sapp_event_type.SAPP_EVENTTYPE_RESUMED && SceneManager.PlayMode != PlayModeState.Paused) Sound.Audio.Resume();
            _screen?.DispatchEvent(e);
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        /// <summary>
        /// Gracefully stops all scripts.  Call once from your Sokol Cleanup() callback.
        /// </summary>
        public static void Cleanup()
        {
            SaveData.Save();
            Sound.Audio.Shutdown();
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
