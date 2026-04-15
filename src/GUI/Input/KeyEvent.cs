namespace Sokol.GUI;

public sealed class KeyEvent : InputEvent
{
    /// <summary>Sokol key code (sapp_keycode cast to int).</summary>
    public int    KeyCode  { get; init; }
    /// <summary>For TextInput events: the UTF-32 character entered.</summary>
    public uint   CharCode { get; init; }
    public bool   Repeat   { get; init; }
}
