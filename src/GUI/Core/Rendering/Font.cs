namespace Sokol.GUI;

/// <summary>A NanoVG font handle.</summary>
public sealed class Font
{
    public string Name  { get; }
    public int    Id    { get; }
    public bool IsValid => Id >= 0;

    internal Font(string name, int id)
    {
        Name = name;
        Id   = id;
    }
}
