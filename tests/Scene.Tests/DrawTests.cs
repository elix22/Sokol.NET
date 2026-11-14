using System.Numerics;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>Draw (G8.4 D3): world lines kept for their seconds of game time (0: one frame), shapes for the frame they are asked in.</summary>
    [Collection("Sequential")]
    public sealed class DrawTests
    {
        [Fact]
        public void A_line_is_kept_for_its_seconds_and_a_zero_line_for_its_frame()
        {
            Draw.Clear();
            Draw.BeginFrame(0.016f);
            Draw.Line(Vector3.Zero, Vector3.UnitX, Vector4.One, 1f);
            Draw.Line(Vector3.Zero, Vector3.UnitY, Vector4.One);          // this frame only
            Assert.Equal(2, Draw.LineCount);
            Draw.BeginFrame(0.5f);
            Assert.Equal(1, Draw.LineCount);                              // the zero one gone, the 1 s one kept
            Draw.BeginFrame(0.4f);
            Assert.Equal(1, Draw.LineCount);
            Draw.BeginFrame(0.2f);                                        // 1.1 s after it was asked
            Assert.Equal(0, Draw.LineCount);
        }

        [Fact]
        public void Shapes_last_their_frame_boxes_and_spheres_are_lines_and_stop_clears()
        {
            Draw.Clear();
            Draw.BeginFrame(0.016f);
            Draw.Screen.Rect(new Vector2(10, 10), new Vector2(50, 20), Vector4.One);
            Draw.Screen.Text(new Vector2(0, 0), "Score", Vector4.One);
            Assert.Equal(2, Draw.ShapeCount);
            Draw.Box(Vector3.Zero, Vector3.One, Vector4.One, 5f);
            Assert.Equal(12, Draw.LineCount);
            Draw.Sphere(Vector3.Zero, 1f, Vector4.One, 5f);
            Assert.Equal(12 + 3 * 32, Draw.LineCount);
            Draw.BeginFrame(0.016f);
            Assert.Equal(0, Draw.ShapeCount);
            Assert.Equal(12 + 3 * 32, Draw.LineCount);
            Draw.Clear();
            Assert.Equal(0, Draw.LineCount);
        }
    }
}
