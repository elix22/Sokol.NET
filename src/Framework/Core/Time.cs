using System;
using static Sokol.SApp;

namespace GameEditor.Framework.Core
{
    public static class Time
    {
        public static float DeltaTime { get; private set; }
        public static float TotalTime { get; private set; }
        public static int FrameCount { get; private set; }

        private static float _fixedDeltaTime = 0.02f;
        /// <summary>The fixed tick (G10, docs DETERMINISM_DESIGN.md; the reference editor's Fixed Timestep, 0.02 s):
        /// <c>OnFixedUpdate</c> runs once for each that the game's time passes. The project setting "fixedTimestep".</summary>
        public static float FixedDeltaTime { get => _fixedDeltaTime; set => _fixedDeltaTime = MathF.Max(0.0001f, value); }
        /// <summary>The game's time in whole fixed ticks since Play (what <c>OnFixedUpdate</c> sees).</summary>
        public static double FixedTime { get; internal set; }
        /// <summary>At most this many fixed ticks a frame (the reference editor's Maximum Allowed Timestep): after a long
        /// frame the game's fixed time falls behind instead of the frame getting longer still.</summary>
        public static int MaximumFixedSteps { get; set; } = 8;

        public static void Update()
        {
            DeltaTime = (float)sapp_frame_duration();
            TotalTime += DeltaTime;
            FrameCount++;
        }
    }
}
