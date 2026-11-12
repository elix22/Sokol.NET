using System.Collections.Generic;
using System.Numerics;
using Frent;
using Sokol;
using GameEditor.Framework.Renderer;
using GameEditor.Framework.Renderer.Server;
using GameEditor.Framework.Renderer.Server.Animation;
using GameEditor.Framework.Renderer.Server.Materials;
using GameEditor.Framework.Assets;
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
            EventBus.RaiseSceneSaving();
            // In an editor: a file made since the project was opened has its id before the scene that names it is written.
            if (AssetDatabase.Root != null)
                foreach (string note in AssetDatabase.Sync().Notes) Logger.Warning("[Assets] " + note);
            string compact = AssetRefs.Stamp(SceneSerializer.Serialize(ActiveScene));   // with the id of each file it names

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
            // A prefab that is open like a scene was written: what was loaded of it, and of prefabs holding it, is stale.
            if (path.EndsWith(PrefabLibrary.Extension, StringComparison.OrdinalIgnoreCase)) PrefabLibrary.Invalidate();
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
            PrefabLibrary.Invalidate();   // a scene read from its file takes its prefabs from their files as they are now
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
                    // Files are read asynchronously here, and a scene resolves its prefab instances while it is
                    // read: the prefabs it names — placed in it, or in a script's field — are read first, and
                    // the prefabs those hold. A scene that names none goes on at once.
                    // The sprite sheets the scene and those prefabs name are read next, for the same reason: a
                    // sprite's frame and an animation's clip are there from the first frame.
                    // Then the tile maps, and the sheets the maps' tiles come from.
                    Prefabs.LoadNamedIn(json, loaded => TilemapLibrary.LoadNamedIn(TextsOf(json, loaded), maps =>
                    {
                        var texts = TextsOf(json, loaded);
                        texts.AddRange(maps);
                        SpriteLibrary.LoadNamedIn(texts, () => Loaded(json, loaded, assetPath));
                    }));
                }
                else
                {
                    Logger.Warning($"Failed to load scene from assets: {assetPath}");
                }
            });

        }

        // The scene's text has arrived, and with it every file it must have read before it.
        private static void Loaded(string json, List<string> loaded, string assetPath)
        {
            EventBus.RaiseSceneUnloaded();
            ActiveScene ??= new Scene("Untitled");
            SceneSerializer.Deserialize(json, ActiveScene);
            ActiveScene.FilePath = null; // Loaded from assets, not a file path
            ActiveScene.IsDirty = false;
            PreloadSceneMeshes();
            PreloadSceneMaterials();
            // A prefab that only a script places has no entity in the scene yet: its meshes and
            // materials are asked for now, so Instantiate has them.
            foreach (string key in loaded)
                if (PrefabLibrary.Get(key) is { } prefab) PreloadPrefabAssets(prefab);
            EventBus.RaiseSceneLoaded();
            UndoStack.Clear();
            Logger.Info($"Scene loaded from assets: {assetPath}");
        }

        // The scene's text and the texts of the prefabs read for it.
        private static List<string> TextsOf(string json, List<string> prefabKeys)
        {
            var texts = new List<string> { json };
            foreach (string key in prefabKeys)
                if (PrefabLibrary.TextOf(key) is { } text) texts.Add(text);
            return texts;
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
            _destroyQueue.Clear();   // the whole scene goes back to the snapshot

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

        // ── Entities that come and go while the scene runs ───────────────────────────────

        // Entities handed to Destroy, until the end of the frame.
        private static readonly List<Entity> _destroyQueue = new();

        /// <summary>
        /// Places an instance of a prefab in the running scene: its entities, their rigid bodies, character
        /// controllers and vehicles, and their scripts — whose <see cref="GameBehaviour.OnStart"/> runs before
        /// their first <see cref="GameBehaviour.OnUpdate"/>, at the top of the next frame's script pass.
        /// <paramref name="position"/> and <paramref name="rotation"/> are the root's: in the world, or under
        /// <paramref name="parent"/> when one is given. Returns the root, or <see cref="Entity.Null"/> when the
        /// prefab cannot be loaded (the log says why).
        /// With a physics backend other than the built-in one the entities get no body.
        /// </summary>
        public static Entity Instantiate(Prefab? prefab, Vector3 position, Quaternion rotation, Entity? parent = null)
        {
            if (prefab == null)
            {
                Logger.Warning("[SceneManager] Instantiate: no prefab (a script's prefab field is empty, or the prefab was not loaded).");
                return Entity.Null;
            }
            var created = new List<Entity>();
            Entity root = SceneSerializer.InstantiatePrefab(prefab.Key, out string? error, created);
            if (root.IsNull)
            {
                Logger.Warning($"[SceneManager] Instantiate: {error}");
                return root;
            }

            var world = ECSWorld.Instance;
            world.TryGetComponent<Transform>(root, out var tr);
            tr.Position = position;
            tr.Rotation = rotation;
            tr.Parent = parent is { IsNull: false } p && p.IsAlive ? p : null;
            world.AddComponent(root, tr);

            foreach (Entity e in created) PreloadEntityAssets(world, e);

            if (PlayMode != PlayModeState.Stopped)
            {
                if (_physics != null)
                {
                    // In Play's order: every body before the constraints that join them.
                    foreach (Entity e in created) CreateBody(world, e);
                    foreach (Entity e in created) CreateConstraint(world, e);
                    foreach (Entity e in created) CreateCharacter(world, e);
                    foreach (Entity e in created) CreateVehicle(world, e);
                }
                foreach (Entity e in created) Physics2D.Physics2DWorld.AddEntity(world, e);
                ScriptSystem.AddEntities(world, created);
            }
            return root;
        }

        /// <summary>
        /// Takes an entity out of the running scene at the end of the frame, with everything under it: its
        /// scripts get <see cref="GameBehaviour.OnDestroy"/>, its bodies leave the physics world. Until then the
        /// entity is alive and updates as before. Works on any entity, placed by <see cref="Instantiate"/> or not.
        /// </summary>
        public static void Destroy(Entity entity)
        {
            if (entity.IsNull || !entity.IsAlive) return;
            // Searched by hand: List<Entity>.Contains traps in a web build (see ECSWorld.RemoveChildLink).
            foreach (Entity queued in _destroyQueue)
                if (queued == entity) return;
            _destroyQueue.Add(entity);
        }

        /// <summary>
        /// Removes the entities handed to <see cref="Destroy"/>. Called by <see cref="ScriptSystem.UpdateAll"/>
        /// when the frame's script pass is over.
        /// </summary>
        public static void FlushDestroyed()
        {
            var world = ECSWorld.Instance;
            // An OnDestroy may hand over more entities: go on until none is left.
            while (_destroyQueue.Count > 0)
            {
                // Each entity with what is under it, a parent before its children.
                var doomed = new List<Entity>();
                var seen = new HashSet<Entity>();
                foreach (Entity root in _destroyQueue)
                {
                    if (!root.IsAlive || !seen.Add(root)) continue;
                    doomed.Add(root);
                    for (int i = doomed.Count - 1; i < doomed.Count; i++)
                        foreach (Entity child in world.GetChildren(doomed[i]))
                            if (child.IsAlive && seen.Add(child)) doomed.Add(child);
                }
                _destroyQueue.Clear();
                if (doomed.Count == 0) continue;

                foreach (Entity e in doomed) ScriptSystem.RemoveEntity(e);   // while their components are still there
                DestroyPhysicsOf(world, doomed, seen);   // the 2D bodies too, which need no 3D world
                foreach (Entity e in doomed)
                    if (e.IsAlive) world.DestroyEntity(e);                   // the world takes the children with a parent
            }
        }

        // Takes the entities' constraints, character controllers, vehicles and bodies out of the physics world.
        static void DestroyPhysicsOf(ECSWorld world, List<Entity> entities, HashSet<Entity> set)
        {
            foreach (Entity e in entities) Physics2D.Physics2DWorld.RemoveEntity(e);
            if (_physics == null) return;
            // Constraints first: those on the entities, and those of any entity that hold one of their bodies.
            bool anyBody = false;
            foreach (Entity e in entities) anyBody |= _entityToHandle.ContainsKey(e);
            foreach (Entity e in anyBody ? (IEnumerable<Entity>)world.Entities : entities)
            {
                if (!world.TryGetComponent<ConstraintComponent>(e, out var cc) || !cc.RuntimeHandle.IsValid) continue;
                if (!set.Contains(e) && !set.Contains(cc.BodyA) && !set.Contains(cc.BodyB)) continue;
                _physics!.DestroyConstraint(cc.RuntimeHandle);
                cc.RuntimeHandle = ConstraintHandle.Invalid;
                world.AddComponent(e, cc);
            }

            foreach (Entity e in entities)
            {
                if (_entityToCharHandle.Remove(e, out var character))
                {
                    _physics!.DestroyCharacter(character);
                    _charHandleToEntity.Remove(character.Value);
                }
                if (_entityToVehicleHandle.Remove(e, out var vehicle))
                {
                    _physics!.DestroyVehicle(vehicle);
                    _vehicleHandleToEntity.Remove(vehicle.Value);
                }
                if (_entityToHandle.Remove(e, out var body))
                {
                    _physics!.DestroyBody(body);
                    _handleToEntity.Remove(body.Value);
                }
            }
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
            AssetLibrary.MeshMaterialFound += BackfillMaterialPath;   // an .obj's own .mtl, found when the mesh was loaded
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
                string? pbrRel = AssetLibrary.ToAssetsRelativePath(mr.MaterialPath);
                if (pbrRel == null) { Logger.Warning($"[SceneManager] Cannot resolve material path: '{mr.MaterialPath}'"); return; }
                if (mr.MaterialPath != pbrRel) { mr.MaterialPath = pbrRel; ECSWorld.Instance.AddComponent(id, mr); }
                AssetLibrary.LoadPbrMaterialAsset(pbrRel);
                return;
            }

            if (!mr.MaterialPath.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase)) return;

            // Normalize to Assets-relative so the path is portable across platforms.
            string? relPath = AssetLibrary.ToAssetsRelativePath(mr.MaterialPath);
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

            AssetLibrary.LoadMtl(relPath);
        }

        /// <summary>
        /// Loads the meshes and materials the scene's entities name and that are not loaded yet — for entities
        /// that came into the scene after it was loaded (a prefab instance placed or brought up to date).
        /// </summary>
        public static void PreloadSceneAssets()
        {
            PreloadSceneMeshes();
            PreloadSceneMaterials();
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
            foreach (Entity id in world.Entities) PreloadMaterialOf(world, id);
        }

        private static void PreloadMaterialOf(ECSWorld world, Entity id)
        {
            if (!world.TryGetComponent<MeshRenderer>(id, out var mr)) return;
            if (string.IsNullOrEmpty(mr.MaterialPath)) return;

            // One material of a .mtl, named by the file's name alone ("duck.mtl#Body", as materials were keyed
            // before): it is the .mtl beside the entity's mesh.
            int inMtl = mr.MaterialPath.IndexOf(".mtl#", StringComparison.OrdinalIgnoreCase);
            if (inMtl > 0 && mr.MaterialPath.IndexOf('/') < 0 && !string.IsNullOrEmpty(mr.MeshPath))
            {
                string beside = Renderer.Server.Resources.MeshRegistry.MtlKey(mr.MeshPath, mr.MaterialPath);
                if (beside != mr.MaterialPath)
                {
                    mr.MaterialPath = beside;
                    world.AddComponent(id, mr);
                }
            }
            if (!mr.MaterialPath.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase) &&
                !mr.MaterialPath.EndsWith(PbrMaterialSerializer.Extension, StringComparison.OrdinalIgnoreCase)) return;

            // Reuse the same normalization + load logic as OnComponentChanged.
            OnComponentChanged(id, nameof(MeshRenderer));
        }

        /// <summary>
        /// Asks for the mesh and the material of an entity that came into the scene while it runs (an instance
        /// placed by <see cref="Instantiate"/>). What is loaded already is not read again, so placing the
        /// hundredth bullet costs no file.
        /// </summary>
        private static void PreloadEntityAssets(ECSWorld world, Entity id)
        {
            PreloadMeshOf(world, id);
            if (world.TryGetComponent<MeshRenderer>(id, out var mr) && !string.IsNullOrEmpty(mr.MaterialPath) &&
                mr.MaterialPath.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase) &&
                RenderingServer.Materials.GetFirstIdByMtlFile(mr.MaterialPath) != 0) return;   // that .mtl is loaded
            PreloadMaterialOf(world, id);
        }

        /// <summary>
        /// Asks for the meshes and materials a prefab's entities name, without placing it — so that a later
        /// <see cref="Instantiate"/> finds them loaded (on the web and on phones files arrive some frames
        /// after they are asked for).
        /// </summary>
        internal static void PreloadPrefabAssets(Prefab prefab)
        {
            foreach (PrefabEntity entity in prefab.Entities)
            {
                if (entity.Components.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
                if (!entity.Components.TryGetProperty("MeshRenderer", out var mr) || mr.ValueKind != System.Text.Json.JsonValueKind.Object) continue;

                if (mr.TryGetProperty("Path", out var pathEl) && pathEl.GetString() is { Length: > 0 } mesh &&
                    !mesh.StartsWith("prim:", StringComparison.Ordinal) &&
                    !mesh.StartsWith(GameEditor.Framework.Renderer.Server.Resources.MeshRegistry.InMemoryPrefix, StringComparison.Ordinal) &&
                    AssetLibrary.ToAssetsRelativePath(mesh) is { } meshRel)
                    AssetLibrary.Mesh(meshRel);

                if (mr.TryGetProperty("Mat", out var matEl) && matEl.GetString() is { Length: > 0 } material &&
                    AssetLibrary.ToAssetsRelativePath(material) is { } materialRel)
                {
                    if (materialRel.EndsWith(PbrMaterialSerializer.Extension, StringComparison.OrdinalIgnoreCase)) AssetLibrary.LoadPbrMaterialAsset(materialRel);
                    else if (materialRel.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase) &&
                             RenderingServer.Materials.GetFirstIdByMtlFile(materialRel) == 0) AssetLibrary.LoadMtl(materialRel);
                }
            }
        }

        // ── Scene mesh preloading ──────────────────────────────────────────────────────────

        private static void PreloadSceneMeshes()
        {
            var world = ECSWorld.Instance;
            foreach (Entity id in world.Entities) PreloadMeshOf(world, id);

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
                AssetLibrary.Gltf(file);
            }
        }

        /// <summary>Asks for the mesh an entity's MeshRenderer names, when it is a file that is not loaded yet.</summary>
        private static void PreloadMeshOf(ECSWorld world, Entity id)
        {
            if (!world.TryGetComponent<MeshRenderer>(id, out var mr)) return;
            if (string.IsNullOrEmpty(mr.MeshPath)) return;
            if (mr.MeshPath.StartsWith("prim:", StringComparison.Ordinal)) return;
            if (mr.MeshPath.StartsWith(GameEditor.Framework.Renderer.Server.Resources.MeshRegistry.InMemoryPrefix, StringComparison.Ordinal)) return;

            // Normalize to Assets-relative so the stored path is portable.
            string? relPath = AssetLibrary.ToAssetsRelativePath(mr.MeshPath);
            if (relPath == null)
            {
                Logger.Warning($"[SceneManager] Cannot resolve mesh path: '{mr.MeshPath}'");
                return;
            }

            if (mr.MeshPath != relPath)
            {
                mr.MeshPath = relPath;
                world.AddComponent(id, mr);
            }

            AssetLibrary.Mesh(relPath);
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
        /// The view and projection matrices of a camera entity for a picture of <paramref name="width"/> ×
        /// <paramref name="height"/> pixels — the one place a camera is read: a game, an editor's Game view and its
        /// camera preview all ask here. A camera looks along its +Z with its +Y up; an orthographic one shows
        /// <see cref="CameraComponent.OrthoSize"/> world units above and below its centre. A camera that is a
        /// child follows its parents.
        /// </summary>
        public static void GetCameraMatrices(ECSWorld world, in Transform tr, in CameraComponent cam, int width, int height,
                                             out Matrix4x4 view, out Matrix4x4 proj, out Vector3 eyePos)
        {
            Vector3 forward, up;
            if (tr.Parent.HasValue)
            {
                Matrix4x4 m = Transform.GetWorldMatrix(world, tr);
                eyePos  = m.Translation;
                forward = Vector3.Normalize(new Vector3(m.M31, m.M32, m.M33));
                up      = Vector3.Normalize(new Vector3(m.M21, m.M22, m.M23));
            }
            else
            {
                Matrix4x4 rotMat = Matrix4x4.CreateFromQuaternion(tr.Rotation);
                eyePos  = tr.Position;
                forward = new Vector3(rotMat.M31, rotMat.M32, rotMat.M33);
                up      = new Vector3(rotMat.M21, rotMat.M22, rotMat.M23);
            }
            view = Matrix4x4.CreateLookAt(eyePos, eyePos + forward, up);

            float aspect = (float)width / height;
            if (cam.IsOrthographic)
            {
                float orthoH = MathF.Max(0.01f, cam.OrthoSize > 0f ? cam.OrthoSize : 5f);
                float orthoW = orthoH * aspect;
                proj = Matrix4x4.CreateOrthographicOffCenter(-orthoW, orthoW, -orthoH, orthoH, cam.NearZ, cam.FarZ);
            }
            else
            {
                float fov = MathF.Max(1f, cam.Fov) * MathF.PI / 180f;
                proj = Matrix4x4.CreatePerspectiveFieldOfView(fov, aspect, cam.NearZ, cam.FarZ);
            }
        }

        /// <summary>
        /// The scene's main camera — the first active entity, in scene order, whose camera is marked main — and
        /// its matrices for a picture of the given size. False when there is none.
        /// </summary>
        public static bool GetMainCamera(int width, int height, out Matrix4x4 view, out Matrix4x4 proj,
                                         out CameraComponent camera, out Vector3 eyePos)
        {
            view = proj = Matrix4x4.Identity;
            camera = default;
            eyePos = Vector3.Zero;
            if (width <= 0 || height <= 0) return false;

            var world = ECSWorld.Instance;
            foreach (Entity id in world.Entities)
            {
                if (!world.TryGetComponent<CameraComponent>(id, out var cam) || !cam.IsMain) continue;
                if (world.TryGetComponent<ActiveFlag>(id, out var af) && !af.Active) continue;
                if (cam.NearZ <= 0f || cam.FarZ <= cam.NearZ) continue; // skip degenerate projection
                if (!world.TryGetComponent<Transform>(id, out var tr)) continue;
                GetCameraMatrices(world, tr, cam, width, height, out view, out proj, out eyePos);
                camera = cam;
                return true;
            }
            return false;
        }

        public static bool GetMainCameraMatrices(int width, int height,out Matrix4x4 viewProj, out Vector3 eyePos)
        {
            bool found = GetMainCamera(width, height, out var view, out var proj, out _, out eyePos);
            viewProj = found ? view * proj : Matrix4x4.Identity;
            return found;
        }

        public static void UpdatePhysics(float deltaTime)
        {
            if (PlayMode == PlayModeState.Playing) Physics2D.Physics2DWorld.Step(ECSWorld.Instance, deltaTime);

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

        // The calls below act on the entity's body, whichever kind it has. For a 2D body x and y are used, and z of
        // a torque or an angular velocity (the turn about z).

        public static bool TryGetLinearVelocity(Entity entity, out Vector3 velocity)
        {
            velocity = Vector3.Zero;
            if (Physics2D.Physics2DWorld.TryGetLinearVelocity(entity, out Vector2 flat)) { velocity = new Vector3(flat, 0f); return true; }
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            velocity = _physics.GetLinearVelocity(handle);
            return true;
        }

        public static bool SetLinearVelocity(Entity entity, Vector3 velocity)
        {
            if (Physics2D.Physics2DWorld.SetLinearVelocity(entity, new Vector2(velocity.X, velocity.Y))) return true;
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.SetLinearVelocity(handle, velocity);
            return true;
        }

        public static bool TryGetAngularVelocity(Entity entity, out Vector3 velocity)
        {
            velocity = Vector3.Zero;
            if (Physics2D.Physics2DWorld.TryGetAngularVelocity(entity, out float turn)) { velocity = new Vector3(0f, 0f, turn); return true; }
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            velocity = _physics.GetAngularVelocity(handle);
            return true;
        }

        public static bool SetAngularVelocity(Entity entity, Vector3 velocity)
        {
            if (Physics2D.Physics2DWorld.SetAngularVelocity(entity, velocity.Z)) return true;
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.SetAngularVelocity(handle, velocity);
            return true;
        }

        public static bool AddForce(Entity entity, Vector3 force)
        {
            if (Physics2D.Physics2DWorld.AddForce(entity, new Vector2(force.X, force.Y))) return true;
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.AddForce(handle, force);
            return true;
        }

        public static bool AddImpulse(Entity entity, Vector3 impulse)
        {
            if (Physics2D.Physics2DWorld.AddImpulse(entity, new Vector2(impulse.X, impulse.Y))) return true;
            if (_physics == null) return false;
            if (!_entityToHandle.TryGetValue(entity, out var handle)) return false;
            _physics.AddImpulse(handle, impulse);
            return true;
        }

        public static bool AddTorque(Entity entity, Vector3 torque)
        {
            if (Physics2D.Physics2DWorld.AddTorque(entity, torque.Z)) return true;
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
            // A 2D body is read from its Transform before the next step: setting the Transform is the whole of it.
            if (Physics2D.Physics2DWorld.Has(entity))
            {
                var world2D = ECSWorld.Instance;
                if (!world2D.TryGetComponent<Transform>(entity, out var tr2D)) return false;
                tr2D.Position = position;
                tr2D.Rotation = rotation;
                world2D.AddComponent(entity, tr2D);
                Physics2D.Physics2DWorld.SetLinearVelocity(entity, Vector2.Zero);
                Physics2D.Physics2DWorld.SetAngularVelocity(entity, 0f);
                return true;
            }
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
            // The 2D world, beside whichever 3D one the scene has: made only when the scene has a 2D body.
            Physics2D.Physics2DWorld.Start(ECSWorld.Instance);

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
            foreach (Entity entity in world.Entities) CreateBody(world, entity);

            Logger.Info($"[SceneManager] Physics initialized. {_entityToHandle.Count} bodies created.");

            // Create constraints after all bodies are registered.
            foreach (Entity entity in world.Entities) CreateConstraint(world, entity);

            // Create character controllers.
            foreach (Entity entity in world.Entities) CreateCharacter(world, entity);

            // Create vehicle controllers.
            foreach (Entity entity in world.Entities) CreateVehicle(world, entity);
        }

        // What Play makes for each entity of the scene, and Instantiate for the entities it places: a rigid body,
        // a constraint, a character controller, a vehicle. Each does nothing for an entity without the component.

        static void CreateBody(ECSWorld world, Entity entity)
        {
            if (!world.TryGetComponent<RigidbodyComponent>(entity, out var rb)) return;
            if (!world.TryGetComponent<Transform>(entity, out var tr)) return;

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

            var handle = _physics!.CreateBody(desc);
            _entityToHandle[entity] = handle;
            _handleToEntity[handle.Value] = entity;
        }

        static void CreateConstraint(ECSWorld world, Entity entity)
        {
            if (!world.TryGetComponent<ConstraintComponent>(entity, out var cc)) return;

            _entityToHandle.TryGetValue(cc.BodyA, out var handleA);
            _entityToHandle.TryGetValue(cc.BodyB, out var handleB);
            if (!handleA.IsValid || !handleB.IsValid) return;

            var cdesc = new ConstraintDesc(
                cc.Type,
                handleA, handleB,
                cc.LocalAnchorA, cc.LocalAnchorB,
                cc.LocalAxisA, cc.LocalAxisB,
                cc.MinLimit, cc.MaxLimit);

            var ch = _physics!.CreateConstraint(cdesc);
            cc.RuntimeHandle = ch;
            world.AddComponent(entity, cc);
        }

        static void CreateCharacter(ECSWorld world, Entity entity)
        {
            if (!world.TryGetComponent<CharacterComponent>(entity, out var cc)) return;
            if (!world.TryGetComponent<Transform>(entity, out var tr)) return;

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
            var charHandle = _physics!.CreateCharacter(charDesc);
            _entityToCharHandle[entity] = charHandle;
            _charHandleToEntity[charHandle.Value] = entity;
        }

        static void CreateVehicle(ECSWorld world, Entity entity)
        {
            if (!world.TryGetComponent<VehicleComponent>(entity, out var vc)) return;
            if (!world.TryGetComponent<Transform>(entity, out var tr)) return;

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
        /// <summary>An entity's rotation in the world, through its parents.</summary>
        internal static Quaternion WorldRotationOf(ECSWorld world, in Transform tr) => GetEntityWorldRotation(world, in tr);

        /// <summary>A place and a rotation in the world as the values an entity under <paramref name="parent"/> stores.</summary>
        internal static (Vector3 Position, Quaternion Rotation) ToLocal(ECSWorld world, Entity? parent, Vector3 worldPosition, Quaternion worldRotation)
            => WorldToLocalTransform(world, parent, worldPosition, worldRotation);

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
            Physics2D.Physics2DWorld.Stop();
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
