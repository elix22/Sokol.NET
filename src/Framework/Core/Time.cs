using static Sokol.SApp;

namespace GameEditor.Framework.Core
{
    public static class Time
    {
        public static float DeltaTime { get; private set; }
        public static float TotalTime { get; private set; }
        public static int FrameCount { get; private set; }

        internal static void Update()
        {
            DeltaTime = (float)sapp_frame_duration();
            TotalTime += DeltaTime;
            FrameCount++;
        }
    }
}
