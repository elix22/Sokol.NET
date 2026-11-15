using System;
using System.Diagnostics;

namespace GameEditor.Framework.Renderer.Server.Profiling
{
    /// <summary>
    /// Lightweight in-process frame profiler based on Stopwatch timestamps.
    ///
    /// Usage:
    ///   Call <see cref="BeginFrame"/> at the start of each rendered frame.
    ///   Wrap sections with <see cref="Begin"/>/<see cref="End"/>.
    ///   Call <see cref="EndFrame"/> after all rendering for the frame.
    ///
    /// All hot-path operations are zero-alloc. History is stored in a fixed-size
    /// ring buffer of 128 frames per zone.
    /// </summary>
    public static class FrameProfiler
    {
        // ── Zone IDs ─────────────────────────────────────────────────────────

        public enum Zone
        {
            EcsExtract    = 0,
            ParallelCull  = 1,
            Sort          = 2,
            InstanceWrite = 3,
            DrawCalls     = 4,
            ShadowPass    = 5,
            Total         = 6,
            _Count        = 7,
        }

        // ── Storage ──────────────────────────────────────────────────────────

        private const int HistoryLen = 128;

        /// <summary>Ring buffer: [frameIndex, zoneIndex] → ticks elapsed.</summary>
        private static readonly long[,] _history = new long[HistoryLen, (int)Zone._Count];

        /// <summary>Timestamp recorded by the most recent Begin() call per zone.</summary>
        private static readonly long[] _starts = new long[(int)Zone._Count];

        private static int _frame;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Enable or disable profiling. When false, Begin/End are no-ops.</summary>
        public static bool Enabled { get; set; } = false;

        /// <summary>Human-readable names indexed by <see cref="Zone"/>.</summary>
        public static ReadOnlySpan<string> ZoneNames => _names;

        private static readonly string[] _names =
        {
            "ECS Extract",    // 0
            "Parallel Cull",  // 1
            "Sort",           // 2
            "Instance Write", // 3
            "Draw Calls",     // 4
            "Shadow Pass",    // 5
            "Total",          // 6
        };

        /// <summary>Call once at the start of each frame (inside BeginFrame).</summary>
        public static void BeginFrame()
        {
            if (!Enabled) return;
            for (int z = 0; z < (int)Zone._Count; z++)
                _history[_frame, z] = 0;
        }

        /// <summary>Start timing the given zone.</summary>
        public static void Begin(Zone zone)
        {
            if (Diagnostics.Profiler.Enabled) Diagnostics.Profiler.Begin(ProfilerId(zone));   // G11.3: a sample of the frame
            if (Enabled) _starts[(int)zone] = Stopwatch.GetTimestamp();
        }

        // The profiler's sample name of each zone (made once).
        private static readonly int[] _profilerIds = new int[(int)Zone._Count];
        private static bool _profilerNamed;
        private static int ProfilerId(Zone zone)
        {
            if (!_profilerNamed)
            {
                for (int z = 0; z < (int)Zone._Count; z++)
                    _profilerIds[z] = Diagnostics.Profiler.Id("Render." + _names[z].Replace(" ", ""), Diagnostics.Profiler.Category.Rendering);
                _profilerNamed = true;
            }
            return _profilerIds[(int)zone];
        }

        /// <summary>
        /// Stop timing the given zone and accumulate the elapsed ticks into the
        /// current frame's slot. Uses += so nested Begin/End calls are summed.
        /// </summary>
        public static void End(Zone zone)
        {
            if (Diagnostics.Profiler.Enabled) Diagnostics.Profiler.End();
            if (!Enabled) return;
            int z = (int)zone;
            _history[_frame, z] += Stopwatch.GetTimestamp() - _starts[z];
        }

        /// <summary>Advance the ring-buffer index. Call once at end of each frame.</summary>
        public static void EndFrame()
        {
            if (!Enabled) return;
            _frame = (_frame + 1) % HistoryLen;
        }

        // ── Query ─────────────────────────────────────────────────────────────

        /// <summary>Duration recorded in the most recently completed frame, in ms.</summary>
        public static float GetLastMs(Zone zone)
        {
            int prev = (_frame + HistoryLen - 1) % HistoryLen;
            return TicksToMs(_history[prev, (int)zone]);
        }

        /// <summary>Average over the entire ring buffer, in ms.</summary>
        public static float GetAvgMs(Zone zone)
        {
            int z = (int)zone;
            long sum = 0;
            for (int i = 0; i < HistoryLen; i++) sum += _history[i, z];
            return TicksToMs(sum / HistoryLen);
        }

        /// <summary>Maximum over the entire ring buffer, in ms.</summary>
        public static float GetMaxMs(Zone zone)
        {
            int z = (int)zone;
            long max = 0;
            for (int i = 0; i < HistoryLen; i++)
                if (_history[i, z] > max) max = _history[i, z];
            return TicksToMs(max);
        }

        /// <summary>
        /// Fill <paramref name="output"/> with the last <c>output.Length</c>
        /// frame durations (oldest first), in ms.
        /// </summary>
        public static void GetHistory(Zone zone, Span<float> output)
        {
            int z = (int)zone;
            int n = output.Length;
            for (int i = 0; i < n; i++)
            {
                int fi = (_frame + HistoryLen - n + i) % HistoryLen;
                output[i] = TicksToMs(_history[fi, z]);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static float TicksToMs(long ticks) =>
            (float)ticks / Stopwatch.Frequency * 1000f;
    }
}
