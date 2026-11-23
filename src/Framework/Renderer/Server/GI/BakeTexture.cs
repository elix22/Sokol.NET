using System;
using System.Numerics;
using GameEditor.Framework.Renderer.Server.Lighting;

namespace GameEditor.Framework.Renderer.Server.GI
{
    /// <summary>
    /// A material's texture as the light bake sees it (BAKED_GI_DESIGN.md §3.8): linear RGB at most <see cref="MaxSize"/>
    /// texels a side (each the mean of the source texels it covers: a bounce needs the colour of an area, not its detail),
    /// sampled at a TEXCOORD_0 bilinearly with the UVs repeating - on the CPU here and, the same arithmetic, in the editor's
    /// GPU tracer (shaders/lightbake.glsl map_at).
    /// </summary>
    public sealed class BakeTexture
    {
        public const int MaxSize = 256;

        public readonly int Width, Height;
        /// <summary>Row-major from the image's top row (glTF's UV (0, 0)).</summary>
        public readonly Vector3[] Linear;

        public BakeTexture(int width, int height, Vector3[] linear)
        {
            Width = width; Height = height; Linear = linear;
        }

        /// <summary>An RGBA8 image (rows from the top), its colour sRGB-encoded when <paramref name="srgb"/>, reduced.</summary>
        public static unsafe BakeTexture FromRgba8(byte* px, int width, int height, bool srgb)
        {
            int w = Math.Min(width, MaxSize), h = Math.Min(height, MaxSize);
            var lin = new Vector3[w * h];
            for (int y = 0; y < h; y++)
            {
                int y0 = (int)((long)y * height / h), y1 = Math.Max(y0 + 1, (int)((long)(y + 1) * height / h));
                for (int x = 0; x < w; x++)
                {
                    int x0 = (int)((long)x * width / w), x1 = Math.Max(x0 + 1, (int)((long)(x + 1) * width / w));
                    var sum = Vector3.Zero;
                    for (int sy = y0; sy < y1; sy++)
                        for (int sx = x0; sx < x1; sx++)
                        {
                            byte* p = px + ((long)sy * width + sx) * 4;
                            sum += srgb ? new Vector3(AmbientSH.SrgbToLinear(p[0] / 255f), AmbientSH.SrgbToLinear(p[1] / 255f), AmbientSH.SrgbToLinear(p[2] / 255f))
                                        : new Vector3(p[0], p[1], p[2]) / 255f;
                        }
                    lin[y * w + x] = sum / ((x1 - x0) * (y1 - y0));
                }
            }
            return new BakeTexture(w, h, lin);
        }

        /// <summary>The colour at <paramref name="uv"/>: bilinear between texel centres, the UVs repeating.</summary>
        public Vector3 Sample(Vector2 uv)
        {
            float fx = uv.X * Width - 0.5f, fy = uv.Y * Height - 0.5f;
            float x0f = MathF.Floor(fx), y0f = MathF.Floor(fy);
            float ax = fx - x0f, ay = fy - y0f;
            int x0 = (int)x0f, y0 = (int)y0f;
            int xa = Wrap(x0, Width), xb = Wrap(x0 + 1, Width), ya = Wrap(y0, Height), yb = Wrap(y0 + 1, Height);
            var top = Vector3.Lerp(Linear[ya * Width + xa], Linear[ya * Width + xb], ax);
            var bottom = Vector3.Lerp(Linear[yb * Width + xa], Linear[yb * Width + xb], ax);
            return Vector3.Lerp(top, bottom, ay);
        }

        private static int Wrap(int i, int n) => (i % n + n) % n;
    }
}
