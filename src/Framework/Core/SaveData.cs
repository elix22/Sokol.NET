using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using static Sokol.SFilesystem;

namespace GameEditor.Framework.Core
{
    /// <summary>
    /// A game's saved data: named values that are there again the next time the app starts — a best score,
    /// whether sound is on. Values are kept in memory; <see cref="Save"/> writes them, and the engine calls it
    /// when the app goes to the background or quits. One file, save.json, in the app's own folder; in an editor,
    /// in the open project's Saved folder, so that playing there touches no built game's data.
    /// Not for what a player must not be able to change: the file is plain text.
    /// </summary>
    public static class SaveData
    {
        public const string FileName = "save.json";

        // A number, a text, or true / false.
        private static readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);
        private static bool _read, _changed;

        /// <summary>
        /// Reads and writes the file's text. When set, they alone are asked (a test); otherwise the file in
        /// <see cref="Folder"/>. The reader gives null when there is no file.
        /// </summary>
        public static Func<string?>? Reader { get; set; }
        public static Action<string>? Writer { get; set; }

        /// <summary>There are values that <see cref="Save"/> has not written yet.</summary>
        public static bool Changed => _changed;

        /// <summary>
        /// The folder the file is in: the open project's Saved folder in an editor, else the app's own folder (its
        /// name comes from the project's). Empty when the platform gives none.
        /// </summary>
        public static string Folder
        {
            get
            {
                if (ConfigManager.HasProject) return Path.Combine(ConfigManager.ProjectFolder!, "Saved") + Path.DirectorySeparatorChar;
                string app = AppName(ConfigManager.Config?.ProjectName);
                IntPtr ptr = sfs_get_pref_path("SokolNET", app);
                if (ptr == IntPtr.Zero) return "";
                string dir = Marshal.PtrToStringUTF8(ptr) ?? "";
                sfs_free_path(ptr);
                return dir;
            }
        }

