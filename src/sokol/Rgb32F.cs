// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace Sokol;

/// <inheritdoc />
/// <summary>
///     A pixel color value type with 32 bits each for the 3 float components: red, green, and blue.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Rgb32F" /> is mutable on purpose for easier use when working with the components directly.
///     </para>
///     <para>
///         <see cref="Rgb32F" /> is blittable.
///     </para>
/// </remarks>

public partial struct Rgb32F : IEquatable<Rgb32F>
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
    ///     Initializes a new instance of the <see cref="Rgb32F" /> structure using float values.
    /// </summary>
    /// <param name="r">The red component value.</param>
    /// <param name="g">The green component value.</param>
    /// <param name="b">The blue component value.</param>
    public Rgb32F(float r, float g, float b)
    {
        R = r;
        G = g;
        B = b;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgb32F" /> struct using a <see cref="uint" /> packed RGB value.
    /// </summary>
    /// <param name="value">The <see cref="uint" /> packed value with the format 0x00RRGGBB.</param>
    public Rgb32F(uint value)
    {
        R = ((value >> 16) & 0xFF) / 255f;
        G = ((value >> 8) & 0xFF) / 255f;
        B = (value & 0xFF) / 255f;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgb32F" /> struct using a hexadecimal <see cref="uint" /> packed RGB
    ///     value.
    /// </summary>
    /// <param name="value">The <see cref="string" /> packed value with the format 0x00RRGGBB.</param>
    public Rgb32F(string value)
    {
        var span = value.AsSpan();
        if (span[0] == '#')
        {
            span = span.Slice(1);
        }

        if (!uint.TryParse(span, NumberStyles.HexNumber, null, out var u))
        {
            throw new ArgumentException($"Failed to parse the hex rgb '{value}' as an unsigned 32-bit integer.");
        }

        u = uint.Parse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        R = ((u >> 16) & 0xFF) / 255f;
        G = ((u >> 8) & 0xFF) / 255f;
        B = (u & 0xFF) / 255f;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgb32F" /> structure using a <see cref="Vector3" /> interpreted as a
    ///     RGB pixel color.
    /// </summary>
    /// <param name="vector3">The vector value.</param>
    public Rgb32F(Vector3 vector3)
    {
        R = vector3.X;
        G = vector3.Y;
        B = vector3.Z;
    }

    /// <summary>
    ///     Compares two <see cref="Rgb32F" /> structs for equality.
    /// </summary>
    /// <param name="a">The first <see cref="Rgb32F" /> struct.</param>
    /// <param name="b">The second <see cref="Rgb32F" /> struct.</param>
    /// <returns><c>true</c> if <paramref name="a" /> and <paramref name="b" /> are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Rgb32F a, Rgb32F b)
    {
        return a.Equals(b);
    }

    /// <summary>
    ///     Compares two <see cref="Rgb32F" /> structs for inequality.
    /// </summary>
    /// <param name="a">The first <see cref="Rgb32F" /> struct.</param>
    /// <param name="b">The second <see cref="Rgb32F" /> struct.</param>
    /// <returns><c>true</c> if <paramref name="a" /> and <paramref name="b" /> are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Rgb32F a, Rgb32F b)
    {
        return !(a == b);
    }

    /// <summary>
    ///     Vector subtraction of two <see cref="Rgb32F" /> structs.
    /// </summary>
    /// <param name="b">The first <see cref="Rgb32F" /> struct.</param>
    /// <param name="a">The second <see cref="Rgb32F" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgb32F" /> struct resulting from vector subtraction of <paramref name="b" /> from
    ///     <paramref name="a" />.
    /// </returns>
    public static Rgb32F operator -(Rgb32F b, Rgb32F a)
    {
        var red = Math.Max(b.R - a.R, 0);
        var green = Math.Max(b.G - a.G, 0);
        var blue = Math.Max(b.B - a.B, 0);
        return new Rgb32F(red, green, blue);
    }

    /// <summary>
    ///     Vector subtraction of two <see cref="Rgb32F" /> structs.
    /// </summary>
    /// <param name="b">The first <see cref="Rgb32F" /> struct.</param>
    /// <param name="a">The second <see cref="Rgb32F" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgb32F" /> struct resulting from vector subtraction of <paramref name="b" /> from
    ///     <paramref name="a" />.
    /// </returns>
    public static Rgb32F Subtract(Rgb32F b, Rgb32F a)
    {
        var red = Math.Max(b.R - a.R, 0);
        var green = Math.Max(b.G - a.G, 0);
        var blue = Math.Max(b.B - a.B, 0);
        return new Rgb32F(red, green, blue);
    }

    /// <summary>
    ///     Vector addition of two <see cref="Rgb32F" /> structs.
    /// </summary>
    /// <param name="a">The first <see cref="Rgb32F" /> struct.</param>
    /// <param name="b">The second <see cref="Rgb32F" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgb32F" /> struct resulting from vector addition of <paramref name="a" /> and <paramref name="b" />.
    /// </returns>
    public static Rgb32F operator +(Rgb32F a, Rgb32F b)
    {
        var red = a.R + b.R;
        var green = a.G + b.G;
        var blue = a.B + b.B;
        return new Rgb32F(red, green, blue);
    }

    /// <summary>
    ///     Vector addition of two <see cref="Rgb32F" /> structs.
    /// </summary>
    /// <param name="a">The first <see cref="Rgb32F" /> struct.</param>
    /// <param name="b">The second <see cref="Rgb32F" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgb32F" /> struct resulting from vector addition of <paramref name="a" /> and <paramref name="b" />.
    /// </returns>
    public static Rgb32F Add(Rgb32F a, Rgb32F b)
    {
        var red = a.R + b.R;
        var green = a.G + b.G;
        var blue = a.B + b.B;
        return new Rgb32F(red, green, blue);
    }

    /// <summary>
    ///     Implicit conversion from <see cref="uint" /> to <see cref="Rgb32F" /> using the <see cref="Rgb32F(uint)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="uint" />.</param>
    /// <returns> The <see cref="Rgb32F" /> struct resulting from converting <paramref name="value" />.</returns>
    public static implicit operator Rgb32F(uint value)
    {
        return new Rgb32F(value);
    }

    /// <summary>
    ///     Conversion from <see cref="uint" /> to <see cref="Rgb32F" /> using the <see cref="Rgb32F(uint)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="uint" />.</param>
    /// <returns> The <see cref="Rgb32F" /> struct resulting from converting <paramref name="value" />.</returns>
    public static Rgb32F ToRgb32F(uint value)
    {
        return new Rgb32F(value);
    }

    /// <summary>
    ///     Implicit conversion from <see cref="string" /> to <see cref="Rgb32F" /> using the <see cref="Rgb32F(string)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="string" />.</param>
    /// <returns> The <see cref="Rgb32F" /> struct resulting from converting <paramref name="value" />.</returns>
    public static implicit operator Rgb32F(string value)
    {
        return new Rgb32F(value);
    }

    /// <summary>
    ///     Implicit conversion from <see cref="Vector3" /> to <see cref="Rgb32F" /> using the <see cref="Rgb32F(Vector3)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="string" />.</param>
    /// <returns> The <see cref="Rgb32F" /> struct resulting from converting <paramref name="value" />.</returns>
    public static implicit operator Rgb32F(Vector3 value)
    {
        return new Rgb32F(value);
    }

    /// <inheritdoc />
    public readonly override string ToString()
    {
        return $"R:{R}, G:{G}, B:{B}";
    }

    /// <inheritdoc />
    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode", Justification = "Mutable value type.")]
    public readonly override int GetHashCode()
    {
        return HashCode.Combine(R, G, B);
    }

    /// <inheritdoc />
    public bool Equals(Rgb32F other)
    {
        return R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is Rgb32F other && Equals(other);
    }

    public static implicit operator SG.sg_color(Rgb32F rgb32f)
    {
        return new SG.sg_color() { r = rgb32f.R, g = rgb32f.G, b = rgb32f.B, a = 1 };
    }
}
