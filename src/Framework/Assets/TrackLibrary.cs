using System;
using System.Collections.Generic;
using System.Text.Json;
using GameEditor.Framework.Core;
using Sokol;

namespace GameEditor.Framework.Assets
{
    /// <summary>
    /// The race tracks that were read, by their keys ("Tracks/loop.track"). A Track component names a track; its road
    /// and walls name it in their meshes' paths ("Tracks/loop.track#road"); the renderer, the physics, a race and
    /// an editor ask here for it. Main thread only. (The same reading as <see cref="TilemapLibrary"/>'s.)
    /// </summary>
    public static class TrackLibrary
    {
        /// <summary>
        /// Reads a track file's text by key; null when there is none. When set, it alone is asked (a test, a tool);
        /// otherwise the open project's Assets folder, and without a project the app's files as they arrive.
        /// </summary>
        public static Func<string, string?>? Reader { get; set; }

        private static readonly Dictionary<string, TrackData?> _tracks = new(StringComparer.Ordinal);   // null: cannot be read
        private static readonly HashSet<string> _pending = new(StringComparer.Ordinal);

        /// <summary>The keys of the tracks that were asked for (read, or found unreadable).</summary>
        public static IReadOnlyCollection<string> Keys => _tracks.Keys;

        /// <summary>Counts up whenever a track is put, forgotten or said to have changed: what was made from a track is made again.</summary>
        public static int Version { get; private set; }

        /// <summary>The track under <paramref name="key"/>. Loading: its file is on its way (the web, phones), ask again.</summary>
        public static SpriteLibrary.Answer Find(string? key, out TrackData? track)
        {
            track = null;
            if (string.IsNullOrEmpty(key)) return SpriteLibrary.Answer.Missing;
            if (_tracks.TryGetValue(key, out track)) return track != null ? SpriteLibrary.Answer.Ready : SpriteLibrary.Answer.Missing;
            if (_pending.Contains(key)) return SpriteLibrary.Answer.Loading;

            if (Reader != null) return Arrived(key, Reader(key), out track);
            if (ConfigManager.HasProject) return Arrived(key, ReadProjectFile(key), out track);

            Ask(key, null);   // a built game: its files are read as they arrive
            return _tracks.TryGetValue(key, out track) ? (track != null ? SpriteLibrary.Answer.Ready : SpriteLibrary.Answer.Missing) : SpriteLibrary.Answer.Loading;
        }

        /// <summary>The track under <paramref name="key"/> when it is read, else null (missing, or on its way).</summary>
        public static TrackData? Track(string? key) => Find(key, out TrackData? track) == SpriteLibrary.Answer.Ready ? track : null;

        /// <summary>Hands over a track file's text, replacing what was known for the key. Returns the track, or null when the text is not one (logged).</summary>
        public static TrackData? Put(string key, string json)
        {
            Arrived(key, json, out TrackData? track);
            return track;
        }

        /// <summary>
        /// Hands over a track itself, to be the one that is drawn under its key — an editor's track that is being
        /// edited, before it is written. Changing its points afterwards needs <see cref="Changed"/> (and
        /// <see cref="TrackData.Rebuild"/>), which tells those who keep something made from a track (its meshes).
        /// </summary>
        public static void Put(TrackData track)
        {
            _tracks[track.Key] = track;
            Version++;
        }

        /// <summary>A track that was handed over was changed (its points, widths, banking or walls).</summary>
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

        private static SpriteLibrary.Answer Arrived(string key, string? json, out TrackData? track)
        {
            track = null;
            if (json == null) Logger.Warning($"[Track] '{key}' could not be read.");
            else
            {
                track = TrackData.Parse(key, json, out string? error);
                if (track == null) Logger.Warning($"[Track] '{key}': {error}.");
            }
            _tracks[key] = track;
            Version++;
            return track != null ? SpriteLibrary.Answer.Ready : SpriteLibrary.Answer.Missing;
        }

        private static string? ReadProjectFile(string key)
        {
            string path = System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets", key);
            try { return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null; }
            catch (System.IO.IOException) { return null; }
        }

        // ── For a built game: the tracks a scene names, read before it ───────────────────────

        /// <summary>The track keys a scene's or a prefab's text names: every text value that ends in ".tiletrack".</summary>
        public static List<string> KeysIn(string json)
        {
            var keys = new List<string>();
            if (json.IndexOf(TrackData.Extension, StringComparison.OrdinalIgnoreCase) < 0) return keys;
            json = AssetRefs.Resolve(json);   // a track that moved is asked for where it is now
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
                        string? key = TrackData.KeyOf(text);
                        if (key != null && !keys.Contains(key)) keys.Add(key);
                        break;
                }
            }
        }

        /// <summary>
        /// Reads the track files the texts name, then calls <paramref name="done"/> with the texts of the tracks that
        /// are known now — at once when the texts name none.
        /// <paramref name="again"/>: a track that is known is read from its file again — a scene a game goes to
        /// starts with its tracks as they were made, not as the game left them; until the file has arrived the track
        /// stays what it was.
        /// </summary>
        internal static void LoadNamedIn(IEnumerable<string> texts, Action<List<string>> done, bool again = false)
        {
            var read = new List<string>();
            var asked = again ? new HashSet<string>(StringComparer.Ordinal) : null;
            int waiting = 0;
            bool listed = false;
            foreach (string text in texts)
                foreach (string key in KeysIn(text))
                {
                    if (_pending.Contains(key) || (asked != null ? !asked.Add(key) : _tracks.ContainsKey(key))) continue;
                    if (Reader != null || ConfigManager.HasProject) { if (again) _tracks.Remove(key); Track(key); continue; }
                    waiting++;
                    Ask(key, trackText =>
                    {
                        if (trackText != null) read.Add(trackText);
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
            _tracks.Remove(key);
            Version++;
        }

        /// <summary>Forgets every track: when another project is opened.</summary>
        public static void Clear()
        {
            _tracks.Clear();
            Version++;
        }
    }
}
