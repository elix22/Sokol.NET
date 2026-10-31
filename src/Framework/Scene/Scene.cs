using System;
using System.Collections.Generic;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;

namespace GameEditor.Framework.Scene
{
    public sealed class Scene
    {
        public string Name { get; set; }
        public string? FilePath { get; set; }
        public bool IsDirty { get; set; }
        /// <summary>Physics backend for Play: null (or "jolt") = the built-in Jolt world, anything else is
        /// handed to <see cref="SceneManager.SimulatorFactory"/>. Saved as the scene JSON's "physics" key.</summary>
        public string? PhysicsBackend { get; set; }
        /// <summary>Scene-level blocks of other assemblies (e.g. a plugin's settings for this scene): root JSON key →
        /// raw JSON value, written back verbatim, so a build that does not know a block keeps it. Keys the
        /// serializer writes itself ("name", "physics", "entities", "nextId") are not allowed.</summary>
        public Dictionary<string, string> Blocks { get; } = new(StringComparer.Ordinal);

        public Scene(string name)
        {
            Name = name;
        }

        public void Clear()
        {
            ECSWorld.Instance.Clear();
            Blocks.Clear();
            IsDirty = false;
        }

        public Entity CreateEntity(string name = "Entity")
        {
            IsDirty = true;
            return ECSWorld.Instance.CreateEntity(name);
        }

        public void DestroyEntity(Entity id)
        {
            IsDirty = true;
            ECSWorld.Instance.DestroyEntity(id);
        }
    }
}
