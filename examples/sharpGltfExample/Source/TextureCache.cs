using System;
using System.Collections.Generic;
using static Sokol.SG;

namespace Sokol
{
    /// <summary>
    /// Texture cache to avoid loading and creating duplicate textures.
    /// Maintains a dictionary of textures keyed by their identifier (e.g., texture path or embedded texture key).
    /// </summary>
    public class TextureCache
    {
        private static TextureCache? _instance;
        public static TextureCache Instance => _instance ??= new TextureCache();

        private readonly Dictionary<string, Texture> _cache = new Dictionary<string, Texture>();
        private int _cacheHits = 0;
        private int _cacheMisses = 0;

        private TextureCache() { }

        /// <summary>
        /// Get or create a texture from raw data.
        /// </summary>
        public unsafe Texture GetOrCreate(string identifier, byte* data, int width, int height, sg_pixel_format format = sg_pixel_format.SG_PIXELFORMAT_RGBA8)
        {
            // Include format in cache key to handle different formats of same texture
            string cacheKey = $"{identifier}_{format}";
            
            if (_cache.TryGetValue(cacheKey, out var existingTexture))
            {
                _cacheHits++;
                Console.WriteLine($"[TextureCache] Cache HIT for '{identifier}' (format: {format}) (Total hits: {_cacheHits}, misses: {_cacheMisses})");
                return existingTexture;
            }

            _cacheMisses++;
            Console.WriteLine($"[TextureCache] Cache MISS for '{identifier}' (format: {format}) - creating new texture (Total hits: {_cacheHits}, misses: {_cacheMisses})");
            
            var texture = new Texture(data, width, height, identifier, format);
            _cache[cacheKey] = texture;
            return texture;
        }

        /// <summary>
        /// Get or create a texture from memory data.
        /// </summary>
        public Texture? GetOrCreate(string identifier, byte[] data, sg_pixel_format format = sg_pixel_format.SG_PIXELFORMAT_RGBA8)
        {
            // Include format in cache key to handle different formats of same texture
            string cacheKey = $"{identifier}_{format}";
            
            if (_cache.TryGetValue(cacheKey, out var existingTexture))
            {
                _cacheHits++;
                Console.WriteLine($"[TextureCache] Cache HIT for '{identifier}' (format: {format}) (Total hits: {_cacheHits}, misses: {_cacheMisses})");
                return existingTexture;
            }

            _cacheMisses++;
            Console.WriteLine($"[TextureCache] Cache MISS for '{identifier}' (format: {format}) - creating new texture (Total hits: {_cacheHits}, misses: {_cacheMisses})");

            var texture = Texture.LoadFromMemory(data, identifier, format);
            if (texture != null)
            {
                _cache[cacheKey] = texture;
            }
            return texture;
        }        /// <summary>
        /// Check if a texture is already cached.
        /// </summary>
        public bool Contains(string key)
        {
            return _cache.ContainsKey(key);
        }

        /// <summary>
        /// Get cache statistics.
        /// </summary>
        public (int hits, int misses, int total) GetStats()
        {
            return (_cacheHits, _cacheMisses, _cache.Count);
        }

        /// <summary>
        /// Remove a texture from the cache (typically called when the texture is disposed).
        /// returns true if the texture was found and removed, false otherwise.
        /// </summary>
        public bool Remove(string key)
        {
            return _cache.Remove(key);
        }

        /// <summary>
        /// Clear the cache and destroy all textures.
        /// </summary>
        public void Clear()
        {
            Console.WriteLine($"[TextureCache] Clearing cache with {_cache.Count} textures");
            
            // Dispose all textures
            foreach (var texture in _cache.Values)
            {
                texture.Dispose();
            }
            
            _cache.Clear();
            _cacheHits = 0;
            _cacheMisses = 0;
        }

        /// <summary>
        /// Print cache statistics.
        /// </summary>
        public void PrintStats()
        {
            var hitRate = _cacheHits + _cacheMisses > 0 
                ? (_cacheHits * 100.0 / (_cacheHits + _cacheMisses)) 
                : 0.0;
            
            Console.WriteLine($"[TextureCache] Stats:");
            Console.WriteLine($"  Unique Textures: {_cache.Count}");
            Console.WriteLine($"  Cache Hits: {_cacheHits}");
            Console.WriteLine($"  Cache Misses: {_cacheMisses}");
            Console.WriteLine($"  Hit Rate: {hitRate:F1}%");
        }
    }
}
