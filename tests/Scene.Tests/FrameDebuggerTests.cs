using GameEditor.Framework.Diagnostics;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The frame debugger (G11.4, examples/GameEditor/docs/QOL_DESIGN.md): off, every draw is made and none kept;
    /// on, the draws inside the scope are the frame's events (in order, with their pass) and those after the limit are
    /// not made.</summary>
    public sealed class FrameDebuggerTests : System.IDisposable
    {
        public FrameDebuggerTests() { FrameDebugger.Enabled = false; FrameDebugger.Limit = -1; FrameDebugger.InScope = false; }
        public void Dispose() { FrameDebugger.Enabled = false; FrameDebugger.Limit = -1; FrameDebugger.InScope = false; FrameDebugger.BeginFrame(); FrameDebugger.EndFrame(); }

        static bool[] Frame()
        {
            FrameDebugger.BeginFrame();
            var made = new bool[5];
            made[0] = FrameDebugger.Draw("outside", 3, 1);          // not in the scope
            FrameDebugger.InScope = true;
            FrameDebugger.Pass = "Skybox";
            made[1] = FrameDebugger.Draw("sky", 36, 1);
            FrameDebugger.Pass = "Scene";
            made[2] = FrameDebugger.Draw("a / Red", 36, 1, "PBR");
            made[3] = FrameDebugger.Draw("b / Green", 36, 4, "PBR");
            FrameDebugger.InScope = false;
            made[4] = FrameDebugger.Draw("after", 3, 1);
            FrameDebugger.EndFrame();
            return made;
        }

        [Fact]
        public void Off_every_draw_is_made_and_none_kept()
        {
            Assert.All(Frame(), m => Assert.True(m));
            Assert.Empty(FrameDebugger.Events);
        }

        [Fact]
        public void On_the_scopes_draws_are_events_and_the_limit_holds_back_the_rest()
        {
            FrameDebugger.Enabled = true;
            Assert.All(Frame(), m => Assert.True(m));                // no limit: every draw
            var ev = FrameDebugger.Events;
            Assert.Equal(3, ev.Count);
            Assert.Equal(("Skybox", "sky", 0), (ev[0].Pass, ev[0].What, ev[0].Index));
            Assert.Equal(("Scene", "b / Green", 36, 4, "PBR"), (ev[2].Pass, ev[2].What, ev[2].Elements, ev[2].Instances, ev[2].Detail));

            FrameDebugger.Limit = 1;                                  // up to the first box
            var made = Frame();
            Assert.Equal(new[] { true, true, true, false, true }, made);   // outside the scope: always made
            Assert.Equal(3, FrameDebugger.Events.Count);             // the held-back draw is still listed

            FrameDebugger.Enabled = false;
            Frame();
            Assert.Empty(FrameDebugger.Events);
        }
    }
}
