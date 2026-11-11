// TextureCache.cs — ref-counted cache of gpu image+view+sampler triples.
// Init() creates built-in placeholders (white, black, normal) so material
// slots always have a valid texture even before any asset loads.
// Not thread-safe: must be called from the main thread only (Sokol constraint).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sokol;
using static Sokol.SG;
using static Sokol.SG.sg_pixel_format;
using static Sokol.Utils;

namespace GameEditor.Framework.Renderer.Server.Resources
{
    public sealed unsafe class TextureCache : IDisposable
    {
        private readonly Dictionary<string, TextureResource> _map = new(StringComparer.Ordinal);
        private bool _disposed;

        // Well-known placeholder handles — always valid after Init().
        public sg_view PlaceholderWhite  { get; private set; }
        public sg_view PlaceholderBlack  { get; private set; }
        public sg_view PlaceholderNormal { get; private set; }
        // 1×1×6 black cubemap — bound to the PBR IBL env slots (binding 5/6) when no
        // environment map is loaded.  The base pbr variant statically references the
        // env cube samplers, so a valid cube view must always be bound even though
        // use_ibl=0 means it is never sampled.
        public sg_view PlaceholderCube   { get; private set; }
        public sg_sampler DefaultSampler { get; private set; }

        // ── lifecycle ────────────────────────────────────────────────────────────────

        public void Init()
        {
            DefaultSampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter  = sg_filter.SG_FILTER_LINEAR,
                mag_filter  = sg_filter.SG_FILTER_LINEAR,
                mipmap_filter = sg_filter.SG_FILTER_LINEAR,
                wrap_u      = sg_wrap.SG_WRAP_REPEAT,
                wrap_v      = sg_wrap.SG_WRAP_REPEAT,
                label       = "default-sampler"
            });

