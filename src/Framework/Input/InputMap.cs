using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using GameEditor.Framework.Core;

namespace GameEditor.Framework.Input
{
    /// <summary>
    /// A game's own input actions — axes and buttons with their keys, mouse button and gamepad input — in one table,
    /// <c>Embedded/input.json</c> in the project:
    /// <c>{"actions": {"Steer": {"kind": "axis", "negative": ["A"], "positive": ["D"], "pad": "LeftStickX"},
    /// "Boost": {"kind": "button", "keys": ["Space"], "mouse": 0, "pad": "South"}}}</c>.
    /// <see cref="InputManager.GetAxis"/> and <see cref="InputManager.GetButton"/> read an action of that name before the
    /// built-in names; a game with no table behaves as before. The table is part of the app (the Embedded folder), read
    /// at once on every target; in an editor it is the project's file.
    /// </summary>
    public static class InputMap
    {
        /// <summary>The table's name in the project's Embedded folder.</summary>
        public const string FileName = "input.json";

        /// <summary>The gamepad's axes an axis action may read.</summary>
        public static readonly string[] PadAxes = ["LeftStickX", "LeftStickY", "RightStickX", "RightStickY", "LeftTrigger", "RightTrigger"];

        /// <summary>The gamepad's buttons a button action may read.</summary>
        public static readonly string[] PadButtons = ["South", "East", "West", "North", "DpadUp", "DpadDown", "DpadLeft", "DpadRight",
                                                      "LeftBumper", "RightBumper", "LeftStickButton", "RightStickButton", "Start", "Select"];

        /// <summary>One action of the table.</summary>
        public sealed class Action
        {
            public string Name = "";
            /// <summary>True: an axis (−1…1 from its negative and positive keys); false: a button.</summary>
            public bool IsAxis;
            public Key[] Negative = [], Positive = [], Keys = [];
            /// <summary>A button's mouse button (0 left, 1 right, 2 middle), −1 for none.</summary>
            public int Mouse = -1;
            /// <summary>A gamepad axis (an axis action) or button (a button action); "" for none.</summary>
            public string Pad = "";
        }

        private static readonly Dictionary<string, Action> _actions = new(StringComparer.Ordinal);
        private static readonly List<string> _names = new();
        private static bool _loaded;
        private static string? _loadedFrom;

        /// <summary>Goes up when the table changes.</summary>
        public static int Version { get; private set; }

        /// <summary>The actions' names, in the table's order.</summary>
        public static IReadOnlyList<string> Names { get { Load(); return _names; } }

        /// <summary>An action by its name, or null.</summary>
        public static Action? Find(string name)
        {
            Load();
            return _actions.TryGetValue(name, out var a) ? a : null;
        }

        // ── Reading the input ───────────────────────────────────────────────────────────

        /// <summary>The action's value: an axis −1…1, a button 0 or 1. False when the table has no such action.</summary>
        public static bool TryAxis(string name, out float value)
        {
            value = 0f;
            if (Find(name) is not { } a) return false;
            if (a.IsAxis)
            {
                float v = (AnyHeld(a.Positive) ? 1f : 0f) - (AnyHeld(a.Negative) ? 1f : 0f) + PadAxis(a.Pad);
                value = Math.Clamp(v, -1f, 1f);
            }
            else value = Held(a) ? 1f : 0f;
            return true;
        }

        /// <summary>The action held (an axis: not 0). False when the table has no such action.</summary>
        public static bool TryButton(string name, out bool held)
        {
            held = false;
            if (Find(name) is not { } a) return false;
            held = a.IsAxis ? TryAxis(name, out float v) && v != 0f : Held(a);
            return true;
        }

        /// <summary>The action pressed this frame (one of its keys, or its mouse button, went down). False when the table has no such action.</summary>
        public static bool TryButtonDown(string name, out bool down)
        {
            down = false;
            if (Find(name) is not { } a) return false;
            down = AnyDown(a.Positive) || AnyDown(a.Negative) || AnyDown(a.Keys) || (a.Mouse >= 0 && InputManager.GetMouseButtonDown(a.Mouse));
            return true;
        }

