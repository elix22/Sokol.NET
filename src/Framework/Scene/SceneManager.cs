using System.Collections.Generic;
using System.Numerics;
using Frent;
using Sokol;
using GameEditor.Framework.Renderer;
using GameEditor.Framework.Renderer.Server;
using GameEditor.Framework.Renderer.Server.Animation;
using GameEditor.Framework.Renderer.Server.Materials;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;
using GameEditor.Framework.Scripting;

namespace GameEditor.Framework.Scene
{
    public static class SceneManager
    {
        public static Scene? ActiveScene { get; private set; }
        public static PlayModeState PlayMode { get; private set; } = PlayModeState.Stopped;

        // JSON snapshot of the scene captured when Play() is called; restored on Stop().
        private static string? _playSnapshot;

        // Async mesh loads in flight (keyed by MeshPath); avoids duplicate SFilesystem requests.
        private static readonly HashSet<string> _pendingMeshLoads = new(StringComparer.Ordinal);
        // glTF/GLB files whose resources are currently being (re-)registered — dedupes the
        // per-file preload when many entities reference the same imported model.
        private static readonly HashSet<string> _pendingGltfPreloads = new(StringComparer.Ordinal);

        // ---- Physics -------------------------------------------------------
        private static IPhysicsWorld? _physics;
        // Replaces Jolt for the Play session when the scene names a backend the factory can create.
        private static ISceneSimulator? _simulator;

        /// <summary>
        /// Creates the simulator for a non-Jolt <see cref="Scene.PhysicsBackend"/> (the argument).
        /// Returns null for a backend it does not provide. When unset or null-returning, Play uses Jolt.
        /// </summary>
        public static Func<string, ISceneSimulator?>? SimulatorFactory { get; set; }
        private static readonly Dictionary<Entity, PhysicsBodyHandle>      _entityToHandle    = new();
        private static readonly Dictionary<int, Entity>                     _handleToEntity    = new();
        private static readonly Dictionary<Entity, CharacterHandle>         _entityToCharHandle = new();
        private static readonly Dictionary<int, Entity>                     _charHandleToEntity = new();
        private static readonly Dictionary<Entity, VehicleHandle>           _entityToVehicleHandle = new();
        private static readonly Dictionary<int, Entity>                     _vehicleHandleToEntity = new();

        // Reused across OverlapSphere/OverlapBox to avoid a per-call List allocation on the
        // script-driven hot path. Main-thread-only, non-reentrant; each query Clear()s before use.
        private static readonly List<PhysicsBodyHandle>                     _overlapScratch        = new(64);

        private sealed class PhysicsCollisionDispatcher : ICollisionListener
        {
            public void OnCollisionEnter(PhysicsBodyHandle a, PhysicsBodyHandle b, ContactPoint contact)
            {
                if (!_handleToEntity.TryGetValue(a.Value, out var eA) ||
                    !_handleToEntity.TryGetValue(b.Value, out var eB)) return;
                ScriptSystem.DispatchCollisionEnter(eA, eB);
                ScriptSystem.DispatchCollisionEnter(eB, eA);
            }

            public void OnCollisionStay(PhysicsBodyHandle a, PhysicsBodyHandle b)
            {
                if (!_handleToEntity.TryGetValue(a.Value, out var eA) ||
                    !_handleToEntity.TryGetValue(b.Value, out var eB)) return;
                ScriptSystem.DispatchCollisionStay(eA, eB);
                ScriptSystem.DispatchCollisionStay(eB, eA);
            }

            public void OnCollisionExit(PhysicsBodyHandle a, PhysicsBodyHandle b)
            {
                if (!_handleToEntity.TryGetValue(a.Value, out var eA) ||
                    !_handleToEntity.TryGetValue(b.Value, out var eB)) return;
                ScriptSystem.DispatchCollisionExit(eA, eB);
                ScriptSystem.DispatchCollisionExit(eB, eA);
            }

            public void OnTriggerEnter(PhysicsBodyHandle trigger, PhysicsBodyHandle other)
            {
                if (!_handleToEntity.TryGetValue(trigger.Value, out var eT) ||
                    !_handleToEntity.TryGetValue(other.Value, out var eO)) return;
                ScriptSystem.DispatchTriggerEnter(eT, eO);
                ScriptSystem.DispatchTriggerEnter(eO, eT);
            }

            public void OnTriggerExit(PhysicsBodyHandle trigger, PhysicsBodyHandle other)
            {
                if (!_handleToEntity.TryGetValue(trigger.Value, out var eT) ||
                    !_handleToEntity.TryGetValue(other.Value, out var eO)) return;
                ScriptSystem.DispatchTriggerExit(eT, eO);
                ScriptSystem.DispatchTriggerExit(eO, eT);
            }
        }

        public static void NewScene(string name = "Untitled")
        {
            ActiveScene?.Clear();
            EventBus.RaiseSceneUnloaded();
            ActiveScene = new Scene(name);
            EventBus.RaiseSceneLoaded();
            UndoStack.Clear();
        }

        public static void SaveScene(string path)
        {
            if (ActiveScene == null) return;
            string compact = SceneSerializer.Serialize(ActiveScene);

            // Re-format as indented JSON for human readability.
            // Uses Utf8JsonWriter directly — avoids JsonSerializer.Serialize which requires
            // reflection and is disabled under NativeAOT.
            string json;
            using (var doc = System.Text.Json.JsonDocument.Parse(compact))
            {
                // Pre-size: indented output is ~2.5× the compact string (whitespace overhead).
                // Avoids MemoryStream doubling-resizes for large scenes.
                var buf = new System.IO.MemoryStream(compact.Length * 3);
                var writerOptions = new System.Text.Json.JsonWriterOptions { Indented = true };
                using (var writer = new System.Text.Json.Utf8JsonWriter(buf, writerOptions))
                    doc.RootElement.WriteTo(writer);
                json = System.Text.Encoding.UTF8.GetString(buf.ToArray());
            }

            SokolFile.WriteAllText(path, json);
            ActiveScene.FilePath = path;
            ActiveScene.IsDirty = false;
            Logger.Info($"Scene saved to {path}");
        }

        public static void LoadScene(string path)
        {
            if (!SokolFile.Exists(path))
            {
                Logger.Warning($"Scene file not found: {path}");
                return;
            }
            EventBus.RaiseSceneUnloaded();
            ActiveScene ??= new Scene("Untitled");
            string json = SokolFile.ReadAllText(path);
            SceneSerializer.Deserialize(json, ActiveScene);
            ActiveScene.FilePath = path;
            ActiveScene.IsDirty = false;
            PreloadSceneMeshes();
            PreloadSceneMaterials();
            EventBus.RaiseSceneLoaded();
            UndoStack.Clear();
            Logger.Info($"Scene loaded from {path}");
        }

