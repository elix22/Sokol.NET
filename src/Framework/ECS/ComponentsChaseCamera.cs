namespace GameEditor.Framework.ECS.Components
{
    /// <summary>
    /// A camera that follows a kart from behind (docs/H5_DESIGN.md §2.7): <see cref="Distance"/> back and <see cref="Height"/> up,
    /// looking <see cref="LookAhead"/> metres past it, moving there smoothly (<see cref="Spring"/>, each second). While the
    /// look-back button is held it is in front of the kart looking back. Block "ChaseCamera", on an entity with a Camera.
    /// </summary>
    public struct ChaseCameraComponent
    {
        /// <summary>The name of the entity it follows; empty: the player's kart.</summary>
        public string Target;
        public float Distance;
        public float Height;
        public float LookAhead;
        /// <summary>How quickly it catches up (1/seconds); 0: at once.</summary>
        public float Spring;
        /// <summary>The button that turns it to look back while held (a key's name or a UI button's Holds), "Fire2" by default.</summary>
        public string LookBack;

        public static ChaseCameraComponent Default => new() { Target = "", Distance = 6f, Height = 2.5f, LookAhead = 4f, Spring = 6f, LookBack = "Fire2" };
    }
}
