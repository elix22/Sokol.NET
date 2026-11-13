// ScriptValue.cs — a script field's value from a scene file, with no reflection.
//
// A built game is compiled ahead of time, where a field cannot be found by its name. The builder therefore writes,
// for each script, a setter that names every field in plain code (Source/Generated/RegisteredScripts.g.cs):
//
//     case "Speed": return ScriptValue.Set(ref s.Speed, value);
//
// and the compiler picks the overload by the field's type. Each overload reads a text with the same parser the
// editor's Play uses (GameBehaviour.ParseFieldValue), so a text is one value everywhere.

using System;
using System.Numerics;
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
        public ScriptFieldValue(string text) { Text = text; Entity = default; }
        public ScriptFieldValue(Entity entity) { Text = ""; Entity = entity; }
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

        // typeof(T) is known where this is compiled: a comparison of types, not a search by name.
        static bool Take<T>(ref T field, string text) where T : struct
        {
            if (GameBehaviour.ParseFieldValue(typeof(T), text) is not T parsed) return false;
            field = parsed;
            return true;
        }
    }
}
