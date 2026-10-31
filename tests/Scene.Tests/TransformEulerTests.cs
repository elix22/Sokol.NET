using System.Numerics;
using GameEditor.Framework.ECS.Components;
using Xunit;

namespace Scene.Tests
{
    /// <summary>Transform.EulerAngles (YXZ, degrees): what is set reads back, up to and including pitch ±90°.</summary>
    public sealed class TransformEulerTests
    {
        static Vector3 RoundTrip(Vector3 euler) => new Transform { Rotation = Quaternion.Identity, EulerAngles = euler }.EulerAngles;

        [Theory]
        [InlineData(-90f, 0f, 0f)]
        [InlineData(90f, 0f, 0f)]
        [InlineData(-89.99f, 0f, 0f)]
        [InlineData(89.9f, 30f, 0f)]
        [InlineData(-90f, 45f, 0f)]
        public void PitchNearNinety_ReadsBackWhatWasSet(float x, float y, float z)
        {
            var r = RoundTrip(new Vector3(x, y, z));
            // At exactly ±90° yaw and roll are one degree of freedom (gimbal lock); only the pitch is unique.
            Assert.True(MathF.Abs(r.X - x) < 1e-3f, $"pitch {x} read back as {r.X}");
            if (MathF.Abs(x) < 90f) Assert.True(Vector3.Distance(new Vector3(x, y, z), r) < 1e-2f, $"{(x, y, z)} read back as {r}");
        }

        [Fact]
        public void RandomAngles_RoundTrip()
        {
            var rng = new Random(3);
            for (int i = 0; i < 1000; i++)
            {
                var e = new Vector3(rng.NextSingle() * 178f - 89f, rng.NextSingle() * 358f - 179f, rng.NextSingle() * 358f - 179f);
                var r = RoundTrip(e);
                Assert.True(Vector3.Distance(e, r) < 1e-2f, $"{e} read back as {r}");
            }
        }
    }
}