            PlaceholderWhite  = MakePlaceholder("builtin:white",  0xFF_FF_FF_FF);
            PlaceholderBlack  = MakePlaceholder("builtin:black",  0xFF_00_00_00);
            // Flat normal map: RGB = (128,128,255) pointing +Z in tangent space.
            PlaceholderNormal = MakePlaceholder("builtin:normal", 0xFF_FF_80_80);
            PlaceholderCube   = MakeCubePlaceholder("builtin:cube-black", 0xFF_00_00_00);
        }

        public void Shutdown()
        {
            foreach (var res in _map.Values) res.Destroy();
            _map.Clear();

            if (DefaultSampler.id != 0)
            {
                sg_destroy_sampler(DefaultSampler);
                DefaultSampler = default;
            }
        }

        // ── public API ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Decode image bytes (PNG/JPEG via stb_image) and upload to GPU.
        /// Returns the view handle. Increments ref-count on repeat calls for the same key.
        /// <paramref name="srgb"/> uploads as <c>SRGB8A8</c> so the GPU auto-linearises on
        /// sample — required for PBR base-colour and emissive maps (colour data); leave false
        /// for data maps (normal, metallic-roughness, occlusion).
        /// </summary>
        public sg_view LoadFromBytes(string key, ReadOnlySpan<byte> data, bool srgb = false)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                existing.RefCount++;
                return existing.View;
            }

            // Without sokol_gfx (a tool that runs with no window) there is nothing to upload to: no view.
            // The caller still records the texture's path on its material.
            if (!sg_isvalid()) return default;

            int w = 0, h = 0, ch = 0;
            byte* pixels = StbImage.stbi_load_csharp(
                in data[0], data.Length, ref w, ref h, ref ch, 4);

            if (pixels == null)
            {
                Console.Error.WriteLine($"[TextureCache] stbi_load failed for '{key}'");
                return PlaceholderWhite;
            }

            var pixelSpan = new ReadOnlySpan<byte>(pixels, w * h * 4);

            var imgDesc = new sg_image_desc
            {
                width        = w,
                height       = h,
                pixel_format = srgb ? SG_PIXELFORMAT_SRGB8A8 : SG_PIXELFORMAT_RGBA8,
                label        = key
            };
            imgDesc.data.mip_levels[0] = SG_RANGE(pixelSpan);

            sg_image img = sg_make_image(imgDesc);
            StbImage.stbi_image_free_csharp(pixels);

            sg_view view = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = img },
                label   = key
            });

            var res = new TextureResource
            {
                Image    = img,
                View     = view,
                Sampler  = DefaultSampler,
                RefCount = 1,
                Key      = key,
                Width    = w,
                Height   = h
            };
            _map[key] = res;
            return view;
        }

        /// <summary>The texture cached under <paramref name="key"/> (its view and its size), or null.</summary>
        public TextureResource? Find(string key) => _map.TryGetValue(key, out var r) ? r : null;

        /// <summary>Decrement ref-count. Destroys GPU resources when count reaches zero.</summary>
        public void Release(string key)
        {
            if (!_map.TryGetValue(key, out var res)) return;
            res.RefCount--;
            if (res.RefCount > 0) return;

            // Don't destroy built-ins that were added by MakePlaceholder.
            if (key.StartsWith("builtin:", StringComparison.Ordinal)) return;

            res.Destroy();
            _map.Remove(key);
        }

        /// <summary>Look up a view by cache key. Returns PlaceholderWhite if not found.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sg_view Get(string key)
            => _map.TryGetValue(key, out var r) ? r.View : PlaceholderWhite;

        // ── helpers ──────────────────────────────────────────────────────────────────

        private sg_view MakePlaceholder(string key, uint rgba8)
        {
            // Single 1×1 pixel: unpack RGBA bytes from the uint.
            byte r = (byte)(rgba8 & 0xFF);
            byte g = (byte)((rgba8 >> 8)  & 0xFF);
            byte b = (byte)((rgba8 >> 16) & 0xFF);
            byte a = (byte)((rgba8 >> 24) & 0xFF);
            byte* pixel = stackalloc byte[4] { r, g, b, a };

            var imgDesc = new sg_image_desc
            {
                width        = 1,
                height       = 1,
                pixel_format = SG_PIXELFORMAT_RGBA8,
                label        = key
            };
            imgDesc.data.mip_levels[0] = new sg_range
            {
                ptr  = pixel,
                size = 4
            };

            sg_image img  = sg_make_image(imgDesc);
            sg_view  view = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = img },
                label   = key
            });

            var res = new TextureResource
            {
                Image    = img,
                View     = view,
                Sampler  = DefaultSampler,
                RefCount = 1,
                Key      = key
            };
            _map[key] = res;
            return view;
        }

        // Single-texel cubemap placeholder.  All six faces share one colour.
        // sokol-gfx packs a cube mip level as the six faces concatenated in one range.
        private sg_view MakeCubePlaceholder(string key, uint rgba8)
        {
            byte r = (byte)(rgba8 & 0xFF);
            byte g = (byte)((rgba8 >> 8)  & 0xFF);
            byte b = (byte)((rgba8 >> 16) & 0xFF);
            byte a = (byte)((rgba8 >> 24) & 0xFF);

            byte* faces = stackalloc byte[6 * 4];
            for (int f = 0; f < 6; f++)
            {
                faces[f * 4 + 0] = r;
                faces[f * 4 + 1] = g;
                faces[f * 4 + 2] = b;
                faces[f * 4 + 3] = a;
            }

            var imgDesc = new sg_image_desc
            {
                type         = sg_image_type.SG_IMAGETYPE_CUBE,
                width        = 1,
                height       = 1,
                pixel_format = SG_PIXELFORMAT_RGBA8,
                label        = key
            };
            imgDesc.data.mip_levels[0] = new sg_range { ptr = faces, size = 6 * 4 };

            sg_image img  = sg_make_image(imgDesc);
            sg_view  view = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = img },
                label   = key
            });

            var res = new TextureResource
            {
                Image    = img,
                View     = view,
                Sampler  = DefaultSampler,
                RefCount = 1,
                Key      = key
            };
            _map[key] = res;
            return view;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Shutdown();
        }
    }
}
