using System;
using System.Collections.Generic;
using System.Text.Json;
using GameEditor.Framework.Core;
using Sokol;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// Prefabs for a running game. A scene's own prefabs — the instances placed in it, and the prefabs named in a
    /// script's <see cref="Prefab"/> field — are loaded with the scene, so <c>Instantiate</c> never waits for a
    /// file. A prefab that only code names is asked for with <see cref="Load"/>.
    /// </summary>
    public static class Prefabs
    {
        /// <summary>
        /// Loads a prefab by its key (its path below Assets, "Prefabs/Bullet.prefab"), with the prefabs it holds,
        /// and asks for its meshes and materials. <paramref name="done"/> gets the prefab, or null when it is not
        /// there or cannot be loaded (the log says why). On desktop, and for a prefab that is loaded already,
        /// <paramref name="done"/> runs before this returns; on the web and on phones when the files have arrived.
        /// </summary>
        public static void Load(string key, Action<Prefab?> done)
        {
            if (PrefabLibrary.CanRead(key)) { Finish(); return; }
            LoadTexts([key], _ => Finish());

            void Finish()
            {
                Prefab? prefab = PrefabLibrary.Get(key);
                if (prefab != null) SceneManager.PreloadPrefabAssets(prefab);
                done(prefab);
            }
        }

        /// <summary>The prefab for a key when it is loaded or can be read at once, else null (use <see cref="Load"/>).</summary>
        public static Prefab? Get(string key) => PrefabLibrary.CanRead(key) ? PrefabLibrary.Get(key) : null;

        /// <summary>
        /// The prefab keys a scene's or a prefab's text names: every text value that ends in ".prefab" — an
        /// instance's reference, a script's prefab field, a plugin's component.
        /// </summary>
        public static List<string> KeysIn(string json)
        {
            var keys = new List<string>();
            json = Assets.AssetRefs.Resolve(json);   // a prefab that moved is asked for where it is now
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
                        if (text != null && text.EndsWith(PrefabLibrary.Extension, StringComparison.OrdinalIgnoreCase) && !keys.Contains(text))
                            keys.Add(text);
                        break;
                }
            }
        }

        /// <summary>
        /// Reads the prefab files a scene's text names, and those the prefabs name in turn, and hands their texts
        /// to <see cref="PrefabLibrary"/>. <paramref name="done"/> gets the keys that were read — at once when the
        /// text names none.
        /// </summary>
        internal static void LoadNamedIn(string json, Action<List<string>> done) => LoadTexts(KeysIn(json), done);

        // Reads each key's file unless its text is known, then the files that text names, until nothing is left
        // to read. A file that is not there is reported once; its instances then show as missing.
        private static void LoadTexts(List<string> keys, Action<List<string>> done)
        {
            var loaded = new List<string>();
            var asked = new HashSet<string>(StringComparer.Ordinal);
            int waiting = 0;
            bool listed = false;   // the first keys are all asked for: from here on the count reaching 0 is the end

            foreach (string key in keys) Ask(key);
            listed = true;
            if (waiting == 0) done(loaded);

            void Ask(string key)
            {
                if (!asked.Add(key)) return;
                if (PrefabLibrary.HasText(key)) return;   // handed over before (another scene of the game)
                waiting++;
                SFilesystem.LoadFileAsync(key, (_, buffer, status) =>
                {
                    if (status == SFileLoadStatus.Success && buffer != null)
                    {
                        string text = System.Text.Encoding.UTF8.GetString(buffer);
                        PrefabLibrary.Put(key, text);
                        loaded.Add(key);
                        foreach (string inner in KeysIn(text)) Ask(inner);
                    }
                    else
                        Logger.Warning($"[Prefab] '{key}' could not be read.");
                    waiting--;
                    if (listed && waiting == 0) done(loaded);
                });
            }
        }
    }
}
