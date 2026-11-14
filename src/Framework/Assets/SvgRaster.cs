// SvgRaster.cs — G8.4 D2 (examples/GameEditor/docs/TEXT3D_SVG_DRAW_DESIGN.md §5b): an .svg file is a picture wherever a
// PNG is. It is rasterized when it is loaded, with NanoSVG (in the sokol native library of every target), at its own
// pixel size times a scale — the reference editor and Godot rasterize at import; the engine has no import cache yet.

using System;
using static Sokol.NanoSVG;

namespace GameEditor.Framework.Assets
{
    public static unsafe class SvgRaster
    {
        private static IntPtr _rasterizer;

        /// <summary>The bytes are an SVG document (they start, after white space, with "&lt;svg" or an XML header followed by one).</summary>
        public static bool IsSvg(ReadOnlySpan<byte> data)
        {
            int i = 0;
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) i = 3;   // UTF-8 BOM
            while (i < data.Length && (data[i] == ' ' || data[i] == '\t' || data[i] == '\r' || data[i] == '\n')) i++;
            if (i + 4 > data.Length || data[i] != '<') return false;
            if (data[i + 1] == 's' && data[i + 2] == 'v' && data[i + 3] == 'g') return true;
            if (data[i + 1] != '?' && data[i + 1] != '!') return false;
            // an XML header or a comment / doctype first: an <svg element within the first 2 KB
            var head = data.Slice(i, Math.Min(2048, data.Length - i));
            return head.IndexOf("<svg"u8) >= 0;
        }

        /// <summary>
        /// The SVG drawn into RGBA pixels (straight alpha, as a decoded PNG's) at its own size × <paramref name="scale"/>;
        /// null when it cannot be read.
        /// </summary>
        public static byte[]? Rasterize(ReadOnlySpan<byte> svg, float scale, out int width, out int height)
        {
            width = height = 0;
            if (_rasterizer == IntPtr.Zero) _rasterizer = nsvgCreateRasterizer();
            if (_rasterizer == IntPtr.Zero) return null;
            byte[] src = Sokol.Render2D.Particles.SvgPreprocess.Apply(svg.ToArray());   // inline CSS classes, strip clip paths (NanoSVG's limits)
            byte[] buf = new byte[src.Length + 1];                                 // nsvgParse writes into it and needs its end
            Array.Copy(src, buf, src.Length);
            fixed (byte* p = buf)
            {
                NSVGimage* image = nsvgParse((IntPtr)p, "px", 96.0f);
                if (image == null || image->width <= 0 || image->height <= 0)
                {
                    if (image != null) nsvgDelete(image);
                    return null;
                }
                scale = MathF.Max(0.01f, scale);
                width = Math.Clamp((int)MathF.Ceiling(image->width * scale), 1, 8192);
                height = Math.Clamp((int)MathF.Ceiling(image->height * scale), 1, 8192);
                var pixels = new byte[width * height * 4];
                fixed (byte* dst = pixels)
                    nsvgRasterize(_rasterizer, image, 0, 0, scale, dst, width, height, width * 4);
                nsvgDelete(image);
                return pixels;
            }
        }
    }
}
