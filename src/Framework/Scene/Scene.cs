using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;

namespace GameEditor.Framework.Scene
{
    public sealed class Scene
    {
        public string Name { get; set; }
        public string? FilePath { get; set; }
        public bool IsDirty { get; set; }

        public Scene(string name)
        {
            Name = name;
        }

        public void Clear()
        {
            ECSWorld.Instance.Clear();
            IsDirty = false;
        }

        public int CreateEntity(string name = "Entity")
        {
            IsDirty = true;
            return ECSWorld.Instance.CreateEntity(name);
        }

        public void DestroyEntity(int id)
        {
            IsDirty = true;
            ECSWorld.Instance.DestroyEntity(id);
        }
    }
}
