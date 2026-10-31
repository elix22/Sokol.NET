using GameEditor.Framework.ECS;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// A physics simulator that replaces the built-in Jolt world for a scene whose
    /// <see cref="Scene.PhysicsBackend"/> names it. Created by <see cref="SceneManager.SimulatorFactory"/>
    /// on Play, stepped from <see cref="SceneManager.UpdatePhysics"/> while Playing, stopped on Stop.
    /// It owns the write-back of simulated poses into the ECS.
    /// </summary>
    public interface ISceneSimulator
    {
        void Start(ECSWorld world);
        void Step(float dt);
        void Stop();
    }
}
