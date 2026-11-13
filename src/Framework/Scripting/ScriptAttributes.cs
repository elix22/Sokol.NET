// ScriptAttributes.cs — what a script says about its public fields, for the editor's Inspector.
//
// A game never reads these: they describe a field's row to the editor (which reads the project's built assembly on
// the desktop). A field's value reaches a built game by a generated setter, with no reflection (ScriptValue.cs).

using System;

namespace GameEditor.Framework.Scripting
{
    /// <summary>The Inspector keeps a number field inside [Min, Max]. It is the Inspector's promise, not the game's:
    /// a value a script sets, or one written into a scene file by hand, is not clamped.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RangeAttribute : Attribute
    {
        public float Min { get; }
        public float Max { get; }
        public RangeAttribute(float min, float max) { Min = min; Max = max; }
    }

    /// <summary>The text the Inspector shows when the pointer rests on the field's row.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute
    {
        public string Text { get; }
        public TooltipAttribute(string text) { Text = text; }
    }

    /// <summary>A heading the Inspector shows above the field's row.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute
    {
        public string Text { get; }
        public HeaderAttribute(string text) { Text = text; }
    }

    /// <summary>The field has no row in the Inspector. A value in the scene file still reaches it.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HideInInspectorAttribute : Attribute { }
}
