namespace GameEditor.Framework.ECS.Components
{
    /// <summary>
    /// A kart the computer drives (docs/H5_DESIGN.md §8): it follows the track's racing line, at the speed the line
    /// allows times its skill, and now and then runs wide in a bend. With a Racer it races; nothing in it looks at a
    /// recording, and it catches up only by its gap to the player (R15). Block "Driver".
    /// </summary>
    public struct DriverComponent
    {
        /// <summary>The share of the speed the line allows that it goes (0.5 to 1).</summary>
        public float Skill;
        /// <summary>How far ahead on the line it aims (metres).</summary>
        public float LookAhead;
        /// <summary>The chance in a lap that it runs wide in one bend (0 to 1).</summary>
        public float Mistakes;
        /// <summary>How far wide it runs then (metres).</summary>
        public float WideBy;
        /// <summary>Up to 6 % faster when far behind the player, 6 % slower when far ahead.</summary>
        public bool CatchUp;

        public static DriverComponent Default => new() { Skill = 0.85f, LookAhead = 8f, Mistakes = 0.2f, WideBy = 2.5f, CatchUp = false };
    }
}
