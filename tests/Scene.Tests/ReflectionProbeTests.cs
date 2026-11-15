using System.Numerics;
using GameEditor.Framework.Renderer.Server.GI;
using Xunit;

namespace Scene.Tests
{
    /// <summary>Reflection probes (M8c, examples/GameEditor/docs/BAKED_GI_DESIGN.md §4.4): the box projection, the probe an
    /// object reflects.</summary>
    public sealed class ReflectionProbeTests
    {
        static readonly Vector3 Min = new(-5, 0, -5), Max = new(5, 4, 5), Capture = new(0, 2, 0);

        [Fact]
        public void From_the_capture_point_a_direction_is_unchanged()
        {
            var r = Vector3.Normalize(new Vector3(1, 0.3f, -0.4f));
            var d = Vector3.Normalize(ReflectionProbes.BoxProject(Capture, r, Min, Max, Capture));
            Assert.True(Vector3.Distance(d, r) < 1e-4f, $"{d} vs {r}");
        }

        [Fact]
        public void From_a_corner_a_ray_toward_a_wall_reads_the_wall_where_it_is()
        {
            // at (4, 2, 4), looking along -Z: the ray meets the far wall z = -5 at (4, 2, -5); from the capture point that
            // spot is to the right (+X) - not straight ahead along -Z as without the box
            var p = new Vector3(4, 2, 4);
            var d = ReflectionProbes.BoxProject(p, -Vector3.UnitZ, Min, Max, Capture);
            Assert.True(Vector3.Distance(d, new Vector3(4, 0, -5)) < 1e-4f, $"{d}");
            // looking along +X it meets the wall x = 5 at (5, 2, 4)
            Assert.True(Vector3.Distance(ReflectionProbes.BoxProject(p, Vector3.UnitX, Min, Max, Capture), new Vector3(5, 0, 4)) < 1e-4f);
        }

        [Fact]
        public void An_object_reflects_the_probe_whose_box_holds_it_the_more_important_then_the_smaller()
        {
            var big = new ReflectionProbeData { Min = new(-10, -10, -10), Max = new(10, 10, 10), Importance = 1 };
            var small = new ReflectionProbeData { Min = new(-1, -1, -1), Max = new(1, 1, 1), Importance = 1 };
            var important = new ReflectionProbeData { Min = new(-10, -10, -10), Max = new(10, 10, 10), Importance = 2 };
            Assert.True(ReflectionProbes.Better(small, Vector3.Zero, big));            // the smaller box
            Assert.False(ReflectionProbes.Better(big, Vector3.Zero, small));
            Assert.True(ReflectionProbes.Better(important, Vector3.Zero, small));      // importance first
            Assert.False(ReflectionProbes.Better(small, new Vector3(5, 0, 0), null)); // outside its box: not it
            Assert.True(ReflectionProbes.Better(big, new Vector3(5, 0, 0), null));
        }
    }
}
