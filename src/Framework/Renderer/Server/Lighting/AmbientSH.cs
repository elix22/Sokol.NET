// AmbientSH.cs — diffuse ambient of an environment as order-2 spherical harmonics.
//
// The sky's radiance is projected onto the 9 SH basis functions (l ≤ 2) and convolved with the
// clamped cosine (Ramamoorthi & Hanrahan 2001), giving irradiance / π: the colour a white
// Lambertian surface facing n reflects. Per colour channel the l ≤ 2 expansion is stored as the
// quadratic form  f(n) = c + v·n + nᵀ Q n  (unit n), so a rotation is exact (v → Rᵀv, Q → RᵀQR)
// and the shader evaluates it with a few multiply-adds. Pure C#, CPU only, built once per environment.

using System;
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    public sealed class AmbientSH
    {
        /// <summary>Samples per face edge used for projection; larger faces are strided down to this.</summary>
        private const int MaxSamplesPerEdge = 64;

        private readonly float[]   _c = new float[3];
        private readonly Vector3[] _v = new Vector3[3];
        private readonly float[,,] _q = new float[3, 3, 3];   // [channel, i, j], symmetric

        /// <summary>
        /// Projects a cube environment. <paramref name="radiance"/>(face, x, y) returns the texel's
        /// linear radiance; faces are +X,-X,+Y,-Y,+Z,-Z with <see cref="EnvironmentMap"/>'s direction mapping.
        /// </summary>
        public static AmbientSH FromCube(int size, Func<int, int, int, Vector3> radiance)
        {
            var sh = new Vector3[9];
            Span<float>   y  = stackalloc float[9];
            int    step = Math.Max(1, size / MaxSamplesPerEdge);
            double weightSum = 0;
            for (int face = 0; face < 6; face++)
            for (int py = step / 2; py < size; py += step)
            for (int px = step / 2; px < size; px += step)
            {
                float u = (px + 0.5f) / size, v = (py + 0.5f) / size;
                float uc = 2f * u - 1f, vc = 2f * v - 1f;
                // Solid angle of a cube texel ∝ (1 + u² + v²)^-3/2; normalised to 4π below.
                float w = 1f / MathF.Pow(1f + uc * uc + vc * vc, 1.5f);
                Basis(EnvironmentMap.CubeDirection(face, u, v), y);
                Vector3 l = radiance(face, px, py) * w;
                for (int k = 0; k < 9; k++) sh[k] += l * y[k];
                weightSum += w;
            }

            float norm = (float)(4.0 * Math.PI / weightSum);
            // Cosine convolution / π per band: Â0/π = 1, Â1/π = 2/3, Â2/π = 1/4.
            for (int k = 0; k < 9; k++) sh[k] *= norm * (k == 0 ? 1f : k < 4 ? 2f / 3f : 0.25f);

            var a = new AmbientSH();
            for (int ch = 0; ch < 3; ch++)
            {
                float D(int k) => sh[k][ch];
                a._c[ch] = 0.282095f * D(0);
                a._v[ch] = 0.488603f * new Vector3(D(3), D(1), D(2));
                // Y20 = 0.315392 (3z² − 1) = 0.315392 (2z² − x² − y²) on the unit sphere.
                float xx = -0.315392f * D(6) + 0.546274f * D(8);
                float yy = -0.315392f * D(6) - 0.546274f * D(8);
                float zz =  0.630784f * D(6);
                float xy =  0.546274f * D(4), yz = 0.546274f * D(5), xz = 0.546274f * D(7);
                a.SetQ(ch, xx, yy, zz, xy, yz, xz);
            }
            return a;
        }

        /// <summary>Projects 6 RGBA8 faces (size² × 4 bytes each, values read as linear 0..1).</summary>
        public static AmbientSH FromRgba8Faces(byte[][] faces, int size)
            => FromCube(size, (f, x, y) =>
            {
                int i = (y * size + x) * 4;
                return new Vector3(faces[f][i], faces[f][i + 1], faces[f][i + 2]) * (1f / 255f);
            });

        /// <summary>Irradiance / π for the unit normal <paramref name="n"/>.</summary>
        public Vector3 Evaluate(Vector3 n)
        {
            Span<float> r = stackalloc float[3];
            Span<float> nv = stackalloc float[] { n.X, n.Y, n.Z };
            for (int ch = 0; ch < 3; ch++)
            {
                float s = _c[ch] + Vector3.Dot(_v[ch], n);
                for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    s += _q[ch, i, j] * nv[i] * nv[j];
                r[ch] = s;
            }
            return new Vector3(r[0], r[1], r[2]);
        }

        /// <summary>
        /// The same environment seen through <paramref name="rotation"/> the way the PBR shader
        /// samples it: result.Evaluate(n) == Evaluate(Vector3.TransformNormal(n, rotation)).
        /// </summary>
        public AmbientSH Rotated(in Matrix4x4 rotation)
        {
            // TransformNormal(n, M) = A n with A[j, i] = M[i, j].
            Matrix4x4 m = rotation;
            float M(int i, int j) => i switch
            {
                0 => j == 0 ? m.M11 : j == 1 ? m.M12 : m.M13,
                1 => j == 0 ? m.M21 : j == 1 ? m.M22 : m.M23,
                _ => j == 0 ? m.M31 : j == 1 ? m.M32 : m.M33,
            };
            var r = new AmbientSH();
            for (int ch = 0; ch < 3; ch++)
            {
                r._c[ch] = _c[ch];
                Vector3 v = _v[ch];
                // v' = Aᵀ v  →  v'_a = Σ_j M[a, j] v_j
                r._v[ch] = new Vector3(
                    M(0, 0) * v.X + M(0, 1) * v.Y + M(0, 2) * v.Z,
                    M(1, 0) * v.X + M(1, 1) * v.Y + M(1, 2) * v.Z,
                    M(2, 0) * v.X + M(2, 1) * v.Y + M(2, 2) * v.Z);
                // Q' = Aᵀ Q A  →  Q'[a, b] = Σ_jk M[a, j] Q[j, k] M[b, k]
                for (int a = 0; a < 3; a++)
                for (int b = 0; b < 3; b++)
                {
                    float s = 0f;
                    for (int j = 0; j < 3; j++)
                    for (int k = 0; k < 3; k++)
                        s += M(a, j) * _q[ch, j, k] * M(b, k);
                    r._q[ch, a, b] = s;
                }
            }
            return r;
        }

        /// <summary>
        /// Writes the 9 vec4 the shaders read (per channel: (c, v), (Qxx, Qyy, Qzz, Qxy), (Qxz, Qyz, 0, 0)),
        /// every term multiplied by <paramref name="scale"/>.
        /// </summary>
        public void Pack(Span<Vector4> dst, float scale)
        {
            for (int ch = 0; ch < 3; ch++)
            {
                dst[ch * 3 + 0] = new Vector4(_c[ch], _v[ch].X, _v[ch].Y, _v[ch].Z) * scale;
                dst[ch * 3 + 1] = new Vector4(_q[ch, 0, 0], _q[ch, 1, 1], _q[ch, 2, 2], _q[ch, 0, 1]) * scale;
                dst[ch * 3 + 2] = new Vector4(_q[ch, 0, 2], _q[ch, 1, 2], 0f, 0f) * scale;
            }
        }

        private void SetQ(int ch, float xx, float yy, float zz, float xy, float yz, float xz)
        {
            _q[ch, 0, 0] = xx; _q[ch, 1, 1] = yy; _q[ch, 2, 2] = zz;
            _q[ch, 0, 1] = _q[ch, 1, 0] = xy;
            _q[ch, 1, 2] = _q[ch, 2, 1] = yz;
            _q[ch, 0, 2] = _q[ch, 2, 0] = xz;
        }

        // Real SH basis l ≤ 2, order: Y00, Y1-1 (y), Y10 (z), Y11 (x), Y2-2 (xy), Y2-1 (yz), Y20, Y21 (xz), Y22.
        private static void Basis(Vector3 d, Span<float> y)
        {
            y[0] = 0.282095f;
            y[1] = 0.488603f * d.Y;
            y[2] = 0.488603f * d.Z;
            y[3] = 0.488603f * d.X;
            y[4] = 1.092548f * d.X * d.Y;
            y[5] = 1.092548f * d.Y * d.Z;
            y[6] = 0.315392f * (3f * d.Z * d.Z - 1f);
            y[7] = 1.092548f * d.X * d.Z;
            y[8] = 0.546274f * (d.X * d.X - d.Y * d.Y);
        }
    }
}
