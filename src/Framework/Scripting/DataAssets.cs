// DataAssets.cs — §6 part 4 (examples/GameEditor/docs/EDITOR_ENHANCEMENTS_DESIGN.md §6.2): shared data as files.
//
//     [DataAsset] public class EnemyTuning { public float Speed = 3; public int Hp = 10; }
//     Assets/Tuning/Fast.asset.json:  {"Type": "EnemyTuning", "Props": {"Speed": "6"}}
//     public EnemyTuning Tuning;            // a script's field: the asset's key, set in the Inspector
//
// One instance per key, shared by every script that names it (an EventChannel asset: both sides hold the same one).
// Its fields are the texts a script's fields have, set by the same setters: the builder writes, for each
// [DataAsset] class, a factory and a setter (Register, no reflection: built games are compiled ahead of time); the
// editor's Play and the web page set them by name. A scene's load reads the asset files it names (and those they
// name) before it starts, as it reads its prefabs; the editor reads them from the project.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using GameEditor.Framework.Core;
using Sokol;

namespace GameEditor.Framework.Scripting
{
    /// <summary>A class whose instances are data assets: <c>.asset.json</c> files under Assets, edited in the Inspector,
    /// named by scripts' fields (shared settings, tuning, event channels - instead of singletons).</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DataAssetAttribute : Attribute { }

    /// <summary>Sets a data asset's field from its file's text: true when the value was one the field's type takes.</summary>
    public delegate bool DataAssetSetter(object asset, string field, in ScriptFieldValue value);

    /// <summary>A built-in data asset: an event both sides of a game hold (a button's script raises it; the sound's and
    /// the score's listen) - the same instance for every script that names its file.</summary>
    [DataAsset]
    public class EventChannel
    {
        /// <summary>Raised: each listener called.</summary>
        public event Action? Raised;
        public void Raise() => Raised?.Invoke();
    }

    public static class DataAssets
    {
        public const string Extension = ".asset.json";

        private static readonly Dictionary<string, object> _instances = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, (Type Type, Func<object> Make, DataAssetSetter Set)> _types = new(StringComparer.Ordinal);

        // The engine's own data asset: made with no generated code (it has no field a file sets).
        static DataAssets() => Register<EventChannel>(static (object asset, string field, in ScriptFieldValue value) => false);

        /// <summary>Reads a key's text in place of the project's file (a test, a tool); null: the file.</summary>
        public static Func<string, string?>? Reader { get; set; }

        /// <summary>The builder's generated code: a [DataAsset] class's factory and setter (a game compiled ahead of time).</summary>
        public static void Register<T>(DataAssetSetter setter) where T : class, new()
            => _types[typeof(T).Name] = (typeof(T), static () => new T(), setter);

        /// <summary>The asset a key names, as <typeparamref name="T"/>; null for none, a missing file or another type.</summary>
        public static T? Get<T>(string? key) where T : class, new() => Get(key, typeof(T), static () => new T()) as T;

