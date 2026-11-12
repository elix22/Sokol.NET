namespace GameEditor.Framework.ECS.Components
{
    /// <summary>
    /// A race on the scene's track (docs/H5_DESIGN.md §7): how many laps, the countdown that holds the karts, when a kart
    /// that is off the road, turned over or stuck is put back. One in a scene. Block "Race". What it finds is
    /// <see cref="Scene.Race"/>'s.
    /// </summary>
    public struct RaceComponent
    {
        /// <summary>Laps to finish.</summary>
        public int Laps;
        /// <summary>Seconds of countdown at the start, the karts held.</summary>
        public float Countdown;
        /// <summary>Seconds a kart may be off the road, turned over or stuck before it is put back.</summary>
        public float RespawnAfter;
        /// <summary>Metres beyond the road's edge that count as off it.</summary>
        public float OffTrack;

        public static RaceComponent Default => new() { Laps = 3, Countdown = 3f, RespawnAfter = 2f, OffTrack = 1.5f };
    }

    /// <summary>A kart that races (an entity with a Kart). Block "Racer". Its lap, place and time are <see cref="Scene.Race.Of"/>'s.</summary>
    public struct RacerComponent
    {
        /// <summary>Its name in the results (the entity's name when empty).</summary>
        public string Name;

        public static RacerComponent Default => new() { Name = "" };
    }

    /// <summary>What a <see cref="RaceHudComponent"/> shows.</summary>
    public enum RaceHudShows { Lap, Place, Time, Countdown, Results }

    /// <summary>
    /// A UI Text the race writes each frame: the player's lap ("Lap 2/3"), place ("1st"), the race's time ("1:02.4"), the
    /// countdown ("3", "2", "1", "GO!") or the results. Block "RaceHud".
    /// </summary>
    public struct RaceHudComponent
    {
        public RaceHudShows Shows;
    }
}
