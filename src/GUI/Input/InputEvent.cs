namespace Sokol.GUI;

public abstract class InputEvent
{
    public bool Handled { get; set; }
    public KeyModifiers Modifiers { get; init; }
}
