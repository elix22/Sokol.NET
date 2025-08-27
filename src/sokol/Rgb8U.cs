// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;


namespace Sokol;

/// <inheritdoc />
/// <summary>
///     A pixel color value type with 8 bits each for the 3 un-signed integer components: red, green, and blue.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Rgb8U" /> is mutable on purpose for easier use when working with the components directly.
///     </para>
///     <para>
///         <see cref="Rgb8U" /> is blittable.
///     </para>
/// </remarks>

[StructLayout(LayoutKind.Sequential)]
public partial struct Rgb8U : IEquatable<Rgb8U>
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
    ///     Initializes a new instance of the <see cref="Rgb8U" /> struct using <see cref="byte" /> values.
    /// </summary>
    /// <param name="r">The red component value.</param>
    /// <param name="g">The green component value.</param>
    /// <param name="b">The blue component value.</param>
    public Rgb8U(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgb8U" /> struct using a <see cref="uint" /> packed RGB value.
    /// </summary>
    /// <param name="value">The <see cref="uint" /> packed value with the format 0x00RRGGBB.</param>
    public Rgb8U(uint value)
    {
        R = (byte)((value >> 16) & 0xFF);
        G = (byte)((value >> 8) & 0xFF);
        B = (byte)(value & 0xFF);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgb8U" /> struct using a hexadecimal <see cref="uint" /> packed RGB
    ///     value.
    /// </summary>
    /// <param name="value">The <see cref="string" /> packed value with the format 0x00RRGGBB.</param>
    public Rgb8U(string value)
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

        R = (byte)((u >> 16) & 0xFF);
        G = (byte)((u >> 8) & 0xFF);
        B = (byte)(u & 0xFF);
    }

    /// <summary>
    ///     Compares two <see cref="Rgb8U" /> structs for equality.
    /// </summary>
    /// <param name="a">The first <see cref="Rgb8U" /> struct.</param>
    /// <param name="b">The second <see cref="Rgb8U" /> struct.</param>
    /// <returns><c>true</c> if <paramref name="a" /> and <paramref name="b" /> are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Rgb8U a, Rgb8U b)
    {
        return a.Equals(b);
    }

    /// <summary>
    ///     Compares two <see cref="Rgb8U" /> structs for inequality.
    /// </summary>
    /// <param name="a">The first <see cref="Rgb8U" /> struct.</param>
    /// <param name="b">The second <see cref="Rgb8U" /> struct.</param>
    /// <returns><c>true</c> if <paramref name="a" /> and <paramref name="b" /> are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Rgb8U a, Rgb8U b)
    {
        return !(a == b);
    }

    /// <summary>
    ///     Vector subtraction of two <see cref="Rgb8U" /> structs.
    /// </summary>
    /// <param name="b">The first <see cref="Rgb8U" /> struct.</param>
    /// <param name="a">The second <see cref="Rgb8U" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgb8U" /> struct resulting from vector subtraction of <paramref name="b" /> from
    ///     <paramref name="a" />.
    /// </returns>
    public static Rgb8U operator -(Rgb8U b, Rgb8U a)
    {
        var red = (byte)Math.Max(b.R - a.R, 0);
        var green = (byte)Math.Max(b.G - a.G, 0);
        var blue = (byte)Math.Max(b.B - a.B, 0);
        return new Rgb8U(red, green, blue);
    }

    /// <summary>
    ///     Vector subtraction of two <see cref="Rgb8U" /> structs.
    /// </summary>
    /// <param name="b">The first <see cref="Rgb8U" /> struct.</param>
    /// <param name="a">The second <see cref="Rgb8U" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgb8U" /> struct resulting from vector subtraction of <paramref name="b" /> from
    ///     <paramref name="a" />.
    /// </returns>
    public static Rgb8U Subtract(Rgb8U b, Rgb8U a)
    {
        var red = (byte)Math.Max(b.R - a.R, 0);
        var green = (byte)Math.Max(b.G - a.G, 0);
        var blue = (byte)Math.Max(b.B - a.B, 0);
        return new Rgb8U(red, green, blue);
    }

    /// <summary>
    ///     Vector addition of two <see cref="Rgb8U" /> structs.
    /// </summary>
    /// <param name="a">The first <see cref="Rgb8U" /> struct.</param>
    /// <param name="b">The second <see cref="Rgb8U" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgb8U" /> struct resulting from vector addition of <paramref name="a" /> and <paramref name="b" />.
    /// </returns>
    public static Rgb8U operator +(Rgb8U a, Rgb8U b)
    {
        var red = (byte)(a.R + b.R);
        var green = (byte)(a.G + b.G);
        var blue = (byte)(a.B + b.B);
        return new Rgb8U(red, green, blue);
    }

    /// <summary>
    ///     Vector addition of two <see cref="Rgb8U" /> structs.
    /// </summary>
    /// <param name="a">The first <see cref="Rgb8U" /> struct.</param>
    /// <param name="b">The second <see cref="Rgb8U" /> struct.</param>
    /// <returns>
    ///     The <see cref="Rgb8U" /> struct resulting from vector addition of <paramref name="a" /> and <paramref name="b" />.
    /// </returns>
    public static Rgb8U Add(Rgb8U a, Rgb8U b)
    {
        var red = (byte)(a.R + b.R);
        var green = (byte)(a.G + b.G);
        var blue = (byte)(a.B + b.B);
        return new Rgb8U(red, green, blue);
    }

    /// <summary>
    ///     Implicit conversion from <see cref="uint" /> to <see cref="Rgb8U" /> using the <see cref="Rgb8U(uint)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="uint" />.</param>
    /// <returns>The converted <see cref="Rgba8U"/>.</returns>
    public static implicit operator Rgb8U(uint value)
    {
        return new Rgb8U(value);
    }

    /// <summary>
    ///     Conversion from <see cref="uint" /> to <see cref="Rgb8U" /> using the <see cref="Rgb8U(uint)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="uint" />.</param>
    /// <returns>The converted <see cref="Rgba8U"/>.</returns>
    public static Rgb8U ToRgb8U(uint value)
    {
        return new Rgb8U(value);
    }

    /// <summary>
    ///     Implicit conversion from <see cref="string" /> to <see cref="Rgb8U" /> using the <see cref="Rgb8U(string)" />
    ///     constructor.
    /// </summary>
    /// <param name="value">The <see cref="string" />.</param>
    /// <returns>The converted <see cref="Rgba8U"/>.</returns>
    public static implicit operator Rgb8U(string value)
    {
        return new Rgb8U(value);
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
    public bool Equals(Rgb8U other)
    {
        return R == other.R && G == other.G && B == other.B;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is Rgb8U other && Equals(other);
    }

    public static implicit operator SG.sg_color(Rgb8U rgb8u)
    {
        return new SG.sg_color() { r = rgb8u.R, g = rgb8u.G, b = rgb8u.B, a = 1 };
    }
}
