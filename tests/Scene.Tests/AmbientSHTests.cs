using System.Numerics;
using GameEditor.Framework.Core;
using GameEditor.Framework.Renderer.Server.Lighting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>AmbientSH: irradiance/π of skies whose answer is exact at order 2, rotation, shader packing.</summary>
    public sealed class AmbientSHTests
    {
        const int Size = 64;

        // Radiance as a function of direction; the test re-derives each texel's direction the same way.
        static AmbientSH Project(Func<Vector3, Vector3> radiance)
            => AmbientSH.FromCube(Size, (f, x, y) => radiance(Dir(f, (x + 0.5f) / Size, (y + 0.5f) / Size)));

        // Same mapping as EnvironmentMap.CubeDirection (sokol cube convention).
        static Vector3 Dir(int face, float u, float v)
        {
            float uc = 2f * u - 1f, vc = 2f * v - 1f;
            return Vector3.Normalize(face switch
            {
                0 => new Vector3(1f, -vc, -uc), 1 => new Vector3(-1f, -vc, uc),
                2 => new Vector3(uc, 1f, vc),   3 => new Vector3(uc, -1f, -vc),
                4 => new Vector3(uc, -vc, 1f),  _ => new Vector3(-uc, -vc, -1f),
            });
        }

        static IEnumerable<Vector3> Normals()
        {
            var rng = new Random(7);
            for (int i = 0; i < 64; i++)
                yield return Vector3.Normalize(new Vector3(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1));
            yield return Vector3.UnitY;
            yield return -Vector3.UnitY;
        }

        static void Near(Vector3 expected, Vector3 actual, float tol)
            => Assert.True(Vector3.Distance(expected, actual) <= tol, $"expected {expected}, got {actual}");

        [Fact]
        public void ConstantSky_IsTheSameColourEverywhere()
        {
            var c = new Vector3(0.5f, 0.25f, 1f);
            var sh = Project(_ => c);
            foreach (var n in Normals()) Near(c, sh.Evaluate(n), 1e-3f);
        }

        [Fact]
        public void LinearSky_BandOneScaledByTwoThirds()
        {
            // L = 1 + 0.5 d.y  →  E/π = 1 + (2/3)(0.5 n.y)
            var sh = Project(d => new Vector3(1f + 0.5f * d.Y));
            foreach (var n in Normals()) Near(new Vector3(1f + n.Y / 3f), sh.Evaluate(n), 2e-3f);
        }

        [Fact]
        public void QuadraticSky_BandTwoScaledByOneQuarter()
        {
            // L = d.x² = 1/3 + (d.x² − 1/3)  →  E/π = 1/3 + (n.x² − 1/3)/4
            var sh = Project(d => new Vector3(d.X * d.X));
            foreach (var n in Normals()) Near(new Vector3(1f / 3f + (n.X * n.X - 1f / 3f) / 4f), sh.Evaluate(n), 2e-3f);
        }

        [Fact]
        public void Rotated_MatchesSamplingThroughTheRotation()
        {
            var sh  = Project(d => new Vector3(1f + d.Y, 0.5f + 0.3f * d.X * d.Z, MathF.Max(d.Z, 0f)));
            var rot = Matrix4x4.CreateFromYawPitchRoll(0.7f, -0.4f, 1.1f);
            var r   = sh.Rotated(rot);
            foreach (var n in Normals()) Near(sh.Evaluate(Vector3.TransformNormal(n, rot)), r.Evaluate(n), 1e-5f);
        }

        [Fact]
        public void Pack_IsWhatTheShaderEvaluates()
        {
            var sh = Project(d => new Vector3(1f + d.Y, 0.5f + 0.3f * d.X * d.Z, MathF.Max(d.Z, 0f)));
            var p  = new Vector4[9];
            sh.Pack(p, 2f);
            foreach (var n in Normals())
            {
                // blinn_phong.glsl sky_ambient_channel, before its max(…, 0)
                float Ch(int i) => p[i].X + Vector3.Dot(new Vector3(p[i].Y, p[i].Z, p[i].W), n)
                                 + Vector3.Dot(new Vector3(p[i + 1].X, p[i + 1].Y, p[i + 1].Z), n * n)
                                 + 2f * (p[i + 1].W * n.X * n.Y + p[i + 2].X * n.X * n.Z + p[i + 2].Y * n.Y * n.Z);
                Near(sh.Evaluate(n) * 2f, new Vector3(Ch(0), Ch(3), Ch(6)), 1e-5f);
            }
        }

        [Fact]
        public void Rgba8Faces_MatchTheirValues()
        {
            var faces = new byte[6][];
            for (int f = 0; f < 6; f++)
            {
                faces[f] = new byte[Size * Size * 4];
                for (int i = 0; i < Size * Size; i++) { faces[f][i * 4] = 255; faces[f][i * 4 + 1] = 51; faces[f][i * 4 + 2] = (byte)(f == 2 ? 255 : 0); }
            }
            var sh = AmbientSH.FromRgba8Faces(faces, Size);
            Near(new Vector3(1f, 0.2f, 0f), sh.Evaluate(-Vector3.UnitY), 0.08f);   // the +Y face is the only blue one
            Assert.True(sh.Evaluate(Vector3.UnitY).Z > sh.Evaluate(-Vector3.UnitY).Z + 0.2f);
        }

        [Fact]
        public void Settings_LoadAmbientFromConfig()
        {
            var s = new EnvironmentSettings();
            Assert.Equal(AmbientSource.Sky, s.Ambient);
            EnvironmentSettings.LoadInto(s, new ProjectConfig
            {
                EnvironmentAmbientSource = "color", EnvironmentAmbientColor = "0.2 0.3 0.4", EnvironmentAmbientEnergy = 2f,
            });
            Assert.Equal(AmbientSource.Color, s.Ambient);
            Assert.Equal(new Vector3(0.2f, 0.3f, 0.4f), s.AmbientColor);
            Assert.Equal(2f, s.AmbientEnergy);
            Assert.Equal("color", s.AmbientSourceName);
            Assert.Equal("0.2 0.3 0.4", s.AmbientColorText);
            EnvironmentSettings.LoadInto(s, new ProjectConfig { EnvironmentAmbientSource = "disabled", EnvironmentAmbientColor = "bad" });
            Assert.Equal(AmbientSource.Disabled, s.Ambient);
            Assert.Equal(new Vector3(0.2f, 0.3f, 0.4f), s.AmbientColor);   // unparsable colour keeps the current one
        }
    }
}
