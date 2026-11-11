using System;
using System.Collections.Generic;
using System.Text.Json;
using GameEditor.Framework.Core;
using Sokol;

namespace GameEditor.Framework.Assets
{
    /// <summary>
    /// The tile maps that were read, by their keys ("Tilemaps/level1.tilemap"). A Tilemap component names a map;
    /// the renderer, the 2D physics and an editor ask here for it. Main thread only.
    /// </summary>
    public static class TilemapLibrary
    {
        /// <summary>
        /// Reads a map file's text by key; null when there is none. When set, it alone is asked (a test, a tool);
        /// otherwise the open project's Assets folder, and without a project the app's files as they arrive.
        /// </summary>
        public static Func<string, string?>? Reader { get; set; }

        private static readonly Dictionary<string, TilemapData?> _maps = new(StringComparer.Ordinal);   // null: cannot be read
        private static readonly HashSet<string> _pending = new(StringComparer.Ordinal);

        /// <summary>The keys of the maps that were asked for (read, or found unreadable).</summary>
        public static IReadOnlyCollection<string> Keys => _maps.Keys;

        /// <summary>Counts up whenever a map is put, forgotten or said to have changed: what was made from a map is made again.</summary>
        public static int Version { get; private set; }

        /// <summary>The map under <paramref name="key"/>. Loading: its file is on its way (the web, phones), ask again.</summary>
        public static SpriteLibrary.Answer Find(string? key, out TilemapData? map)
        {
            map = null;
            if (string.IsNullOrEmpty(key)) return SpriteLibrary.Answer.Missing;
            if (_maps.TryGetValue(key, out map)) return map != null ? SpriteLibrary.Answer.Ready : SpriteLibrary.Answer.Missing;
            if (_pending.Contains(key)) return SpriteLibrary.Answer.Loading;

            if (Reader != null) return Arrived(key, Reader(key), out map);
            if (ConfigManager.HasProject) return Arrived(key, ReadProjectFile(key), out map);

            Ask(key, null);   // a built game: its files are read as they arrive
            return _maps.TryGetValue(key, out map) ? (map != null ? SpriteLibrary.Answer.Ready : SpriteLibrary.Answer.Missing) : SpriteLibrary.Answer.Loading;
        }

        /// <summary>The map under <paramref name="key"/> when it is read, else null (missing, or on its way).</summary>
        public static TilemapData? Map(string? key) => Find(key, out TilemapData? map) == SpriteLibrary.Answer.Ready ? map : null;

        /// <summary>Hands over a map file's text, replacing what was known for the key. Returns the map, or null when the text is not one (logged).</summary>
        public static TilemapData? Put(string key, string json)
        {
            Arrived(key, json, out TilemapData? map);
            return map;
        }

        /// <summary>
        /// Hands over a map itself, to be the one that is drawn under its key — an editor's map that is being
        /// painted, before it is written. Changing its cells afterwards needs no further call; <see cref="Changed"/>
        /// tells those who keep something made from a map (the walls of a solid layer).
        /// </summary>
        public static void Put(TilemapData map)
        {
            _maps[map.Key] = map;
            Version++;
        }

        /// <summary>The cells or layers of a map that was handed over were changed.</summary>
        public static void Changed() => Version++;

        // Apart from Find, which is asked every frame: a method that hands its argument to a callback makes an
        // object for it each time it is entered.
        private static void Ask(string key, Action<string?>? then)
        {
            _pending.Add(key);
            SFilesystem.LoadFileAsync(key, (path, buffer, status) =>
            {
                _pending.Remove(key);
                string? text = status == SFileLoadStatus.Success && buffer != null ? System.Text.Encoding.UTF8.GetString(buffer) : null;
                Arrived(key, text, out _);
                then?.Invoke(text);
            });
        }

        private static SpriteLibrary.Answer Arrived(string key, string? json, out TilemapData? map)
        {
            map = null;
            if (json == null) Logger.Warning($"[Tilemap] '{key}' could not be read.");
            else
            {
                map = TilemapData.Parse(key, json, out string? error);
                if (map == null) Logger.Warning($"[Tilemap] '{key}': {error}.");
            }
            _maps[key] = map;
            Version++;
            return map != null ? SpriteLibrary.Answer.Ready : SpriteLibrary.Answer.Missing;
        }

        private static string? ReadProjectFile(string key)
        {
            string path = System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets", key);
            try { return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null; }
            catch (System.IO.IOException) { return null; }
        }

        // ── For a built game: the maps a scene names, read before it ───────────────────────

        /// <summary>The map keys a scene's or a prefab's text names: every text value that ends in ".tilemap".</summary>
        public static List<string> KeysIn(string json)
        {
            var keys = new List<string>();
            if (json.IndexOf(TilemapData.Extension, StringComparison.OrdinalIgnoreCase) < 0) return keys;
            json = AssetRefs.Resolve(json);   // a map that moved is asked for where it is now
            try
            {
                using var doc = JsonDocument.Parse(json);
                Collect(doc.RootElement, keys);
            }
            catch (JsonException) { }   // the reader of the scene reports a text that is not JSON
            return keys;

            static void Collect(JsonElement e, List<string> keys)
            {
                switch (e.ValueKind)
                {
                    case JsonValueKind.Object:
                        foreach (var p in e.EnumerateObject()) Collect(p.Value, keys);
                        break;
                    case JsonValueKind.Array:
                        foreach (var item in e.EnumerateArray()) Collect(item, keys);
                        break;
                    case JsonValueKind.String:
                        string? text = e.GetString();
                        if (TilemapData.IsMapKey(text) && !keys.Contains(text!)) keys.Add(text!);
                        break;
                }
            }
        }

        /// <summary>
        /// Reads the map files the texts name, then calls <paramref name="done"/> with the texts of the maps that
        /// are known now (they name the sheets their tiles come from) — at once when the texts name none.
        /// </summary>
        internal static void LoadNamedIn(IEnumerable<string> texts, Action<List<string>> done)
        {
            var read = new List<string>();
            int waiting = 0;
            bool listed = false;
            foreach (string text in texts)
                foreach (string key in KeysIn(text))
                {
                    if (_maps.ContainsKey(key) || _pending.Contains(key)) continue;
                    if (Reader != null || ConfigManager.HasProject) { Map(key); continue; }
                    waiting++;
                    Ask(key, mapText =>
                    {
                        if (mapText != null) read.Add(mapText);
                        waiting--;
                        if (listed && waiting == 0) done(read);
                    });
                }
            listed = true;
            if (waiting == 0) done(read);
        }

        // ── Forgetting ──────────────────────────────────────────────────────────────────

        /// <summary>Forgets what was read under <paramref name="key"/>: after its file changed, moved or came back.</summary>
        public static void Forget(string key)
        {
            _maps.Remove(key);
            Version++;
        }

        /// <summary>Forgets every map: when another project is opened.</summary>
        public static void Clear()
        {
            _maps.Clear();
            Version++;
        }
    }
}
