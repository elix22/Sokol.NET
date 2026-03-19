// GameAssemblyRunner.cs
// Manages hot-reload of game-project script assemblies in the GameEditor.
//
// Flow when the user presses Play (with a project loaded):
//   1. EnsureLoaded(projectFolder) checks if sources changed and rebuilds if needed.
//   2. The resulting DLL is loaded in a CollectibleAssemblyLoadContext.
//   3. All GameBehaviour-derived types are found by name inspection.
//   4. Factories are registered with ScriptSystem.
//   5. SceneManager.Play() → ScriptSystem.PopulateFromScene() + ScriptSystem.StartAll().
//
// Flow when the user presses Stop:
//   1. SceneManager.Stop() calls ScriptSystem.StopAll().
//   2. Optionally call Unload() to release the DLL from memory.
//
// NOTE: dynamic assembly loading and reflection are used here.
//       This works fine when the editor is run via `dotnet run` (JIT / CLR).
//       The trim/AOT warnings below are suppressed because the GameEditor is
//       never AOT-published; it is always JIT-run (dotnet run / Debug build).
//
#pragma warning disable IL2026  // RequiresUnreferencedCode: LoadFromAssemblyPath
#pragma warning disable IL2065  // DynamicallyAccessedMembers: GetMethod/GetProperty on runtime Type
#pragma warning disable IL2070  // RequiresUnreferencedCode: GetTypes
#pragma warning disable IL2072  // RequiresUnreferencedCode: Activator.CreateInstance
#pragma warning disable IL2077  // RequiresUnreferencedCode: GetMethod/GetProperty
#pragma warning disable IL2080  // _wrappedType.GetField — JIT only, intentional
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using GameEditor.CodeEditor;
using GameEditor.Framework.Core;
using GameEditor.Framework.Scripting;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor
{
    /// <summary>
    /// Builds and hot-reloads the game project's script assembly inside the editor.
    /// </summary>
    public static class GameAssemblyRunner
    {
        private static AssemblyLoadContext? _context;
        private static Assembly?            _assembly;
        private static string?              _loadedProjectFolder;
        private static DateTime             _loadedDllWriteTimeUtc;

        // ── Public state ─────────────────────────────────────────────────────

        public static bool IsLoaded             => _assembly != null;
        public static bool LastBuildSucceeded    { get; private set; } = true;
        public static bool IsBuilding            { get; private set; } = false;

        /// <summary>Diagnostics produced by the most recent build. Empty when build succeeded cleanly.</summary>
        public static IReadOnlyList<BuildDiagnostic> LastBuildDiagnostics { get; private set; }
            = Array.Empty<BuildDiagnostic>();

        /// <summary>
        /// Raised on the thread-pool thread when an async build triggered by
        /// <see cref="TriggerBuild"/> finishes. The argument is the list of diagnostics.
        /// </summary>
        public static event Action<IReadOnlyList<BuildDiagnostic>>? BuildCompleted;

        private static FileSystemWatcher? _watcher;
        private static Timer?             _debounceTimer;
        private static string?            _watcherFolder;

        /// <summary>
        /// Ensures the game project's DLL is built and loaded.
        /// Rebuilds automatically when user source files are newer than the DLL.
        /// Registers all GameBehaviour factories with <see cref="ScriptSystem"/>.
        /// </summary>
        public static bool EnsureLoaded(string projectFolder)
        {
            bool sourceChanged = HasSourceChanged(projectFolder);
            bool dllChangedSinceLoad = HasBuiltDllChangedSinceLoad(projectFolder);

            if (_assembly != null && _loadedProjectFolder == projectFolder && !sourceChanged && !dllChangedSinceLoad)
            {
                Logger.Info("[GameAssemblyRunner] Assembly is up-to-date.");
                return true;
            }

            // Unload stale version
            Unload();

            // Build if DLL is missing or source changed
            if (sourceChanged || !DllExists(projectFolder))
            {
                Logger.Info("[GameAssemblyRunner] Building game project...");
                if (!Build(projectFolder))
                {
                    LastBuildSucceeded = false;
                    return false;
                }
            }

            LastBuildSucceeded = true;

            // Load and register factories
            if (!Load(projectFolder)) return false;
            // Bridge the game assembly's ECSWorld.Instance to the editor's singleton.
            // The game DLL compiles the framework source directly in, so without this
            // the game's GameBehaviour.World returns an empty, separate ECSWorld.
            BridgeECSWorld(_assembly!);
            RegisterFactoriesFromAssembly(_assembly!);
            return true;
        }

        // ── Source change detection ──────────────────────────────────────────

        /// <summary>
        /// Returns true when any user-authored .cs file in <c>Source/</c> (top-level
        /// and one level deep, excluding Framework/, sokol/, imgui/ subfolders) is
        /// newer than the compiled DLL.
        /// </summary>
        public static bool HasSourceChanged(string projectFolder)
        {
            string? dllPath = FindDllPath(projectFolder);
            if (dllPath == null || !File.Exists(dllPath)) return true;

            DateTime dllTime = File.GetLastWriteTimeUtc(dllPath);
            string sourceDir = Path.Combine(projectFolder, "Source");
            if (!Directory.Exists(sourceDir)) return false;

            return GetUserSourceFiles(sourceDir)
                .Any(f => File.GetLastWriteTimeUtc(f) > dllTime);
        }

        private static bool DllExists(string projectFolder)
        {
            string? dll = FindDllPath(projectFolder);
            return dll != null && File.Exists(dll);
        }

        // Returns .cs files the user authored, skipping the engine sub-folders
        private static IEnumerable<string> GetUserSourceFiles(string sourceDir)
        {
            // Excluded sub-folders that contain engine/library code
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Framework", "sokol", "imgui" };

            // Top-level cs files
            foreach (string f in Directory.GetFiles(sourceDir, "*.cs", SearchOption.TopDirectoryOnly))
                yield return f;

            // One level deeper — user's own script sub-folders
            foreach (string sub in Directory.GetDirectories(sourceDir))
            {
                string name = Path.GetFileName(sub);
                if (excluded.Contains(name)) continue;
                foreach (string f in Directory.GetFiles(sub, "*.cs", SearchOption.AllDirectories))
                    yield return f;
            }
        }

        // ── Build ────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs <c>dotnet build -p:BuildAsLibrary=true</c> on the game project.
        /// Build output is forwarded to the in-editor Logger / Console Window.
        /// </summary>
        public static bool Build(string projectFolder)
        {
            string? csproj = FindCsproj(projectFolder);
            if (csproj == null)
            {
                Logger.Error("[GameAssemblyRunner] No .csproj found in project folder.");
                LastBuildDiagnostics = Array.Empty<BuildDiagnostic>();
                return false;
            }

            Logger.Info($"[GameAssemblyRunner] dotnet build {Path.GetFileName(csproj)} -p:BuildAsLibrary=true");

            var psi = new ProcessStartInfo("dotnet",
                $"build \"{csproj}\" -c Debug -p:BuildAsLibrary=true --nologo -v:minimal")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                WorkingDirectory       = projectFolder
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                Logger.Error("[GameAssemblyRunner] Failed to start build process.");
                LastBuildDiagnostics = Array.Empty<BuildDiagnostic>();
                return false;
            }

            // Capture output and forward to the in-editor logger
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            foreach (string line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                Logger.Info(line.TrimEnd());
            foreach (string line in stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                Logger.Warning(line.TrimEnd());

            // Parse structured diagnostics from combined output
            string combined = stdout + "\n" + stderr;
            LastBuildDiagnostics = BuildErrorParser.Parse(combined);

            if (proc.ExitCode != 0)
            {
                Logger.Error($"[GameAssemblyRunner] Build FAILED (exit code {proc.ExitCode}).");
                return false;
            }

            Logger.Info("[GameAssemblyRunner] Build succeeded.");
            return true;
        }

        // ── Load / Unload ────────────────────────────────────────────────────

        /// <summary>Loads the game DLL in an isolated, collectible AssemblyLoadContext.</summary>
        public static bool Load(string projectFolder)
        {
            string? dllPath = FindDllPath(projectFolder);
            if (dllPath == null || !File.Exists(dllPath))
            {
                Logger.Error($"[GameAssemblyRunner] DLL not found: {dllPath}");
                return false;
            }

            Unload();

            try
            {
                string shadowDir = Path.Combine(Path.GetTempPath(), "SokolNet", "GameEditor", "ShadowAssemblies");
                Directory.CreateDirectory(shadowDir);
                string shadowPath = Path.Combine(
                    shadowDir,
                    $"{Path.GetFileNameWithoutExtension(dllPath)}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fffffff}_{Guid.NewGuid():N}.dll");
                File.Copy(dllPath, shadowPath, overwrite: true);

                _context  = new AssemblyLoadContext("GameScripts", isCollectible: true);
                _assembly = _context.LoadFromAssemblyPath(shadowPath);
                _loadedProjectFolder = projectFolder;
                _loadedDllWriteTimeUtc = File.GetLastWriteTimeUtc(dllPath);
                Logger.Info($"[GameAssemblyRunner] Loaded: {Path.GetFileName(dllPath)}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[GameAssemblyRunner] Load failed: {ex.Message}");
                _context?.Unload();
                _context  = null;
                _assembly = null;
                return false;
            }
        }

        /// <summary>Unloads the current game DLL and clears all script factories.</summary>
        public static void Unload()
        {
            if (_context == null) return;
            ScriptSystem.ClearFactories();
            _assembly = null;
            _loadedProjectFolder = null;
            _loadedDllWriteTimeUtc = default;
            _context.Unload();
            _context = null;
            Logger.Info("[GameAssemblyRunner] Game assembly unloaded.");
        }

        /// <summary>
        /// Returns true if the current project should be reloaded because either no assembly is
        /// loaded, the project changed, sources changed, or a newer built DLL exists on disk.
        /// </summary>
        public static bool NeedsReload(string projectFolder)
            => _assembly == null
            || _loadedProjectFolder != projectFolder
            || HasSourceChanged(projectFolder)
            || HasBuiltDllChangedSinceLoad(projectFolder);

        private static bool HasBuiltDllChangedSinceLoad(string projectFolder)
        {
            string? dllPath = FindDllPath(projectFolder);
            if (dllPath == null || !File.Exists(dllPath)) return false;
            return _assembly != null && File.GetLastWriteTimeUtc(dllPath) > _loadedDllWriteTimeUtc;
        }

        // ── Async build + file watcher ────────────────────────────────────────

        /// <summary>
        /// Triggers a background build, updating <see cref="IsBuilding"/> and
        /// <see cref="LastBuildSucceeded"/> when complete. No-op if already building.
        /// </summary>
        public static void TriggerBuild(string projectFolder)
        {
            if (IsBuilding) return;
            IsBuilding = true;
            Task.Run(() =>
            {
                try   { LastBuildSucceeded = Build(projectFolder); }
                catch
                {
                    LastBuildSucceeded    = false;
                    LastBuildDiagnostics = Array.Empty<BuildDiagnostic>();
                }
                finally
                {
                    IsBuilding = false;
                    BuildCompleted?.Invoke(LastBuildDiagnostics);
                }
            });
        }

        /// <summary>
        /// Starts a <see cref="FileSystemWatcher"/> on the project's Source/ directory.
        /// Any .cs change triggers a debounced async rebuild after 1.5 s of silence.
        /// </summary>
        public static void StartWatcher(string projectFolder)
        {
            StopWatcher();
            string sourceDir = Path.Combine(projectFolder, "Source");
            if (!Directory.Exists(sourceDir)) return;
            _watcherFolder = projectFolder;
            _watcher = new FileSystemWatcher(sourceDir, "*.cs")
            {
                IncludeSubdirectories = true,
                NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents   = true,
            };
            _watcher.Changed += OnSourceChanged;
            _watcher.Created += OnSourceChanged;
            _watcher.Deleted += OnSourceChanged;
            _watcher.Renamed += OnSourceRenamed;
            Logger.Info($"[GameAssemblyRunner] Watching {sourceDir} for changes.");
        }

        /// <summary>Stops the file watcher and cancels any pending debounce timer.</summary>
        public static void StopWatcher()
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
            _watcher?.Dispose();
            _watcher       = null;
            _watcherFolder = null;
        }

        private static void OnSourceChanged(object sender, FileSystemEventArgs e)
            => ScheduleRebuild();

        private static void OnSourceRenamed(object sender, RenamedEventArgs e)
        {
            // If a .cs file is renamed externally, sync the class name inside the file.
            if (e.OldFullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                e.FullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                string oldClassName = Path.GetFileNameWithoutExtension(e.OldFullPath);
                string newClassName = Path.GetFileNameWithoutExtension(e.FullPath);
                if (oldClassName != newClassName && File.Exists(e.FullPath))
                {
                    try
                    {
                        string content = File.ReadAllText(e.FullPath);
                        string updated = content.Replace($"class {oldClassName}", $"class {newClassName}");
                        if (updated != content)
                            File.WriteAllText(e.FullPath, updated);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"[GameAssemblyRunner] Could not update class name after rename: {ex.Message}");
                    }
                }
            }
            ScheduleRebuild();
        }

        private static void ScheduleRebuild()
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(_ =>
            {
                _debounceTimer = null;
                string? folder = _watcherFolder;
                if (folder != null) TriggerBuild(folder);
            }, null, 1500, Timeout.Infinite);
        }

        // ── Factory registration ─────────────────────────────────────────────

        /// <summary>
        /// Scans <paramref name="assembly"/> for GameBehaviour-derived types and
        /// registers a factory for each in <see cref="ScriptSystem"/>.
        /// Uses full-name matching to work across assembly-load-context boundaries.
        /// </summary>
        /// <summary>
        /// Sets the game assembly's <c>ECSWorld.Instance</c> to the editor's singleton so
        /// that script code (GameBehaviour.World) sees the same entities as the editor.
        /// </summary>
        private static void BridgeECSWorld(Assembly assembly)
        {
            try
            {
                var gameEcsType = assembly.GetType("GameEditor.Framework.ECS.ECSWorld");
                // skip if the type resolves to the SAME type as the editor (shared ALC)
                if (gameEcsType == null || gameEcsType == typeof(ECSWorld)) return;
                var setInstance = gameEcsType.GetMethod("SetInstance",
                    BindingFlags.Public | BindingFlags.Static);
                setInstance?.Invoke(null, new object[] { ECSWorld.Instance });
                Logger.Info("[GameAssemblyRunner] ECSWorld bridged to editor instance.");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[GameAssemblyRunner] ECSWorld bridge failed: {ex.Message}");
            }
        }

        public static int RegisterFactoriesFromAssembly(Assembly assembly)
        {
            ScriptSystem.ClearFactories();
            int count = 0;

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract) continue;
                if (!IsGameBehaviourSubclass(type)) continue;

                var capturedType = type;
                ScriptSystem.RegisterType(type.Name, () =>
                {
                    var instance = Activator.CreateInstance(capturedType)
                                   ?? throw new InvalidOperationException(
                                          $"Cannot create instance of {capturedType.Name}");
                    return new DynamicBehaviourProxy(instance, capturedType);
                });
                count++;
            }

            Logger.Info($"[GameAssemblyRunner] Registered {count} script type(s).");
            return count;
        }

        // ── Path helpers ─────────────────────────────────────────────────────

        private static string? FindCsproj(string folder)
        {
            var files = Directory.GetFiles(folder, "*.csproj");
            // Prefer the desktop csproj (skip *Web.csproj which targets WebAssembly)
            return files.FirstOrDefault(f => !Path.GetFileNameWithoutExtension(f).EndsWith("Web",
                       StringComparison.OrdinalIgnoreCase))
                   ?? files.FirstOrDefault();
        }

        private static string? FindDllPath(string folder)
        {
            string? csproj = FindCsproj(folder);
            if (csproj == null) return null;
            string projName = Path.GetFileNameWithoutExtension(csproj);
            return Path.Combine(folder, "bin", "Debug", "net10.0", $"lib{projName}.dll");
        }

        // ── Type inspection ──────────────────────────────────────────────────

        /// <summary>
        /// Returns true when <paramref name="t"/> derives from a type named
        /// "GameEditor.Framework.Scripting.GameBehaviour" anywhere in its hierarchy.
        /// This works across AssemblyLoadContext boundaries where direct type equality fails.
        /// </summary>
        private static bool IsGameBehaviourSubclass(Type t)
        {
            const string target = "GameEditor.Framework.Scripting.GameBehaviour";
            var b = t.BaseType;
            while (b != null)
            {
                if (b.FullName == target) return true;
                b = b.BaseType;
            }
            return false;
        }

        /// <summary>
        /// Returns the concrete <see cref="Type"/> for <paramref name="typeName"/> from
        /// the loaded game assembly, or null when the assembly is not yet loaded.
        /// </summary>
        public static Type? GetScriptType(string typeName)
            => string.IsNullOrEmpty(typeName) ? null
             : _assembly?.GetTypes().FirstOrDefault(t =>
                   t.Name == typeName && IsGameBehaviourSubclass(t));

        /// <summary>
        /// Returns the names of all concrete <see cref="GameBehaviour"/> subclasses
        /// found in the currently-loaded game assembly. Empty when not loaded.
        /// </summary>
        public static string[] GetAvailableScriptTypes()
        {
            if (_assembly == null) return Array.Empty<string>();
            return _assembly.GetTypes()
                .Where(t => !t.IsAbstract && IsGameBehaviourSubclass(t))
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToArray();
        }
    }

    // ── Dynamic proxy ────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps a dynamically-loaded GameBehaviour instance so it is usable as the
    /// editor's own <see cref="GameBehaviour"/> type, bridging the
    /// AssemblyLoadContext boundary via reflection.
    /// </summary>
    internal sealed class DynamicBehaviourProxy : GameBehaviour
    {
        private readonly object        _wrapped;
        private readonly Type          _wrappedType;
        private readonly MethodInfo?   _onStart;
        private readonly MethodInfo?   _onUpdate;
        private readonly MethodInfo?   _onDestroy;
        private readonly PropertyInfo? _entityIdProp;
        private readonly object?       _wrappedWorld;
        private readonly Type?         _wrappedTransformType;
        private readonly MethodInfo?   _wrappedAddTransform;
        private readonly MethodInfo?   _wrappedTryGetTransform;

        internal DynamicBehaviourProxy(
            object wrapped,
            [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
                System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods |
                System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
                System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] Type wrappedType)
        {
            _wrapped     = wrapped;
            _wrappedType = wrappedType;
            var pub      = BindingFlags.Public | BindingFlags.Instance;
            _onStart     = wrappedType.GetMethod("OnStart",   pub);
            _onUpdate    = wrappedType.GetMethod("OnUpdate",  pub);
            _onDestroy   = wrappedType.GetMethod("OnDestroy", pub);
            // property may have an internal setter — use nonPublic=true to find it
            _entityIdProp = wrappedType.GetProperty("EntityId",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            // Resolve the wrapped assembly's ECSWorld + Transform methods once.
            // This lets us mirror Transform between editor ECS and wrapped ECS.
            var a = wrappedType.Assembly;
            var wrappedWorldType = a.GetType("GameEditor.Framework.ECS.ECSWorld");
            _wrappedTransformType = a.GetType("GameEditor.Framework.ECS.Components.Transform");
            var instProp = wrappedWorldType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            _wrappedWorld = instProp?.GetValue(null);
            if (wrappedWorldType != null && _wrappedTransformType != null)
            {
                var add = wrappedWorldType.GetMethod("AddComponent", BindingFlags.Public | BindingFlags.Instance);
                var trg = wrappedWorldType.GetMethod("TryGetComponent", BindingFlags.Public | BindingFlags.Instance);
                if (add != null) _wrappedAddTransform = add.MakeGenericMethod(_wrappedTransformType);
                if (trg != null) _wrappedTryGetTransform = trg.MakeGenericMethod(_wrappedTransformType);
            }
        }

        /// <summary>
        /// Gets the name of the wrapped script type (e.g., "Rotator", "Mover").
        /// </summary>
        public string WrappedScriptTypeName => _wrappedType.Name;

        /// <summary>
        /// Gets the wrapped script instance.
        /// </summary>
        public object WrappedInstance => _wrapped;

        public override void OnStart()
        {
            // Propagate the EntityId from the proxy to the wrapped instance
            try { _entityIdProp?.SetValue(_wrapped, EntityId); } catch { }
            SyncTransformToWrappedWorld();
            try { _onStart?.Invoke(_wrapped, null); }
            catch (Exception ex)
            { Logger.Error($"[Script] {_wrapped.GetType().Name}.OnStart: {(ex.InnerException ?? ex).Message}"); }
            SyncTransformFromWrappedWorld();
        }

        public override void OnUpdate(float deltaTime)
        {
            // Re-sync EntityId each frame in case OnStart's propagation was skipped
            try { _entityIdProp?.SetValue(_wrapped, EntityId); } catch { }
            SyncTransformToWrappedWorld();
            try { _onUpdate?.Invoke(_wrapped, new object[] { deltaTime }); }
            catch (Exception ex)
            { Logger.Error($"[Script] {_wrapped.GetType().Name}.OnUpdate: {(ex.InnerException ?? ex).Message}"); }
            SyncTransformFromWrappedWorld();
        }

        public override void OnDestroy()
        {
            try { _onDestroy?.Invoke(_wrapped, null); }
            catch (Exception ex)
            { Logger.Error($"[Script] {_wrapped.GetType().Name}.OnDestroy: {(ex.InnerException ?? ex).Message}"); }
        }

        public override void ApplySerializedProperties(Dictionary<string, string> properties)
        {
            foreach (var kvp in properties)
            {
                var fi = _wrappedType.GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                if (fi == null) continue;
                try
                {
                    object? value = ParseFieldValue(fi.FieldType, kvp.Value);
                    if (value != null) fi.SetValue(_wrapped, value);
                }
                catch { }
            }
        }

        private static object? ParseFieldValue(Type t, string s)
        {
            if (t == typeof(float))
                return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : (object?)null;
            if (t == typeof(int))
                return int.TryParse(s, out int i) ? i : (object?)null;
            if (t == typeof(bool))
                return s is "true" or "True" or "1";
            if (t == typeof(string))
                return s;
            if (t == typeof(double))
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : (object?)null;
            if (t == typeof(Vector3))
            {
                var p = s.Trim('[', ']').Split(',');
                if (p.Length == 3 &&
                    float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    return new Vector3(x, y, z);
            }
            if (t == typeof(Vector2))
            {
                var p = s.Trim('[', ']').Split(',');
                if (p.Length == 2 &&
                    float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    return new Vector2(x, y);
            }
            return null;
        }

        private void SyncTransformToWrappedWorld()
        {
            if (_wrappedWorld == null || _wrappedTransformType == null || _wrappedAddTransform == null)
                return;
            if (!ECSWorld.Instance.TryGetComponent<Transform>(EntityId, out var src))
                return;

            object dst = Activator.CreateInstance(_wrappedTransformType)!;
            SetMember(dst, "Position", src.Position);
            SetMember(dst, "EulerAngles", src.EulerAngles);
            SetMember(dst, "Scale", src.Scale);
            SetMember(dst, "Parent", src.Parent);
            _wrappedAddTransform.Invoke(_wrappedWorld, new[] { (object)EntityId, dst });
        }

        private void SyncTransformFromWrappedWorld()
        {
            if (_wrappedWorld == null || _wrappedTryGetTransform == null)
                return;
            if (!ECSWorld.Instance.TryGetComponent<Transform>(EntityId, out var dst))
                return;

            var args = new object?[] { EntityId, null };
            bool ok = _wrappedTryGetTransform.Invoke(_wrappedWorld, args) is bool b && b;
            if (!ok || args[1] == null)
                return;

            object src = args[1]!;
            if (GetMember(src, "Position") is Vector3 p) dst.Position = p;
            if (GetMember(src, "EulerAngles") is Vector3 e) dst.EulerAngles = e;
            if (GetMember(src, "Scale") is Vector3 s) dst.Scale = s;
            if (GetMember(src, "Parent") is int pi) dst.Parent = pi;
            else if (GetMember(src, "Parent") == null) dst.Parent = null;

            ECSWorld.Instance.AddComponent(EntityId, dst);
        }

        private static void SetMember(object obj, string name, object? value)
        {
            var t = obj.GetType();
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null)
            {
                f.SetValue(obj, value);
                return;
            }
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p?.CanWrite == true)
                p.SetValue(obj, value);
        }

        private static object? GetMember(object obj, string name)
        {
            var t = obj.GetType();
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) return f.GetValue(obj);
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return p?.CanRead == true ? p.GetValue(obj) : null;
        }
    }
}
