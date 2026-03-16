namespace GameEditor.Framework.Scripting
{
    public abstract class GameBehaviour
    {
        public int EntityId { get; internal set; }

        public virtual void OnStart() { }
        public virtual void OnUpdate(float deltaTime) { }
        public virtual void OnDestroy() { }
    }
}
