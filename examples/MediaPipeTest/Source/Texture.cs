using System;
using static Sokol.SG;
using static Sokol.StbImage;
using static Sokol.SBasisu;
using static Sokol.Utils;

namespace Sokol
{
    public class Texture2D : IDisposable
    {
        public sg_image Image { get; private set; }
        public sg_view View { get; private set; }
        public sg_sampler Sampler { get; private set; }
        public bool IsValid => Image.id != 0;
        
        // Properties for MediaPipe compatibility
        public int width { get; private set; }
        public int height { get; private set; }
        public sg_pixel_format format { get; private set; }
        
        private bool disposed;
        private string? _cacheKey; // Track the cache key for removal on dispose
        private SharedBuffer? _pixelDataBuffer; // Store pixel data for GetRawTextureData

        public unsafe Texture2D(void* pixels, int width, int height, string label, sg_pixel_format format, SamplerSettings? samplerSettings = null)
        {
            _cacheKey = label; // Use label as cache key
            this.width = width;
            this.height = height;
            this.format = format;
            
            samplerSettings ??= new SamplerSettings(); // Use defaults if null
            
            // Create image with mipmaps
            // Setting num_mipmaps = 0 tells Sokol to auto-calculate the mip count based on dimensions
            // and auto-generate the mipmap chain on the GPU
            var img_desc = new sg_image_desc
            {
                width = width,
                height = height,
                pixel_format = format,
                num_mipmaps = 0,  // 0 = auto-calculate and generate mipmaps
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
            ViewTracker.TrackViewCreation(View, $"{label}-view");

            // Create sampler with proper settings from glTF
            Sampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = samplerSettings.MinFilter,
                mag_filter = samplerSettings.MagFilter,
                mipmap_filter = samplerSettings.MipmapFilter,
                wrap_u = samplerSettings.WrapU,
                wrap_v = samplerSettings.WrapV,
                label = $"{label}-sampler"
            });
        }

        public static unsafe Texture2D? LoadFromMemory(byte[] data, string label, sg_pixel_format format = sg_pixel_format.SG_PIXELFORMAT_RGBA8, SamplerSettings? samplerSettings = null)
        {
            // Check if this is a basisu/basis file by checking magic bytes
            if (IsBasisImage(data))
            {
                return LoadFromMemoryBasisU(data, label, samplerSettings);
            }
            
            // Regular PNG/JPEG loading via stb_image
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

            var texture = new Texture2D(pixels, width, height, label, format, samplerSettings);
            stbi_image_free_csharp(pixels);
            return texture;
        }

        private static bool IsBasisImage(byte[] data)
        {
            // Basis Universal (.basis) magic bytes: "sB" (0x73, 0x42)
            // Reference: https://github.com/BinomialLLC/basis_universal
            if (data.Length < 2) return false;
            if (data[0] != 0x73) return false; // 's'
            if (data[1] != 0x42) return false; // 'B'
            return true;
        }

        private static unsafe Texture2D? LoadFromMemoryBasisU(byte[] data, string label, SamplerSettings? samplerSettings = null)
        {
            var texture = new Texture2D();
            texture._cacheKey = label;
            
            samplerSettings ??= new SamplerSettings(); // Use defaults if null
            
            // First transcode to get the image descriptor with dimensions and format
            var img_desc = sbasisu_transcode(SG_RANGE(data));
            texture.width = img_desc.width;
            texture.height = img_desc.height;
            texture.format = img_desc.pixel_format;
            
            // Create basisu image using sokol-basisu
            texture.Image = sbasisu_make_image(SG_RANGE(data));
            
            if (texture.Image.id == 0)
                return null;
                
            // Create view
            texture.View = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = texture.Image },
                label = $"{label}-view"
            });
            ViewTracker.TrackViewCreation(texture.View, $"{label}-view");
            
            // Create sampler with proper settings from glTF
            texture.Sampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = samplerSettings.MinFilter,
                mag_filter = samplerSettings.MagFilter,
                mipmap_filter = samplerSettings.MipmapFilter,
                wrap_u = samplerSettings.WrapU,
                wrap_v = samplerSettings.WrapV,
                label = $"{label}-sampler"
            });
            
            return texture;
        }        // Private parameterless constructor for basisu loading
        private Texture2D() { }

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
                
                // Dispose pixel data buffer if exists
                if (_pixelDataBuffer != null)
                {
                    SharedBuffer.Dispose(_pixelDataBuffer);
                    _pixelDataBuffer = null;
                }
                
                // Destroy sokol graphics resources
                if (Image.id != 0)
                {
                    ViewTracker.TrackViewDestruction(View);
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

        ~Texture2D()
        {
            Dispose(false);
        }
        
        /// <summary>
        /// Get raw texture data as SharedBuffer. Creates and caches the buffer on first call.
        /// The buffer will be managed by the Texture2D and disposed when the texture is disposed.
        /// </summary>
        public SharedBuffer GetRawTextureData<T>() where T : unmanaged
        {
            // If buffer already exists, return it
            if (_pixelDataBuffer != null)
                return _pixelDataBuffer;
            
            // Calculate buffer size (assuming RGBA format)
            uint bufferSize = (uint)(width * height * 4);
            _pixelDataBuffer = SharedBuffer.Create(bufferSize);
            
            // Note: In a real implementation, you would need to read back the pixel data from the GPU
            // This is a simplified version that just allocates the buffer
            // sg_read_image would be needed if Sokol supports it, or you'd need to keep original pixel data
            
            return _pixelDataBuffer;
        }
    }
}
