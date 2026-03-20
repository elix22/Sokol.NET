using System;
using System.Collections.Generic;
using System.Linq;
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
        private static readonly List<(int EntityId, GameBehaviour Behaviour)> _running = new();

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
            foreach (int id in world.Entities)
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

        private static void TryCreateBehaviour(int id, ScriptComponent sc)
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
                _running.Add((id, behaviour));
            }
            catch (Exception ex)
            {
                Logger.Error($"[ScriptSystem] Failed to create '{sc.TypeName}': {ex.Message}");
            }
        }

        /// <summary>Manually register a pre-created behaviour for an entity.</summary>
        public static void Register(int entityId, GameBehaviour behaviour)
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
            foreach (var (_, b) in _running)
            {
                float dt = deltaTime;
                SafeCall(b, beh => beh.OnUpdate(dt), "OnUpdate");
            }
        }

        /// <summary>Calls <see cref="GameBehaviour.OnDestroy"/> on all behaviours and clears the list.</summary>
        public static void StopAll()
        {
            foreach (var (_, b) in _running)
                SafeCall(b, static beh => beh.OnDestroy(), "OnDestroy");
            _running.Clear();
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
        public static void UpdateScriptProperty(int entityId, string typeName, string propertyName, string propertyValue)
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
                    value = propertyValue;
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
    }
}
