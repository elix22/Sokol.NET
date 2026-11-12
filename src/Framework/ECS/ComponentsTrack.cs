namespace GameEditor.Framework.ECS.Components
{
    /// <summary>
    /// A race track: the entity that names a ".track" file (<see cref="Assets.TrackData"/>: a closed centre line, its
    /// widths, banking, walls and checkpoints). Its road and walls are its children's meshes, "&lt;key&gt;#road" and
    /// "&lt;key&gt;#walls", each with a static body; a race and its drivers read the track itself
    /// (<see cref="Assets.TrackLibrary.Track"/>). Block "Track".
    /// </summary>
    public struct TrackComponent
    {
        /// <summary>The track file's key below Assets ("Tracks/loop.track").</summary>
        public string Key;

        public static TrackComponent Default => new() { Key = "" };
    }
}
