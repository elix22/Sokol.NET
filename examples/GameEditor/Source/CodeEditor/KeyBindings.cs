// KeyBindings.cs — Editor keyboard shortcut configuration.
//
// Defines KeyChord (key + modifiers) and KeyBindings (action-name → chord map).
// Bindings are persisted as a separate JSON file alongside the editor state.
//
// Usage:
//   if (KeyBindings.IsPressed("Find"))  { ... }
//   KeyBindings.Set("Find", new KeyChord { Key = ImGuiKey.F, Ctrl = true });
//   KeyBindings.Save();   // persist to disk
//   KeyBindings.Load();   // restore from disk

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Imgui;
using static Imgui.ImguiNative;

namespace GameEditor.CodeEditor
{
    // ── KeyChord ──────────────────────────────────────────────────────────────

    /// <summary>One keyboard shortcut: a base key plus optional Ctrl/Shift/Alt modifiers.</summary>
    public readonly struct KeyChord : IEquatable<KeyChord>
    {
        public ImGuiKey Key   { get; init; }
        public bool     Ctrl  { get; init; }
        public bool     Shift { get; init; }
        public bool     Alt   { get; init; }

        /// <summary>Returns <c>true</c> if the chord was just pressed this frame.</summary>
        public unsafe bool IsPressed()
        {
            if (Key == ImGuiKey.None) return false;
            if (!igIsKeyPressed_Bool(Key, false)) return false;
            var io = igGetIO_Nil();
            bool ctrlDown  = (io->KeyMods & ImGuiKey.ImGuiMod_Ctrl)  != 0;
            bool shiftDown = (io->KeyMods & ImGuiKey.ImGuiMod_Shift) != 0;
            bool altDown   = (io->KeyMods & ImGuiKey.ImGuiMod_Alt)   != 0;
            return ctrlDown == Ctrl && shiftDown == Shift && altDown == Alt;
        }

        /// <summary>Human-readable string such as "Ctrl+Shift+F".</summary>
        public override string ToString()
        {
            var parts = new System.Text.StringBuilder();
            if (Ctrl)  parts.Append("Ctrl+");
            if (Shift) parts.Append("Shift+");
            if (Alt)   parts.Append("Alt+");
            parts.Append(KeyName(Key));
            return parts.ToString();
        }

        /// <summary>Parses a chord string produced by <see cref="ToString"/>.</summary>
        public static bool TryParse(string s, out KeyChord chord)
        {
            chord = default;
            if (string.IsNullOrWhiteSpace(s)) return false;
            bool ctrl = false, shift = false, alt = false;
            string rest = s;
            if (rest.StartsWith("Ctrl+",  StringComparison.OrdinalIgnoreCase))  { ctrl  = true; rest = rest[5..]; }
            if (rest.StartsWith("Shift+", StringComparison.OrdinalIgnoreCase))  { shift = true; rest = rest[6..]; }
            if (rest.StartsWith("Alt+",   StringComparison.OrdinalIgnoreCase))  { alt   = true; rest = rest[4..]; }
            if (!TryParseKey(rest, out ImGuiKey key)) return false;
            chord = new KeyChord { Key = key, Ctrl = ctrl, Shift = shift, Alt = alt };
            return true;
        }

        public bool Equals(KeyChord other) =>
            Key == other.Key && Ctrl == other.Ctrl && Shift == other.Shift && Alt == other.Alt;
        public override bool Equals(object? obj) => obj is KeyChord k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(Key, Ctrl, Shift, Alt);

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string KeyName(ImGuiKey k) => k switch
        {
            ImGuiKey.A             => "A",   ImGuiKey.B    => "B",   ImGuiKey.C    => "C",
            ImGuiKey.D             => "D",   ImGuiKey.E    => "E",   ImGuiKey.F    => "F",
            ImGuiKey.G             => "G",   ImGuiKey.H    => "H",   ImGuiKey.I    => "I",
            ImGuiKey.J             => "J",   ImGuiKey.K    => "K",   ImGuiKey.L    => "L",
            ImGuiKey.M             => "M",   ImGuiKey.N    => "N",   ImGuiKey.O    => "O",
            ImGuiKey.P             => "P",   ImGuiKey.Q    => "Q",   ImGuiKey.R    => "R",
            ImGuiKey.S             => "S",   ImGuiKey.T    => "T",   ImGuiKey.U    => "U",
            ImGuiKey.V             => "V",   ImGuiKey.W    => "W",   ImGuiKey.X    => "X",
            ImGuiKey.Y             => "Y",   ImGuiKey.Z    => "Z",
            ImGuiKey._0            => "0",   ImGuiKey._1   => "1",   ImGuiKey._2   => "2",
            ImGuiKey._3            => "3",   ImGuiKey._4   => "4",   ImGuiKey._5   => "5",
            ImGuiKey._6            => "6",   ImGuiKey._7   => "7",   ImGuiKey._8   => "8",
            ImGuiKey._9            => "9",
            ImGuiKey.F1            => "F1",  ImGuiKey.F2   => "F2",  ImGuiKey.F3   => "F3",
            ImGuiKey.F4            => "F4",  ImGuiKey.F5   => "F5",  ImGuiKey.F6   => "F6",
            ImGuiKey.F7            => "F7",  ImGuiKey.F8   => "F8",  ImGuiKey.F9   => "F9",
            ImGuiKey.F10           => "F10", ImGuiKey.F11  => "F11", ImGuiKey.F12  => "F12",
            ImGuiKey.Space         => "Space",
            ImGuiKey.Enter         => "Enter",
            ImGuiKey.Escape        => "Escape",
            ImGuiKey.Tab           => "Tab",
            ImGuiKey.Backspace     => "Backspace",
            ImGuiKey.Delete        => "Delete",
            ImGuiKey.UpArrow       => "Up",
            ImGuiKey.DownArrow     => "Down",
            ImGuiKey.LeftArrow     => "Left",
            ImGuiKey.RightArrow    => "Right",
            ImGuiKey.Home          => "Home",
            ImGuiKey.End           => "End",
            ImGuiKey.PageUp        => "PageUp",
            ImGuiKey.PageDown      => "PageDown",
            ImGuiKey.Slash         => "/",
            ImGuiKey.Apostrophe    => "'",
            ImGuiKey.Period        => ".",
            ImGuiKey.Comma         => ",",
            ImGuiKey.Semicolon     => ";",
            ImGuiKey.Equal         => "=",
            ImGuiKey.LeftBracket   => "[",
            ImGuiKey.RightBracket  => "]",
            ImGuiKey.Backslash     => "\\",
            ImGuiKey.GraveAccent   => "`",
            ImGuiKey.Minus         => "-",
            _                      => k.ToString()
        };

        private static bool TryParseKey(string s, out ImGuiKey key)
        {
            key = s switch
            {
                "A" => ImGuiKey.A, "B" => ImGuiKey.B, "C" => ImGuiKey.C,
                "D" => ImGuiKey.D, "E" => ImGuiKey.E, "F" => ImGuiKey.F,
                "G" => ImGuiKey.G, "H" => ImGuiKey.H, "I" => ImGuiKey.I,
                "J" => ImGuiKey.J, "K" => ImGuiKey.K, "L" => ImGuiKey.L,
                "M" => ImGuiKey.M, "N" => ImGuiKey.N, "O" => ImGuiKey.O,
                "P" => ImGuiKey.P, "Q" => ImGuiKey.Q, "R" => ImGuiKey.R,
                "S" => ImGuiKey.S, "T" => ImGuiKey.T, "U" => ImGuiKey.U,
                "V" => ImGuiKey.V, "W" => ImGuiKey.W, "X" => ImGuiKey.X,
                "Y" => ImGuiKey.Y, "Z" => ImGuiKey.Z,
                "0" => ImGuiKey._0, "1" => ImGuiKey._1, "2" => ImGuiKey._2,
                "3" => ImGuiKey._3, "4" => ImGuiKey._4, "5" => ImGuiKey._5,
                "6" => ImGuiKey._6, "7" => ImGuiKey._7, "8" => ImGuiKey._8,
                "9" => ImGuiKey._9,
                "F1"  => ImGuiKey.F1,  "F2"  => ImGuiKey.F2,  "F3"  => ImGuiKey.F3,
                "F4"  => ImGuiKey.F4,  "F5"  => ImGuiKey.F5,  "F6"  => ImGuiKey.F6,
                "F7"  => ImGuiKey.F7,  "F8"  => ImGuiKey.F8,  "F9"  => ImGuiKey.F9,
                "F10" => ImGuiKey.F10, "F11" => ImGuiKey.F11, "F12" => ImGuiKey.F12,
                "Space"    => ImGuiKey.Space,    "Enter"  => ImGuiKey.Enter,
                "Escape"   => ImGuiKey.Escape,   "Tab"    => ImGuiKey.Tab,
                "Backspace"=> ImGuiKey.Backspace, "Delete" => ImGuiKey.Delete,
                "Up"       => ImGuiKey.UpArrow,   "Down"   => ImGuiKey.DownArrow,
                "Left"     => ImGuiKey.LeftArrow, "Right"  => ImGuiKey.RightArrow,
                "Home"     => ImGuiKey.Home,       "End"   => ImGuiKey.End,
                "PageUp"   => ImGuiKey.PageUp,     "PageDown" => ImGuiKey.PageDown,
                "/"  => ImGuiKey.Slash,    "'"  => ImGuiKey.Apostrophe,
                "."  => ImGuiKey.Period,   ","  => ImGuiKey.Comma,
                ";"  => ImGuiKey.Semicolon,"="  => ImGuiKey.Equal,
                "["  => ImGuiKey.LeftBracket, "]" => ImGuiKey.RightBracket,
                "\\" => ImGuiKey.Backslash, "`"  => ImGuiKey.GraveAccent,
                "-"  => ImGuiKey.Minus,
                _ => ImGuiKey.None
            };
            return key != ImGuiKey.None;
        }
    }

