namespace Sokol.GUI;

/// <summary>Represents spacing around the four edges of a widget (margin or padding).</summary>
public readonly record struct Thickness(float Left, float Top, float Right, float Bottom)
{
    /// <summary>Uniform thickness on all sides.</summary>
    public Thickness(float all) : this(all, all, all, all) { }

    /// <summary>Symmetric horizontal and vertical thickness.</summary>
    public Thickness(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }

    public static readonly Thickness Zero = new(0f);

    public float Horizontal => Left + Right;
    public float Vertical   => Top + Bottom;

    public static Thickness operator +(Thickness a, Thickness b) =>
        new(a.Left + b.Left, a.Top + b.Top, a.Right + b.Right, a.Bottom + b.Bottom);

    public static Thickness operator *(Thickness t, float s) =>
        new(t.Left * s, t.Top * s, t.Right * s, t.Bottom * s);

    public override string ToString() => $"({Left}, {Top}, {Right}, {Bottom})";
}
