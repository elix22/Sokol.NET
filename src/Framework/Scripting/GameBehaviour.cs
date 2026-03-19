using System.Collections.Generic;
using System.Numerics;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scripting
{
    /// <summary>
    /// Base class for all game scripts (analogous to Unity's MonoBehaviour).
    /// Subclasses are discovered by the ScriptSystem and run during play mode.
    ///
    /// Attach to entities via a <see cref="ScriptComponent"/> whose TypeName matches
    /// the subclass name.
    /// </summary>
    public abstract class GameBehaviour
    {
        /// <summary>The ECS entity this behaviour is attached to.</summary>
        public int EntityId { get; internal set; }

        // ── ECS convenience ─────────────────────────────────────────────────

        /// <summary>Access to the global ECS world.</summary>
        protected ECSWorld World => ECSWorld.Instance;

        /// <summary>Gets the entity's Transform component by reference.</summary>
        protected ref Transform Transform => ref World.GetComponent<Transform>(EntityId);

        /// <summary>Try to get a component; returns false if not present.</summary>
        protected bool TryGetComponent<T>(out T component) where T : struct
            => World.TryGetComponent<T>(EntityId, out component);

        /// <summary>Gets a component by reference; throws if absent.</summary>
        protected ref T GetComponent<T>() where T : struct
            => ref World.GetComponent<T>(EntityId);

        /// <summary>Adds or replaces a component on this entity.</summary>
        protected void SetComponent<T>(T component) where T : struct
            => World.AddComponent(EntityId, component);

        /// <summary>Returns true when this entity has the given component type.</summary>
        protected bool HasComponent<T>() where T : struct
            => World.HasComponent<T>(EntityId);

        // ── Lifecycle ───────────────────────────────────────────────────────

        /// <summary>
        /// Called by the editor before <see cref="OnStart"/> to apply serialized
        /// public field values from the scene file. Override in the editor proxy.
        /// </summary>
        public virtual void ApplySerializedProperties(Dictionary<string, string> properties) { }

        /// <summary>Called once when play mode starts. Use for initialization.</summary>
        public virtual void OnStart() { }

        /// <summary>Called every frame while in play mode.</summary>
        /// <param name="deltaTime">Seconds elapsed since the previous frame.</param>
        public virtual void OnUpdate(float deltaTime) { }

        /// <summary>Called when play mode stops or the entity is destroyed.</summary>
        public virtual void OnDestroy() { }
    }
}

