using System;
using System.Collections.Generic;
using Sokol;

/// <summary>
/// Texture cache to avoid loading and creating duplicate textures.
/// Maintains a dictionary of textures keyed by their file path or embedded texture identifier.
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
    /// Get or create a texture from file path.
    /// </summary>
    public Texture GetOrCreate(string filePath)
    {
        if (_cache.TryGetValue(filePath, out var existingTexture))
        {
            _cacheHits++;
            // Console.WriteLine($"TextureCache: Cache HIT for '{filePath}' (Total hits: {_cacheHits}, misses: {_cacheMisses})");
            return existingTexture;
        }

        _cacheMisses++;
        // Console.WriteLine($"TextureCache: Cache MISS for '{filePath}' - creating new texture (Total hits: {_cacheHits}, misses: {_cacheMisses})");
        
        var texture = new Texture(filePath);
        _cache[filePath] = texture;
        return texture;
    }

    /// <summary>
    /// Get or create a texture from raw data (for embedded textures).
    /// </summary>
    public unsafe Texture GetOrCreate(string identifier, byte* data, int width, int height)
    {
        if (_cache.TryGetValue(identifier, out var existingTexture))
        {
            _cacheHits++;
            // Console.WriteLine($"TextureCache: Cache HIT for embedded '{identifier}' (Total hits: {_cacheHits}, misses: {_cacheMisses})");
            return existingTexture;
        }

        _cacheMisses++;
        // Console.WriteLine($"TextureCache: Cache MISS for embedded '{identifier}' - creating new texture (Total hits: {_cacheHits}, misses: {_cacheMisses})");
        
        var texture = new Texture(data, width, height, identifier);
        _cache[identifier] = texture;
        return texture;
    }

    /// <summary>
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
    /// </summary>
    public void Remove(string key)
    {
        _cache.Remove(key);
    }

    /// <summary>
    /// Clear the cache and destroy all textures.
    /// </summary>
    public void Clear()
    {
        Console.WriteLine($"TextureCache: Clearing cache with {_cache.Count} textures");
        
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
        
        Console.WriteLine($"TextureCache Stats:");
        Console.WriteLine($"  Unique Textures: {_cache.Count}");
        Console.WriteLine($"  Cache Hits: {_cacheHits}");
        Console.WriteLine($"  Cache Misses: {_cacheMisses}");
        Console.WriteLine($"  Hit Rate: {hitRate:F1}%");
    }
}
