// ScriptValue.cs — a script field's value from a scene file, with no reflection.
//
// A built game is compiled ahead of time, where a field cannot be found by its name. The builder therefore writes,
// for each script, a setter that names every field in plain code (Source/Generated/RegisteredScripts.g.cs):
//
//     case "Speed": return ScriptValue.Set(ref s.Speed, value);
//
// and the compiler picks the overload by the field's type. Each overload reads a text with the same parser the
// editor's Play uses (GameBehaviour.ParseFieldValue), so a text is one value everywhere.
//
// A List<T> or T[] (§8, docs/EDITOR_ENHANCEMENTS_DESIGN.md) is one JSON array in its text - "[1,2.5]", "[[0,0,1],[2,0,1]]",
// "[\"Walk\",\"Run\"]" - each element the text a field of T has (Elements). A list of entities has its length there
// ("[null,null]") and its elements as references "Targets[0]", "Targets[1]" … (ScriptFieldValue.Index).

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using Frent;
using GameEditor.Framework.Scene;

namespace GameEditor.Framework.Scripting
{
    /// <summary>What a scene gives one field of a script: a text (a number, a name, a list of numbers …), or — for a
    /// field of type <see cref="Frent.Entity"/> — another entity of the scene.</summary>
    public readonly struct ScriptFieldValue
    {
        public readonly string Text;
        public readonly Entity Entity;
        /// <summary>An entity list's element (the scene's "Targets[2]"): its index; -1 for a whole value.</summary>
        public readonly int Index;
        public ScriptFieldValue(string text) { Text = text; Entity = default; Index = -1; }
        public ScriptFieldValue(Entity entity) { Text = ""; Entity = entity; Index = -1; }
        public ScriptFieldValue(Entity entity, int index) { Text = ""; Entity = entity; Index = index; }
    }

    /// <summary>Sets a script's field: true when the value was one the field's type takes.</summary>
    public delegate bool ScriptFieldSetter(GameBehaviour script, string field, in ScriptFieldValue value);

    public static class ScriptValue
    {
        public static bool Set(ref float field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref double field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref int field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref uint field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref long field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref ulong field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref short field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref byte field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref bool field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref char field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref Vector2 field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref Vector3 field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref Vector4 field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref Quaternion field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref Matrix3x2 field, in ScriptFieldValue value) => Take(ref field, value.Text);
        public static bool Set(ref Matrix4x4 field, in ScriptFieldValue value) => Take(ref field, value.Text);

        public static bool Set(ref string field, in ScriptFieldValue value) { field = value.Text; return true; }

        public static bool Set(ref Prefab field, in ScriptFieldValue value)
        {
            if (GameBehaviour.ParseFieldValue(typeof(Prefab), value.Text) is not Prefab prefab) return false;
            field = prefab;
            return true;
        }

        /// <summary>Another entity of the scene (or none: <see cref="Entity.IsNull"/>).</summary>
        public static bool Set(ref Entity field, in ScriptFieldValue value) { field = value.Entity; return true; }

        /// <summary>An enum by the name of its value ("Walk"; several of a [Flags] enum as "A, B"), whatever the case.</summary>
        public static bool SetEnum<T>(ref T field, in ScriptFieldValue value) where T : struct, Enum
        {
            if (!Enum.TryParse(value.Text, ignoreCase: true, out T parsed)) return false;
            field = parsed;
            return true;
        }

        /// <summary>A list of any type a field may be (a number, a text, a vector, a prefab, an entity …).</summary>
        public static bool Set<T>(ref List<T> field, in ScriptFieldValue value)
        {
            if (value.Index >= 0)
            {
                // An entity list's element: the list made long enough, the element put in its place.
                if (typeof(T) != typeof(Entity)) return false;
                field ??= new List<T>();
                while (field.Count <= value.Index) field.Add(default!);
                field[value.Index] = (T)(object)value.Entity;
                return true;
            }
            if (!Parse(value.Text, out List<T> items)) return false;
            field = items;
            return true;
        }

        /// <summary>An array of any type a field may be.</summary>
        public static bool Set<T>(ref T[] field, in ScriptFieldValue value)
        {
            if (value.Index >= 0)
            {
                if (typeof(T) != typeof(Entity)) return false;
                if (field == null || field.Length <= value.Index) Array.Resize(ref field, value.Index + 1);
                field[value.Index] = (T)(object)value.Entity;
                return true;
            }
            if (!Parse(value.Text, out List<T> items)) return false;
            field = items.ToArray();
            return true;
        }

        /// <summary>A field of a script's type (§6, <c>public Health Target;</c>): the script of that type on the entity the
        /// scene points to (none: null).</summary>
        public static bool SetScript<T>(ref T field, in ScriptFieldValue value) where T : class
        {
            field = value.Entity.IsNull ? null! : ScriptSystem.Find<T>(value.Entity)!;
            return true;
        }

