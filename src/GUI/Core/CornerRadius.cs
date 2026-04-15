namespace Sokol.GUI;

/// <summary>Radii for the four corners of a rounded rectangle.</summary>
public readonly record struct CornerRadius(float TopLeft, float TopRight, float BottomRight, float BottomLeft)
{
    /// <summary>Uniform corner radius on all corners.</summary>
    public CornerRadius(float all) : this(all, all, all, all) { }

    public static readonly CornerRadius Zero = new(0f);

    public bool IsUniform => TopLeft == TopRight && TopRight == BottomRight && BottomRight == BottomLeft;

    public override string ToString() => IsUniform ? $"{TopLeft}" : $"({TopLeft}, {TopRight}, {BottomRight}, {BottomLeft})";
}
