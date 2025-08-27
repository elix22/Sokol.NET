// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Sokol;

/// <inheritdoc />
/// <summary>
///     A pixel color value type with 8 bits each for the 4 un-signed integer components: red, green, blue, and
///     alpha.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Rgba8U" /> is mutable on purpose for easier use when working with the components directly.
///     </para>
///     <para>
///         <see cref="Rgba8U" /> is blittable.
///     </para>
/// </remarks>

public partial struct Rgba8U : IEquatable<Rgba8U>
{
    /// <summary>
    ///     The red component value.
    /// </summary>
    public byte R;

    /// <summary>
    ///     The green component value.
    /// </summary>
    public byte G;

    /// <summary>
    ///     The blue component value.
    /// </summary>
    public byte B;

    /// <summary>
    ///     The alpha component value.
    /// </summary>
    public byte A;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgba8U" /> structure using specified <see cref="byte" /> values.
    /// </summary>
    /// <param name="r">The red component value.</param>
    /// <param name="g">The green component value.</param>
    /// <param name="b">The blue component value.</param>
    /// <param name="a">The alpha component value.</param>
    public Rgba8U(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgba8U" /> struct using a <see cref="uint" /> packed RGBA value.
    /// </summary>
    /// <param name="value">The <see cref="uint" /> packed value with the format 0xRRGGBBAA.</param>
    public Rgba8U(uint value)
    {
        R = (byte)((value >> 24) & 0xFF);
        G = (byte)((value >> 16) & 0xFF);
        B = (byte)((value >> 8) & 0xFF);
        A = (byte)(value & 0xFF);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgba8U" /> struct using a hexadecimal <see cref="uint" /> packed RGBA
    ///     value.
    /// </summary>
    /// <param name="value">The <see cref="string" /> packed value with the format 0xRRGGBBAA.</param>
    public Rgba8U(string value)
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

        R = (byte)((u >> 24) & 0xFF);
        G = (byte)((u >> 16) & 0xFF);
        B = (byte)((u >> 8) & 0xFF);
        A = (byte)(u & 0xFF);
    }

    /// <summary>
    ///     Compares two <see cref="Rgba8U" /> structs for equality.
    /// </summary>
    /// <param name="a">The first <see cref="Rgba8U" /> struct.</param>
    /// <param name="b">The second <see cref="Rgba8U" /> struct.</param>
    /// <returns><c>true</c> if <paramref name="a" /> and <paramref name="b" /> are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Rgba8U a, Rgba8U b)
    {
        return a.Equals(b);
    }

    /// <summary>
    ///     Compares two <see cref="Rgba8U" /> structs for inequality.
    /// </summary>
    /// <param name="a">The first <see cref="Rgba8U" /> struct.</param>
    /// <param name="b">The second <see cref="Rgba8U" /> struct.</param>
    /// <returns><c>true</c> if <paramref name="a" /> and <paramref name="b" /> are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Rgba8U a, Rgba8U b)
    {
        return !(a == b);
    }

    /// <summary>
    ///     Vector subtraction of two <see cref="Rgba8U" /> structs.
    /// </summary>
    /// <param name="b">The first <see cref="Rgba8U" /> struct.</param>
    /// <param name="a">The second <see cref="Rgba8U" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgba8U" /> struct resulting from vector subtraction of <paramref name="b" /> from
    ///     <paramref name="a" />.
    /// </returns>
    public static Rgba8U operator -(Rgba8U b, Rgba8U a)
    {
        var red = (byte)Math.Max(b.R - a.R, 0);
        var green = (byte)Math.Max(b.G - a.G, 0);
        var blue = (byte)Math.Max(b.B - a.B, 0);
        var alpha = (byte)Math.Max(b.A - a.A, 0);
        return new Rgba8U(red, green, blue, alpha);
    }

    /// <summary>
    ///     Vector subtraction of two <see cref="Rgba8U" /> structs.
    /// </summary>
    /// <param name="b">The first <see cref="Rgba8U" /> struct.</param>
    /// <param name="a">The second <see cref="Rgba8U" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgba8U" /> struct resulting from vector subtraction of <paramref name="b" /> from
    ///     <paramref name="a" />.
    /// </returns>
    public static Rgba8U Subtract(Rgba8U b, Rgba8U a)
    {
        var red = (byte)Math.Max(b.R - a.R, 0);
        var green = (byte)Math.Max(b.G - a.G, 0);
        var blue = (byte)Math.Max(b.B - a.B, 0);
        var alpha = (byte)Math.Max(b.A - a.A, 0);
        return new Rgba8U(red, green, blue, alpha);
    }

    /// <summary>
    ///     Vector addition of two <see cref="Rgba8U" /> structs.
    /// </summary>
    /// <param name="a">The first <see cref="Rgba8U" /> struct.</param>
    /// <param name="b">The second <see cref="Rgba8U" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgba8U" /> struct resulting from vector addition of <paramref name="a" /> and <paramref name="b" />.
    /// </returns>
    public static Rgba8U operator +(Rgba8U a, Rgba8U b)
    {
        var red = (byte)(a.R + b.R);
        var green = (byte)(a.G + b.G);
        var blue = (byte)(a.B + b.B);
        var alpha = (byte)(a.A + b.A);
        return new Rgba8U(red, green, blue, alpha);
    }

    /// <summary>
    ///     Vector addition of two <see cref="Rgba8U" /> structs.
    /// </summary>
    /// <param name="a">The first <see cref="Rgba8U" /> struct.</param>
    /// <param name="b">The second <see cref="Rgba8U" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgba8U" /> struct resulting from vector addition of <paramref name="a" /> and <paramref name="b" />.
    /// </returns>
    public static Rgba8U Add(Rgba8U a, Rgba8U b)
    {
        var red = (byte)(a.R + b.R);
        var green = (byte)(a.G + b.G);
        var blue = (byte)(a.B + b.B);
        var alpha = (byte)(a.A + b.A);
        return new Rgba8U(red, green, blue, alpha);
    }

    /// <summary>
    ///     Implicit conversion from <see cref="uint" /> to <see cref="Rgba8U" /> using the <see cref="Rgba8U(uint)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="uint" />.</param>
    /// <returns> The <see cref="Rgba8U" /> struct resulting from converting <paramref name="value" />.</returns>
    public static implicit operator Rgba8U(uint value)
    {
        return new Rgba8U(value);
    }

    /// <summary>
    ///     Conversion from <see cref="uint" /> to <see cref="Rgba8U" /> using the <see cref="Rgba8U(uint)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="uint" />.</param>
    /// <returns> The <see cref="Rgba8U" /> struct resulting from converting <paramref name="value" />.</returns>
    public static Rgba8U ToRgba8U(uint value)
    {
        return new Rgba8U(value);
    }

    /// <summary>
    ///     Implicit conversion from <see cref="string" /> to <see cref="Rgba8U" /> using the <see cref="Rgba8U(string)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="string" />.</param>
    /// <returns> The <see cref="Rgba8U" /> struct resulting from converting <paramref name="value" />.</returns>
    public static implicit operator Rgba8U(string value)
    {
        return new Rgba8U(value);
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
    public bool Equals(Rgba8U other)
    {
        return R == other.R && G == other.G && B == other.B && A == other.A;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is Rgba8U other && Equals(other);
    }

    public static implicit operator SG.sg_color(Rgba8U rgba8u)
    {
        return new SG.sg_color() { r = rgba8u.R, g = rgba8u.G, b = rgba8u.B, a = rgba8u.A };
    }
}