        /// <summary>The action let go this frame. False when the table has no such action.</summary>
        public static bool TryButtonUp(string name, out bool up)
        {
            up = false;
            if (Find(name) is not { } a) return false;
            up = AnyUp(a.Positive) || AnyUp(a.Negative) || AnyUp(a.Keys) || (a.Mouse >= 0 && InputManager.GetMouseButtonUp(a.Mouse));
            return true;
        }

        private static bool Held(Action a) =>
            AnyHeld(a.Keys) || (a.Mouse >= 0 && InputManager.GetMouseButton(a.Mouse)) || PadButton(a.Pad);

        private static bool AnyHeld(Key[] keys) { foreach (var k in keys) if (InputManager.GetKey(k)) return true; return false; }
        private static bool AnyDown(Key[] keys) { foreach (var k in keys) if (InputManager.GetKeyDown(k)) return true; return false; }
        private static bool AnyUp(Key[] keys) { foreach (var k in keys) if (InputManager.GetKeyUp(k)) return true; return false; }

        private static float PadAxis(string pad)
        {
            if (pad.Length == 0) return 0f;
            var g = InputManager.GetGamepadState();
            return pad switch
            {
                "LeftStickX" => g.LeftStick.X, "LeftStickY" => g.LeftStick.Y,
                "RightStickX" => g.RightStick.X, "RightStickY" => g.RightStick.Y,
                "LeftTrigger" => g.LeftTrigger, "RightTrigger" => g.RightTrigger,
                _ => 0f,
            };
        }

