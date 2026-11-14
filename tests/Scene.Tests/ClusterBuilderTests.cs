using System.Numerics;
using GameEditor.Framework.Renderer.Server.Lighting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// ClusterBuilder (G8.2, Forward+): every point a light reaches is in a cluster that holds the light; a light is only
    /// in clusters near it; directional and never-ending lights everywhere, range 0 nowhere.
    /// </summary>
    public sealed class ClusterBuilderTests
    {
        static GpuLight Point(Vector3 p, float range)
            => new(new Light(LightType.Point, p, Vector3.UnitZ, Vector3.One, 1f, range));

        static GpuLight Spot(Vector3 p, Vector3 dir, float range)
            => new(new Light(LightType.Spot, p, dir, Vector3.One, 1f, range, 0.3f, 0.5f));

        static (Matrix4x4 view, Matrix4x4 proj) Camera(Vector3 eye, Vector3 at, float fovDeg, float aspect, float near, float far, float shiftX = 0f)
        {
            var view = Matrix4x4.CreateLookAt(eye, at, Vector3.UnitY);
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(fovDeg * MathF.PI / 180f, aspect, near, far);
            proj.M31 = shiftX;   // an off-centre frustum (as a lens shift)
            return (view, proj);
        }

        // A random point inside the view frustum (between near and far, on screen).
        static Vector3 PointInView(Random rnd, in Matrix4x4 view, in Matrix4x4 proj, float near, float far)
        {
            Matrix4x4.Invert(view * proj, out var inv);
            float depth = near * MathF.Pow(far / near, (float)rnd.NextDouble());
            // NDC z of that depth for a right-handed perspective: z_clip = -d·M33 + M43, w = d.
            float z = (-depth * proj.M33 + proj.M43) / depth;
            var p = Vector4.Transform(new Vector4((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 2 - 1, z, 1f), inv);
            return new Vector3(p.X, p.Y, p.Z) / p.W;
        }

        [Theory]
        [InlineData(0.1f, 1000f, 60f, 0f)]
        [InlineData(0.3f, 100f, 45f, 0f)]
        [InlineData(1f, 50f, 90f, 0.2f)]
        public void Every_point_a_light_reaches_is_in_a_cluster_holding_it(float near, float far, float fov, float shift)
        {
            var (view, proj) = Camera(new Vector3(2, 3, 8), new Vector3(0, 0, -5), fov, 16f / 9f, near, far, shift);
            var rnd = new Random(7);
            var lights = new GpuLight[64];
            for (int i = 0; i < lights.Length; i++)
            {
                var p = PointInView(rnd, view, proj, near, MathF.Min(far, 60f));
                float range = 0.2f + (float)rnd.NextDouble() * 6f;
                lights[i] = i % 3 == 0 ? Spot(p, Vector3.Normalize(new Vector3(0, -1, 0.3f)), range) : Point(p, range);
            }
            var b = new ClusterBuilder(256);
            Assert.True(b.Build(view, proj, near, far, lights));
            int tested = 0;
            for (int s = 0; s < 40000; s++)
            {
                var q = PointInView(rnd, view, proj, near, MathF.Min(far, 70f));
                int c = b.ClusterOf(q);
                for (int li = 0; li < lights.Length; li++)
                {
                    var lp = new Vector3(lights[li].PositionType.X, lights[li].PositionType.Y, lights[li].PositionType.Z);
                    if (Vector3.Distance(q, lp) >= lights[li].DirectionRange.W) continue;
                    tested++;
                    Assert.True(b.Has(c, li), $"light {li} reaches {q} (cluster {c}) but is not in it");
                }
            }
            Assert.True(tested > 2000, $"only {tested} lit points tested");
        }

        [Fact]
        public void Points_near_a_light_reach_it_around_the_screen_edges_and_the_planes()
        {
            const float near = 0.5f, far = 40f;
            var (view, proj) = Camera(Vector3.Zero, -Vector3.UnitZ, 60f, 16f / 9f, near, far);
            var rnd = new Random(3);
            var b = new ClusterBuilder(32);
            // Big lights at the corners of the frustum and near both planes.
            var lights = new[] { Point(new Vector3(-6, 3.3f, -10), 4f), Point(new Vector3(0, 0, -0.6f), 1f), Point(new Vector3(20, -11, -39), 8f) };
            Assert.True(b.Build(view, proj, near, far, lights));
            for (int s = 0; s < 60000; s++)
            {
                var q = PointInView(rnd, view, proj, near, far);
                for (int li = 0; li < lights.Length; li++)
                {
                    var lp = new Vector3(lights[li].PositionType.X, lights[li].PositionType.Y, lights[li].PositionType.Z);
                    if (Vector3.Distance(q, lp) < lights[li].DirectionRange.W)
                        Assert.True(b.Has(b.ClusterOf(q), li), $"light {li} at {q}");
                }
            }
        }

        [Fact]
        public void A_small_light_is_only_in_clusters_near_it()
        {
            var (view, proj) = Camera(Vector3.Zero, -Vector3.UnitZ, 60f, 16f / 9f, 0.1f, 100f);
            var b = new ClusterBuilder(32);
            // Left of the screen, 10 m away, range 0.5.
            var light = Point(new Vector3(-4.5f, 0, -10), 0.5f);
            Assert.True(b.Build(view, proj, 0.1f, 100f, new[] { light }));
            int count = 0, minI = 99, maxI = -1, minK = 99, maxK = -1;
            for (int c = 0; c < ClusterBuilder.Count; c++)
            {
                if (!b.Has(c, 0)) continue;
                count++;
                int i = c % ClusterBuilder.TilesX, k = c / (ClusterBuilder.TilesX * ClusterBuilder.TilesY);
                minI = Math.Min(minI, i); maxI = Math.Max(maxI, i); minK = Math.Min(minK, k); maxK = Math.Max(maxK, k);
            }
            Assert.InRange(count, 1, 24);
            Assert.True(maxI < ClusterBuilder.TilesX / 2, $"tiles {minI}..{maxI}: left half only");
            int home = b.ClusterOf(new Vector3(-4.5f, 0, -10)) / (ClusterBuilder.TilesX * ClusterBuilder.TilesY);
            Assert.InRange(minK, home - 1, home);
            Assert.InRange(maxK, home, home + 1);
        }

        [Fact]
        public void Behind_the_camera_and_past_far_a_light_is_in_no_cluster()
        {
            var (view, proj) = Camera(Vector3.Zero, -Vector3.UnitZ, 60f, 16f / 9f, 0.1f, 50f);
            var b = new ClusterBuilder(32);
            Assert.True(b.Build(view, proj, 0.1f, 50f, new[] { Point(new Vector3(0, 0, 3), 2f), Point(new Vector3(0, 0, -60), 5f), Point(new Vector3(0, 0, -5), 0f) }));
            for (int c = 0; c < ClusterBuilder.Count; c++)
            {
                Assert.False(b.Has(c, 0), "behind the camera");
                Assert.False(b.Has(c, 1), "past far");
                Assert.False(b.Has(c, 2), "range 0 gives no light");
            }
        }

        [Fact]
        public void A_directional_light_and_a_never_ending_one_are_in_every_cluster()
        {
            var (view, proj) = Camera(Vector3.Zero, -Vector3.UnitZ, 60f, 16f / 9f, 0.1f, 50f);
            var b = new ClusterBuilder(64);
            var sun = new GpuLight(new Light(LightType.Directional, Vector3.Zero, -Vector3.UnitY, Vector3.One, 1f, 0f));
            var lights = new GpuLight[40];
            for (int i = 0; i < lights.Length; i++) lights[i] = Point(new Vector3(100, 0, 0), 1f);
            lights[0] = sun;
            lights[37] = Point(new Vector3(0, 0, 200), -1f);
            Assert.True(b.Build(view, proj, 0.1f, 50f, lights));
            for (int c = 0; c < ClusterBuilder.Count; c++)
            {
                Assert.True(b.Has(c, 0));
                Assert.True(b.Has(c, 37), "light 37 (word 2) everywhere");
                Assert.False(b.Has(c, 36));
            }
        }

        [Fact]
        public void Lights_past_the_limit_are_left_out_and_an_orthographic_view_is_refused()
        {
            var (view, proj) = Camera(Vector3.Zero, -Vector3.UnitZ, 60f, 16f / 9f, 0.1f, 50f);
            var b = new ClusterBuilder(32);
            Assert.Equal(2, b.Words);
            var lights = new GpuLight[40];
            for (int i = 0; i < lights.Length; i++) lights[i] = Point(new Vector3(0, 0, -5), 3f);
            Assert.True(b.Build(view, proj, 0.1f, 50f, lights));
            Assert.Equal(32, b.LightCount);
            Assert.True(b.Has(b.ClusterOf(new Vector3(0, 0, -5)), 31));

            var ortho = Matrix4x4.CreateOrthographic(10, 6, 0.1f, 50f);
            Assert.False(b.Build(view, ortho, 0.1f, 50f, lights));
        }

        [Fact]
        public void The_cluster_of_a_point_follows_the_shaders_formula()
        {
            var (view, proj) = Camera(Vector3.Zero, -Vector3.UnitZ, 60f, 1f, 1f, 100f);
            var b = new ClusterBuilder(16);
            Assert.True(b.Build(view, proj, 1f, 100f, ReadOnlySpan<GpuLight>.Empty));
            // On the axis at depth 10: the middle tile (8, 4), slice ⌊ln 10 · 24 / ln 100⌋ = 12.
            Assert.Equal((12 * ClusterBuilder.TilesY + 4) * ClusterBuilder.TilesX + 8, b.ClusterOf(new Vector3(0.001f, 0.001f, -10f)));
            // Up and to the left, near (depth 1.1: ⌊ln 1.1 · 24 / ln 100⌋ = 0): tile (0, 8), slice 0.
            float t = MathF.Tan(MathF.PI / 6f) * 1.1f * 0.99f;
            Assert.Equal((0 * ClusterBuilder.TilesY + 8) * ClusterBuilder.TilesX + 0, b.ClusterOf(new Vector3(-t, t, -1.1f)));
        }
    }
}
