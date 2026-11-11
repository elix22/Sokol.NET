using System;
using System.Collections.Generic;
using System.Text.Json;
using GameEditor.Framework.Core;

namespace GameEditor.Framework.Scene
{
    /// <summary>One entity of a loaded prefab, with everything a nested prefab contributes already applied.</summary>
    public sealed class PrefabEntity
    {
        /// <summary>Its id among the prefab's entities — the id entity references inside <see cref="Components"/> use.
        /// Given by the file, or made up at load for an entity of a nested prefab the file has no entry for yet.</summary>
        public int Id;
        /// <summary>Its address for a scene (or prefab) that holds an instance: empty for the root, the entity's id
        /// for the prefab's own entities, and for an entity of a nested instance the nested root's id followed by
        /// the entity's address inside the nested prefab.</summary>
        public int[] Path = [];
        /// <summary>Its components in the scene-file format (the value of an entity's "c").</summary>
        public JsonElement Components;
        /// <summary>The prefab's key when this entity is the root of a nested instance, else null.</summary>
        public string? NestedAsset;
    }

    /// <summary>A prefab file, loaded: its entities, root first.</summary>
    public sealed class Prefab
    {
        public string Key = "";
        public string Name = "";
        // Concrete collections, not interfaces: generic interface dispatch crashes under WASM NativeAOT (see ECSWorld.Entities).
        public List<PrefabEntity> Entities = new();
        /// <summary>Every prefab this one holds an instance of, at any depth.</summary>
        public HashSet<string> Dependencies = new(StringComparer.Ordinal);
        /// <summary>The id a new entity of the prefab gets: above every id the file ever used.</summary>
        public int NextId = 1;
        /// <summary>The file's root members besides "name", "entities" and "nextId", as <c>,"key":value…</c> — written back when the file is rewritten.</summary>
        public string OtherRootMembers = "";

        private readonly Dictionary<string, PrefabEntity> _byPath = new(StringComparer.Ordinal);
        private readonly Dictionary<int, PrefabEntity> _byId = new();

        public PrefabEntity Root => Entities[0];

        public PrefabEntity? ByPath(ReadOnlySpan<int> path) => _byPath.TryGetValue(PathKey(path), out var e) ? e : null;
        public PrefabEntity? ById(int id) => _byId.TryGetValue(id, out var e) ? e : null;

        internal void Add(PrefabEntity e)
        {
            Entities.Add(e);
            _byPath[PathKey(e.Path)] = e;
            _byId[e.Id] = e;
        }

        /// <summary>A path as a dictionary key: "4/2"; "" for the root.</summary>
        public static string PathKey(ReadOnlySpan<int> path)
        {
            if (path.Length == 0) return "";
            if (path.Length == 1) return path[0].ToString(System.Globalization.CultureInfo.InvariantCulture);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < path.Length; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(path[i]);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// The prefabs of the project, by key (a prefab file's path below Assets, "Prefabs/Enemy.prefab"): the one
    /// place that turns a key into a loaded prefab. Scenes resolve their instances through it when they load,
    /// and compare against it when they save.
    /// A file is read when first asked for: from text handed over with <see cref="Put"/> (platforms that load
    /// files asynchronously hand the texts over before the scene is read), else by <see cref="Reader"/>, else
    /// from the open project's Assets folder. Main thread only.
    /// </summary>
    public static class PrefabLibrary
    {
        public const string Extension = ".prefab";

        /// <summary>Reads a prefab file's text by key; null when there is none. Asked before the project's Assets folder.</summary>
        public static Func<string, string?>? Reader { get; set; }

        private static readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Prefab> _loaded = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);

        /// <summary>Hands over a prefab file's text (loaded asynchronously, or just written), replacing what was known for the key.</summary>
        public static void Put(string key, string json)
        {
            _texts[key] = json;
            Invalidate(key);
        }

        /// <summary>True when the prefab's text was handed over with <see cref="Put"/>.</summary>
        public static bool HasText(string key) => _texts.ContainsKey(key);

        /// <summary>The text handed over for the key with <see cref="Put"/>, or null.</summary>
        public static string? TextOf(string key) => _texts.TryGetValue(key, out string? text) ? text : null;

        /// <summary>
        /// True when <see cref="Get"/> has something to read for the key: it is loaded, its text was handed over,
        /// or its file is there (<see cref="Reader"/>, the open project's Assets folder). False on a platform that
        /// reads files asynchronously until the text was handed over — asking <see cref="Get"/> then would only
        /// record that the prefab is missing.
        /// </summary>
        public static bool CanRead(string key)
            => _loaded.ContainsKey(key) || _texts.ContainsKey(key) || Reader?.Invoke(key) != null || ReadProjectFile(key) != null;

        /// <summary>The loaded prefab, or null when its file is missing, is not a prefab, or holds an instance
        /// that cannot be loaded (<see cref="ErrorOf"/> says which).</summary>
        public static Prefab? Get(string key) => Get(key, null);

        /// <summary>Why <see cref="Get"/> returned null for the key, or null.</summary>
        public static string? ErrorOf(string key) => _errors.TryGetValue(key, out string? e) ? e : null;

        /// <summary>
        /// Forgets what was loaded for the key, and for every prefab that holds an instance of it, so the next
        /// <see cref="Get"/> reads the files again — call it when a prefab file was written, renamed or deleted.
        /// Without a key: everything loaded. Texts handed over with <see cref="Put"/> stay.
        /// </summary>
        public static void Invalidate(string? key = null)
        {
            _errors.Clear();
            if (key == null) { _loaded.Clear(); return; }
            _loaded.Remove(key);
            List<string>? dependents = null;
            foreach (var kv in _loaded)
                if (kv.Value.Dependencies.Contains(key)) (dependents ??= new List<string>()).Add(kv.Key);
            if (dependents != null)
                foreach (string k in dependents) _loaded.Remove(k);
        }

        /// <summary>Forgets everything, the texts handed over too (another project is opened).</summary>
        public static void Clear()
        {
            _texts.Clear();
            _loaded.Clear();
            _errors.Clear();
        }

        // `loading`: the prefabs being loaded right now, outermost first — a key found in it is a prefab that contains itself.
        internal static Prefab? Get(string key, HashSet<string>? loading)
        {
            if (_loaded.TryGetValue(key, out Prefab? known)) return known;
            if (_errors.ContainsKey(key)) return null;
            if (loading != null && loading.Contains(key)) return null;   // the caller reports the loop; nothing is cached for a prefab still loading

            string? text = _texts.TryGetValue(key, out string? put) ? put : (Reader?.Invoke(key) ?? ReadProjectFile(key));
            if (text == null) return Fail(key, $"prefab '{key}' was not found");

            loading ??= new HashSet<string>(StringComparer.Ordinal);
            loading.Add(key);
            Prefab? prefab;
            string? error;
            try { prefab = SceneSerializer.LoadPrefab(key, text, loading, out error); }
            finally { loading.Remove(key); }
            if (prefab == null) return Fail(key, error ?? $"prefab '{key}' could not be loaded");

            _loaded[key] = prefab;
            return prefab;
        }

        private static Prefab? Fail(string key, string error)
        {
            _errors[key] = error;
            Logger.Warning("[Prefab] " + error);
            return null;
        }

        private static string? ReadProjectFile(string key)
        {
            string? folder = ConfigManager.ProjectFolder;
            if (folder == null) return null;
            string path = System.IO.Path.Combine(folder, "Assets", key);
            try { return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null; }
            catch (System.IO.IOException) { return null; }
        }
    }
}
