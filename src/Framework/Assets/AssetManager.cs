using System.Collections.Generic;

namespace GameEditor.Framework.Assets
{
    public static class AssetManager
    {
        private static readonly Dictionary<string, object> _cache = new();

        public static void Register(string path, object asset)
        {
            _cache[path] = asset;
        }

        public static bool TryGet<T>(string path, out T asset) where T : class
        {
            if (_cache.TryGetValue(path, out var raw) && raw is T typed)
            {
                asset = typed;
                return true;
            }
            asset = null!;
            return false;
        }

        public static void Unload(string path)
        {
            _cache.Remove(path);
        }

        public static void Clear() => _cache.Clear();
    }
}