        public static void LoadSceneFromAssetsAsync(string assetPath)
        {
            SFilesystem.LoadFileAsync(assetPath, (path, buffer, status) =>
            {
                if (status == SFileLoadStatus.Success)
                {
                    string json = System.Text.Encoding.UTF8.GetString(buffer);
                    EventBus.RaiseSceneUnloaded();
                    ActiveScene ??= new Scene("Untitled");
                    SceneSerializer.Deserialize(json, ActiveScene);
                    ActiveScene.FilePath = null; // Loaded from assets, not a file path
                    ActiveScene.IsDirty = false;
                    PreloadSceneMeshes();
                    PreloadSceneMaterials();
                    EventBus.RaiseSceneLoaded();
                    UndoStack.Clear();
                    Logger.Info($"Scene loaded from assets: {assetPath}");
                }
                else
                {
                    Logger.Warning($"Failed to load scene from assets: {assetPath}");
                }
            });

        }

        public static void SetPlayMode(PlayModeState state)
        {
            PlayMode = state;
            EventBus.RaisePlayModeChanged(state);
        }

        /// <summary>
        /// Saves a scene snapshot, populates the ScriptSystem from current entities,
        /// starts all behaviours and transitions to Playing state.
        /// </summary>
        public static void Play()
        {
            if (PlayMode == PlayModeState.Playing) return;

            if (ActiveScene == null)
            {
                Logger.Warning("[SceneManager] No active scene to play.");
                return;
            }

            if (PlayMode == PlayModeState.Stopped)
            {
                // Snapshot the scene so we can restore it on Stop()
                if (ActiveScene != null)
                {
                    _playSnapshot = SceneSerializer.Serialize(ActiveScene);
                    Logger.Info("[SceneManager] Play snapshot saved.");
                }

                // Populate script behaviours from current scene entities
                ScriptSystem.PopulateFromScene(ECS.ECSWorld.Instance);

                SetPlayMode(PlayModeState.Playing);

                // Initialize physics
                InitializePhysics();

                // Start all registered behaviours
                ScriptSystem.StartAll();
                Logger.Info($"[SceneManager] Play started. {ScriptSystem.Count} script(s) running.");
            }
            else if (PlayMode == PlayModeState.Paused)
            {
                // Resume without restarting scripts
                SetPlayMode(PlayModeState.Playing);
            }
        }

        /// <summary>Freezes script execution without destroying behaviours.</summary>
        public static void Pause()
        {
            if (PlayMode != PlayModeState.Playing) return;
            SetPlayMode(PlayModeState.Paused);
        }

        /// <summary>
        /// Stops all scripts, transitions to Stopped state and restores the pre-play
        /// scene snapshot so the editor sees the original unmodified scene.
        /// </summary>
        public static void Stop()
        {
            if (PlayMode == PlayModeState.Stopped) return;

            // Destroy all running scripts
            ScriptSystem.StopAll();

            // Shutdown physics
            ShutdownPhysics();

            SetPlayMode(PlayModeState.Stopped);

            // Restore the pre-play snapshot
            if (_playSnapshot != null && ActiveScene != null)
            {
                EventBus.RaiseSceneUnloaded();
                SceneSerializer.Deserialize(_playSnapshot, ActiveScene);
                ActiveScene.IsDirty = false;
                _playSnapshot = null;
                PreloadSceneMeshes();
                EventBus.RaiseSceneLoaded();
                Logger.Info("[SceneManager] Scene restored from play snapshot.");
            }

            UndoStack.Clear();
        }

        /// <summary>
        /// Pre-loads all OBJ meshes referenced by MeshRenderer components in the current scene.
        /// Desktop: synchronous via File.ReadAllBytes.
        /// Web / asset bundle: async via SFilesystem.LoadFileAsync.
        /// Must be called after scene deserialization, on the main thread.
        /// </summary>
        // ── Static initializer: subscribe to EventBus events ─────────────────────────────
        static SceneManager()
        {
            EventBus.ComponentChanged += OnComponentChanged;
        }

        // ── Path helpers ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Normalizes a path to Assets-relative (e.g. "Models/duck.obj").
        /// Already-relative paths are returned unchanged.
        /// Absolute paths are stripped at the "/Assets/" marker.
        /// Returns null if an absolute path has no "/Assets/" component.
        /// </summary>
        public static string? ToAssetsRelativePath(string path)
        {
            if (!System.IO.Path.IsPathRooted(path)) return path; // already relative
            const string marker = "/Assets/";
            string normalised = path.Replace('\\', '/');
            int idx = normalised.IndexOf(marker, StringComparison.Ordinal);
            return idx >= 0 ? normalised.Substring(idx + marker.Length) : null;
        }

        /// <summary>
        /// Resolves an Assets-relative path to an absolute filesystem path on desktop
        /// using <see cref="ConfigManager.ProjectFolder"/>. Returns null when no project
        /// is loaded or when the input is already absolute.
        /// </summary>
        private static string? ToAbsoluteDesktopPath(string relPath)
        {
            if (System.IO.Path.IsPathRooted(relPath)) return relPath; // already absolute
            if (!ConfigManager.HasProject) return null;
            return System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets", relPath);
        }

        // ── Event handler ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reacts to Inspector changes on MeshRenderer: normalizes MaterialPath to
        /// Assets-relative and loads the referenced .mtl file into MaterialRegistry.
        /// Works on desktop (sync File I/O) and web/mobile (async SFilesystem).
        /// </summary>
        private static void OnComponentChanged(Entity id, string componentName)
        {
            if (componentName != nameof(MeshRenderer)) return;
            if (!ECSWorld.Instance.TryGetComponent<MeshRenderer>(id, out var mr)) return;
            if (string.IsNullOrEmpty(mr.MaterialPath)) return;

            // `.pbrmat` material assets (Inspector PBR editor output) load via their own path.
            if (mr.MaterialPath.EndsWith(PbrMaterialSerializer.Extension, StringComparison.OrdinalIgnoreCase))
            {
                string? pbrRel = ToAssetsRelativePath(mr.MaterialPath);
                if (pbrRel == null) { Logger.Warning($"[SceneManager] Cannot resolve material path: '{mr.MaterialPath}'"); return; }
                if (mr.MaterialPath != pbrRel) { mr.MaterialPath = pbrRel; ECSWorld.Instance.AddComponent(id, mr); }
                LoadPbrMaterialAsset(pbrRel);
                return;
            }

            if (!mr.MaterialPath.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase)) return;

            // Normalize to Assets-relative so the path is portable across platforms.
            string? relPath = ToAssetsRelativePath(mr.MaterialPath);
            if (relPath == null)
            {
                Logger.Warning($"[SceneManager] Cannot resolve material path: '{mr.MaterialPath}'");
                return;
            }

            if (mr.MaterialPath != relPath)
            {
                mr.MaterialPath = relPath;
                ECSWorld.Instance.AddComponent(id, mr);
            }

            // The registry key must be just the filename so it matches sub.MaterialKey
            // ("mtllib#materialName" where mtllib is the OBJ's raw mtllib filename).
            string mtlFileName = System.IO.Path.GetFileName(relPath);

