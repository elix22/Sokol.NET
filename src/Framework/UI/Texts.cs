using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.UI
{
    /// <summary>
    /// A game's texts by key, in each of its languages: one table, <c>Embedded/texts.json</c> in the project —
    /// <c>{"languages": ["en", "he"], "start": "he", "texts": {"title": {"en": "Bopalop", "he": "בופלופ"}}}</c>.
    /// A <see cref="UIText"/> or a <see cref="UIButton"/> with a <c>Key</c> shows the text of the game's
    /// <see cref="Language"/> (its own <c>Text</c> when the key has none there); a script asks <see cref="Get"/> and
    /// <see cref="Format"/>. The table is part of the app (the Embedded folder), so it is there before the first frame
    /// on every target; in an editor it is the project's file.
    /// </summary>
    public static class Texts
    {
        /// <summary>The table's name in the project's Embedded folder.</summary>
        public const string FileName = "texts.json";

        private static readonly List<string> _languages = new();
        // key → language → text, keys in the order they were added.
        private static readonly Dictionary<string, Dictionary<string, string>> _texts = new(StringComparer.Ordinal);
        private static readonly List<string> _keys = new();
        private static string _start = "";
        private static string? _language;
        private static bool _loaded;
        private static string? _loadedFrom;        // the project folder the table was read from (an editor), or "" (a built game)

        /// <summary>Goes up when the table or the language changes: what shows texts looks again.</summary>
        public static int Version { get; private set; }

        /// <summary>The table's languages, in its order ("en", "he").</summary>
        public static IReadOnlyList<string> Languages { get { Load(); return _languages; } }

        /// <summary>The keys of the table, in the order they were added.</summary>
        public static IReadOnlyList<string> Keys { get { Load(); return _keys; } }

        /// <summary>The language the game starts in whatever the device's; "" when it follows the device.</summary>
        public static string Start { get { Load(); return _start; } }

        /// <summary>
        /// The game's language: set by a script (one of the table's), or the start language, or the device's when the
        /// table has it, or the table's first. "" when the table has no language.
        /// </summary>
        public static string Language
        {
            get
            {
                Load();
                // Worked out once; again when the table had no language then and has one now.
                if (_language == null || (_language.Length == 0 && _languages.Count > 0)) _language = FirstLanguage();
                return _language;
            }
            set
            {
                Load();
                string code = Subtag(value);
                if (!_languages.Contains(code))
                {
                    Logger.Warning($"[Texts] '{value}' is not one of the game's languages ({string.Join(", ", _languages)}).");
                    return;
                }
                if (_language == code) return;
                _language = code;
                Version++;
            }
        }

        /// <summary>The game's language is written right to left (Hebrew, Arabic, Persian, Urdu …).</summary>
        public static bool RightToLeft => IsRightToLeft(Language);

        public static bool IsRightToLeft(string? language) =>
            Subtag(language) is "he" or "iw" or "ar" or "fa" or "ur" or "yi" or "ps" or "sd" or "ug" or "dv" or "ckb";

        /// <summary>The text of <paramref name="key"/> in the game's language; null when the key has none there.</summary>
        public static string? Get(string key)
        {
            Load();
            return Find(key, Language);
        }

        /// <summary>The text of <paramref name="key"/> in the game's language, or <paramref name="fallback"/>.</summary>
        public static string Get(string key, string fallback) => Get(key) ?? fallback;

        /// <summary>The text of <paramref name="key"/> with "{0}", "{1}" … filled in ("Score: {0}"); the key itself when it has no text.</summary>
        public static string Format(string key, params object?[] values)
        {
            string text = Get(key) ?? key;
            try { return string.Format(System.Globalization.CultureInfo.InvariantCulture, text, values); }
            catch (FormatException) { return text; }
        }

        /// <summary>The text of <paramref name="key"/> in <paramref name="language"/> itself (no fallback); null when none.</summary>
        public static string? Find(string key, string language)
        {
            Load();
            return _texts.TryGetValue(key, out var row) && row.TryGetValue(language, out var text) ? text : null;
        }

        /// <summary>The device's language, a bare subtag ("he"), or "" when unknown.</summary>
        public static string DeviceLanguage()
        {
            // A browser's game is built without culture data: the page hands navigator.language in (the template's main.js).
            string? handed = Environment.GetEnvironmentVariable("GAME_LANGUAGE");
            if (!string.IsNullOrWhiteSpace(handed)) return Subtag(handed);
            try { return Subtag(System.Globalization.CultureInfo.CurrentUICulture.Name); }
            catch { return ""; }
        }

        private static string FirstLanguage()
        {
            if (_start.Length > 0 && _languages.Contains(_start)) return _start;
            string device = DeviceLanguage();
            if (device.Length > 0 && _languages.Contains(device)) return device;
            if (device == "iw" && _languages.Contains("he")) return "he";
            return _languages.Count > 0 ? _languages[0] : "";
        }

        private static string Subtag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return "";
            int cut = tag.IndexOfAny(['-', '_']);
            return (cut < 0 ? tag : tag[..cut]).Trim().ToLowerInvariant();
        }

        // ── The table ───────────────────────────────────────────────────────────────────

        // In an editor, the project's file (read again when another project opens); in a built game, the embedded one.
        private static void Load()
        {
            string? project = ConfigManager.HasProject ? ConfigManager.ProjectFolder : null;
            string from = project ?? "";
            if (_loaded && _loadedFrom == from) return;
            _loaded = true;
            _loadedFrom = from;
            string? json = null;
            if (project != null)
            {
                string path = PathIn(project);
                if (File.Exists(path))
                {
                    try { json = File.ReadAllText(path); }
                    catch (IOException ex) { Logger.Warning($"[Texts] {path} could not be read: {ex.Message}"); }
                }
            }
            json ??= EmbeddedFiles.ReadText(FileName);
            Read(json ?? "");
        }

        /// <summary>Where a project keeps its table.</summary>
        public static string PathIn(string projectFolder) => Path.Combine(projectFolder, EmbeddedFiles.Folder, FileName);

        /// <summary>Takes a table's text (an editor, a test); an empty or broken text is an empty table. Null, or what was wrong.</summary>
        public static string? Read(string json)
        {
            _loaded = true;
            _loadedFrom ??= "";
            _languages.Clear(); _texts.Clear(); _keys.Clear();
            _start = "";
            _language = null;
            Version++;
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("languages", out var langs) && langs.ValueKind == JsonValueKind.Array)
                    foreach (var l in langs.EnumerateArray())
                        if (l.ValueKind == JsonValueKind.String && Subtag(l.GetString()) is { Length: > 0 } code && !_languages.Contains(code)) _languages.Add(code);
                if (root.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.String) _start = Subtag(start.GetString());
                if (root.TryGetProperty("texts", out var texts) && texts.ValueKind == JsonValueKind.Object)
                    foreach (var key in texts.EnumerateObject())
                    {
                        if (key.Value.ValueKind != JsonValueKind.Object) continue;
                        var row = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (var cell in key.Value.EnumerateObject())
                            if (cell.Value.ValueKind == JsonValueKind.String) row[Subtag(cell.Name)] = cell.Value.GetString() ?? "";
                        if (_texts.TryAdd(key.Name, row)) _keys.Add(key.Name);
                    }
                return null;
            }
            catch (JsonException ex)
            {
                Logger.Warning($"[Texts] The text table is not JSON: {ex.Message}");
                return ex.Message;
            }
        }

        /// <summary>The table as its file has it.</summary>
        public static string Write()
        {
            Load();
            var buffer = new MemoryStream();
            using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            {
                w.WriteStartObject();
                w.WriteStartArray("languages");
                foreach (var l in _languages) w.WriteStringValue(l);
                w.WriteEndArray();
                if (_start.Length > 0) w.WriteString("start", _start);
                w.WriteStartObject("texts");
                foreach (var key in _keys)
                {
                    w.WriteStartObject(key);
                    var row = _texts[key];
                    foreach (var l in _languages) if (row.TryGetValue(l, out var t)) w.WriteString(l, t);
                    w.WriteEndObject();
                }
                w.WriteEndObject();
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
        }

        // ── Changing the table (an editor) ──────────────────────────────────────────────

        /// <summary>Adds a language (a subtag, "he"). Null, or why not.</summary>
        public static string? AddLanguage(string language)
        {
            Load();
            string code = Subtag(language);
            if (code.Length < 2 || code.Length > 3 || !IsLetters(code)) return $"'{language}' is no language code (two or three letters: en, he, ar)";
            if (_languages.Contains(code)) return null;
            _languages.Add(code);
            Version++;
            return null;
        }

        /// <summary>A key's text in a language: the key and the language are added when new; an empty text takes that one out. Null, or why not.</summary>
        public static string? Set(string key, string language, string text)
        {
            Load();
            key = key.Trim();
            if (key.Length == 0) return "a text needs a key";
            if (AddLanguage(language) is { } bad) return bad;
            string code = Subtag(language);
            if (!_texts.TryGetValue(key, out var row))
            {
                row = new Dictionary<string, string>(StringComparer.Ordinal);
                _texts[key] = row;
                _keys.Add(key);
            }
            if (text.Length == 0) row.Remove(code);
            else row[code] = text;
            Version++;
            return null;
        }

        /// <summary>Takes a key out with its texts. False when there is no such key.</summary>
        public static bool Remove(string key)
        {
            Load();
            if (!_texts.Remove(key)) return false;
            _keys.Remove(key);
            Version++;
            return true;
        }

        /// <summary>The language the game starts in ("" for the device's). Null, or why not.</summary>
        public static string? SetStart(string language)
        {
            Load();
            string code = Subtag(language);
            if (code.Length > 0 && !_languages.Contains(code)) return $"'{language}' is not one of the table's languages ({string.Join(", ", _languages)})";
            _start = code;
            Version++;
            return null;
        }

        private static bool IsLetters(string s)
        {
            foreach (char c in s) if (c < 'a' || c > 'z') return false;
            return true;
        }

        // ── The font's letters ──────────────────────────────────────────────────────────

        private static readonly HashSet<string> _told = new(StringComparer.Ordinal);

        /// <summary>
        /// When a game starts: a text by key with letters the engine's font does not have (it has Latin, Greek and
        /// Cyrillic), on an element with no font of its own, is told in the Console — once a key and a language.
        /// </summary>
        public static void CheckFonts(ECSWorld world)
        {
            Load();
            if (_keys.Count == 0) return;
            foreach (var e in world.Entities)
            {
                string key, font;
                if (world.TryGetComponent<UIText>(e, out var t)) (key, font) = (t.Key ?? "", t.Font ?? "");
                else if (world.TryGetComponent<UIButton>(e, out var b)) (key, font) = (b.Key ?? "", b.Font ?? "");
                else continue;
                if (key.Length == 0 || font.Length > 0 || !_texts.TryGetValue(key, out var row)) continue;
                foreach (var (language, text) in row)
                {
                    if (!HasForeignLetters(text) || !_told.Add(key + "\n" + language)) continue;
                    string name = world.TryGetComponent<NameTag>(e, out var tag) ? tag.Name : "an element";
                    Logger.Warning($"[Texts] '{key}' in {language} has letters the engine's font does not have ({name}): give the element a font with them (Fonts/….ttf).");
                }
            }
        }

        /// <summary>Letters outside Latin, Greek and Cyrillic (and the punctuation and symbols around them).</summary>
        public static bool HasForeignLetters(string text)
        {
            foreach (char c in text)
            {
                if (c < 0x0250) continue;                       // Basic Latin, Latin-1, Latin Extended A/B
                if (c >= 0x0370 && c <= 0x052F) continue;       // Greek, Cyrillic
                if (c >= 0x1E00 && c <= 0x1FFF) continue;       // Latin and Greek extended
                if (c >= 0x2000 && c <= 0x206F) continue;       // general punctuation
                if (char.IsLetter(c)) return true;
            }
            return false;
        }
    }
}
