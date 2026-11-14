using System;
using System.Collections.Generic;
using System.Text.Json;
using GameEditor.Framework.Animation;
using GameEditor.Framework.Core;
using Sokol;

namespace GameEditor.Framework.Assets
{
    /// <summary>
    /// The animator controllers that were read, by their keys ("Animations/Hero.controller.json"). An Animator names one;
    /// the animator system asks here for it. Read as <see cref="ClipLibrary"/> reads clips (an editor: again when the file
    /// changed; a built game: the ones a scene names, before it starts).
    /// </summary>
    public static class ControllerLibrary
    {
        public const string Extension = ControllerJson.Extension;

        /// <summary>Reads a controller file's text by key; null when there is none. When set, it alone is asked (a test, a tool).</summary>
        public static Func<string, string?>? Reader { get; set; }

        private static readonly Dictionary<string, AnimatorController?> _controllers = new(StringComparer.Ordinal);   // null: cannot be read
        private static readonly Dictionary<string, (string Path, DateTime Time)> _written = new(StringComparer.Ordinal);   // an editor's: the file read, and its time
        private static readonly HashSet<string> _pending = new(StringComparer.Ordinal);

        /// <summary>Counts up whenever a controller is read, put or forgotten: what runs a controller starts it again.</summary>
        public static int Version { get; private set; }

        /// <summary>The controller under <paramref name="key"/>; null while its file is on its way, or when it cannot be read (warned once).</summary>
        public static AnimatorController? Controller(string? key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (Reader != null)
            {
                if (!_controllers.TryGetValue(key, out var known)) known = Arrived(key, Reader(key));
                return known;
            }
            if (ConfigManager.HasProject)
            {
                // An editor: read again when the file was written since (edited outside, or saved by the editor).
                string path = System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets", key);
                DateTime written = System.IO.File.Exists(path) ? System.IO.File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
                if (_controllers.TryGetValue(key, out var have) && _written.TryGetValue(key, out var was) && was.Path == path && was.Time == written) return have;
                _written[key] = (path, written);   // another project's file under the same key: read too
                string? text = null;
                try { if (written != DateTime.MinValue) text = System.IO.File.ReadAllText(path); }
                catch (System.IO.IOException) { }
                return Arrived(key, text);
            }
            if (_controllers.TryGetValue(key, out var clip)) return clip;
            if (!_pending.Contains(key)) Ask(key, null);   // a built game: its files are read as they arrive
            return _controllers.TryGetValue(key, out clip) ? clip : null;
        }

        /// <summary>Hands over a controller, to be the one run under its key (an editor's controller being edited).</summary>
        public static void Put(string key, AnimatorController clip)
        {
            _controllers[key] = clip;
            Version++;
        }

        /// <summary>An editor wrote the controller under <paramref name="key"/> to its file: the one held is the file's (not read again).</summary>
        public static void Saved(string key)
        {
            if (!ConfigManager.HasProject) return;
            string path = System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets", key);
            if (System.IO.File.Exists(path)) _written[key] = (path, System.IO.File.GetLastWriteTimeUtc(path));
        }

        private static void Ask(string key, Action? then)
        {
            _pending.Add(key);
            SFilesystem.LoadFileAsync(key, (path, buffer, status) =>
            {
                _pending.Remove(key);
                Arrived(key, status == SFileLoadStatus.Success && buffer != null ? System.Text.Encoding.UTF8.GetString(buffer) : null);
                then?.Invoke();
            });
        }

        private static AnimatorController? Arrived(string key, string? json)
        {
            AnimatorController? clip = null;
            if (json == null) Logger.Warning($"[Animation] the controller '{key}' could not be read.");
            else
            {
                try { clip = ControllerJson.Read(json); }
                catch (Exception e) when (e is FormatException or JsonException or InvalidOperationException or KeyNotFoundException)
                {
                    Logger.Warning($"[Animation] the controller '{key}' is not a controller: {e.Message}");
                }
            }
            _controllers[key] = clip;
            Version++;
            return clip;
        }

        // ── For a built game: the controllers a scene names, read before it ────────────────────

        /// <summary>The controller keys a scene's or a prefab's text names: every text value that ends in ".controller.json".</summary>
        public static List<string> KeysIn(string json)
        {
            var keys = new List<string>();
            if (json.IndexOf(Extension, StringComparison.OrdinalIgnoreCase) < 0) return keys;
            json = AssetRefs.Resolve(json);   // a clip that moved is asked for where it is now
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
                        if (text != null && text.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) && !keys.Contains(text)) keys.Add(text);
                        break;
                }
            }
        }

        /// <summary>Reads the controller files the texts name, then calls <paramref name="done"/> — at once when they name none.</summary>
        internal static void LoadNamedIn(IEnumerable<string> texts, Action done)
        {
            int waiting = 0;
            bool listed = false;
            foreach (string text in texts)
                foreach (string key in KeysIn(text))
                {
                    if (_pending.Contains(key) || _controllers.ContainsKey(key)) continue;
                    if (Reader != null || ConfigManager.HasProject) { Controller(key); continue; }
                    waiting++;
                    Ask(key, () =>
                    {
                        waiting--;
                        if (listed && waiting == 0) done();
                    });
                }
            listed = true;
            if (waiting == 0) done();
        }

        /// <summary>Forgets what was read under <paramref name="key"/>: after its file changed, moved or came back.</summary>
        public static void Forget(string key)
        {
            _controllers.Remove(key);
            _written.Remove(key);
            Version++;
        }

        /// <summary>Forgets every clip: when another project is opened.</summary>
        public static void Clear()
        {
            _controllers.Clear();
            _written.Clear();
            Version++;
        }
    }
}