        /// <summary>A project's name as a folder's: letters, digits, '-' and '_' kept, anything else '_'; "Game" for none.</summary>
        public static string AppName(string? projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName)) return "Game";
            var sb = new StringBuilder(projectName.Length);
            foreach (char c in projectName.Trim())
                sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_');
            return sb.ToString();
        }

        // ── Values ──────────────────────────────────────────────────────────────────────

        public static bool Has(string key) { Read(); return _values.ContainsKey(key); }

        /// <summary>A whole number; <paramref name="fallback"/> when there is no such value or it is not a number.</summary>
        public static int GetInt(string key, int fallback = 0)
        {
            Read();
            return _values.TryGetValue(key, out object? v) && v is double d && d >= int.MinValue && d <= int.MaxValue ? (int)Math.Round(d) : fallback;
        }

        public static float GetFloat(string key, float fallback = 0f)
        {
            Read();
            return _values.TryGetValue(key, out object? v) && v is double d ? (float)d : fallback;
        }

        public static string GetString(string key, string fallback = "")
        {
            Read();
            return _values.TryGetValue(key, out object? v) && v is string s ? s : fallback;
        }

        public static bool GetBool(string key, bool fallback = false)
        {
            Read();
            return _values.TryGetValue(key, out object? v) && v is bool b ? b : fallback;
        }

        public static void SetInt(string key, int value) => Set(key, (double)value);
        public static void SetFloat(string key, float value) => Set(key, float.IsFinite(value) ? (double)value : 0.0);
        public static void SetString(string key, string value) => Set(key, value ?? "");
        public static void SetBool(string key, bool value) => Set(key, value);

        private static void Set(string key, object value)
        {
            Read();
            if (_values.TryGetValue(key, out object? old) && old.Equals(value)) return;
            _values[key] = value;
            _changed = true;
        }

        /// <summary>Takes one value out.</summary>
        public static void Delete(string key)
        {
            Read();
            if (_values.Remove(key)) _changed = true;
        }

        /// <summary>Takes every value out.</summary>
        public static void Clear()
        {
            Read();
            if (_values.Count == 0) return;
            _values.Clear();
            _changed = true;
        }

        /// <summary>The names of the values, sorted.</summary>
        public static List<string> Keys()
        {
            Read();
            var keys = new List<string>(_values.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        // ── The file ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes the values when any changed since the last time. False when they could not be written (said in
        /// the log; the values stay in memory and the next Save tries again).
        /// </summary>
        public static bool Save()
        {
            if (!_read || !_changed) return true;
            string text = ToJson();
            try
            {
                if (Writer != null) Writer(text);
                else
                {
                    string folder = Folder;
                    if (folder.Length == 0) { Logger.Warning("[SaveData] This platform gives the app no folder of its own: nothing was saved."); return false; }
                    SokolFile.EnsureDirectory(folder.TrimEnd('/', '\\'));
                    // To another name first, then over the file: a write that is cut short leaves the old file.
                    string file = folder + FileName, fresh = file + ".new";
                    SokolFile.WriteAllText(fresh, text);
                    if (SokolFile.Exists(file)) SokolFile.Delete(file);
                    SokolFile.Move(fresh, file);
                }
            }
            catch (IOException e)
            {
                Logger.Warning($"[SaveData] The saved data could not be written: {e.Message}");
                return false;
            }
            _changed = false;
            return true;
        }

        /// <summary>
        /// Forgets what is in memory, so that the next value asked for is read from the file again: another game
        /// is about to play (an editor's Play), or the file was changed from outside.
        /// </summary>
        public static void Forget()
        {
            _values.Clear();
            _read = false;
            _changed = false;
        }

        private static void Read()
        {
            if (_read) return;
            _read = true;
            _values.Clear();
            string? text;
            string file = "";
            try
            {
                if (Reader != null) text = Reader();
                else
                {
                    string folder = Folder;
                    file = folder + FileName;
                    // A write that was cut short after the old file was taken away: the new one is whole.
                    if (folder.Length > 0 && !SokolFile.Exists(file) && SokolFile.Exists(file + ".new")) SokolFile.Move(file + ".new", file);
                    text = folder.Length > 0 && SokolFile.Exists(file) ? SokolFile.ReadAllText(file) : null;
                }
            }
            catch (IOException e)
            {
                Logger.Warning($"[SaveData] The saved data could not be read: {e.Message}");
                return;
            }
            if (text == null) return;
            if (Parse(text)) return;

            Logger.Warning("[SaveData] save.json is not a file of saved data: it is put aside as save.json.bad, and the game starts with no values.");
            try
            {
                if (Reader == null && file.Length > 0)
                {
                    if (SokolFile.Exists(file + ".bad")) SokolFile.Delete(file + ".bad");
                    SokolFile.Move(file, file + ".bad");
                }
            }
            catch (IOException) { }
        }

        private static bool Parse(string text)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("values", out JsonElement values)
                    || values.ValueKind != JsonValueKind.Object) return false;
                foreach (JsonProperty p in values.EnumerateObject())
                    switch (p.Value.ValueKind)
                    {
                        case JsonValueKind.Number: _values[p.Name] = p.Value.GetDouble(); break;
                        case JsonValueKind.String: _values[p.Name] = p.Value.GetString() ?? ""; break;
                        case JsonValueKind.True: _values[p.Name] = true; break;
                        case JsonValueKind.False: _values[p.Name] = false; break;
                    }
                return true;
            }
            catch (JsonException) { return false; }
        }

        private static string Text(string s) => "\"" + JsonEncodedText.Encode(s) + "\"";

        /// <summary>The values as the file holds them: {"save": 1, "values": {"best": 12, "sound": true}}, names sorted.</summary>
        public static string ToJson()
        {
            Read();
            var sb = new StringBuilder("{\n  \"save\": 1,\n  \"values\": {");
            bool first = true;
            foreach (string key in Keys())
            {
                sb.Append(first ? "\n    " : ",\n    ").Append(Text(key)).Append(": ");
                first = false;
                switch (_values[key])
                {
                    case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case string s: sb.Append(Text(s)); break;
                }
            }
            return sb.Append(first ? "}\n}\n" : "\n  }\n}\n").ToString();
        }
    }
}
