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

        // Type name → the setter of its fields, written by the builder for a game compiled ahead of time (where a
        // field cannot be found by its name). A type with none has its fields set by name (a JIT: the editor's Play).
        private static readonly Dictionary<string, ScriptFieldSetter> _setters = new();

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

        /// <summary>Registers a script with the setter of its public fields (generated code: no reflection).</summary>
        public static void RegisterType<T>(ScriptFieldSetter setter, string? typeName = null) where T : GameBehaviour, new()
        {
            string key = typeName ?? typeof(T).Name;
            _factories[key] = static () => new T();
            _setters[key] = setter;
            Logger.Info($"[ScriptSystem] Registered type: {key}");
        }

        /// <summary>True when the script's fields are set by generated code, not by their names.</summary>
        public static bool HasFieldSetter(string typeName) => _setters.ContainsKey(typeName);

        /// <summary>True when scripts were registered by generated code: the game's assemblies need not be searched for them.</summary>
        public static bool HasGeneratedTypes => _setters.Count > 0;

        /// <summary>How many field values the generated setters have set since the game started (0 where fields are set by name).</summary>
        public static int FieldsSetByGeneratedCode { get; private set; }

        public static void RegisterType(string typeName)
        {
            _factories[typeName] = () => Activator.CreateInstance(Type.GetType(typeName)!) as GameBehaviour;
        }

        /// <summary>Remove all registered factories (called by GameAssemblyRunner on unload).</summary>
        public static void ClearFactories() { _factories.Clear(); _setters.Clear(); }

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
            ApplyRefs();
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
            ApplyRefs();
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
                EndEvents(_running[i].Behaviour);
                _running.RemoveAt(i);
            }
            for (int i = 0; i < _pendingStart.Count; i++)
                if (_pendingStart[i].EntityId == entity) EndEvents(_pendingStart[i].Behaviour);
            _pendingStart.RemoveAll(entry => entry.EntityId == entity);   // never started: nothing to end
        }

        // §6: the handlers a script added (GameBehaviour.On) end with it - the script itself, or the one a proxy wraps.
        private static void EndEvents(GameBehaviour b)
        {
            Events.RemoveOwner(b);
            if (b is IScriptProxy p) Events.RemoveOwner(p.Script);
        }

        // ── §6: a script of an entity, by its type ───────────────────────────

        /// <summary>The first running script of type <typeparamref name="T"/> on <paramref name="entity"/> (one added this
        /// frame too), or null. A type test, no reflection: a script of the editor's Play (wrapped) is found as itself.</summary>
        public static T? Find<T>(Entity entity) where T : class => FindIn<T>(_running, entity) ?? FindIn<T>(_pendingStart, entity);

        /// <summary>Every running script of type <typeparamref name="T"/> on <paramref name="entity"/>, into <paramref name="into"/>
        /// (cleared first); how many.</summary>
        public static int FindAll<T>(Entity entity, List<T> into) where T : class
        {
            into.Clear();
            AllIn(_running, entity, into);
            AllIn(_pendingStart, entity, into);
            return into.Count;
        }

        private static T? FindIn<T>(List<(Entity EntityId, GameBehaviour Behaviour)> list, Entity entity) where T : class
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].EntityId != entity) continue;
                if (As<T>(list[i].Behaviour) is { } t) return t;
            }
            return null;
        }

        private static void AllIn<T>(List<(Entity EntityId, GameBehaviour Behaviour)> list, Entity entity, List<T> into) where T : class
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].EntityId == entity && As<T>(list[i].Behaviour) is { } t) into.Add(t);
        }

        private static T? As<T>(GameBehaviour b) where T : class => b as T ?? (b is IScriptProxy p ? p.Script as T : null);

        private static void TryCreateBehaviour(Entity id, ScriptComponent sc) => TryCreateBehaviour(id, sc, _running);

        private static readonly List<(GameBehaviour Behaviour, string Type, Dictionary<string, Entity> Refs)> _refsLater = new();

        // The references of the scripts just made: an entity, an element of a list of them, or (§6) a script of an
        // entity. The entity, or none when it is no longer in the scene.
        private static void ApplyRefs()
        {
            foreach (var (behaviour, type, refs) in _refsLater)
            {
                if (_setters.TryGetValue(type, out var set))
                {
                    foreach (var (key, pointed) in refs)
                    {
                        Entity live = !pointed.IsNull && pointed.IsAlive ? pointed : default;
                        var (field, element) = ScriptValue.ElementKey(key);     // "Targets[2]": a list's element
                        if (set(behaviour, field, element >= 0 ? new ScriptFieldValue(live, element) : new ScriptFieldValue(live))) FieldsSetByGeneratedCode++;
                        else Logger.Warning($"[ScriptSystem] {type}.{field}: no field of type Entity (or of a script's type) of that name.");
                    }
                }
                else behaviour.ApplySerializedReferences(refs);
            }
            _refsLater.Clear();
        }

        /// <summary>The first running script on <paramref name="entity"/> that is a <paramref name="type"/> (by name: the
        /// reflection path of the editor's Play and the web page), or null.</summary>
        internal static object? FindOfType(Entity entity, Type type)
        {
            foreach (var list in new[] { _running, _pendingStart })
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].EntityId != entity) continue;
                    object b = list[i].Behaviour is IScriptProxy p ? p.Script : list[i].Behaviour;
                    if (type.IsInstanceOfType(b)) return b;
                }
            return null;
        }

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
                {
                    if (_setters.TryGetValue(sc.TypeName, out var set))
                    {
                        foreach (var (field, text) in sc.Properties)
                            if (set(behaviour, field, new ScriptFieldValue(text))) FieldsSetByGeneratedCode++;
                            else
                                Logger.Warning($"[ScriptSystem] {sc.TypeName}.{field}: '{text}' was not set (no such field, or no value of its type).");
                    }
                    else behaviour.ApplySerializedProperties(sc.Properties);
                }
                // §6: the references once every script of the scene (or of what came into it) is made - a field of a
                // script's type is that entity's script, which may be made after this one.
                if (sc.Refs != null && sc.Refs.Count > 0) _refsLater.Add((behaviour, sc.TypeName, sc.Refs));
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
            UI.Texts.CheckFonts(ECS.ECSWorld.Instance);   // a text by key in letters the engine's font lacks: told once
            foreach (var (_, b) in _running)
                SafeCall(b, static beh => beh.OnStart(), "OnStart");
        }

        /// <summary>Calls <see cref="GameBehaviour.OnUpdate"/> on all registered behaviours.</summary>
        /// <summary>The real time of the frame being run, whatever <see cref="Scene.SceneManager.TimeScale"/> is.</summary>
        public static float UnscaledDeltaTime { get; private set; }

        // The profiler's samples (G11.3).
        private static readonly int _profUpdate = Diagnostics.Profiler.Id("Scripts.Update", Diagnostics.Profiler.Category.Scripts);
        private static readonly int _profAnimation = Diagnostics.Profiler.Id("Animation", Diagnostics.Profiler.Category.Animation);
        private static readonly int _profAudio = Diagnostics.Profiler.Id("Audio", Diagnostics.Profiler.Category.Audio);

        /// <summary>One fixed tick (G10): every running behaviour's OnFixedUpdate, in the order they run.</summary>
        public static void FixedUpdateAll()
        {
            if (!_started) return;
            int count = _running.Count;
            for (int i = 0; i < count; i++)
                SafeCall(_running[i].Behaviour, static beh => beh.OnFixedUpdate(), "OnFixedUpdate");
        }

        public static void UpdateAll(float deltaTime)
        {
            if (!_started) return;
            UnscaledDeltaTime = deltaTime;
            float realTime = deltaTime;
            deltaTime *= Scene.SceneManager.TimeScale;
            Draw.BeginFrame(deltaTime);              // G8.4 D3: lines and shapes whose time is over, gone

            Diagnostics.Profiler.Begin(_profUpdate);
            // Behaviours placed since the last pass start now. One that places another in its OnStart adds to the
            // list while it is walked: that one starts in this pass too.
            for (int i = 0; i < _pendingStart.Count; i++)
            {
                var entry = _pendingStart[i];
                _running.Add(entry);
                SafeCall(entry.Behaviour, static beh => beh.OnStart(), "OnStart");
            }
            _pendingStart.Clear();

            // The UI buttons pressed since the last frame: their entities' scripts are told, before this frame's updates.
            UI.GameUI.TakeClicks(_tellClick);

            // By index: a behaviour may place entities during the pass (their behaviours wait in _pendingStart),
            // and nothing leaves the list before the pass is over.
            int count = _running.Count;
            for (int i = 0; i < count; i++)
            {
                float dt = deltaTime;
                SafeCall(_running[i].Behaviour, beh => beh.OnUpdate(dt), "OnUpdate");
            }
            Diagnostics.Profiler.End();

            // After the scripts: sprite animations show the frame for this moment, a clip a script just chose too.
            Scene.SpriteAnimSystem.Update(ECS.ECSWorld.Instance, deltaTime);
            // And the animation players: a clip's pose for this moment (animation comes after the scripts' update), its events to the scripts.
            Animation.AnimationSystem.EventSink ??= DispatchAnimationEvent;
            Diagnostics.Profiler.Begin(_profAnimation);
            Animation.AnimationSystem.Update(ECS.ECSWorld.Instance, deltaTime);
            Animation.AnimatorSystem.Update(ECS.ECSWorld.Instance, deltaTime);      // and the controllers' characters
            Diagnostics.Profiler.End();
            Diagnostics.Profiler.Begin(_profAudio);
            Sound.Audio.Update(ECS.ECSWorld.Instance, realTime);     // sound goes on while the game's time stands
            Diagnostics.Profiler.End();

            // The end of the frame for scripts: entities handed to Destroy leave now, and a scene that was
            // asked for takes this one's place.
            Scene.SceneManager.FlushDestroyed();
            Scene.SceneManager.ChangeSceneIfAsked();
        }

        /// <summary>Calls <see cref="GameBehaviour.OnDestroy"/> on all behaviours and clears the list.</summary>
        public static void StopAll()
        {
            foreach (var (_, b) in _running)
                SafeCall(b, static beh => beh.OnDestroy(), "OnDestroy");
            _running.Clear();
            _pendingStart.Clear();
            _started = false;
            Events.Clear();                         // §6: no handler outlives the game that added it
            DataAssets.ForgetInstances();           // §6: the next Play reads the asset files as they are then
            Animation.AnimationSystem.Reset();      // its bindings name this game's entities
            Draw.Clear();
            Animation.AnimatorSystem.Reset();
            Scene.WaysSystem.Reset();
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

        /// <summary>An animation event the entity's clip passed: to each of its scripts (the animated entity's).</summary>
        internal static void DispatchAnimationEvent(Entity entity, Animation.AnimationEvent ev)
        {
            foreach (var entry in _running)
            {
                if (entry.EntityId != entity) continue;
                SafeCall(entry.Behaviour, b => b.OnAnimationEvent(ev), nameof(GameBehaviour.OnAnimationEvent));
            }
        }

        internal static void DispatchCollisionEnter(Entity entity, Entity other)
        {
            foreach (var entry in _running)
            {
                if (entry.EntityId != entity) continue;
                SafeCall(entry.Behaviour, b => b.OnCollisionEnter(other), nameof(GameBehaviour.OnCollisionEnter));
            }
        }

        internal static void DispatchParticleTrigger(Entity entity)
        {
            foreach (var entry in _running)
            {
                if (entry.EntityId != entity) continue;
                SafeCall(entry.Behaviour, b => b.OnParticleTrigger(), nameof(GameBehaviour.OnParticleTrigger));
            }
        }

        internal static void DispatchParticleCollision(Entity entity, Entity other)
        {
            foreach (var entry in _running)
            {
                if (entry.EntityId != entity) continue;
                SafeCall(entry.Behaviour, b => b.OnParticleCollision(other), nameof(GameBehaviour.OnParticleCollision));
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

        private static readonly Action<Entity> _tellClick = static entity =>
        {
            foreach (var entry in _running)
                if (entry.EntityId == entity) SafeCall(entry.Behaviour, static b => b.OnClick(), nameof(GameBehaviour.OnClick));
        };

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
