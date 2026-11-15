using System.Collections.Generic;

namespace GameEditor.Framework.Diagnostics
{
    /// <summary>
    /// The frame debugger (G11.4, the GameEditor's docs/QOL_DESIGN.md; the reference editor's): while <see cref="Enabled"/>,
    /// every draw of the frame inside the scope (the editor's Game view) is an event - its pass, what it draws, its
    /// elements and instances, its state - and draws after <see cref="Limit"/> are not made, so the picture is the frame
    /// up to the chosen draw. Each draw site asks <see cref="Draw"/> before <c>sg_draw</c>; off, that is one bool test.
    /// </summary>
    public static class FrameDebugger
    {
        public readonly struct Event
        {
            public readonly int Index;
            public readonly string Pass, What, Detail;
            public readonly int Elements, Instances;
            public Event(int index, string pass, string what, int elements, int instances, string detail)
            { Index = index; Pass = pass; What = what; Elements = elements; Instances = instances; Detail = detail; }
        }

        /// <summary>Capturing (the editor pauses the game while it is on).</summary>
        public static bool Enabled { get; set; }
        /// <summary>The last draw made (an event index), or -1: every draw.</summary>
        public static int Limit { get; set; } = -1;
        /// <summary>Set around what is debugged (the Game view's rendering): draws outside it are neither counted nor held back.</summary>
        public static bool InScope;
        /// <summary>The pass the next draws belong to ("Shadows", "Opaque", "Skybox", …).</summary>
        public static string Pass = "";

        static List<Event> _building = new(), _last = new();
        static int _index;

        /// <summary>The events of the last frame captured.</summary>
        public static IReadOnlyList<Event> Events => _last;

        public static void BeginFrame()
        {
            _index = 0;
            _building.Clear();
            Pass = "";
        }

        public static void EndFrame()
        {
            if (!Enabled) { if (_last.Count > 0) _last.Clear(); return; }
            (_last, _building) = (_building, _last);
        }

        /// <summary>Before a draw: false when it is not to be made (it is after the chosen event). <paramref name="detail"/>
        /// is asked only while capturing.</summary>
        public static bool Draw(string what, int elements, int instances, string detail = "")
        {
            if (!Enabled || !InScope) return true;
            int i = _index++;
            _building.Add(new Event(i, Pass, what, elements, instances, detail));
            return Limit < 0 || i <= Limit;
        }
    }
}