        /// <summary>A list of a script's type: its length from the text, each element the script of the entity at its index.</summary>
        public static bool SetScripts<T>(ref List<T> field, in ScriptFieldValue value) where T : class
        {
            if (value.Index < 0) return Set(ref field, value);
            field ??= new List<T>();
            while (field.Count <= value.Index) field.Add(null!);
            field[value.Index] = value.Entity.IsNull ? null! : ScriptSystem.Find<T>(value.Entity)!;
            return true;
        }

        /// <summary>An array of a script's type.</summary>
        public static bool SetScripts<T>(ref T[] field, in ScriptFieldValue value) where T : class
        {
            if (value.Index < 0) return Set(ref field, value);
            if (field == null || field.Length <= value.Index) Array.Resize(ref field, value.Index + 1);
            field[value.Index] = value.Entity.IsNull ? null! : ScriptSystem.Find<T>(value.Entity)!;
            return true;
        }

        /// <summary>A field of a data asset's type (§6, <c>public EnemyTuning Tuning;</c>): the asset its key names, shared.</summary>
        public static bool SetData<T>(ref T field, in ScriptFieldValue value) where T : class, new()
        {
            field = DataAssets.Get<T>(value.Text)!;
            return true;
        }

        /// <summary>A list of an enum's values, each by its name.</summary>
        public static bool SetEnums<T>(ref List<T> field, in ScriptFieldValue value) where T : struct, Enum
        {
            if (!ParseEnums(value.Text, out List<T> items)) return false;
            field = items;
            return true;
        }

        /// <summary>An array of an enum's values, each by its name.</summary>
        public static bool SetEnums<T>(ref T[] field, in ScriptFieldValue value) where T : struct, Enum
        {
            if (!ParseEnums(value.Text, out List<T> items)) return false;
            field = items.ToArray();
            return true;
        }

        /// <summary>
        /// A list's elements, each the text a field of the element's type has: a number as it is written, true / false,
        /// a string's own text, a vector's "[x,y,z]"; null for a JSON null (an entity list's slot). Null when the text is
        /// no JSON array.
        /// </summary>
        public static List<string?>? Elements(string text)
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
                var list = new List<string?>(doc.RootElement.GetArrayLength());
                foreach (var el in doc.RootElement.EnumerateArray()) list.Add(ElementText(el));
                return list;
            }
            catch (JsonException) { return null; }
        }

        static string? ElementText(JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Null: return null;
                case JsonValueKind.True: return "true";
                case JsonValueKind.False: return "false";
                case JsonValueKind.String: return el.GetString();
                case JsonValueKind.Array:
                {
                    var parts = new List<string>();
                    foreach (var x in el.EnumerateArray()) parts.Add(x.GetRawText());
                    return "[" + string.Join(",", parts) + "]";
                }
                default: return el.GetRawText();
            }
        }

        // The elements read as T: a text as it is, an entity (and a null) as none - set by its reference.
        static bool Parse<T>(string text, out List<T> list)
        {
            list = null!;
            if (Elements(text) is not { } items) return false;
            list = new List<T>(items.Count);
            foreach (string? item in items)
            {
                if (typeof(T) == typeof(Entity) || item == null) { list.Add(default!); continue; }
                if (typeof(T) == typeof(string)) { list.Add((T)(object)item); continue; }
                object? value = GameBehaviour.ParseFieldValue(typeof(T), item);
                if (value is T parsed) list.Add(parsed);
                else if (!typeof(T).IsValueType) list.Add(default!);    // a prefab that is none ("") or not found: an empty slot
                else return false;
            }
            return true;
        }

        static bool ParseEnums<T>(string text, out List<T> list) where T : struct, Enum
        {
            list = null!;
            if (Elements(text) is not { } items) return false;
            list = new List<T>(items.Count);
            foreach (string? item in items)
            {
                if (item == null) { list.Add(default); continue; }
                if (!Enum.TryParse(item, ignoreCase: true, out T parsed)) return false;
                list.Add(parsed);
            }
            return true;
        }

        /// <summary>"Targets[2]": an entity list's element - its field's name and its index; the key itself and -1 otherwise.</summary>
        public static (string Field, int Index) ElementKey(string key)
        {
            int open = key.IndexOf('[');
            if (open > 0 && key.EndsWith(']') &&
                int.TryParse(key.AsSpan(open + 1, key.Length - open - 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index))
                return (key[..open], index);
            return (key, -1);
        }

        // typeof(T) is known where this is compiled: a comparison of types, not a search by name.
        static bool Take<T>(ref T field, string text) where T : struct
        {
            if (GameBehaviour.ParseFieldValue(typeof(T), text) is not T parsed) return false;
            field = parsed;
            return true;
        }
    }
}