    // ── KeyBindings ───────────────────────────────────────────────────────────

    /// <summary>
    /// Static registry that maps action names to <see cref="KeyChord"/> values.
    /// Defaults match the original hardcoded shortcuts. Override by calling
    /// <see cref="Set"/> and persist with <see cref="Save"/> / <see cref="Load"/>.
    /// </summary>
    public static class KeyBindings
    {
        // ── Action names ─────────────────────────────────────────────────────

        public const string Save          = "Save";
        public const string Find          = "Find";
        public const string Replace       = "Replace";
        public const string GotoLine      = "GotoLine";
        public const string GotoDef       = "GotoDef";
        public const string GotoImpl      = "GotoImpl";
        public const string FindAllRef    = "FindAllRef";
        public const string ToggleComment = "ToggleComment";
        public const string DuplicateLine = "DuplicateLine";
        public const string MoveLineUp    = "MoveLineUp";
        public const string MoveLineDown  = "MoveLineDown";
        public const string TriggerCompletion = "TriggerCompletion";

        // ── Defaults ─────────────────────────────────────────────────────────

        private static readonly Dictionary<string, KeyChord> _defaults = new()
        {
            [Save]              = new KeyChord { Key = ImGuiKey.S,          Ctrl = true  },
            [Find]              = new KeyChord { Key = ImGuiKey.F,          Ctrl = true  },
            [Replace]           = new KeyChord { Key = ImGuiKey.H,          Ctrl = true  },
            [GotoLine]          = new KeyChord { Key = ImGuiKey.G,          Ctrl = true  },
            [GotoDef]           = new KeyChord { Key = ImGuiKey.F12                      },
            [GotoImpl]          = new KeyChord { Key = ImGuiKey.F12,        Ctrl = true  },
            [FindAllRef]        = new KeyChord { Key = ImGuiKey.F12,        Shift = true },
            [ToggleComment]     = new KeyChord { Key = ImGuiKey.Slash,      Ctrl = true  },
            [DuplicateLine]     = new KeyChord { Key = ImGuiKey.D,          Ctrl = true  },
            [MoveLineUp]        = new KeyChord { Key = ImGuiKey.UpArrow,    Alt  = true  },
            [MoveLineDown]      = new KeyChord { Key = ImGuiKey.DownArrow,  Alt  = true  },
            [TriggerCompletion] = new KeyChord { Key = ImGuiKey.Space,      Ctrl = true  },
        };