        private static bool PadButton(string pad)
        {
            if (pad.Length == 0) return false;
            var g = InputManager.GetGamepadState();
            return pad switch
            {
                "South" => g.South, "East" => g.East, "West" => g.West, "North" => g.North,
                "DpadUp" => g.DpadUp, "DpadDown" => g.DpadDown, "DpadLeft" => g.DpadLeft, "DpadRight" => g.DpadRight,
                "LeftBumper" => g.LeftBumper, "RightBumper" => g.RightBumper,
                "LeftStickButton" => g.LeftStickButton, "RightStickButton" => g.RightStickButton,
                "Start" => g.Start, "Select" => g.Select,
                _ => false,
            };
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
                    catch (IOException ex) { Logger.Warning($"[Input] {path} could not be read: {ex.Message}"); }
                }
            }
            json ??= EmbeddedFiles.ReadText(FileName);
            Read(json ?? "");
        }

        /// <summary>Where a project keeps its input map.</summary>
        public static string PathIn(string projectFolder) => Path.Combine(projectFolder, EmbeddedFiles.Folder, FileName);

        /// <summary>Takes a table's text (an editor, a test); an empty text is an empty table. Null, or what was wrong (the actions read so far kept).</summary>
        public static string? Read(string json)
        {
            _loaded = true;
            _loadedFrom ??= "";
            _actions.Clear(); _names.Clear();
            Version++;
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Object) return null;
                var problems = new List<string>();
                foreach (var p in actions.EnumerateObject())
                {
                    var a = new Action { Name = p.Name };
                    if (p.Value.ValueKind != JsonValueKind.Object) { problems.Add($"'{p.Name}' is not an object"); continue; }
                    a.IsAxis = Str(p.Value, "kind") == "axis";
                    a.Negative = KeysOf(p.Value, "negative", problems, p.Name);
                    a.Positive = KeysOf(p.Value, "positive", problems, p.Name);
                    a.Keys = KeysOf(p.Value, "keys", problems, p.Name);
                    a.Mouse = p.Value.TryGetProperty("mouse", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetInt32() : -1;
                    a.Pad = Str(p.Value, "pad");
                    if (_actions.TryAdd(a.Name, a)) _names.Add(a.Name);
                }
                if (problems.Count > 0)
                {
                    string said = string.Join("; ", problems);
                    Logger.Warning("[Input] The input map: " + said);
                    return said;
                }
                return null;
            }
            catch (JsonException ex)
            {
                Logger.Warning($"[Input] The input map is not JSON: {ex.Message}");
                return ex.Message;
            }
        }

        private static string Str(JsonElement el, string name) =>
            el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        private static Key[] KeysOf(JsonElement el, string name, List<string> problems, string action)
        {
            if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
            var keys = new List<Key>();
            foreach (var k in arr.EnumerateArray())
            {
                string text = k.GetString() ?? "";
                if (ParseKey(text) is { } key) keys.Add(key);
                else problems.Add($"'{action}': '{text}' is no key");
            }
            return keys.ToArray();
        }

        /// <summary>A key by its name ("Space", "A", "LeftArrow"; any case), or null.</summary>
        public static Key? ParseKey(string name)
        {
            name = name.Trim();
            if (name.Length == 0 || char.IsDigit(name[0]) || name[0] == '-') return null;   // a number is no key's name
            return Enum.TryParse(name, true, out Key key) && key != Key.None ? key : null;
        }

        /// <summary>The table as its file has it.</summary>
        public static string Write()
        {
            Load();
            var buffer = new MemoryStream();
            using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteStartObject("actions");
                foreach (string name in _names)
                {
                    var a = _actions[name];
                    w.WriteStartObject(name);
                    w.WriteString("kind", a.IsAxis ? "axis" : "button");
                    if (a.IsAxis)
                    {
                        WriteKeys(w, "negative", a.Negative);
                        WriteKeys(w, "positive", a.Positive);
                    }
                    else
                    {
                        WriteKeys(w, "keys", a.Keys);
                        w.WriteNumber("mouse", a.Mouse);
                    }
                    w.WriteString("pad", a.Pad);
                    w.WriteEndObject();
                }
                w.WriteEndObject();
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
        }

        private static void WriteKeys(Utf8JsonWriter w, string name, Key[] keys)
        {
            w.WriteStartArray(name);
            foreach (var k in keys) w.WriteStringValue(k.ToString());
            w.WriteEndArray();
        }

        // ── Changing the table (an editor) ──────────────────────────────────────────────

        /// <summary>
        /// Adds an action, or changes the fields given of one (null: kept). Keys are names ("A, LeftArrow"). Null, or why not
        /// (nothing changed then).
        /// </summary>
        public static string? Set(string name, string? kind = null, string? negative = null, string? positive = null,
                                  string? keys = null, int? mouse = null, string? pad = null)
        {
            Load();
            name = name.Trim();
            if (name.Length == 0) return "an action needs a name";
            var a = _actions.TryGetValue(name, out var had) ? had : new Action { Name = name };
            if (kind != null && kind != "axis" && kind != "button") return $"'{kind}' is no kind of action: axis or button";
            bool isAxis = kind == null ? a.IsAxis : kind == "axis";
            if (ParseKeys(negative, out var neg) is { } e1) return e1;
            if (ParseKeys(positive, out var pos) is { } e2) return e2;
            if (ParseKeys(keys, out var ks) is { } e3) return e3;
            if (mouse is < -1 or > 2) return $"{mouse} is no mouse button: 0 left, 1 right, 2 middle, -1 none";
            string padName = pad?.Trim() ?? a.Pad;
            if (padName.Length > 0 && Array.IndexOf(isAxis ? PadAxes : PadButtons, padName) < 0)
                return $"'{padName}' is no gamepad {(isAxis ? "axis" : "button")}: {string.Join(", ", isAxis ? PadAxes : PadButtons)}";
            a.IsAxis = isAxis;
            if (neg != null) a.Negative = neg;
            if (pos != null) a.Positive = pos;
            if (ks != null) a.Keys = ks;
            if (mouse != null) a.Mouse = mouse.Value;
            a.Pad = padName;
            if (had == null) { _actions[name] = a; _names.Add(name); }
            Version++;
            return null;
        }

        // "A, LeftArrow" → keys; null text → null (kept). Null, or which name is no key.
        private static string? ParseKeys(string? text, out Key[]? keys)
        {
            keys = null;
            if (text == null) return null;
            var list = new List<Key>();
            foreach (string part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (ParseKey(part) is not { } k) return $"'{part}' is no key (key names: A, Space, LeftArrow, Alpha1, LeftShift …)";
                if (!list.Contains(k)) list.Add(k);
            }
            keys = list.ToArray();
            return null;
        }

        /// <summary>Takes an action out. False when there is no such action.</summary>
        public static bool Remove(string name)
        {
            Load();
            if (!_actions.Remove(name)) return false;
            _names.Remove(name);
            Version++;
            return true;
        }
    }
}
