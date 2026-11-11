using System;
using System.Collections.Generic;
using System.Linq;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scripting
{
    /// <summary>
    /// Manages GameBehaviour lifecycle during play mode.
    ///
    /// Usage (deployed game):
    ///   1. Register type factories in startup:
    ///        ScriptSystem.RegisterType("MyBehaviour", () => new MyBehaviour());
    ///   2. After loading a scene, call ScriptSystem.PopulateFromScene(ECSWorld.Instance)
    ///   3. Call ScriptSystem.StartAll() when play begins
    ///   4. Call ScriptSystem.UpdateAll(dt) every frame
    ///   5. Call ScriptSystem.StopAll() on shutdown / scene unload
    ///
    /// Usage (editor with GameAssemblyRunner):
    ///   GameAssemblyRunner.RegisterFactoriesFromAssembly() populates the factories.
    ///   SceneManager.Play() calls PopulateFromScene + StartAll automatically.
    /// </summary>
    public static class ScriptSystem
    {
        // Type name → factory function (registered in startup code or by GameAssemblyRunner)
        private static readonly Dictionary<string, Func<GameBehaviour>> _factories = new();

        // Active behaviour instances for the running scene
        private static readonly List<(Entity EntityId, GameBehaviour Behaviour)> _running = new();

        // Behaviours of entities that came into the scene during play (SceneManager.Instantiate): OnStart runs at
        // the top of the next UpdateAll, before their first OnUpdate.
        private static readonly List<(Entity EntityId, GameBehaviour Behaviour)> _pendingStart = new();

        private static bool _started = false;

        // ── Factory registration ─────────────────────────────────────────────

        /// <summary>Register a factory by explicit name and factory lambda.</summary>
        public static void RegisterType(string typeName, Func<GameBehaviour> factory)
        {
            _factories[typeName] = factory;
            Logger.Info($"[ScriptSystem] Registered type: {typeName}");
        }

        /// <summary>Register a zero-arg constructible GameBehaviour subtype by its class name.</summary>
        public static void RegisterType<T>(string? typeName = null) where T : GameBehaviour, new()
        {
            string key = typeName ?? typeof(T).Name;
            _factories[key] = static () => new T(); 
            Logger.Info($"[ScriptSystem] Registered type: {key}");
        }

        public static void RegisterType(string typeName)
        {
            _factories[typeName] = () => Activator.CreateInstance(Type.GetType(typeName)!) as GameBehaviour;
        }

        /// <summary>Remove all registered factories (called by GameAssemblyRunner on unload).</summary>
        public static void ClearFactories() => _factories.Clear();

        // ── Instantiation ────────────────────────────────────────────────────

        /// <summary>
        /// Creates GameBehaviour instances for every entity in the world that has a
        /// <see cref="ScriptComponent"/> whose TypeName has a registered factory.
        /// Call this after loading a scene and before <see cref="StartAll"/>.
        /// </summary>
        public static void PopulateFromScene(ECSWorld world)
        {
            _running.Clear();
            _pendingStart.Clear();
            foreach (Entity id in world.Entities)
            {
                if (world.TryGetComponent<ScriptComponent>(id, out var sc))
                    TryCreateBehaviour(id, sc);

                if (world.TryGetComponent<ScriptCollectionComponent>(id, out var extra)
                    && extra.Scripts != null)
                {
                    foreach (var item in extra.Scripts)
                        TryCreateBehaviour(id, item);
                }
            }
            Logger.Info($"[ScriptSystem] Populated {_running.Count} behaviour(s) from scene.");
        }

        /// <summary>
        /// Creates the behaviours of entities that came into the scene while it plays (a prefab instance placed by
        /// <see cref="Scene.SceneManager.Instantiate"/>). Their <see cref="GameBehaviour.OnStart"/> runs at the top
        /// of the next <see cref="UpdateAll"/>, before their first <see cref="GameBehaviour.OnUpdate"/> — so the
        /// script that placed them can set them up first. Before play has started they join the scene's own.
        /// </summary>
        public static void AddEntities(ECSWorld world, IReadOnlyList<Entity> entities)
        {
            var target = _started ? _pendingStart : _running;
            foreach (Entity id in entities)
            {
                if (world.TryGetComponent<ScriptComponent>(id, out var sc))
                    TryCreateBehaviour(id, sc, target);

                if (world.TryGetComponent<ScriptCollectionComponent>(id, out var extra) && extra.Scripts != null)
                    foreach (var item in extra.Scripts)
                        TryCreateBehaviour(id, item, target);
            }
        }

        /// <summary>
        /// Ends the behaviours of an entity that leaves the scene during play: <see cref="GameBehaviour.OnDestroy"/>
        /// for each one that has started, then they are forgotten. Not to be called while behaviours are being
        /// updated — <see cref="Scene.SceneManager.Destroy"/> waits for the end of the frame for that reason.
        /// </summary>
        public static void RemoveEntity(Entity entity)
        {
            for (int i = _running.Count - 1; i >= 0; i--)
            {
                if (_running[i].EntityId != entity) continue;
                SafeCall(_running[i].Behaviour, static beh => beh.OnDestroy(), "OnDestroy");
                _running.RemoveAt(i);
            }
            _pendingStart.RemoveAll(entry => entry.EntityId == entity);   // never started: nothing to end
        }

        private static void TryCreateBehaviour(Entity id, ScriptComponent sc) => TryCreateBehaviour(id, sc, _running);

        private static void TryCreateBehaviour(Entity id, ScriptComponent sc, List<(Entity EntityId, GameBehaviour Behaviour)> target)
        {
            if (string.IsNullOrEmpty(sc.TypeName)) return;

            if (!_factories.TryGetValue(sc.TypeName, out var factory))
            {
                Logger.Warning($"[ScriptSystem] No factory for script type '{sc.TypeName}' on entity {id}.");
                return;
            }

            try
            {
                var behaviour = factory();
                behaviour.EntityId = id;
                if (sc.Properties != null && sc.Properties.Count > 0)
                    behaviour.ApplySerializedProperties(sc.Properties);
                target.Add((id, behaviour));
            }
            catch (Exception ex)
            {
                Logger.Error($"[ScriptSystem] Failed to create '{sc.TypeName}': {ex.Message}");
            }
        }

        /// <summary>Manually register a pre-created behaviour for an entity.</summary>
        public static void Register(Entity entityId, GameBehaviour behaviour)
        {
            behaviour.EntityId = entityId;
            _running.Add((entityId, behaviour));
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        /// <summary>Calls <see cref="GameBehaviour.OnStart"/> on all registered behaviours.</summary>
        public static void StartAll()
        {
            _started = true;
            foreach (var (_, b) in _running)
                SafeCall(b, static beh => beh.OnStart(), "OnStart");
        }

        /// <summary>Calls <see cref="GameBehaviour.OnUpdate"/> on all registered behaviours.</summary>
        public static void UpdateAll(float deltaTime)
        {
            if (!_started) return;

            // Behaviours placed since the last pass start now. One that places another in its OnStart adds to the
            // list while it is walked: that one starts in this pass too.
            for (int i = 0; i < _pendingStart.Count; i++)
            {
                var entry = _pendingStart[i];
                _running.Add(entry);
                SafeCall(entry.Behaviour, static beh => beh.OnStart(), "OnStart");
            }
            _pendingStart.Clear();

            // By index: a behaviour may place entities during the pass (their behaviours wait in _pendingStart),
            // and nothing leaves the list before the pass is over.
            int count = _running.Count;
            for (int i = 0; i < count; i++)
            {
                float dt = deltaTime;
                SafeCall(_running[i].Behaviour, beh => beh.OnUpdate(dt), "OnUpdate");
            }

            // After the scripts: sprite animations show the frame for this moment, a clip a script just chose too.
            Scene.SpriteAnimSystem.Update(ECS.ECSWorld.Instance, deltaTime);

            // The end of the frame for scripts: entities handed to Destroy leave now.
            Scene.SceneManager.FlushDestroyed();
        }

        /// <summary>Calls <see cref="GameBehaviour.OnDestroy"/> on all behaviours and clears the list.</summary>
        public static void StopAll()
        {
            foreach (var (_, b) in _running)
                SafeCall(b, static beh => beh.OnDestroy(), "OnDestroy");
            _running.Clear();
            _pendingStart.Clear();
            _started = false;
            Logger.Info("[ScriptSystem] Stopped — all behaviours destroyed.");
        }

        // ── State ────────────────────────────────────────────────────────────

        public static int Count        => _running.Count;
        public static bool IsRunning   => _started;
        public static bool HasFactory(string typeName) => _factories.ContainsKey(typeName);

        // ── Live property updates ─────────────────────────────────────────────

        /// <summary>
        /// Updates a property on a running script instance.
        /// Used for live editing during play mode.
        /// </summary>
        public static void UpdateScriptProperty(Entity entityId, string typeName, string propertyName, string propertyValue)
        {
            // Find the running behaviour for this entity and type
            // Note: In editor mode, scripts are wrapped in DynamicBehaviourProxy, so we need to match against the wrapped type
            var match = _running.FirstOrDefault(b => 
            {
                if (b.EntityId != entityId) return false;
                
                // Check if this is a DynamicBehaviourProxy (editor mode)
                var behaviourType = b.Behaviour.GetType();
                if (behaviourType.Name == "DynamicBehaviourProxy")
                {
                    // Access the wrapped script type via reflection
                    try
                    {
                        var wrappedTypeProp = behaviourType.GetProperty("WrappedScriptTypeName", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (wrappedTypeProp != null)
                        {
                            var wrappedTypeName = wrappedTypeProp.GetValue(b.Behaviour) as string;
                            return wrappedTypeName == typeName;
                        }
                    }
                    catch { }
                    return false;
                }
                
                // Direct type matching (deployed mode)
                return b.Behaviour.GetType().Name == typeName;
            });
            
            var behaviour = match.Behaviour;
            if (behaviour == null)
                return;

            // Get the actual wrapped script instance if this is a proxy
            object scriptInstance = behaviour;
            if (behaviour.GetType().Name == "DynamicBehaviourProxy")
            {
                try
                {
                    var wrappedProp = behaviour.GetType().GetProperty("WrappedInstance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    scriptInstance = wrappedProp?.GetValue(behaviour) ?? behaviour;
                }
                catch { }
            }
            
            var field = scriptInstance.GetType().GetField(propertyName, 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field == null)
                return;
            
            try
            {
                object? value = null;
                if (field.FieldType == typeof(float) && float.TryParse(propertyValue, 
                    System.Globalization.NumberStyles.Float, 
                    System.Globalization.CultureInfo.InvariantCulture, out var fval))
                {
                    value = fval;
                }
                else if (field.FieldType == typeof(int) && int.TryParse(propertyValue, out var ival))
                {
                    value = ival;
                }
                else if (field.FieldType == typeof(bool))
                {
                    value = propertyValue is "true" or "True";
                }
                else
                {
                    // A text stays a text; a vector, an enum or a prefab is read as it is when play starts.
                    value = GameBehaviour.ParseFieldValue(field.FieldType, propertyValue);
                }

                if (value != null)
                    field.SetValue(scriptInstance, value);
            }
            catch (Exception ex) 
            { 
                Logger.Warning($"[ScriptSystem] Error updating property '{propertyName}': {ex.Message}"); 
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void SafeCall(GameBehaviour b, Action<GameBehaviour> action, string name)
        {
            try { action(b); }
            catch (Exception ex)
            {
                Logger.Error($"[ScriptSystem] {b.GetType().Name}.{name}: {ex.Message}");
            }
        }

        // ── Physics event dispatch ────────────────────────────────────────────

        internal static void DispatchCollisionEnter(Entity entity, Entity other)
        {
            foreach (var entry in _running)
            {
                if (entry.EntityId != entity) continue;
                SafeCall(entry.Behaviour, b => b.OnCollisionEnter(other), nameof(GameBehaviour.OnCollisionEnter));
            }
        }

        internal static void DispatchCollisionExit(Entity entity, Entity other)
        {
            foreach (var entry in _running)
            {
                if (entry.EntityId != entity) continue;
                SafeCall(entry.Behaviour, b => b.OnCollisionExit(other), nameof(GameBehaviour.OnCollisionExit));
            }
        }

        internal static void DispatchCollisionStay(Entity entity, Entity other)
        {
            foreach (var entry in _running)
            {
                if (entry.EntityId != entity) continue;
                SafeCall(entry.Behaviour, b => b.OnCollisionStay(other), nameof(GameBehaviour.OnCollisionStay));
            }
        }

        internal static void DispatchTriggerEnter(Entity entity, Entity other)
        {
            foreach (var entry in _running)
            {
                if (entry.EntityId != entity) continue;
                SafeCall(entry.Behaviour, b => b.OnTriggerEnter(other), nameof(GameBehaviour.OnTriggerEnter));
            }
        }

        internal static void DispatchTriggerExit(Entity entity, Entity other)
        {
            foreach (var entry in _running)
            {
                if (entry.EntityId != entity) continue;
                SafeCall(entry.Behaviour, b => b.OnTriggerExit(other), nameof(GameBehaviour.OnTriggerExit));
            }
        }
    }
}
