using System;
using static Sokol.SG;
using static Sokol.StbImage;

namespace Sokol
{
    public class Texture : IDisposable
    {
        public sg_image Image { get; private set; }
        public sg_view View { get; private set; }
        public sg_sampler Sampler { get; private set; }
        public bool IsValid => Image.id != 0;
        
        private bool disposed;
        private string? _cacheKey; // Track the cache key for removal on dispose

        public unsafe Texture(byte* pixels, int width, int height, string label)
        {
            _cacheKey = label; // Use label as cache key
            
            // Create image
            var img_desc = new sg_image_desc
            {
                width = width,
                height = height,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                data = { mip_levels = { [0] = new sg_range { ptr = pixels, size = (nuint)(width * height * 4) } } },
                label = label
            };
            Image = sg_make_image(img_desc);

            // Create view
            View = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = Image },
                label = $"{label}-view"
            });

            // Create sampler
            Sampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = sg_filter.SG_FILTER_LINEAR,
                mag_filter = sg_filter.SG_FILTER_LINEAR,
                wrap_u = sg_wrap.SG_WRAP_REPEAT,
                wrap_v = sg_wrap.SG_WRAP_REPEAT,
                label = $"{label}-sampler"
            });
        }

        public static unsafe Texture? LoadFromMemory(byte[] data, string label)
        {
            int width = 0, height = 0, channels = 0;
            byte* pixels = stbi_load_csharp(
                in data[0],
                data.Length,
                ref width,
                ref height,
                ref channels,
                4 // desired_channels (RGBA)
            );

            if (pixels == null)
                return null;

            var texture = new Texture(pixels, width, height, label);
            stbi_image_free_csharp(pixels);
            return texture;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                // Remove from cache if we have a cache key
                if (_cacheKey != null)
                {
                    TextureCache.Instance.Remove(_cacheKey);
                }
                
                // Destroy sokol graphics resources
                if (Image.id != 0)
                {
                    sg_destroy_sampler(Sampler);
                    sg_destroy_view(View);
                    sg_destroy_image(Image);
                    Image = default;
                    View = default;
                    Sampler = default;
                }
                
                disposed = true;
            }
        }

        ~Texture()
        {
            Dispose(false);
        }
    }
}
