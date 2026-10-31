// EnvironmentMap.cs — Image-Based Lighting (IBL) environment for the PBR forward path.
//
// Holds the three textures the base `pbr.glsl` variant samples when use_ibl > 0:
//   • DiffuseCube     — Lambertian irradiance cubemap   (u_LambertianEnvSampler, binding 6)
//   • SpecularCube    — GGX-prefiltered radiance cubemap (u_GGXEnvSampler,        binding 5; mipped)
//   • GgxLut          — split-sum BRDF lookup table      (u_GGXLUT,               binding 7)
//
// CreateProcedural() bakes all three on the CPU in pure C# (no native EXR / stb HDR),
// so it works on every target including WASM/WebGL2 — mirroring the cross-platform
// fallback in examples/CGltfViewer/Source/EnvironmentMapLoader.cs (CreateTestEnvironment).
// The procedural source is a neutral sky/ground gradient; HDR-panorama-sourced
// environments (desktop-only, native prefiltering) are a later increment that can
// reuse this same resource holder.
//
// Trimmed vs. the CGltfViewer reference: no sheen (Charlie) cube/LUT — the base PBR
// variant strips those bindings, so they are never sampled here.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Sokol;
using static Sokol.SG;
using static Sokol.SG.sg_pixel_format;
using GameEditor.Framework.Core;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    public sealed unsafe class EnvironmentMap : IDisposable
    {
        // Resolutions mirror the CGltfViewer procedural fallback.
        private const int DiffuseSize  = 64;   // low-res irradiance
        private const int SpecularSize = 128;  // base radiance; mips → roughness levels
        private const int LutSize      = 256;

        public sg_image DiffuseCube  { get; private set; }
        public sg_image SpecularCube { get; private set; }
        public sg_image GgxLut       { get; private set; }

        public sg_view DiffuseCubeView  { get; private set; }
        public sg_view SpecularCubeView { get; private set; }
        public sg_view GgxLutView       { get; private set; }

        public sg_sampler CubeSampler { get; private set; }
        public sg_sampler LutSampler  { get; private set; }

        public int       MipCount  { get; private set; }
        public float     Intensity { get; set; } = 1f;
        public Matrix4x4 Rotation  { get; set; } = Matrix4x4.Identity;
        public string    Name      { get; private set; } = "";

        /// <summary>Diffuse ambient (irradiance / π) of this sky in its own frame, for materials without IBL.</summary>
        public AmbientSH? Ambient { get; private set; }

        public bool IsLoaded => DiffuseCube.id != 0 && SpecularCube.id != 0 && GgxLut.id != 0;

        // ── Factory ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bakes a neutral procedural IBL environment (diffuse irradiance cube,
        /// GGX-prefiltered specular cube with mips, split-sum BRDF LUT). Pure C# —
        /// no native HDR decode — so it is valid on all six target platforms.
        /// </summary>
        public static EnvironmentMap CreateProcedural(string name = "default-ibl")
        {
            var env = new EnvironmentMap { Name = name };
            // RGBA8 stores 0..1; lift into HDR range so metal reflections read bright
            // (the shader multiplies sampled radiance/irradiance by EnvIntensity).
            env.Intensity = 2.2f;
            env.DiffuseCube  = BakeDiffuseCube(name);
            // From the sharp sky (blur 0), like a cubemap's faces: the SH projection does the cosine
            // convolution itself; the pre-blurred diffuse cube would flatten the up/down contrast twice.
            env.Ambient      = AmbientSH.FromCube(DiffuseSize, (f, x, y) =>
                SampleSky(CubeDirection(f, (x + 0.5f) / DiffuseSize, (y + 0.5f) / DiffuseSize), 0f));
            (env.SpecularCube, env.MipCount) = BakeSpecularCube(name);
            env.GgxLut       = BakeGgxLut(name);
            env.CreateViewsAndSamplers();
            Logger.Info(
                $"[IBL] Procedural env '{name}' baked: diffuse {DiffuseSize}², " +
                $"specular {SpecularSize}²×{env.MipCount} mips, lut {LutSize}². " +
                $"IsLoaded={env.IsLoaded} (diffuse.id={env.DiffuseCube.id}, " +
                $"specular.id={env.SpecularCube.id}, lut.id={env.GgxLut.id})");
            return env;
        }

        /// <summary>
        /// Builds an environment from 6 pre-baked cubemap face images (RGBA8, all <paramref
        /// name="faceSize"/>², order +X,-X,+Y,-Y,+Z,-Z). The faces become the diffuse cube
        /// directly and a box-filter-mipped specular cube; the BRDF LUT is procedural.
        /// Mirrors CGltfViewer's <c>CreateCubemapEnvironment</c> — real sky content, so
        /// metals reflect a recognisable environment. Pure GPU upload — cross-platform.
        /// </summary>
        public static EnvironmentMap CreateFromFaces(byte[][] faces, int faceSize, string name = "skybox-ibl")
        {
            var env = new EnvironmentMap { Name = name, Intensity = 1.0f };
            env.DiffuseCube  = BuildCubeFromFaces(faces, faceSize, $"ibl-{name}-diffuse");
            env.Ambient      = AmbientSH.FromRgba8Faces(faces, faceSize);
            (env.SpecularCube, env.MipCount) = BuildMippedCubeFromFaces(faces, faceSize, $"ibl-{name}-specular");
            env.GgxLut       = BakeGgxLut(name);
            env.CreateViewsAndSamplers();
            Logger.Info(
                $"[IBL] Skybox env '{name}' built from 6×{faceSize}² faces: " +
                $"specular {faceSize}²×{env.MipCount} mips. IsLoaded={env.IsLoaded}");
            return env;
        }

        // Single-mip cube: concatenate the 6 RGBA8 faces into one upload.
        private static sg_image BuildCubeFromFaces(byte[][] faces, int faceSize, string label)
        {
            int   faceBytes = faceSize * faceSize * 4;
            nuint total     = (nuint)(faceBytes * 6);
            byte* buf       = (byte*)NativeMemory.Alloc(total);
            try
            {
                for (int f = 0; f < 6; f++)
                    fixed (byte* src = faces[f])
                        Buffer.MemoryCopy(src, buf + f * faceBytes, faceBytes, faceBytes);

                var desc = new sg_image_desc
                {
                    type         = sg_image_type.SG_IMAGETYPE_CUBE,
                    width        = faceSize,
                    height       = faceSize,
                    num_slices   = 6,
                    num_mipmaps  = 1,
                    pixel_format = SG_PIXELFORMAT_RGBA8,
                    label        = label
                };
                desc.data.mip_levels[0] = new sg_range { ptr = buf, size = total };
                return sg_make_image(desc);
            }
            finally { NativeMemory.Free(buf); }
        }

        // Mipped cube: mip 0 = the faces; higher mips = 2×2 box-filter downsamples
        // (a cheap stand-in for GGX roughness prefiltering, as in the reference).
        private static (sg_image, int) BuildMippedCubeFromFaces(byte[][] faces, int baseSize, string label)
        {
            int mipCount = Math.Min((int)Math.Floor(Math.Log2(baseSize)) + 1, 8);

            nuint total = 0;
            for (int mip = 0; mip < mipCount; mip++)
            {
                int s = Math.Max(1, baseSize >> mip);
                total += (nuint)(s * s * 4 * 6);
            }

            byte* buf = (byte*)NativeMemory.Alloc(total);
            try
            {
                var desc = new sg_image_desc
                {
                    type         = sg_image_type.SG_IMAGETYPE_CUBE,
                    width        = baseSize,
                    height       = baseSize,
                    num_slices   = 6,
                    num_mipmaps  = mipCount,
                    pixel_format = SG_PIXELFORMAT_RGBA8,
                    label        = label
                };

                // mip 0 — copy the source faces.
                int faceBytes0 = baseSize * baseSize * 4;
                for (int f = 0; f < 6; f++)
                    fixed (byte* src = faces[f])
                        Buffer.MemoryCopy(src, buf + f * faceBytes0, faceBytes0, faceBytes0);
                desc.data.mip_levels[0] = new sg_range { ptr = buf, size = (nuint)(faceBytes0 * 6) };

                // higher mips — box-filter the previous mip per face.
                nuint prevOffset = 0;
                nuint offset     = (nuint)(faceBytes0 * 6);
                for (int mip = 1; mip < mipCount; mip++)
                {
                    int prevSize  = Math.Max(1, baseSize >> (mip - 1));
                    int s         = Math.Max(1, baseSize >> mip);
                    int faceBytes = s * s * 4;
                    int prevFace  = prevSize * prevSize * 4;
                    for (int f = 0; f < 6; f++)
                        DownsampleFace(buf + prevOffset + (nuint)(f * prevFace), prevSize,
                                       buf + offset     + (nuint)(f * faceBytes), s);

                    desc.data.mip_levels[mip] = new sg_range { ptr = buf + offset, size = (nuint)(faceBytes * 6) };
                    prevOffset = offset;
                    offset    += (nuint)(faceBytes * 6);
                }
                return (sg_make_image(desc), mipCount);
            }
            finally { NativeMemory.Free(buf); }
        }

        // 2×2 average box filter, RGBA8.
        private static void DownsampleFace(byte* src, int srcSize, byte* dst, int dstSize)
        {
            for (int y = 0; y < dstSize; y++)
            for (int x = 0; x < dstSize; x++)
            {
                int sx = x * 2, sy = y * 2;
                int r = 0, g = 0, b = 0, a = 0, n = 0;
                for (int dy = 0; dy < 2 && sy + dy < srcSize; dy++)
                for (int dx = 0; dx < 2 && sx + dx < srcSize; dx++)
                {
                    int si = ((sy + dy) * srcSize + (sx + dx)) * 4;
                    r += src[si + 0]; g += src[si + 1]; b += src[si + 2]; a += src[si + 3]; n++;
                }
                int di = (y * dstSize + x) * 4;
                dst[di + 0] = (byte)(r / n);
                dst[di + 1] = (byte)(g / n);
                dst[di + 2] = (byte)(b / n);
                dst[di + 3] = (byte)(a / n);
            }
        }

        private void CreateViewsAndSamplers()
        {
            DiffuseCubeView = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = DiffuseCube },
                label   = $"{Name}-diffuse-view"
            });
            SpecularCubeView = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = SpecularCube },
                label   = $"{Name}-specular-view"
            });
            GgxLutView = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = GgxLut },
                label   = $"{Name}-ggx-lut-view"
            });

            // Cubemaps: trilinear so the GGX mip chain blends smoothly across roughness.
            CubeSampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter    = sg_filter.SG_FILTER_LINEAR,
                mag_filter    = sg_filter.SG_FILTER_LINEAR,
                mipmap_filter = sg_filter.SG_FILTER_LINEAR,
                wrap_u        = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_v        = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_w        = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                label         = $"{Name}-cube-sampler"
            });
            // LUT: bilinear, single mip (NEAREST mip filter).
            LutSampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter    = sg_filter.SG_FILTER_LINEAR,
                mag_filter    = sg_filter.SG_FILTER_LINEAR,
                mipmap_filter = sg_filter.SG_FILTER_NEAREST,
                wrap_u        = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_v        = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                label         = $"{Name}-lut-sampler"
            });
        }

        // ── Baking (CPU, init-time only) ─────────────────────────────────────────────

        // Single-mip irradiance cube: one neutral gradient per face.
        private static sg_image BakeDiffuseCube(string name)
        {
            int  s         = DiffuseSize;
            int  faceBytes = s * s * 4;
            nuint total    = (nuint)(faceBytes * 6);

            byte* buf = (byte*)NativeMemory.Alloc(total);
            try
            {
                // Irradiance ≈ broad average of the sky → bake with full blur (no sharp sun).
                for (int face = 0; face < 6; face++)
                    FillFace(buf + face * faceBytes, s, face, blur: 1.0f);

                var desc = new sg_image_desc
                {
                    type         = sg_image_type.SG_IMAGETYPE_CUBE,
                    width        = s,
                    height       = s,
                    num_slices   = 6,
                    num_mipmaps  = 1,
                    pixel_format = SG_PIXELFORMAT_RGBA8,
                    label        = $"ibl-{name}-diffuse"
                };
                desc.data.mip_levels[0] = new sg_range { ptr = buf, size = total };
                return sg_make_image(desc);
            }
            finally { NativeMemory.Free(buf); }
        }

        // Mipped radiance cube: mip 0 = sharp, higher mips = progressively blurred
        // (stand-in for increasing GGX roughness). One contiguous native block holds
        // the whole mip chain so every range stays valid until sg_make_image copies.
        private static (sg_image, int) BakeSpecularCube(string name)
        {
            int baseSize = SpecularSize;
            int mipCount = Math.Min((int)Math.Floor(Math.Log2(baseSize)) + 1, 8);

            nuint total = 0;
            for (int mip = 0; mip < mipCount; mip++)
            {
                int s = Math.Max(1, baseSize >> mip);
                total += (nuint)(s * s * 4 * 6);
            }

            byte* buf = (byte*)NativeMemory.Alloc(total);
            try
            {
                var desc = new sg_image_desc
                {
                    type         = sg_image_type.SG_IMAGETYPE_CUBE,
                    width        = baseSize,
                    height       = baseSize,
                    num_slices   = 6,
                    num_mipmaps  = mipCount,
                    pixel_format = SG_PIXELFORMAT_RGBA8,
                    label        = $"ibl-{name}-specular"
                };

                nuint offset = 0;
                for (int mip = 0; mip < mipCount; mip++)
                {
                    int   s         = Math.Max(1, baseSize >> mip);
                    int   faceBytes = s * s * 4;
                    // mip 0 = mirror-sharp (sun is a tight disc); higher mips = rougher.
                    float blur      = mipCount > 1 ? mip / (float)(mipCount - 1) : 0f;
                    for (int face = 0; face < 6; face++)
                        FillFace(buf + offset + (nuint)(face * faceBytes), s, face, blur);

                    desc.data.mip_levels[mip] = new sg_range
                    {
                        ptr  = buf + offset,
                        size = (nuint)(faceBytes * 6)
                    };
                    offset += (nuint)(faceBytes * 6);
                }
                return (sg_make_image(desc), mipCount);
            }
            finally { NativeMemory.Free(buf); }
        }

        // Split-sum BRDF integration LUT (analytic approximation; X=NdotV, Y=roughness,
        // R=scale, G=bias). Faithful port of CGltfViewer's CreateBRDFLUT for parity.
        private static sg_image BakeGgxLut(string name)
        {
            int   s     = LutSize;
            nuint total = (nuint)(s * s * 4);
            byte* buf   = (byte*)NativeMemory.Alloc(total);
            try
            {
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    int   idx       = (y * s + x) * 4;
                    float NdotV     = x / (float)(s - 1);
                    float roughness = y / (float)(s - 1);

                    float scale = 1.0f - roughness * (1.0f - NdotV);
                    float bias  = roughness * (1.0f - NdotV) * 0.5f;

                    buf[idx + 0] = (byte)Math.Clamp(scale * 255f, 0f, 255f);
                    buf[idx + 1] = (byte)Math.Clamp(bias  * 255f, 0f, 255f);
                    buf[idx + 2] = 0;
                    buf[idx + 3] = 255;
                }

                var desc = new sg_image_desc
                {
                    type         = sg_image_type.SG_IMAGETYPE_2D,
                    width        = s,
                    height       = s,
                    pixel_format = SG_PIXELFORMAT_RGBA8,
                    label        = $"ibl-{name}-ggx-lut"
                };
                desc.data.mip_levels[0] = new sg_range { ptr = buf, size = total };
                return sg_make_image(desc);
            }
            finally { NativeMemory.Free(buf); }
        }

        // Sun direction (world space) — high and to one side so reflections read clearly.
        private static readonly Vector3 SunDir = Vector3.Normalize(new Vector3(0.5f, 0.7f, 0.35f));

        // Bakes one cube face by sampling a direction-based sky per texel. `blur` (0=sharp
        // mip0 … 1=roughest) broadens the sun disc and flattens contrast, standing in for
        // GGX prefiltering. Unlike the old face-index gradient, this gives metals a real,
        // recognisable reflection: a bright sun highlight over a sky→ground gradient.
        private static void FillFace(byte* data, int size, int face, float blur)
        {
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u   = (x + 0.5f) / size;
                float v   = (y + 0.5f) / size;
                Vector3 d = CubeDirection(face, u, v);
                Vector3 c = SampleSky(d, blur);

                int idx = (y * size + x) * 4;
                data[idx + 0] = (byte)Math.Clamp(c.X * 255f, 0f, 255f);
                data[idx + 1] = (byte)Math.Clamp(c.Y * 255f, 0f, 255f);
                data[idx + 2] = (byte)Math.Clamp(c.Z * 255f, 0f, 255f);
                data[idx + 3] = 255;
            }
        }

        // Analytic sky: blue zenith → near-white horizon → dark ground, plus a sun
        // highlight whose tightness/intensity falls off with `blur` (roughness).
        // Stored RGBA8 (0..1); the EnvIntensity multiplier in the shader lifts it into
        // HDR range so reflections read bright through dark metal F0.
        private static Vector3 SampleSky(Vector3 dir, float blur)
        {
            var zenith  = new Vector3(0.30f, 0.55f, 1.00f);
            var horizon = new Vector3(0.92f, 0.96f, 1.00f);
            var ground  = new Vector3(0.12f, 0.10f, 0.08f);

            float up = dir.Y;
            Vector3 col;
            if (up >= 0f)
                col = Vector3.Lerp(horizon, zenith, MathF.Pow(up, 0.5f));   // sky
            else
                col = Vector3.Lerp(horizon, ground, MathF.Min(-up * 2.5f, 1f)); // ground

            // Sun: tight bright disc at low roughness, broad soft glow at high roughness.
            float sunDot    = MathF.Max(Vector3.Dot(dir, SunDir), 0f);
            float sharpness = float.Lerp(900f, 12f, blur);
            float sun       = MathF.Pow(sunDot, sharpness);
            col += new Vector3(1.0f, 0.97f, 0.90f) * sun * (1f - 0.4f * blur);

            // Flatten contrast toward the mid sky tone as roughness rises.
            float contrast = 1f - 0.5f * blur;
            var   mid      = new Vector3(0.55f, 0.62f, 0.72f);
            col = mid + (col - mid) * contrast;

            return Vector3.Clamp(col, Vector3.Zero, Vector3.One);
        }

        // Maps a cube face + face-local UV to a world-space direction
        // (sokol cube convention; ported from the CGltfViewer reference).
        internal static Vector3 CubeDirection(int face, float u, float v)
        {
            float uc = 2f * u - 1f;
            float vc = 2f * v - 1f;
            return face switch
            {
                0 => Vector3.Normalize(new Vector3( 1f, -vc, -uc)), // +X
                1 => Vector3.Normalize(new Vector3(-1f, -vc,  uc)), // -X
                2 => Vector3.Normalize(new Vector3( uc,  1f,  vc)), // +Y
                3 => Vector3.Normalize(new Vector3( uc, -1f, -vc)), // -Y
                4 => Vector3.Normalize(new Vector3( uc, -vc,  1f)), // +Z
                _ => Vector3.Normalize(new Vector3(-uc, -vc, -1f)), // -Z
            };
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (DiffuseCubeView.id  != 0) { sg_destroy_view(DiffuseCubeView);  DiffuseCubeView  = default; }
            if (SpecularCubeView.id != 0) { sg_destroy_view(SpecularCubeView); SpecularCubeView = default; }
            if (GgxLutView.id       != 0) { sg_destroy_view(GgxLutView);       GgxLutView       = default; }
            if (DiffuseCube.id      != 0) { sg_destroy_image(DiffuseCube);     DiffuseCube      = default; }
            if (SpecularCube.id     != 0) { sg_destroy_image(SpecularCube);    SpecularCube     = default; }
            if (GgxLut.id           != 0) { sg_destroy_image(GgxLut);          GgxLut           = default; }
            if (CubeSampler.id      != 0) { sg_destroy_sampler(CubeSampler);   CubeSampler      = default; }
            if (LutSampler.id       != 0) { sg_destroy_sampler(LutSampler);    LutSampler       = default; }
        }
    }
}
