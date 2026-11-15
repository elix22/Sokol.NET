using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using GameEditor.Framework.Diagnostics;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The profiler (G11.3, examples/GameEditor/docs/QOL_DESIGN.md): nested samples timed, a script's sample in the
    /// category of the one it is in, self time per category, nothing while off or from another thread, the frame as link
    /// lines (the names once), the frame's allocations.</summary>
    public sealed class ProfilerTests : IDisposable
    {
        public ProfilerTests() { Profiler.Enabled = false; }
        public void Dispose() { Profiler.Enabled = false; }

        static void Busy(double ms)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalMilliseconds < ms) { }
        }

        [Fact]
        public void Samples_nest_inherit_their_category_and_sum_self_time()
        {
            int a = Profiler.Id("T.Work", Profiler.Category.Scripts);
            int c = Profiler.Id("T.Step", Profiler.Category.Physics);
            Profiler.Enabled = true;
            int frames = 0;
            Action<Profiler.FrameData> seen = _ => frames++;
            Profiler.FrameDone += seen;
            try
            {
                Profiler.BeginFrame();
                Profiler.Begin(a);
                Busy(2);
                Profiler.BeginSample("T.Inner");   // a script's own: Scripts, as the sample it is in
                Busy(1);
                Profiler.EndSample();
                Profiler.End();
                Profiler.Begin(c);
                Busy(1);
                Profiler.End();
                Busy(0.5);                        // in no sample: Others
                Profiler.EndFrame();
            }
            finally { Profiler.FrameDone -= seen; }
            var f = Profiler.Last;
            Assert.Equal(1, frames);
            Assert.Equal(3, f.SampleCount);
            Assert.Equal(new byte[] { 0, 1, 0 }, f.Depths[..3]);
            Assert.Equal("T.Inner", Profiler.NameOf(f.Names[1]));
            Assert.Equal((byte)Profiler.Category.Scripts, f.Cats[1]);
            Assert.Equal((byte)Profiler.Category.Physics, f.Cats[2]);
            Assert.InRange(f.LengthUs[0], 2900, 6000);
            Assert.InRange(f.LengthUs[1], 950, 3000);
            Assert.True(f.StartUs[1] >= f.StartUs[0] && f.StartUs[2] >= f.StartUs[0] + f.LengthUs[0] - 1);
            Assert.InRange(f.CategoryMs[(int)Profiler.Category.Scripts], 2.9f, 6f);    // Work's 2 ms + Inner's 1 ms
            Assert.InRange(f.CategoryMs[(int)Profiler.Category.Physics], 0.95f, 3f);
            Assert.True(f.CategoryMs[(int)Profiler.Category.Others] >= 0.45f);
            float sum = 0; foreach (float m in f.CategoryMs) sum += m;
            Assert.InRange(sum, f.Ms - 0.05f, f.Ms + 0.05f);                              // the categories add up to the frame
        }

        [Fact]
        public void Nothing_is_recorded_while_off_or_from_another_thread_and_an_open_sample_ends_with_the_frame()
        {
            int frames = 0;
            Action<Profiler.FrameData> seen = _ => frames++;
            Profiler.FrameDone += seen;
            try
            {
                Profiler.BeginFrame(); Profiler.BeginSample("T.Off"); Profiler.EndSample(); Profiler.EndFrame();
                Assert.Equal(0, frames);

                Profiler.Enabled = true;
                Profiler.BeginFrame();
                var t = new Thread(() => { Profiler.BeginSample("T.Thread"); Profiler.EndSample(); });
                t.Start(); t.Join();
                Profiler.BeginSample("T.Open");      // never ended
                Busy(0.5);
                Profiler.EndFrame();
            }
            finally { Profiler.FrameDone -= seen; }
            Assert.Equal(1, frames);
            Assert.Equal(1, Profiler.Last.SampleCount);
            Assert.Equal("T.Open", Profiler.NameOf(Profiler.Last.Names[0]));
            Assert.True(Profiler.Last.LengthUs[0] > 400);
        }

        [Fact]
        public void A_frame_is_link_lines_with_the_names_once()
        {
            Profiler.Enabled = true;
            Profiler.BeginFrame(); Profiler.BeginSample("T.Line \"q\""); Profiler.EndSample(); Profiler.EndFrame();
            string first = Profiler.FrameLine(Profiler.Last);
            var lines = first.Split('\n');
            Assert.Equal(2, lines.Length);
            var names = JsonDocument.Parse(lines[0]).RootElement;
            Assert.Equal("names", names.GetProperty("t").GetString());
            bool found = false;
            foreach (var n in names.GetProperty("n").EnumerateArray()) found |= n[0].GetString() == "T.Line \"q\"";
            Assert.True(found);
            var frame = JsonDocument.Parse(lines[1]).RootElement;
            Assert.Equal("frame", frame.GetProperty("t").GetString());
            Assert.Equal(Profiler.Categories, frame.GetProperty("c").GetArrayLength());
            Assert.Equal(1, frame.GetProperty("s").GetArrayLength());

            Profiler.BeginFrame(); Profiler.BeginSample("T.Line \"q\""); Profiler.EndSample(); Profiler.EndFrame();
            string second = Profiler.FrameLine(Profiler.Last);
            Assert.DoesNotContain('\n', second);                       // the names were sent
            Assert.Equal("frame", PlayerLink.TypeOf(second));
        }

        [Fact]
        public void A_frames_allocations_are_counted()
        {
            Profiler.Enabled = true;
            Profiler.BeginFrame();
            var keep = new byte[200_000];
            Profiler.EndFrame();
            GC.KeepAlive(keep);
            Assert.True(Profiler.Last.GcAllocated >= 200_000, $"{Profiler.Last.GcAllocated}");
            Assert.True(Profiler.Last.Memory > 0);
        }
    }
}