            // Desktop: synchronous load.
            string? absPath = ToAbsoluteDesktopPath(relPath);
            if (absPath != null && System.IO.File.Exists(absPath))
            {
                string basePath = System.IO.Path.GetDirectoryName(absPath) ?? string.Empty;
                byte[] bytes    = System.IO.File.ReadAllBytes(absPath);
                RenderingServer.Materials.LoadMtl(mtlFileName, bytes, basePath, rp =>
                {
                    string abs = System.IO.Path.IsPathRooted(rp) ? rp : System.IO.Path.Combine(basePath, rp);
                    return System.IO.File.Exists(abs) ? System.IO.File.ReadAllBytes(abs) : null;
                });
                return;
            }

            // Web / mobile: async load, then async-load any textures referenced by the MTL.
            string capturedKey = mtlFileName;
            string capturedDir = System.IO.Path.GetDirectoryName(relPath)?.Replace('\\', '/') ?? string.Empty;
            SFilesystem.LoadFileAsync(relPath, (_, buffer, status) =>
            {
                if (status == SFileLoadStatus.Success && buffer != null)
                {
                    RenderingServer.Materials.LoadMtl(capturedKey, buffer, capturedDir, null);
                    LoadMaterialTexturesAsync(capturedKey);
                }
                else
                    Logger.Warning($"[SceneManager] Failed to load MTL: '{relPath}'");
            });
        }

        // ── Scene material preloading ─────────────────────────────────────────────────────

