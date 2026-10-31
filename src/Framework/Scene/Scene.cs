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

        public Scene(string name)
        {
            Name = name;
        }

        public void Clear()
        {
            ECSWorld.Instance.Clear();
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
