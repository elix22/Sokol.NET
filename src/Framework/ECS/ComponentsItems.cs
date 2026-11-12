using Frent;

namespace GameEditor.Framework.ECS.Components
{
    /// <summary>The items of a race (docs/H5_DESIGN.md §13). None = an empty slot.</summary>
    public enum ItemKind { None, Boost, Puddle, Bolt, Guard }

    /// <summary>
    /// An item box: a racer that drives within 1.6 m of it while it is there, with an empty slot, gets an item, and the box
    /// is hidden for <see cref="Respawn"/> seconds. Block "ItemBox". The entity's mesh is what is seen (it spins in a game).
    /// </summary>
    public struct ItemBoxComponent
    {
        public float Respawn;
        public static ItemBoxComponent Default => new() { Respawn = 3f };
    }

    /// <summary>A puddle dropped by a racer: a kart that drives into it loses drive and grip for a while. Made by the race.</summary>
    public struct PuddleComponent
    {
        public Entity Owner;
        public float Age;
    }

    /// <summary>A bolt fired ahead along the track by a racer: the kart it reaches hops and loses its drive. Made by the race.</summary>
    public struct BoltComponent
    {
        public Entity Owner;
        public float Along, Offset, Travelled;
    }
}
