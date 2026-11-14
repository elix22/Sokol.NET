namespace GameEditor.Framework.Renderer.Server.Materials.Graph
{
    /// <summary>
    /// The shader graphs' Time (seconds; graph_params[7].x; G8.10): the game's time - moved on by the game's update (dt ×
    /// the Time Scale, in Play), standing still while paused, running in edit mode (the Scene view shows the graphs move) -
    /// the reference editor's Time node. A class of its own: the game's update reaches it without the renderer.
    /// </summary>
    public static class GraphClock
    {
        public static double Time { get; private set; }

        public static void Advance(double dt) { if (dt > 0) Time += dt; }
    }
}