        /// <summary>The asset a key names when its type is known only as a Type (a list's element, the reflection path).</summary>
        public static object? Get(string? key, Type type) =>
            Get(key, type, () => _types.TryGetValue(type.Name, out var r) && r.Type == type ? r.Make() : CreateByReflection(type));

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "The reflection path only: a game compiled ahead of time registers a factory.")]
        private static object? CreateByReflection(Type type) => Activator.CreateInstance(type);

        private static object? Get(string? key, Type type, Func<object?> make)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_instances.TryGetValue(key, out object? had)) return type.IsInstanceOfType(had) ? had : null;
            string? text = TextOf(key);
            if (text == null) { Logger.Warning($"[DataAssets] '{key}' could not be read."); return null; }
            string typeName;
            Dictionary<string, string> props;
            try { (typeName, props) = Parse(text); }
            catch (JsonException ex) { Logger.Warning($"[DataAssets] '{key}': {ex.Message}"); return null; }
            if (typeName != type.Name) { Logger.Warning($"[DataAssets] '{key}' is a {typeName}, not a {type.Name}."); return null; }
            object? asset = make();
            if (asset == null) return null;
            _instances[key] = asset;              // before its fields: an asset that names itself gets itself
            if (_types.TryGetValue(typeName, out var reg) && reg.Type == type)
            {
                foreach (var (field, value) in props)
                    if (!reg.Set(asset, field, new ScriptFieldValue(value)))
                        Logger.Warning($"[DataAssets] {typeName}.{field}: '{value}' was not set (no such field, or no value of its type).");
            }
            else SetByName(asset, props);
            return asset;
        }

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The reflection path only (the editor's Play, the web page).")]
        private static void SetByName(object asset, Dictionary<string, string> props)
        {
            foreach (var (name, text) in props)
            {
                var fi = asset.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (fi == null) continue;
                try
                {
                    object? value = GameBehaviour.ParseFieldValue(fi.FieldType, text);
                    if (value != null) fi.SetValue(asset, value);
                }
                catch (Exception ex) { Logger.Warning($"[DataAssets] {asset.GetType().Name}.{name}: {ex.Message}"); }
            }
        }

        /// <summary>A data asset file's type and field texts.</summary>
        public static (string Type, Dictionary<string, string> Props) Parse(string text)
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            string type = root.TryGetProperty("Type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
            var props = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("Props", out var p) && p.ValueKind == JsonValueKind.Object)
                foreach (var kv in p.EnumerateObject())
                    if (kv.Value.ValueKind == JsonValueKind.String) props[kv.Name] = kv.Value.GetString() ?? "";
            return (type, props);
        }

        /// <summary>A type is a data asset's: registered by the builder, or (the reflection path) marked [DataAsset].</summary>
        public static bool IsDataAsset(Type type)
        {
            if (_types.TryGetValue(type.Name, out var r) && r.Type == type) return true;
            return !type.IsValueType && type != typeof(string) && type.IsDefined(typeof(DataAssetAttribute), false);
        }

        private static string? TextOf(string key)
        {
            if (_texts.TryGetValue(key, out string? put)) return put;
            if (Reader?.Invoke(key) is { } read) return read;
            string? folder = ConfigManager.HasProject ? ConfigManager.ProjectFolder : null;
            if (folder == null) return null;
            string path = System.IO.Path.Combine(folder, "Assets", key);
            try { return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null; }
            catch (System.IO.IOException) { return null; }
        }

        /// <summary>A key's text handed over (a scene's load read it).</summary>
        public static void Put(string key, string text) { _texts[key] = text; _instances.Remove(key); }

        /// <summary>The instances forgotten: the next Get makes them again from their files (Play stopped: the next
        /// Play sees the files as they are now). The texts handed over stay.</summary>
        public static void ForgetInstances() => _instances.Clear();

        /// <summary>Everything forgotten (another project).</summary>
        public static void Clear() { _instances.Clear(); _texts.Clear(); }

        // ── A scene's load: the asset files it names ─────────────────────────────

        /// <summary>The asset keys a text names (a scene, a prefab, an asset: any string ending in .asset.json).</summary>
        public static List<string> KeysIn(string json)
        {
            var keys = new List<string>();
            try { using var doc = JsonDocument.Parse(json); Collect(doc.RootElement, keys); }
            catch (JsonException) { }
            return keys;

            static void Collect(JsonElement e, List<string> keys)
            {
                switch (e.ValueKind)
                {
                    case JsonValueKind.Object: foreach (var p in e.EnumerateObject()) Collect(p.Value, keys); break;
                    case JsonValueKind.Array: foreach (var item in e.EnumerateArray()) Collect(item, keys); break;
                    case JsonValueKind.String:
                        string? text = e.GetString();
                        if (text == null) break;
                        if (text.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) { if (!keys.Contains(text)) keys.Add(text); }
                        else if (text.StartsWith('[') && text.Contains(Extension, StringComparison.OrdinalIgnoreCase))
                            foreach (string k in KeysIn(text)) if (!keys.Contains(k)) keys.Add(k);   // a list of them, in a field's text
                        break;
                }
            }
        }

        /// <summary>
        /// Reads the asset files the texts name (and those they name in turn) and hands them over; <paramref name="done"/>
        /// when all are there. In the editor (a project open) the files are read when asked for: nothing to wait for.
        /// </summary>
        internal static void LoadNamedIn(IEnumerable<string> texts, Action done)
        {
            if (Reader != null || ConfigManager.HasProject) { done(); return; }
            var asked = new HashSet<string>(StringComparer.Ordinal);
            int waiting = 0;
            bool listed = false;
            void Ask(string key)
            {
                if (_texts.ContainsKey(key) || !asked.Add(key)) return;
                waiting++;
                SFilesystem.LoadFileAsync(key, (_, buffer, status) =>
                {
                    if (status == SFileLoadStatus.Success && buffer != null)
                    {
                        string text = System.Text.Encoding.UTF8.GetString(buffer);
                        _texts[key] = text;
                        foreach (string inner in KeysIn(text)) Ask(inner);
                    }
                    else Logger.Warning($"[DataAssets] '{key}' could not be read.");
                    waiting--;
                    if (listed && waiting == 0) done();
                });
            }
            foreach (string text in texts)
                foreach (string key in KeysIn(text)) Ask(key);
            listed = true;
            if (waiting == 0) done();
        }
    }
}