        /// <summary>
        /// For every entity whose MaterialPath is already set in the scene JSON, trigger
        /// the same MTL load that the editor's OnComponentChanged handler would perform.
        /// This ensures standalone apps and scene reloads apply materials without needing
        /// an Inspector change event.
        /// </summary>
        private static void PreloadSceneMaterials()
        {
            var world = ECSWorld.Instance;
            foreach (Entity id in world.Entities)
            {
                if (!world.TryGetComponent<MeshRenderer>(id, out var mr)) continue;
                if (string.IsNullOrEmpty(mr.MaterialPath)) continue;
                if (!mr.MaterialPath.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase) &&
                    !mr.MaterialPath.EndsWith(PbrMaterialSerializer.Extension, StringComparison.OrdinalIgnoreCase)) continue;

                // Reuse the same normalization + load logic as OnComponentChanged.
                OnComponentChanged(id, nameof(MeshRenderer));
            }
        }

        // ── Scene mesh preloading ──────────────────────────────────────────────────────────

        private static void PreloadSceneMeshes()
        {
            var world = ECSWorld.Instance;
            foreach (Entity id in world.Entities)
            {
                if (!world.TryGetComponent<MeshRenderer>(id, out var mr)) continue;
                if (string.IsNullOrEmpty(mr.MeshPath)) continue;
                if (mr.MeshPath.StartsWith("prim:", StringComparison.Ordinal)) continue;
                if (mr.MeshPath.StartsWith(GameEditor.Framework.Renderer.Server.Resources.MeshRegistry.InMemoryPrefix, StringComparison.Ordinal)) continue;

                // Normalize to Assets-relative so the stored path is portable.
                string? relPath = ToAssetsRelativePath(mr.MeshPath);
                if (relPath == null)
                {
                    Logger.Warning($"[SceneManager] Cannot resolve mesh path: '{mr.MeshPath}'");
                    continue;
                }

                if (mr.MeshPath != relPath)
                {
                    mr.MeshPath = relPath;
                    world.AddComponent(id, mr);
                }

                string path = relPath;
                if (RenderingServer.Meshes.GetByPath(path) != null) continue;

                // glTF/GLB resource key ("<file>.glb#m{i}p{j}"): re-register the file's meshes,
                // materials, and textures (NOT entities — those are already deserialized). One
                // preload per unique file covers every primitive it references.
                int hashIdx = path.IndexOf('#');
                if (hashIdx > 0)
                {
                    string gltfFile = path.Substring(0, hashIdx);
                    if (gltfFile.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ||
                        gltfFile.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_pendingGltfPreloads.Add(gltfFile))
                        {
                            string captured = gltfFile;
                            RenderingServer.PreloadGltfAsync(captured, () => _pendingGltfPreloads.Remove(captured));
                        }
                        continue;
                    }
                }

                if (_pendingMeshLoads.Contains(path)) continue;

                // Try desktop sync path first (resolves relative → absolute via ProjectFolder).
                string? absPath = ToAbsoluteDesktopPath(path);
                if (absPath != null && System.IO.File.Exists(absPath))
                {
                    RenderingServer.Meshes.Load(path, System.IO.File.ReadAllBytes(absPath));
                    TryLoadMeshMtlDesktop(path);
                }
                else
                {
                    // Web / mobile: async load using the Assets-relative path.
                    _pendingMeshLoads.Add(path);
                    string captured = path;
                    SFilesystem.LoadFileAsync(path, (_, buffer, status) =>
                    {
                        _pendingMeshLoads.Remove(captured);
                        if (status == SFileLoadStatus.Success && buffer != null)
                        {
                            RenderingServer.Meshes.Load(captured, buffer);
                            TryLoadMeshMtlAsync(captured);
                        }
                        else
                            Logger.Warning($"[SceneManager] Failed to load mesh: '{captured}'");
                    });
                }
            }

            // Skinned characters: re-import each referenced glTF once so SkinnedCharacterRegistry is
            // repopulated on a cold scene load (the deserialized SkinnedMeshRenderer entities resolve
            // their mesh + animator from it by key). Skipped when already registered — e.g. the
            // Play->Stop snapshot, where the static registry survived the round-trip.
            foreach (Entity id in world.Entities)
            {
                if (!world.TryGetComponent<SkinnedMeshRenderer>(id, out var skmr)) continue;
                string charKey = skmr.CharacterKey;
                if (string.IsNullOrEmpty(charKey)) continue;
                if (SkinnedCharacterRegistry.TryGet(charKey, out var entry) && entry.Meshes.Count > 0) continue;
                // CharacterKey is the glTF FILE for single-skin ("<file>") but "<file>#skin{i}" /
                // "<file>#morph" for multi-skin models — preload the FILE (before '#'); one preload
                // repopulates ALL of that file's character entries (every skin).
                int hash = charKey.IndexOf('#');
                string file = hash > 0 ? charKey.Substring(0, hash) : charKey;
                if (!_pendingGltfPreloads.Add(file)) continue;
                string captured = file;
                RenderingServer.PreloadGltfAsync(captured, () => _pendingGltfPreloads.Remove(captured));
            }
        }

        /// <summary>
        /// After an OBJ mesh has been loaded into MeshRegistry, load its co-located .mtl.
        /// Selects sync (desktop) or async (web/mobile) path automatically.
        /// <paramref name="meshRelPath"/> must be Assets-relative (e.g. "Models/duck.obj").
        /// </summary>
        public static void TryLoadMeshMtl(string meshRelPath)
        {
            string? absPath = ToAbsoluteDesktopPath(meshRelPath);
            if (absPath != null && System.IO.File.Exists(absPath))
                TryLoadMeshMtlDesktop(meshRelPath);
            else
                TryLoadMeshMtlAsync(meshRelPath);
        }

        /// <summary>
        /// After an OBJ mesh has been loaded, load its co-located .mtl file synchronously.
        /// <paramref name="meshRelPath"/> is Assets-relative (e.g. "Models/duck.obj").
        /// </summary>
        private static void TryLoadMeshMtlDesktop(string meshRelPath)
        {
            var meshRes = RenderingServer.Meshes.GetByPath(meshRelPath);
            if (meshRes == null || string.IsNullOrEmpty(meshRes.MtlLib)) return;

            string meshDir    = System.IO.Path.GetDirectoryName(meshRelPath)?.Replace('\\', '/') ?? string.Empty;
            string mtlRelPath = string.IsNullOrEmpty(meshDir) ? meshRes.MtlLib : meshDir + "/" + meshRes.MtlLib;
            string? mtlAbs    = ToAbsoluteDesktopPath(mtlRelPath);
            if (mtlAbs == null || !System.IO.File.Exists(mtlAbs)) return;

            string basePath = System.IO.Path.GetDirectoryName(mtlAbs) ?? string.Empty;
            byte[] bytes    = System.IO.File.ReadAllBytes(mtlAbs);
            RenderingServer.Materials.LoadMtl(meshRes.MtlLib, bytes, basePath, relPath =>
            {
                string abs = System.IO.Path.IsPathRooted(relPath) ? relPath : System.IO.Path.Combine(basePath, relPath);
                return System.IO.File.Exists(abs) ? System.IO.File.ReadAllBytes(abs) : null;
            });
            BackfillMaterialPath(meshRelPath, mtlRelPath);
        }

        /// <summary>
        /// After an OBJ mesh has been loaded async, load its co-located .mtl file async.
        /// <paramref name="meshRelPath"/> is Assets-relative (e.g. "Models/duck.obj").
        /// Textures fall back to placeholders on web; material colors and shininess apply.
        /// </summary>
        private static void TryLoadMeshMtlAsync(string meshRelPath)
        {
            var meshRes = RenderingServer.Meshes.GetByPath(meshRelPath);
            if (meshRes == null || string.IsNullOrEmpty(meshRes.MtlLib)) return;

            string meshDir    = System.IO.Path.GetDirectoryName(meshRelPath)?.Replace('\\', '/') ?? string.Empty;
            string mtlRelPath = string.IsNullOrEmpty(meshDir) ? meshRes.MtlLib : meshDir + "/" + meshRes.MtlLib;
            string capturedKey          = meshRes.MtlLib;
            string capturedDir          = meshDir;
            string capturedMeshRelPath  = meshRelPath;
            string capturedMtlRelPath   = mtlRelPath;
            SFilesystem.LoadFileAsync(mtlRelPath, (_, buffer, status) =>
            {
                if (status == SFileLoadStatus.Success && buffer != null)
                {
                    RenderingServer.Materials.LoadMtl(capturedKey, buffer, capturedDir, null);
                    LoadMaterialTexturesAsync(capturedKey);
                    BackfillMaterialPath(capturedMeshRelPath, capturedMtlRelPath);
                }
                else
                    Logger.Warning($"[SceneManager] Failed to load MTL: '{mtlRelPath}'");
            });
        }

        /// <summary>
        /// For every entity whose MeshPath == <paramref name="meshRelPath"/> and whose
        /// MaterialPath is empty, fills in <paramref name="mtlRelPath"/> so the Inspector
        /// shows the auto-discovered material file.
        /// </summary>
        private static void BackfillMaterialPath(string meshRelPath, string mtlRelPath)
        {
            var world = ECSWorld.Instance;
            foreach (Entity e in world.Entities)
            {
                if (!world.TryGetComponent<MeshRenderer>(e, out var mr)) continue;
                if (mr.MeshPath != meshRelPath) continue;
                if (!string.IsNullOrEmpty(mr.MaterialPath)) continue;
                mr.MaterialPath = mtlRelPath;
                world.AddComponent(e, mr);
            }
        }

        /// <summary>
        /// After materials have been registered for <paramref name="mtlKey"/>, async-load every
        /// texture path they reference via SFilesystem and patch the GPU views on arrival.
        /// </summary>
        private static void LoadMaterialTexturesAsync(string mtlKey)
        {
            var texPaths = RenderingServer.Materials.GetTexturePaths(mtlKey);
            foreach (string texPath in texPaths)
            {
                string captured = texPath;
                SFilesystem.LoadFileAsync(texPath, (_, buffer, status) =>
                {
                    if (status == SFileLoadStatus.Success && buffer != null)
                        RenderingServer.Materials.ApplyTextureBytes(captured, buffer);
                    else
                        Logger.Warning($"[SceneManager] Failed to load texture: '{captured}'");
                });
            }
        }

        /// <summary>
        /// Load the image at <paramref name="uiPath"/> and assign it to a PBR texture
        /// <paramref name="slot"/> of the live material registered under <paramref name="materialKey"/>.
        /// An empty path clears the slot. Desktop reads synchronously; web/mobile load async via
        /// SFilesystem. Used by the Inspector's PBR material editor.
        /// </summary>
        public static void ApplyPbrMaterialTexture(
            string materialKey, MaterialRegistry.PbrTextureSlot slot, string? uiPath)
        {
            if (RenderingServer.Materials.GetByKey(materialKey) is not PbrMaterial mat) return;

            string trimmed = uiPath?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                RenderingServer.Materials.ClearPbrTexture(mat, slot);
                return;
            }

            // Normalize to an Assets-relative key so the cache entry is portable across platforms.
            string rel = ToAssetsRelativePath(trimmed) ?? trimmed;

            // Desktop: synchronous load.
            string? absPath = ToAbsoluteDesktopPath(rel);
            if (absPath != null && System.IO.File.Exists(absPath))
            {
                RenderingServer.Materials.LoadPbrTexture(mat, slot, rel, System.IO.File.ReadAllBytes(absPath));
                return;
            }

            // Web / mobile: async load.
            string capturedKey = materialKey;
            SFilesystem.LoadFileAsync(rel, (_, buffer, status) =>
            {
                if (status == SFileLoadStatus.Success && buffer != null)
                {
                    if (RenderingServer.Materials.GetByKey(capturedKey) is PbrMaterial m)
                        RenderingServer.Materials.LoadPbrTexture(m, slot, rel, buffer);
                }
                else
                    Logger.Warning($"[SceneManager] Failed to load PBR texture: '{rel}'");
            });
        }

        // ── .pbrmat material assets ─────────────────────────────────────────────────────────

        /// <summary>
        /// Load a <c>.pbrmat</c> material asset, register it under its Assets-relative key
        /// (= <see cref="MeshRenderer.MaterialPath"/>), and load each referenced texture map.
        /// No-op when the material is already registered (keeps in-memory edits/views). Desktop
        /// reads synchronously; web/mobile load async via SFilesystem.
        /// </summary>
        public static void LoadPbrMaterialAsset(string relPath)
        {
            string key = relPath.Replace('\\', '/');
            if (RenderingServer.Materials.GetByKey(key) is PbrMaterial) return; // already loaded this session

            string? absPath = ToAbsoluteDesktopPath(relPath);
            if (absPath != null && System.IO.File.Exists(absPath))
            {
                RegisterAndLoadPbr(key, System.IO.File.ReadAllBytes(absPath));
                return;
            }

            SFilesystem.LoadFileAsync(relPath, (_, buffer, status) =>
            {
                if (status == SFileLoadStatus.Success && buffer != null) RegisterAndLoadPbr(key, buffer);
                else Logger.Warning($"[SceneManager] Failed to load .pbrmat: '{relPath}'");
            });
        }

        private static void RegisterAndLoadPbr(string key, byte[] json)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(key);
            PbrMaterial mat = PbrMaterialSerializer.Deserialize(json, name);
            RenderingServer.Materials.RegisterOrReplacePbr(key, mat);

            ApplyMapIfSet(MaterialRegistry.PbrTextureSlot.BaseColor,         mat.BaseColorMapPath);
            ApplyMapIfSet(MaterialRegistry.PbrTextureSlot.MetallicRoughness, mat.MetallicRoughnessMapPath);
            ApplyMapIfSet(MaterialRegistry.PbrTextureSlot.Normal,            mat.NormalMapPath);
            ApplyMapIfSet(MaterialRegistry.PbrTextureSlot.Occlusion,         mat.OcclusionMapPath);
            ApplyMapIfSet(MaterialRegistry.PbrTextureSlot.Emissive,          mat.EmissiveMapPath);

            void ApplyMapIfSet(MaterialRegistry.PbrTextureSlot slot, string? p)
            {
                if (!string.IsNullOrEmpty(p)) ApplyPbrMaterialTexture(key, slot, p);
            }
        }

        /// <summary>
        /// Write <paramref name="mat"/> to a new <c>Assets/Materials/&lt;name&gt;.pbrmat</c>
        /// (deduped), register it under the resulting Assets-relative key, and return that key.
        /// Editor-only (desktop authoring); returns null with no project or on write failure.
        /// </summary>
        public static string? SaveNewPbrMaterialAsset(PbrMaterial mat, string preferredName)
        {
            if (!ConfigManager.HasProject) return null;
            string matsDirAbs = System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets", "Materials");
            try { SokolFile.EnsureDirectory(matsDirAbs); } catch { }

            string baseName = SanitizeFileName(string.IsNullOrWhiteSpace(preferredName) ? "Material" : preferredName);
            string fileName = baseName;
            string absPath  = System.IO.Path.Combine(matsDirAbs, fileName + PbrMaterialSerializer.Extension);
            int n = 1;
            while (System.IO.File.Exists(absPath))
            {
                fileName = $"{baseName}_{n++}";
                absPath  = System.IO.Path.Combine(matsDirAbs, fileName + PbrMaterialSerializer.Extension);
            }

            mat.Name = fileName;
            string relKey = $"Materials/{fileName}{PbrMaterialSerializer.Extension}";
            try { System.IO.File.WriteAllText(absPath, PbrMaterialSerializer.Serialize(mat)); }
            catch (Exception ex) { Logger.Warning($"[SceneManager] Failed to write .pbrmat: {ex.Message}"); return null; }

            RenderingServer.Materials.RegisterOrReplacePbr(relKey, mat);
            return relKey;
        }

        /// <summary>Re-serialize an already file-backed material to its <c>.pbrmat</c> (auto-save on edit). Desktop-only.</summary>
        public static void SavePbrMaterialAsset(string relKey, PbrMaterial mat)
        {
            string? absPath = ToAbsoluteDesktopPath(relKey);
            if (absPath == null) return; // web/mobile bundle is read-only
            try
            {
                string? dir = System.IO.Path.GetDirectoryName(absPath);
                if (dir != null) SokolFile.EnsureDirectory(dir);
                System.IO.File.WriteAllText(absPath, PbrMaterialSerializer.Serialize(mat));
            }
            catch (Exception ex) { Logger.Warning($"[SceneManager] Failed to save .pbrmat '{relKey}': {ex.Message}"); }
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(System.Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString();
        }

        public static bool GetMainCameraMatrices(int width, int height,out Matrix4x4 viewProj, out Vector3 eyePos)
        {
            viewProj = Matrix4x4.Identity;
            eyePos = Vector3.Zero;
            if (width <= 0 || height <= 0) return false;

            var world = ECSWorld.Instance;
            foreach (var row in world.Query<CameraComponent, Transform>()
                                     .Enumerate<CameraComponent, Transform>())
            {
                ref readonly var cam = ref row.Item1.Value;
                if (!cam.IsMain) continue;
                if (cam.NearZ <= 0f || cam.FarZ <= cam.NearZ) continue; // skip degenerate projection
                ref readonly var tr = ref row.Item2.Value;

                Matrix4x4 rotMat = Matrix4x4.CreateFromQuaternion(tr.Rotation);

                // Use the rotation matrix's +Z column as forward (matches gizmo convention)
                Vector3 forward = new Vector3(rotMat.M31, rotMat.M32, rotMat.M33);
                Vector3 up = new Vector3(rotMat.M21, rotMat.M22, rotMat.M23);

                eyePos = tr.Position;
                Matrix4x4 view = Matrix4x4.CreateLookAt(eyePos, eyePos + forward, up);
                Matrix4x4 proj;
                if (cam.IsOrthographic)
                {
                    float orthoH = MathF.Max(0.01f, cam.OrthoSize > 0f ? cam.OrthoSize : 5f);
                    float orthoW = orthoH * ((float)width / height);
                    proj = Matrix4x4.CreateOrthographicOffCenter(-orthoW, orthoW, -orthoH, orthoH, cam.NearZ, cam.FarZ);
                }
                else
                {
                    float fov = MathF.Max(1f, cam.Fov) * MathF.PI / 180f;
                    proj = Matrix4x4.CreatePerspectiveFieldOfView(fov, (float)width / height, cam.NearZ, cam.FarZ);
                }
                viewProj = view * proj;
                return true;
            }
            return false;
        }

        public static void UpdatePhysics(float deltaTime)
        {
            if (_simulator != null)
            {
                if (PlayMode == PlayModeState.Playing) _simulator.Step(deltaTime);
                return;
            }

            if (_physics == null || PlayMode != PlayModeState.Playing) return;

            var world = ECSWorld.Instance;

            // Pre-step: push Transform → Jolt for kinematic bodies so scripts can drive them.
            foreach (var (entity, handle) in _entityToHandle)
            {
                if (!world.TryGetComponentRef<RigidbodyComponent>(entity, out var rbRef)) continue;
                if (rbRef.Value.MotionType != RigidbodyMotionType.Kinematic) continue;
                if (!world.TryGetComponentRef<Transform>(entity, out var trRef)) continue;
                ref readonly var tr = ref trRef.Value;

                // MoveKinematic needs world-space; child entities store local position/rotation.
                var kmWorldPos = GetEntityWorldPosition(world, tr);
                var kmWorldRot = GetEntityWorldRotation(world, tr);
                _physics.MoveKinematic(handle, kmWorldPos, kmWorldRot, deltaTime);
            }

            _physics.Step(deltaTime);

            // Post-step: sync physics positions/rotations back to Transform for dynamic bodies only.
            // Kinematic bodies are script-authoritative — skip them to avoid Quaternion→Euler round-trip drift.
            foreach (var (entity, handle) in _entityToHandle)
            {
                if (!world.TryGetComponentRef<RigidbodyComponent>(entity, out var rbRef)) continue;
                ref readonly var rb = ref rbRef.Value;
                if (rb.IsStatic || rb.MotionType == RigidbodyMotionType.Kinematic) continue;
                if (!world.TryGetComponentRef<Transform>(entity, out var trRef)) continue;
                ref var tr = ref trRef.Value;

                // Jolt returns world-space; convert to local if this entity has a parent.
                var (syncLocalPos, syncLocalRot) = WorldToLocalTransform(
                    world, tr.Parent, _physics.GetPosition(handle), _physics.GetRotation(handle));
                tr.Position = syncLocalPos;
                tr.Rotation = syncLocalRot;
            }

            // Post-step: sync character controller positions back to Transform.
            foreach (var (entity, charHandle) in _entityToCharHandle)
            {
                if (!world.TryGetComponentRef<Transform>(entity, out var trRef)) continue;
                ref var tr = ref trRef.Value;

                tr.Position = _physics.GetCharacterPosition(charHandle);
                tr.Rotation = _physics.GetCharacterRotation(charHandle);
            }

            // Post-step: sync vehicle chassis positions back to Transform.
            foreach (var (handle, entity) in _vehicleHandleToEntity)
            {
                var vHandle = new VehicleHandle(handle);
                if (!world.TryGetComponentRef<Transform>(entity, out var trRef)) continue;
                ref var tr = ref trRef.Value;
                tr.Position = _physics.GetVehiclePosition(vHandle);
                tr.Rotation = _physics.GetVehicleRotation(vHandle);
            }

            // Sync wheel follower entity transforms to wheel world transforms.
            // Frent struct-enumerator query: visits only entities that actually have both
            // components (vs. scanning every entity) and writes Transform in place.
            foreach (var row in world.Query<WheelFollowerComponent, Transform>()
                                     .Enumerate<WheelFollowerComponent, Transform>())
            {
                ref readonly var wf = ref row.Item1.Value;
                if (wf.VehicleEntity.IsNull) continue;
                var wm = GetWheelWorldTransform(wf.VehicleEntity, wf.WheelIndex);
                ref var wfTr = ref row.Item2.Value;
                wfTr.Position = new Vector3(wm.M41, wm.M42, wm.M43);
                wfTr.Rotation = Quaternion.CreateFromRotationMatrix(wm);
            }
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Entity hitEntity, out RaycastHit hit)
        {
            hitEntity = default;
            hit = default;
            if (_physics == null) return false;
            if (!_physics.Raycast(origin, direction, maxDistance, out hit)) return false;
            if (!hit.Body.IsValid) return false;
            return _handleToEntity.TryGetValue(hit.Body.Value, out hitEntity);
        }

        public static bool TryGetLinearVelocity(Entity entity, out Vector3 velocity)
        {
            velocity = Vector3.Zero;
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            velocity = _physics.GetLinearVelocity(handle);
            return true;
        }

        public static bool SetLinearVelocity(Entity entity, Vector3 velocity)
        {
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.SetLinearVelocity(handle, velocity);
            return true;
        }

        public static bool TryGetAngularVelocity(Entity entity, out Vector3 velocity)
        {
            velocity = Vector3.Zero;
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            velocity = _physics.GetAngularVelocity(handle);
            return true;
        }

        public static bool SetAngularVelocity(Entity entity, Vector3 velocity)
        {
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.SetAngularVelocity(handle, velocity);
            return true;
        }

        public static bool AddForce(Entity entity, Vector3 force)
        {
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.AddForce(handle, force);
            return true;
        }

        public static bool AddImpulse(Entity entity, Vector3 impulse)
        {
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.AddImpulse(handle, impulse);
            return true;
        }

        public static bool AddTorque(Entity entity, Vector3 torque)
        {
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.AddTorque(handle, torque);
            return true;
        }

        public static int OverlapSphere(Vector3 center, float radius, List<Entity> results, int maxResults = 64)
        {
            results.Clear();
            if (_physics == null) return 0;

            int hitCount = _physics.OverlapSphere(center, radius, _overlapScratch, maxResults);
            for (int i = 0; i < hitCount; i++)
            {
                if (_handleToEntity.TryGetValue(_overlapScratch[i].Value, out var entity))
                    results.Add(entity);
            }
            return results.Count;
        }

        public static int OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, List<Entity> results, int maxResults = 64)
        {
            results.Clear();
            if (_physics == null) return 0;

            int hitCount = _physics.OverlapBox(center, halfExtents, rotation, _overlapScratch, maxResults);
            for (int i = 0; i < hitCount; i++)
            {
                if (_handleToEntity.TryGetValue(_overlapScratch[i].Value, out var entity))
                    results.Add(entity);
            }
            return results.Count;
        }

        public static bool TeleportBody(Entity entity, Vector3 position, Quaternion rotation)
        {
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;

            _physics.SetPosition(handle, position);
            _physics.SetRotation(handle, rotation);

            var world = ECSWorld.Instance;
            if (!world.TryGetComponent<Transform>(entity, out var tr)) return true;

            tr.Position = position;
            tr.Rotation = rotation;
            world.AddComponent(entity, tr);
            return true;
        }

        static void InitializePhysics()
        {
            string? backend = ActiveScene?.PhysicsBackend;
            if (backend != null && !backend.Equals("jolt", StringComparison.OrdinalIgnoreCase))
            {
                if (SimulatorFactory?.Invoke(backend) is { } simulator)
                {
                    try
                    {
                        simulator.Start(ECSWorld.Instance);
                        _simulator = simulator;
                        Logger.Info($"[SceneManager] Physics backend '{backend}' started.");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[SceneManager] Physics backend '{backend}' failed to start: {ex.Message}");
                    }
                    return;
                }
                Logger.Warning($"[SceneManager] Physics backend '{backend}' is not available in this build; using Jolt.");
            }

            _physics = new JoltPhysicsWorld();
            _physics.Initialize(new Vector3(0f, -9.81f, 0f));
            _physics.SetCollisionListener(new PhysicsCollisionDispatcher());

            var world = ECSWorld.Instance;
            foreach (Entity entity in world.Entities)
            {
                if (!world.TryGetComponent<RigidbodyComponent>(entity, out var rb)) continue;
                if (!world.TryGetComponent<Transform>(entity, out var tr)) continue;

                // Build the shapes list, populating geometry for ConvexHull/Mesh from the mesh renderer.
                var shapes = rb.Shapes != null
                    ? new List<ShapeEntry>(rb.Shapes)
                    : new List<ShapeEntry> { ShapeEntry.Default() };

                if (world.TryGetComponent<MeshRenderer>(entity, out var mr) &&
                    PrimitiveMeshSpec.TryParse(mr.MeshPath, out var spec))
                {
                    for (int i = 0; i < shapes.Count; i++)
                    {
                        var entry = shapes[i];
                        if (entry.Shape == ColliderShape.ConvexHull && entry.MeshVertices == null)
                        {
                            entry.MeshVertices = PrimitiveMeshGeometry.GetHullPoints(spec);
                            shapes[i] = entry;
                        }
                        else if (entry.Shape == ColliderShape.Mesh && entry.MeshVertices == null)
                        {
                            (entry.MeshVertices, entry.MeshIndices) = PrimitiveMeshGeometry.GetMeshTriangles(spec);
                            shapes[i] = entry;
                        }
                    }
                }

                // Child entities store local position/rotation; Jolt needs world-space.
                var worldPos = GetEntityWorldPosition(world, tr);
                var worldRot = GetEntityWorldRotation(world, tr);

                var desc = new BodyDesc(
                    worldPos, worldRot, tr.Scale,
                    rb.MotionType, rb.Mass, rb.UseGravity, shapes,
                    rb.Friction, rb.Restitution, rb.LinearDamping, rb.AngularDamping,
                    rb.IsTrigger, rb.Layer, rb.LayerMask);

                var handle = _physics.CreateBody(desc);
                _entityToHandle[entity] = handle;
                _handleToEntity[handle.Value] = entity;
            }

            Logger.Info($"[SceneManager] Physics initialized. {_entityToHandle.Count} bodies created.");

            // Create constraints after all bodies are registered.
            foreach (Entity entity in world.Entities)
            {
                if (!world.TryGetComponent<ConstraintComponent>(entity, out var cc)) continue;

                _entityToHandle.TryGetValue(cc.BodyA, out var handleA);
                _entityToHandle.TryGetValue(cc.BodyB, out var handleB);
                if (!handleA.IsValid || !handleB.IsValid) continue;

                var cdesc = new ConstraintDesc(
                    cc.Type,
                    handleA, handleB,
                    cc.LocalAnchorA, cc.LocalAnchorB,
                    cc.LocalAxisA, cc.LocalAxisB,
                    cc.MinLimit, cc.MaxLimit);

                var ch = _physics.CreateConstraint(cdesc);
                cc.RuntimeHandle = ch;
                world.AddComponent(entity, cc);
            }

            // Create character controllers.
            foreach (Entity entity in world.Entities)
            {
                if (!world.TryGetComponent<CharacterComponent>(entity, out var cc)) continue;
                if (!world.TryGetComponent<Transform>(entity, out var tr)) continue;

                var charDesc = new CharacterDesc
                {
                    Position           = tr.Position,
                    Rotation           = tr.Rotation,
                    Height             = cc.Height,
                    Radius             = cc.Radius,
                    MaxSlopeAngle      = cc.MaxSlopeAngle,
                    MaxStrength        = cc.MaxStrength,
                    Mass               = cc.Mass,
                    Friction           = cc.Friction,
                    GravityFactor      = cc.GravityFactor,
                    CollisionTolerance = cc.CollisionTolerance,
                    ShapeOffset        = cc.ShapeOffset,
                    IsKinematic        = cc.Mode == CharacterMode.Kinematic,
                    Layer              = cc.Layer,
                    LayerMask          = cc.LayerMask,
                };
                var charHandle = _physics.CreateCharacter(charDesc);
                _entityToCharHandle[entity] = charHandle;
                _charHandleToEntity[charHandle.Value] = entity;
            }

            // Create vehicle controllers.
            foreach (Entity entity in world.Entities)
            {
                if (!world.TryGetComponent<VehicleComponent>(entity, out var vc)) continue;
                if (!world.TryGetComponent<Transform>(entity, out var tr)) continue;

                var wheels = new VehicleWheelDesc[vc.Wheels.Length];
                for (int i = 0; i < vc.Wheels.Length; i++)
                {
                    var w = vc.Wheels[i];
                    wheels[i] = new VehicleWheelDesc
                    {
                        LocalPosition     = w.Position,
                        Radius            = w.Radius,
                        Width             = w.Width,
                        SuspMinLength     = w.SuspMinLength,
                        SuspMaxLength     = w.SuspMaxLength,
                        SuspFrequency     = w.SuspFrequency,
                        SuspDamping       = w.SuspDamping,
                        MaxSteerAngle     = w.MaxSteerAngle,
                        MaxHandBrakeTorque = w.MaxHandBrakeTorque,
                        IsDriven          = w.IsDriven,
                    };
                }
                var vdesc = new VehicleDesc
                {
                    Type              = vc.Type,
                    Position          = tr.Position,
                    Rotation          = tr.Rotation,
                    ChassisHalfExtent = vc.ChassisHalfExtent,
                    Mass              = vc.Mass,
                    COMOffsetY        = vc.COMOffsetY,
                    MaxEngineTorque   = vc.MaxEngineTorque,
                    ClutchStrength    = vc.ClutchStrength,
                    MaxRollAngle      = vc.MaxRollAngle,
                    Friction          = vc.Friction,
                    Wheels            = wheels,
                    Layer             = vc.Layer,
                    LayerMask         = vc.LayerMask,
                };
                var vHandle = _physics!.CreateVehicle(vdesc);
                vc.RuntimeHandle = vHandle;
                world.AddComponent(entity, vc);
                _entityToVehicleHandle[entity] = vHandle;
                _vehicleHandleToEntity[vHandle.Value] = entity;
            }
        }

        /// <summary>
        /// Computes sensible default <see cref="ShapeEntry"/> dimensions for <paramref name="shape"/>
        /// from the entity's mesh spec (if available).  Used by the inspector when adding shapes.
        /// </summary>
        public static ShapeEntry DefaultShapeEntry(ColliderShape shape, PrimitiveMeshSpec? spec)
        {
            var entry = ShapeEntry.Default(shape);
            if (!spec.HasValue) return entry;
            var s = spec.Value;
            switch (shape)
            {
                case ColliderShape.Box:
                    entry.HalfExtent = new Vector3(s.Width * 0.5f, s.Height * 0.5f, s.Depth * 0.5f);
                    break;
                case ColliderShape.Sphere:
                    entry.Radius = s.Radius;
                    break;
                case ColliderShape.Capsule:
                    entry.Radius     = s.Radius;
                    entry.HalfHeight = MathF.Max(0f, s.Height * 0.5f - s.Radius);
                    break;
                case ColliderShape.Cylinder:
                    entry.Radius     = s.Radius;
                    entry.HalfHeight = s.Height * 0.5f;
                    break;
            }
            return entry;
        }

        // ── Physics world/local helpers ────────────────────────────────────────

        /// <summary>
        /// Returns the world-space position of <paramref name="tr"/> by resolving the parent chain.
        /// For root entities (no parent) this is just <c>tr.Position</c>.
        /// </summary>
        static Vector3 GetEntityWorldPosition(ECSWorld world, in Transform tr)
        {
            if (!tr.Parent.HasValue) return tr.Position;
            var mat = Transform.GetWorldMatrix(world, tr);
            return new Vector3(mat.M41, mat.M42, mat.M43);
        }

        /// <summary>
        /// Returns the world-space rotation of <paramref name="tr"/> by walking the parent chain
        /// and composing quaternions (avoids scale contamination from the matrix path).
        /// </summary>
        static Quaternion GetEntityWorldRotation(ECSWorld world, in Transform tr)
        {
            if (!tr.Parent.HasValue) return tr.Rotation;
            var q = tr.Rotation;
            Entity? parentEnt = tr.Parent;
            int depth = 0;
            while (parentEnt.HasValue && parentEnt.Value.IsAlive && depth++ < 32
                   && world.TryGetComponent<Transform>(parentEnt.Value, out var parentTr))
            {
                q = parentTr.Rotation * q;
                parentEnt = parentTr.Parent;
            }
            return q;
        }

        /// <summary>
        /// Converts a Jolt world-space position/rotation back to the local-space values
        /// stored in <see cref="Transform"/>, accounting for the entity's parent chain.
        /// For root entities the values are returned unchanged.
        /// </summary>
        static (Vector3 localPos, Quaternion localRot) WorldToLocalTransform(
            ECSWorld world, Entity? parentEntity, Vector3 worldPos, Quaternion worldRot)
        {
            if (!parentEntity.HasValue) return (worldPos, worldRot);
            Entity p = parentEntity.Value;
            if (!p.IsAlive || !world.TryGetComponent<Transform>(p, out var parentTr))
                return (worldPos, worldRot);

            // Remove parent world position/rotation from the physics world values.
            var parentWorldMat = Transform.GetWorldMatrix(world, parentTr);
            if (!Matrix4x4.Invert(parentWorldMat, out var invParent))
                return (worldPos, worldRot);

            var localPos = Vector3.Transform(worldPos, invParent);
            var parentWorldRot = GetEntityWorldRotation(world, parentTr);
            var localRot = Quaternion.Conjugate(parentWorldRot) * worldRot;
            return (localPos, localRot);
        }

        static void ShutdownPhysics()
        {
            if (_simulator != null)
            {
                _simulator.Stop();
                _simulator = null;
                Logger.Info("[SceneManager] Physics backend stopped.");
                return;
            }

            // Destroy constraints before bodies.
            var world = ECSWorld.Instance;
            foreach (Entity entity in world.Entities)
            {
                if (!world.TryGetComponent<ConstraintComponent>(entity, out var cc)) continue;
                if (!cc.RuntimeHandle.IsValid) continue;
                _physics?.DestroyConstraint(cc.RuntimeHandle);
                cc.RuntimeHandle = ConstraintHandle.Invalid;
                world.AddComponent(entity, cc);
            }

            // Destroy character controllers.
            foreach (var (_, charHandle) in _entityToCharHandle)
                _physics?.DestroyCharacter(charHandle);
            _entityToCharHandle.Clear();
            _charHandleToEntity.Clear();

            // Destroy vehicle controllers.
            foreach (var (_, vHandle) in _entityToVehicleHandle)
                _physics?.DestroyVehicle(vHandle);
            _entityToVehicleHandle.Clear();
            _vehicleHandleToEntity.Clear();
            _setVehicleInputBridge       = null;
            _isWheelOnGroundBridge       = null;
            _getWheelRotationSpeedBridge  = null;
            _getWheelWorldTransformBridge = null;

            _physics?.Shutdown();
            _physics = null;
            _entityToHandle.Clear();
            _handleToEntity.Clear();
            Logger.Info("[SceneManager] Physics shutdown.");
        }

        // ---- Character controller helpers (called by GameBehaviour) --------

        public static bool MoveCharacter(Entity entity, Vector3 velocity)
        {
            if (_physics == null || !_entityToCharHandle.TryGetValue(entity, out var h)) return false;
            _physics.SetCharacterLinearVelocity(h, velocity);
            return true;
        }

        public static bool IsCharacterGrounded(Entity entity)
        {
            if (_physics == null || !_entityToCharHandle.TryGetValue(entity, out var h)) return false;
            return _physics.IsCharacterGrounded(h);
        }

        public static bool TryGetCharacterGroundNormal(Entity entity, out Vector3 normal)
        {
            normal = Vector3.Zero;
            if (_physics == null || !_entityToCharHandle.TryGetValue(entity, out var h)) return false;
            normal = _physics.GetCharacterGroundNormal(h);
            return true;
        }

        // ---- Vehicle controller helpers (called by GameBehaviour) ----------

        // Bridge delegates injected by GameAssemblyRunner when the game DLL is
        // loaded in an isolated AssemblyLoadContext (same pattern as Logger/Input).
        private static Func<Entity, float, float, float, float, bool>? _setVehicleInputBridge;
        private static Func<Entity, int, bool>?                         _isWheelOnGroundBridge;
        private static Func<Entity, int, float>?                        _getWheelRotationSpeedBridge;
        private static Func<Entity, int, Matrix4x4>?                    _getWheelWorldTransformBridge;

        public static void RegisterVehicleCallbacks(
            Func<Entity, float, float, float, float, bool> setVehicleInput,
            Func<Entity, int, bool>                         isWheelOnGround,
            Func<Entity, int, float>                        getWheelRotationSpeed,
            Func<Entity, int, Matrix4x4>                    getWheelWorldTransform)
        {
            _setVehicleInputBridge          = setVehicleInput;
            _isWheelOnGroundBridge          = isWheelOnGround;
            _getWheelRotationSpeedBridge     = getWheelRotationSpeed;
            _getWheelWorldTransformBridge    = getWheelWorldTransform;
        }

        public static bool SetVehicleInput(Entity entity, float steer, float throttle, float brake, float handBrake = 0f)
        {
            if (_setVehicleInputBridge != null) return _setVehicleInputBridge(entity, steer, throttle, brake, handBrake);
            if (_physics == null || !_entityToVehicleHandle.TryGetValue(entity, out var h)) return false;
            _physics.SetVehicleInput(h, steer, throttle, brake, handBrake);
            return true;
        }

        public static bool IsWheelOnGround(Entity entity, int wheelIndex)
        {
            if (_isWheelOnGroundBridge != null) return _isWheelOnGroundBridge(entity, wheelIndex);
            if (_physics == null || !_entityToVehicleHandle.TryGetValue(entity, out var h)) return false;
            return _physics.IsWheelOnGround(h, wheelIndex);
        }

        public static float GetWheelRotationSpeed(Entity entity, int wheelIndex)
        {
            if (_getWheelRotationSpeedBridge != null) return _getWheelRotationSpeedBridge(entity, wheelIndex);
            if (_physics == null || !_entityToVehicleHandle.TryGetValue(entity, out var h)) return 0f;
            return _physics.GetWheelRotationSpeed(h, wheelIndex);
        }

        public static Matrix4x4 GetWheelWorldTransform(Entity entity, int wheelIndex)
        {
            if (_getWheelWorldTransformBridge != null) return _getWheelWorldTransformBridge(entity, wheelIndex);
            if (_physics == null || !_entityToVehicleHandle.TryGetValue(entity, out var h)) return Matrix4x4.Identity;
            return _physics.GetWheelWorldTransform(h, wheelIndex);
        }

        public static PhysicsBodyHandle GetVehicleBodyHandle(Entity entity)
        {
            if (_physics == null || !_entityToVehicleHandle.TryGetValue(entity, out var h)) return PhysicsBodyHandle.Invalid;
            return _physics.GetVehicleBodyHandle(h);
        }
    }
}
