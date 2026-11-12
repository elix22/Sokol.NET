using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using GameEditor.Framework.Assets;
using GameEditor.Framework.Core;
using Sokol;

namespace GameEditor.Framework.Sound
{
    /// <summary>
    /// The sound files a game has read, by key ("Audio/coin.wav"): wav, ogg, mp3 and flac. A file's bytes are kept
    /// as they are in the file; <see cref="Audio"/> hands them to the mixer, which decodes them.
    /// In an editor a file is read at once from the open project's Assets; in a built game it arrives a little
    /// later (the web, phones), and the clips a scene names are read before the scene.
    /// </summary>
    public static unsafe class AudioClips
    {
        /// <summary>A clip's bytes, in memory that does not move (the mixer reads them while a sound plays).</summary>
        public sealed class Clip
        {
            public string Key = "";
            public byte* Data;
            public int Size;
        }

        private static readonly Dictionary<string, Clip?> _clips = new(StringComparer.Ordinal);
        private static readonly HashSet<string> _pending = new(StringComparer.Ordinal);

        /// <summary>Reads a sound file's bytes by key; null when there is none. When set, it alone is asked (a test, a tool).</summary>
        public static Func<string, byte[]?>? Reader { get; set; }

        /// <summary>Called when a clip is forgotten, before its bytes are given back: the mixer lets go of it.</summary>
        internal static Action<Clip>? Forgetting;

        private static readonly string[] Endings = { ".wav", ".ogg", ".mp3", ".flac" };

        /// <summary>Is the text the key of a sound file?</summary>
        public static bool IsClipKey(string? key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            foreach (string ending in Endings)
                if (key.EndsWith(ending, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>The clip under <paramref name="key"/>. Loading: its file is on its way, ask again.</summary>
        public static SpriteLibrary.Answer Find(string? key, out Clip? clip)
        {
            clip = null;
            if (string.IsNullOrEmpty(key)) return SpriteLibrary.Answer.Missing;
            if (_clips.TryGetValue(key, out clip)) return clip != null ? SpriteLibrary.Answer.Ready : SpriteLibrary.Answer.Missing;
            if (_pending.Contains(key)) return SpriteLibrary.Answer.Loading;

            if (Reader != null) return Arrived(key, Reader(key), out clip);
            if (ConfigManager.HasProject) return Arrived(key, ReadProjectFile(key), out clip);

            Ask(key, null);   // a built game: its files are read as they arrive
            return SpriteLibrary.Answer.Loading;
        }

        private static void Ask(string key, Action? then)
        {
            _pending.Add(key);
            SFilesystem.LoadFileAsync(key, (path, buffer, status) =>
            {
                _pending.Remove(key);
                Arrived(key, status == SFileLoadStatus.Success ? buffer : null, out _);
                then?.Invoke();
            });
        }

        private static SpriteLibrary.Answer Arrived(string key, byte[]? bytes, out Clip? clip)
        {
            clip = null;
            if (bytes == null || bytes.Length == 0) Logger.Warning($"[Audio] The sound '{key}' could not be read.");
            else
            {
                clip = new Clip { Key = key, Size = bytes.Length, Data = (byte*)NativeMemory.Alloc((nuint)bytes.Length) };
                bytes.CopyTo(new Span<byte>(clip.Data, bytes.Length));
            }
            _clips[key] = clip;
            return clip != null ? SpriteLibrary.Answer.Ready : SpriteLibrary.Answer.Missing;
        }

        private static byte[]? ReadProjectFile(string key)
        {
            string path = System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets", key);
            try { return System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null; }
            catch (System.IO.IOException) { return null; }
        }

        // ── For a built game: the clips a scene names, read before it ──────────────────────

        /// <summary>The clip keys a scene's or a prefab's text names: every text value that ends as a sound file does.</summary>
        public static List<string> KeysIn(string json)
        {
            var keys = new List<string>();
            bool any = false;
            foreach (string ending in Endings) any |= json.IndexOf(ending, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!any) return keys;
            json = AssetRefs.Resolve(json);   // a file that moved is asked for where it is now
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
                        if (IsClipKey(text) && !keys.Contains(text!)) keys.Add(text!);
                        break;
                }
            }
        }

        /// <summary>Reads the sound files the texts name, then calls <paramref name="done"/> — at once when they name none.</summary>
        internal static void LoadNamedIn(IEnumerable<string> texts, Action done)
        {
            int waiting = 0;
            bool listed = false;
            foreach (string text in texts)
                foreach (string key in KeysIn(text))
                {
                    if (_clips.ContainsKey(key) || _pending.Contains(key)) continue;
                    if (Reader != null || ConfigManager.HasProject) { Find(key, out _); continue; }
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

        // ── Forgetting ──────────────────────────────────────────────────────────────────

        /// <summary>Forgets what was read under <paramref name="key"/>: after its file changed, moved or came back.</summary>
        public static void Forget(string key)
        {
            if (_clips.Remove(key, out Clip? clip) && clip != null) Free(clip);
        }

        /// <summary>Forgets every clip: when another project is opened.</summary>
        public static void Clear()
        {
            foreach (Clip? clip in _clips.Values)
                if (clip != null) Free(clip);
            _clips.Clear();
        }

        private static void Free(Clip clip)
        {
            Forgetting?.Invoke(clip);
            NativeMemory.Free(clip.Data);
            clip.Data = null;
        }
    }
}