        private static Dictionary<string, KeyChord> _bindings = new(_defaults);

        private static readonly string _bindingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sokolnet_config",
            "keybindings.json");

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Returns <c>true</c> if the chord for <paramref name="action"/> is pressed this frame.</summary>
        public static bool IsPressed(string action)
            => _bindings.TryGetValue(action, out var chord) && chord.IsPressed();

        /// <summary>Returns the current chord for <paramref name="action"/>.</summary>
        public static KeyChord Get(string action)
            => _bindings.TryGetValue(action, out var c) ? c : default;

        /// <summary>Overrides the chord for <paramref name="action"/>.</summary>
        public static void Set(string action, KeyChord chord)
            => _bindings[action] = chord;

        /// <summary>Resets a single action to its default chord.</summary>
        public static void Reset(string action)
        {
            if (_defaults.TryGetValue(action, out var def))
                _bindings[action] = def;
        }

        /// <summary>Resets all bindings to their defaults.</summary>
        public static void ResetAll() => _bindings = new(_defaults);

        /// <summary>All registered action names.</summary>
        public static IEnumerable<string> Actions => _defaults.Keys;

        /// <summary>Snapshot of all current bindings (action → chord string).</summary>
        public static IReadOnlyDictionary<string, KeyChord> All => _bindings;

        // ── Persistence ──────────────────────────────────────────────────────

        /// <summary>Loads overrides from disk. Missing entries keep their defaults.</summary>
        public static void LoadFromFile()
        {
            try
            {
                if (!File.Exists(_bindingsFile)) return;
                string json = File.ReadAllText(_bindingsFile);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (data == null) return;
                foreach (var (action, chordStr) in data)
                {
                    if (KeyChord.TryParse(chordStr, out var chord))
                        _bindings[action] = chord;
                }
            }
            catch { /* ignore malformed file — keep defaults */ }
        }

        /// <summary>Saves all non-default overrides to disk.</summary>
        public static void SaveToFile()
        {
            try
            {
                var data = new Dictionary<string, string>();
                foreach (var (action, chord) in _bindings)
                {
                    if (_defaults.TryGetValue(action, out var def) && chord.Equals(def))
                        continue; // don't write unchanged defaults
                    data[action] = chord.ToString();
                }
                Directory.CreateDirectory(Path.GetDirectoryName(_bindingsFile)!);
                File.WriteAllText(_bindingsFile, JsonSerializer.Serialize(data,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* ignore write errors */ }
        }
    }
}
