// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;


namespace Sokol;

/// <inheritdoc />
/// <summary>
///     A pixel color value type with 32 bits each for the 4 float components: red, green, blue, and alpha.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Rgba32F" /> is mutable on purpose for easier use when working with the components directly.
///     </para>
///     <para>
///         <see cref="Rgba32F" /> is blittable.
///     </para>
/// </remarks>

[StructLayout(LayoutKind.Sequential)]
public partial struct Rgba32F : IEquatable<Rgba32F>
{
    /// <summary>
    ///     The red component value.
    /// </summary>
    public float R;

    /// <summary>
    ///     The green component value.
    /// </summary>
    public float G;

    /// <summary>
    ///     The blue component value.
    /// </summary>
    public float B;

    /// <summary>
    ///     The alpha component value.
    /// </summary>
    public float A;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgba32F" /> structure using float values.
    /// </summary>
    /// <param name="r">The red component value.</param>
    /// <param name="g">The green component value.</param>
    /// <param name="b">The blue component value.</param>
    /// <param name="a">The alpha component value.</param>
    public Rgba32F(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgba32F" /> struct using a <see cref="uint" /> packed RGBA value.
    /// </summary>
    /// <param name="value">The <see cref="uint" /> packed value with the format 0xRRGGBBAA.</param>
    public Rgba32F(uint value)
    {
        R = ((value >> 24) & 0xFF) / 255f;
        G = ((value >> 16) & 0xFF) / 255f;
        B = ((value >> 8) & 0xFF) / 255f;
        A = (value & 0xFF) / 255f;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgba32F" /> struct using a hexadecimal <see cref="uint" /> packed RGBA
    ///     value.
    /// </summary>
    /// <param name="value">The <see cref="string" /> packed value with the format 0xRRGGBBAA.</param>
    public Rgba32F(string value)
    {
        var span = value.AsSpan();
        if (span[0] == '#')
        {
            span = span.Slice(1);
        }

        if (!uint.TryParse(span, NumberStyles.HexNumber, null, out var u))
        {
            throw new ArgumentException($"Failed to parse the hex rgba '{value}' as an unsigned 32-bit integer.");
        }

        u = uint.Parse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        R = ((u >> 24) & 0xFF) / 255f;
        G = ((u >> 16) & 0xFF) / 255f;
        B = ((u >> 8) & 0xFF) / 255f;
        A = (u & 0xFF) / 255f;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgba32F" /> structure using a <see cref="Vector4" />
    ///     interpreted as a RGBA pixel color.
    /// </summary>
    /// <param name="vector4">The vector value.</param>
    public Rgba32F(Vector4 vector4)
    {
        R = vector4.X;
        G = vector4.Y;
        B = vector4.Z;
        A = vector4.W;
    }

    /// <summary>
    ///     Compares two <see cref="Rgba32F" /> structs for equality.
    /// </summary>
    /// <param name="a">The first <see cref="Rgba32F" /> struct.</param>
    /// <param name="b">The second <see cref="Rgba32F" /> struct.</param>
    /// <returns><c>true</c> if <paramref name="a" /> and <paramref name="b" /> are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Rgba32F a, Rgba32F b)
    {
        return a.Equals(b);
    }

    /// <summary>
    ///     Compares two <see cref="Rgba32F" /> structs for inequality.
    /// </summary>
    /// <param name="a">The first <see cref="Rgba32F" /> struct.</param>
    /// <param name="b">The second <see cref="Rgba32F" /> struct.</param>
    /// <returns><c>true</c> if <paramref name="a" /> and <paramref name="b" /> are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Rgba32F a, Rgba32F b)
    {
        return !(a == b);
    }

    /// <summary>
    ///     Vector subtraction of two <see cref="Rgba32F" /> structs.
    /// </summary>
    /// <param name="b">The first <see cref="Rgba32F" /> struct.</param>
    /// <param name="a">The second <see cref="Rgba32F" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgba32F" /> struct resulting from vector subtraction of <paramref name="b" /> from
    ///     <paramref name="a" />.
    /// </returns>
    public static Rgba32F operator -(Rgba32F b, Rgba32F a)
    {
        var red = Math.Max(b.R - a.R, 0);
        var green = Math.Max(b.G - a.G, 0);
        var blue = Math.Max(b.B - a.B, 0);
        var alpha = Math.Max(b.A - a.A, 0);
        return new Rgba32F(red, green, blue, alpha);
    }

    /// <summary>
    ///     Vector subtraction of two <see cref="Rgba32F" /> structs.
    /// </summary>
    /// <param name="b">The first <see cref="Rgba32F" /> struct.</param>
    /// <param name="a">The second <see cref="Rgba32F" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgba32F" /> struct resulting from vector subtraction of <paramref name="b" /> from
    ///     <paramref name="a" />.
    /// </returns>
    public static Rgba32F Subtract(Rgba32F b, Rgba32F a)
    {
        var red = Math.Max(b.R - a.R, 0);
        var green = Math.Max(b.G - a.G, 0);
        var blue = Math.Max(b.B - a.B, 0);
        var alpha = Math.Max(b.A - a.A, 0);
        return new Rgba32F(red, green, blue, alpha);
    }

    /// <summary>
    ///     Vector addition of two <see cref="Rgba32F" /> structs.
    /// </summary>
    /// <param name="a">The first <see cref="Rgba32F" /> struct.</param>
    /// <param name="b">The second <see cref="Rgba32F" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgba32F" /> struct resulting from vector addition of <paramref name="a" /> and
    ///     <paramref name="b" />.
    /// </returns>
    public static Rgba32F operator +(Rgba32F a, Rgba32F b)
    {
        var red = a.R + b.R;
        var green = a.G + b.G;
        var blue = a.B + b.B;
        var alpha = a.A + b.A;
        return new Rgba32F(red, green, blue, alpha);
    }

    /// <summary>
    ///     Vector addition of two <see cref="Rgba32F" /> structs.
    /// </summary>
    /// <param name="a">The first <see cref="Rgba32F" /> struct.</param>
    /// <param name="b">The second <see cref="Rgba32F" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgba32F" /> struct resulting from vector addition of <paramref name="a" /> and
    ///     <paramref name="b" />.
    /// </returns>
    public static Rgba32F Add(Rgba32F a, Rgba32F b)
    {
        var red = a.R + b.R;
        var green = a.G + b.G;
        var blue = a.B + b.B;
        var alpha = a.A + b.A;
        return new Rgba32F(red, green, blue, alpha);
    }

    /// <summary>
    ///     Implicit conversion from <see cref="uint" /> to <see cref="Rgba32F" /> using the <see cref="Rgba32F(uint)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="uint" />.</param>
    /// <returns> The <see cref="Rgba32F" /> struct resulting from converting <paramref name="value" />.</returns>
    public static implicit operator Rgba32F(uint value)
    {
        return new Rgba32F(value);
    }

    /// <summary>
    ///     Implicit conversion from <see cref="uint" /> to <see cref="Rgba32F" /> using the <see cref="Rgba32F(uint)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="uint" />.</param>
    /// <returns> The <see cref="Rgba32F" /> struct resulting from converting <paramref name="value" />.</returns>
    public static Rgba32F ToRgba32F(uint value)
    {
        return new Rgba32F(value);
    }

    /// <summary>
    ///     Conversion from <see cref="string" /> to <see cref="Rgba32F" /> using the <see cref="Rgba32F(string)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="string" />.</param>
    /// <returns> The <see cref="Rgba32F" /> struct resulting from converting <paramref name="value" />.</returns>
    public static implicit operator Rgba32F(string value)
    {
        return new Rgba32F(value);
    }

    /// <summary>
    ///     Implicit conversion from <see cref="Vector4" /> to <see cref="Rgba32F" /> using the <see cref="Rgba32F(Vector4)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="string" />.</param>
    /// <returns> The <see cref="Rgba32F" /> struct resulting from converting <paramref name="value" />.</returns>
    public static implicit operator Rgba32F(Vector4 value)
    {
        return new Rgba32F(value);
    }

    /// <inheritdoc />
    public readonly override string ToString()
    {
        return $"R:{R}, G:{G}, B:{B}, A:{A}";
    }

    /// <inheritdoc />
    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode", Justification = "Mutable value type.")]
    public readonly override int GetHashCode()
    {
        return HashCode.Combine(R, G, B, A);
    }

    /// <inheritdoc />
    public bool Equals(Rgba32F other)
    {
        return R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is Rgba32F other && Equals(other);
    }


    public static implicit operator SG.sg_color(Rgba32F rgba32f)
    {
        return new SG.sg_color() { r = rgba32f.R, g = rgba32f.G, b = rgba32f.B, a = rgba32f.A };
    }
}
