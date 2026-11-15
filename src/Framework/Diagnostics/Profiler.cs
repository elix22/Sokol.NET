using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace GameEditor.Framework.Diagnostics
{
    /// <summary>
    /// The profiler (G11.3, the GameEditor's docs/QOL_DESIGN.md): while <see cref="Enabled"/>, each frame's samples - the
    /// engine's (scripts, physics, animation, audio, UI, rendering and its stages) and a script's own
    /// (<see cref="BeginSample"/> / <see cref="EndSample"/>) - nested, timed, and summed into the reference editor's CPU
    /// categories; with the frame's rendering counters (sokol's own stats) and memory (GC). Zero allocations a frame once
    /// every name was seen. A development build sends each frame to an attached editor (<see cref="PlayerLink"/>).
    /// </summary>
    public static class Profiler
    {
        public enum Category : byte { Rendering, Scripts, Physics, Animation, Audio, UI, Others, Inherit }
        public const int Categories = 7;
        public static readonly string[] CategoryNames = { "Rendering", "Scripts", "Physics", "Animation", "Audio", "UI", "Others" };

        /// <summary>One frame: its time, the time of each category (self time: a sample's children are theirs), the counters,
        /// the samples (name id, depth, start and length in microseconds from the frame's start, its category).</summary>
        public sealed class FrameData
        {
            public long Index;
            public float Ms;
            public readonly float[] CategoryMs = new float[Categories];
            public int DrawCalls, SetPass, Passes, Triangles, Bindings;
            public long UploadBytes, GcAllocated, Memory;
            public int GcCollections;
            public int SampleCount;
            public int[] Names = new int[256];
            public byte[] Depths = new byte[256];
            public byte[] Cats = new byte[256];
            public float[] StartUs = new float[256], LengthUs = new float[256];

            internal void Ensure(int n)
            {
                if (Names.Length >= n) return;
                int size = Math.Max(n, Names.Length * 2);
                Array.Resize(ref Names, size); Array.Resize(ref Depths, size); Array.Resize(ref Cats, size);
                Array.Resize(ref StartUs, size); Array.Resize(ref LengthUs, size);
            }
        }

        /// <summary>Recording: off costs one bool test a sample.</summary>
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                _depth = 0; _count = 0; _inFrame = false;
                StatsChanged?.Invoke(value);
            }
        }
        static bool _enabled;
        /// <summary>The renderer turns sokol's frame stats on and off with recording.</summary>
        public static event Action<bool>? StatsChanged;

        /// <summary>A frame was recorded (on the game's thread). The same object is reused: copy what is kept.</summary>
        public static event Action<FrameData>? FrameDone;
        public static FrameData Last { get; } = new();

        // Names: id → name and category.
        static readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
        static readonly List<string> _names = new();
        static readonly List<Category> _cats = new();
        public static int NameCount => _names.Count;
        public static string NameOf(int id) => (uint)id < (uint)_names.Count ? _names[id] : "?";
        public static Category CategoryOf(int id) => (uint)id < (uint)_cats.Count ? _cats[id] : Category.Others;

        /// <summary>The id of a sample's name (made the first time: the one allocation). A script's samples inherit the
        /// category of the sample they are in.</summary>
        public static int Id(string name, Category category = Category.Inherit)
        {
            lock (_ids)
            {
                if (_ids.TryGetValue(name, out int id)) return id;
                id = _names.Count;
                _ids[name] = id; _names.Add(name); _cats.Add(category);
                return id;
            }
        }

        // The frame being recorded: its samples in the order they began.
        struct Sample { public int Name; public int Depth; public long Start, End; }
        static Sample[] _samples = new Sample[1024];
        static int _count, _depth;
        static readonly int[] _stack = new int[64];
        static long _frameStart, _frameIndex, _gcBefore;
        static int _gcCountBefore;
        static bool _inFrame;
        static int _mainThread = -1;

        /// <summary>Counters the renderer reports for the frame (sokol's stats, triangles).</summary>
        public static int DrawCalls, SetPass, Passes, Triangles, Bindings;
        public static long UploadBytes;

        public static void BeginSample(string name) => Begin(Id(name));
        public static void EndSample() => End();

        /// <summary>Opens a sample (the game's thread only: another thread's are not recorded).</summary>
        public static void Begin(int id)
        {
            if (!_enabled || !_inFrame || Environment.CurrentManagedThreadId != _mainThread) return;
            if (_depth >= _stack.Length) { _depth++; return; }
            if (_count == _samples.Length) Array.Resize(ref _samples, _samples.Length * 2);
            _samples[_count] = new Sample { Name = id, Depth = _depth, Start = Stopwatch.GetTimestamp() };
            _stack[_depth++] = _count++;
        }

        public static void End()
        {
            if (!_enabled || !_inFrame || _depth == 0 || Environment.CurrentManagedThreadId != _mainThread) return;
            _depth--;
            if (_depth < _stack.Length) _samples[_stack[_depth]].End = Stopwatch.GetTimestamp();
        }

        /// <summary>The frame starts (the game's loop, the editor's frame).</summary>
        public static void BeginFrame()
        {
            if (!_enabled) return;
            _mainThread = Environment.CurrentManagedThreadId;
            _inFrame = true;
            _count = 0; _depth = 0;
            DrawCalls = SetPass = Passes = Triangles = Bindings = 0; UploadBytes = 0;
            _frameStart = Stopwatch.GetTimestamp();
            _gcBefore = GC.GetAllocatedBytesForCurrentThread();
            _gcCountBefore = GC.CollectionCount(0);
        }

        /// <summary>The frame ends: its samples closed, summed, handed out.</summary>
        public static void EndFrame()
        {
            if (!_enabled || !_inFrame) return;
            long now = Stopwatch.GetTimestamp();
            while (_depth > 0) End();   // a sample left open ends with the frame
            _inFrame = false;
            var f = Last;
            f.Index = ++_frameIndex;
            double toUs = 1e6 / Stopwatch.Frequency;
            f.Ms = (float)((now - _frameStart) * toUs / 1000.0);
            Array.Clear(f.CategoryMs);
            f.Ensure(_count);
            f.SampleCount = _count;
            Span<byte> cats = stackalloc byte[64];
            double topLevel = 0;
            for (int i = 0; i < _count; i++)
            {
                ref var s = ref _samples[i];
                if (s.End < s.Start) s.End = now;
                var c = CategoryOf(s.Name);
                if (c == Category.Inherit) c = s.Depth > 0 && s.Depth <= cats.Length ? (Category)cats[s.Depth - 1] : Category.Scripts;
                if (s.Depth < cats.Length) cats[s.Depth] = (byte)c;
                f.Names[i] = s.Name; f.Depths[i] = (byte)Math.Min(s.Depth, 255); f.Cats[i] = (byte)c;
                f.StartUs[i] = (float)((s.Start - _frameStart) * toUs);
                f.LengthUs[i] = (float)((s.End - s.Start) * toUs);
            }
            // Self time per category: a sample's length less its direct children's.
            for (int i = 0; i < _count; i++)
            {
                float self = f.LengthUs[i];
                for (int j = i + 1; j < _count && f.Depths[j] > f.Depths[i]; j++)
                    if (f.Depths[j] == f.Depths[i] + 1) self -= f.LengthUs[j];
                f.CategoryMs[f.Cats[i]] += MathF.Max(0f, self) / 1000f;
                if (f.Depths[i] == 0) topLevel += f.LengthUs[i];
            }
            f.CategoryMs[(int)Category.Others] += MathF.Max(0f, f.Ms - (float)(topLevel / 1000.0));
            f.DrawCalls = DrawCalls; f.SetPass = SetPass; f.Passes = Passes; f.Triangles = Triangles; f.Bindings = Bindings;
            f.UploadBytes = UploadBytes;
            f.GcAllocated = GC.GetAllocatedBytesForCurrentThread() - _gcBefore;
            f.GcCollections = GC.CollectionCount(0) - _gcCountBefore;
            f.Memory = GC.GetTotalMemory(false);
            FrameDone?.Invoke(f);
        }

        // ── The player link: frames as lines ──────────────────────────────────────────────
        static int _namesSent;
        static readonly StringBuilder _line = new(4096);

        /// <summary>A development build: recording follows the attached editor ({"t":"profile","on":…}), each frame is sent.</summary>
        internal static void Link()
        {
            PlayerLink.Received += (t, line) =>
            {
                if (t != "profile") return;
                bool on = line.Contains("\"on\":true", StringComparison.Ordinal);
                if (on) _namesSent = 0;   // a new editor: the names again
                Enabled = on;
            };
            FrameDone += f => { if (PlayerLink.Connected) PlayerLink.Send(FrameLine(f)); };
        }

        /// <summary>A frame as a link line: {"t":"frame", …} after the names it uses that were not sent yet ({"t":"names"}).</summary>
        public static string FrameLine(FrameData f)
        {
            var b = _line.Clear();
            int names;
            lock (_ids) names = _names.Count;
            if (_namesSent < names)
            {
                b.Append("{\"t\":\"names\",\"from\":").Append(_namesSent).Append(",\"n\":[");
                for (int i = _namesSent; i < names; i++)
                {
                    if (i > _namesSent) b.Append(',');
                    b.Append('[').Append(PlayerLink.Str(_names[i])).Append(',').Append((int)_cats[i]).Append(']');
                }
                b.Append("]}\n");
                _namesSent = names;
            }
            var ci = CultureInfo.InvariantCulture;
            b.Append("{\"t\":\"frame\",\"i\":").Append(f.Index).Append(",\"ms\":").Append(f.Ms.ToString("0.###", ci)).Append(",\"c\":[");
            for (int c = 0; c < Categories; c++) { if (c > 0) b.Append(','); b.Append(f.CategoryMs[c].ToString("0.###", ci)); }
            b.Append("],\"dc\":").Append(f.DrawCalls).Append(",\"sp\":").Append(f.SetPass).Append(",\"ps\":").Append(f.Passes)
             .Append(",\"tr\":").Append(f.Triangles).Append(",\"bd\":").Append(f.Bindings).Append(",\"up\":").Append(f.UploadBytes)
             .Append(",\"ga\":").Append(f.GcAllocated).Append(",\"gc\":").Append(f.GcCollections).Append(",\"mem\":").Append(f.Memory)
             .Append(",\"s\":[");
            for (int i = 0; i < f.SampleCount; i++)
            {
                if (i > 0) b.Append(',');
                b.Append('[').Append(f.Names[i]).Append(',').Append(f.Depths[i]).Append(',').Append(f.Cats[i]).Append(',')
                 .Append(f.StartUs[i].ToString("0", ci)).Append(',').Append(f.LengthUs[i].ToString("0.#", ci)).Append(']');
            }
            return b.Append("]}").ToString();
        }
    }
}
