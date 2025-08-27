// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Sokol;

public partial struct Rgb32F
{
    // https://en.wikipedia.org/wiki/X11_color_names

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #000000.
    /// </summary>
    public static readonly Rgb32F TransparentBlack = 0x000000;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has an RGB value of #000000.
    /// </summary>
    public static readonly Rgb32F Transparent = 0x000000;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #E73C00.
    /// </summary>
    public static readonly Rgb32F MonoGameOrange = 0xE73C00;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #F0F8FF.
    /// </summary>
    public static readonly Rgb32F AliceBlue = 0xF0F8FF;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FAEBD7.
    /// </summary>
    public static readonly Rgb32F AntiqueWhite = 0xFAEBD7;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #00FFFF.
    /// </summary>
    public static readonly Rgb32F Aqua = 0x00FFFF;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #7FFFD4.
    /// </summary>
    public static readonly Rgb32F Aquamarine = 0x7FFFD4;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #F0FFFF.
    /// </summary>
    public static readonly Rgb32F Azure = 0xF0FFFF;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #F5F5DC.
    /// </summary>
    public static readonly Rgb32F Beige = 0xF5F5DC;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFE4C4.
    /// </summary>
    public static readonly Rgb32F Bisque = 0xFFE4C4;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #000000.
    /// </summary>
    public static readonly Rgb32F Black = 0x000000;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFEBCD.
    /// </summary>
    public static readonly Rgb32F BlanchedAlmond = 0xFFEBCD;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #0000FF.
    /// </summary>
    public static readonly Rgb32F Blue = 0x0000FF;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #8A2BE2.
    /// </summary>
    public static readonly Rgb32F BlueViolet = 0x8A2BE2;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #A52A2A.
    /// </summary>
    public static readonly Rgb32F Brown = 0xA52A2A;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #DEB887.
    /// </summary>
    public static readonly Rgb32F BurlyWood = 0xDEB887;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #5F9EA0.
    /// </summary>
    public static readonly Rgb32F CadetBlue = 0x5F9EA0;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #7FFF00.
    /// </summary>
    public static readonly Rgb32F Chartreuse = 0x7FFF00;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #D2691E.
    /// </summary>
    public static readonly Rgb32F Chocolate = 0xD2691E;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FF7F50.
    /// </summary>
    public static readonly Rgb32F Coral = 0xFF7F50;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #6495ED.
    /// </summary>
    public static readonly Rgb32F CornflowerBlue = 0x6495ED;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFF8DC.
    /// </summary>
    public static readonly Rgb32F Cornsilk = 0xFFF8DC;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #DC143C.
    /// </summary>
    public static readonly Rgb32F Crimson = 0xDC143C;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #00FFFF.
    /// </summary>
    public static readonly Rgb32F Cyan = 0x00FFFF;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #00008B.
    /// </summary>
    public static readonly Rgb32F DarkBlue = 0x00008B;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #008B8B.
    /// </summary>
    public static readonly Rgb32F DarkCyan = 0x008B8B;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #B8860B.
    /// </summary>
    public static readonly Rgb32F DarkGoldenrod = 0xB8860B;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #A9A9A9.
    /// </summary>
    public static readonly Rgb32F DarkGray = 0xA9A9A9;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #006400.
    /// </summary>
    public static readonly Rgb32F DarkGreen = 0x006400;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #A9A9A9.
    /// </summary>
    public static readonly Rgb32F DarkGrey = 0xA9A9A9;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #BDB76B.
    /// </summary>
    public static readonly Rgb32F DarkKhaki = 0xBDB76B;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #8B008B.
    /// </summary>
    public static readonly Rgb32F DarkMagenta = 0x8B008B;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #556B2F.
    /// </summary>
    public static readonly Rgb32F DarkOliveGreen = 0x556B2F;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FF8C00.
    /// </summary>
    public static readonly Rgb32F DarkOrange = 0xFF8C00;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #9932CC.
    /// </summary>
    public static readonly Rgb32F DarkOrchid = 0x9932CC;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #8B0000.
    /// </summary>
    public static readonly Rgb32F DarkRed = 0x8B0000;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #E9967A.
    /// </summary>
    public static readonly Rgb32F DarkSalmon = 0xE9967A;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #8FBC8F.
    /// </summary>
    public static readonly Rgb32F DarkSeaGreen = 0x8FBC8F;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #483D8B.
    /// </summary>
    public static readonly Rgb32F DarkStateBlue = 0x483D8B;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #2F4F4F.
    /// </summary>
    public static readonly Rgb32F DarkStateGray = 0x2F4F4F;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #2F4F4F.
    /// </summary>
    public static readonly Rgb32F DarkStateGrey = 0x2F4F4F;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #00CED1.
    /// </summary>
    public static readonly Rgb32F DarkTurquoise = 0x00CED1;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #9400D3.
    /// </summary>
    public static readonly Rgb32F DarkViolet = 0x9400D3;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FF1493.
    /// </summary>
    public static readonly Rgb32F DeepPink = 0xFF1493;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #00BFFF.
    /// </summary>
    public static readonly Rgb32F DeepSkyBlue = 0x00BFFF;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #696969.
    /// </summary>
    public static readonly Rgb32F DimGray = 0x696969;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #696969.
    /// </summary>
    public static readonly Rgb32F DimGrey = 0x696969;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #1E90FF.
    /// </summary>
    public static readonly Rgb32F DodgerBlue = 0x1E90FF;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #B22222.
    /// </summary>
    public static readonly Rgb32F Firebrick = 0xB22222;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFFAF0.
    /// </summary>
    public static readonly Rgb32F FloralWhite = 0xFFFAF0;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #228B22.
    /// </summary>
    public static readonly Rgb32F ForestGreen = 0x228B22;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FF00FF.
    /// </summary>
    public static readonly Rgb32F Fuchsia = 0xFF00FF;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #DCDCDC.
    /// </summary>
    public static readonly Rgb32F Gainsboro = 0xDCDCDC;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #F8F8FF.
    /// </summary>
    public static readonly Rgb32F GhostWhite = 0xF8F8FF;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFD700.
    /// </summary>
    public static readonly Rgb32F Gold = 0xFFD700;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #DAA520.
    /// </summary>
    public static readonly Rgb32F Goldenrod = 0xDAA520;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #808080.
    /// </summary>
    public static readonly Rgb32F Gray = 0x808080;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #008000.
    /// </summary>
    public static readonly Rgb32F Green = 0x008000;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #ADFF2F.
    /// </summary>
    public static readonly Rgb32F GreenYellow = 0xADFF2F;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #808080.
    /// </summary>
    public static readonly Rgb32F Grey = 0x808080;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #F0FFF0.
    /// </summary>
    public static readonly Rgb32F Honeydew = 0xF0FFF0;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FF69B4.
    /// </summary>
    public static readonly Rgb32F HotPink = 0xFF69B4;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #CD5C5C.
    /// </summary>
    public static readonly Rgb32F IndianRed = 0xCD5C5C;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #4B0082.
    /// </summary>
    public static readonly Rgb32F Indigo = 0x4B0082;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFFFF0.
    /// </summary>
    public static readonly Rgb32F Ivory = 0xFFFFF0;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #F0E68C.
    /// </summary>
    public static readonly Rgb32F Khaki = 0xF0E68C;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #E6E6FA.
    /// </summary>
    public static readonly Rgb32F Lavender = 0xE6E6FA;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFF0F5.
    /// </summary>
    public static readonly Rgb32F LavenderBlush = 0xFFF0F5;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #7CFC00.
    /// </summary>
    public static readonly Rgb32F LawnGreen = 0x7CFC00;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFFACD.
    /// </summary>
    public static readonly Rgb32F LemonChiffon = 0xFFFACD;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #ADD8E6.
    /// </summary>
    public static readonly Rgb32F LightBlue = 0xADD8E6;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #F08080.
    /// </summary>
    public static readonly Rgb32F LightCoral = 0xF08080;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #E0FFFF.
    /// </summary>
    public static readonly Rgb32F LightCyan = 0xE0FFFF;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FAFAD2.
    /// </summary>
    public static readonly Rgb32F LightGoldenrodYellow = 0xFAFAD2;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #D3D3D3.
    /// </summary>
    public static readonly Rgb32F LightGray = 0xD3D3D3;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #90EE90.
    /// </summary>
    public static readonly Rgb32F LightGreen = 0x90EE90;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #D3D3D3.
    /// </summary>
    public static readonly Rgb32F LightGrey = 0xD3D3D3;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFB6C1.
    /// </summary>
    public static readonly Rgb32F LightPink = 0xFFB6C1;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFA07A.
    /// </summary>
    public static readonly Rgb32F LightSalmon = 0xFFA07A;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #20B2AA.
    /// </summary>
    public static readonly Rgb32F LightSeaGreen = 0x20B2AA;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #87CEFA.
    /// </summary>
    public static readonly Rgb32F LightSkyBlue = 0x87CEFA;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #778899.
    /// </summary>
    public static readonly Rgb32F LightSlateGray = 0x778899;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #778899.
    /// </summary>
    public static readonly Rgb32F LightSlateGrey = 0x778899;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #B0C4DE.
    /// </summary>
    public static readonly Rgb32F LightSteelBlue = 0xB0C4DE;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFFFE0.
    /// </summary>
    public static readonly Rgb32F LightYellow = 0xFFFFE0;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #00FF00.
    /// </summary>
    public static readonly Rgb32F Lime = 0x00FF00;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #32CD32.
    /// </summary>
    public static readonly Rgb32F LimeGreen = 0x32CD32;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FAF0E6.
    /// </summary>
    public static readonly Rgb32F Linen = 0xFAF0E6;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FF00FF.
    /// </summary>
    public static readonly Rgb32F Magenta = 0xFF00FF;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #800000.
    /// </summary>
    public static readonly Rgb32F Maroon = 0x800000;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #66CDAA.
    /// </summary>
    public static readonly Rgb32F MediumAquamarine = 0x66CDAA;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #0000CD.
    /// </summary>
    public static readonly Rgb32F MediumBlue = 0x0000CD;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #BA55D3.
    /// </summary>
    public static readonly Rgb32F MediumOrchid = 0xBA55D3;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #9370DB.
    /// </summary>
    public static readonly Rgb32F MediumPurple = 0x9370DB;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #3CB371.
    /// </summary>
    public static readonly Rgb32F MediumSeaGreen = 0x3CB371;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #7B68EE.
    /// </summary>
    public static readonly Rgb32F MediumStateBlue = 0x7B68EE;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #00FA9A.
    /// </summary>
    public static readonly Rgb32F MediumSpringGreen = 0x00FA9A;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #48D1CC.
    /// </summary>
    public static readonly Rgb32F MediumTurquoise = 0x48D1CC;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #C71585.
    /// </summary>
    public static readonly Rgb32F MediumVioletRed = 0xC71585;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #191970.
    /// </summary>
    public static readonly Rgb32F MidnightBlue = 0x191970;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #F5FFFA.
    /// </summary>
    public static readonly Rgb32F MintCream = 0xF5FFFA;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFE4E1.
    /// </summary>
    public static readonly Rgb32F MistyRose = 0xFFE4E1;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFE4B5.
    /// </summary>
    public static readonly Rgb32F Moccasin = 0xFFE4B5;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFDEAD.
    /// </summary>
    public static readonly Rgb32F NavajoWhite = 0xFFDEAD;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #000080.
    /// </summary>
    public static readonly Rgb32F Navy = 0x000080;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FDF5E6.
    /// </summary>
    public static readonly Rgb32F OldLace = 0xFDF5E6;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #808000.
    /// </summary>
    public static readonly Rgb32F Olive = 0x808000;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #6B8E23.
    /// </summary>
    public static readonly Rgb32F OliveDrab = 0x6B8E23;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFA500.
    /// </summary>
    public static readonly Rgb32F Orange = 0xFFA500;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FF4500.
    /// </summary>
    public static readonly Rgb32F OrangeRed = 0xFF4500;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #DA70D6.
    /// </summary>
    public static readonly Rgb32F Orchid = 0xDA70D6;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #EEE8AA.
    /// </summary>
    public static readonly Rgb32F PaleGoldenrod = 0xEEE8AA;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #98FB98.
    /// </summary>
    public static readonly Rgb32F PaleGreen = 0x98FB98;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #AFEEEE.
    /// </summary>
    public static readonly Rgb32F PaleTurquoise = 0xAFEEEE;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #DB7093.
    /// </summary>
    public static readonly Rgb32F PaleVioletRed = 0xDB7093;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFEFD5.
    /// </summary>
    public static readonly Rgb32F PapayaWhip = 0xFFEFD5;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFDAB9.
    /// </summary>
    public static readonly Rgb32F PeachPuff = 0xFFDAB9;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #CD853F.
    /// </summary>
    public static readonly Rgb32F Peru = 0xCD853F;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFC0CB.
    /// </summary>
    public static readonly Rgb32F Pink = 0xFFC0CB;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #DDA0DD.
    /// </summary>
    public static readonly Rgb32F Plum = 0xDDA0DD;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #B0E0E6.
    /// </summary>
    public static readonly Rgb32F PowderBlue = 0xB0E0E6;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #800080.
    /// </summary>
    public static readonly Rgb32F Purple = 0x800080;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FF0000.
    /// </summary>
    public static readonly Rgb32F Red = 0xFF0000;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #BC8F8F.
    /// </summary>
    public static readonly Rgb32F RosyBrown = 0xBC8F8F;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #4169E1.
    /// </summary>
    public static readonly Rgb32F RoyalBlue = 0x4169E1;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #8B4513.
    /// </summary>
    public static readonly Rgb32F SaddleBrown = 0x8B4513;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FA8072.
    /// </summary>
    public static readonly Rgb32F Salmon = 0xFA8072;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #F4A460.
    /// </summary>
    public static readonly Rgb32F SandyBrown = 0xF4A460;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #2E8B57.
    /// </summary>
    public static readonly Rgb32F SeaGreen = 0x2E8B57;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFF5EE.
    /// </summary>
    public static readonly Rgb32F SeaShell = 0xFFF5EE;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #A0522D.
    /// </summary>
    public static readonly Rgb32F Sienna = 0xA0522D;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #C0C0C0.
    /// </summary>
    public static readonly Rgb32F Silver = 0xC0C0C0;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #87CEEB.
    /// </summary>
    public static readonly Rgb32F SkyBlue = 0x87CEEB;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #6A5ACD.
    /// </summary>
    public static readonly Rgb32F SlateBlue = 0x6A5ACD;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #708090.
    /// </summary>
    public static readonly Rgb32F SlateGray = 0x708090;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #708090.
    /// </summary>
    public static readonly Rgb32F SlateGrey = 0x708090;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFFAFA.
    /// </summary>
    public static readonly Rgb32F Snow = 0xFFFAFA;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #00FF7F.
    /// </summary>
    public static readonly Rgb32F SpringGreen = 0x00FF7F;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #4682B4.
    /// </summary>
    public static readonly Rgb32F SteelBlue = 0x4682B4;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #D2B48C.
    /// </summary>
    public static readonly Rgb32F Tan = 0xD2B48C;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #008080.
    /// </summary>
    public static readonly Rgb32F Teal = 0x008080;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #D8BFD8.
    /// </summary>
    public static readonly Rgb32F Thistle = 0xD8BFD8;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FF6347.
    /// </summary>
    public static readonly Rgb32F Tomato = 0xFF6347;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #40E0D0.
    /// </summary>
    public static readonly Rgb32F Turquoise = 0x40E0D0;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #EE82EE.
    /// </summary>
    public static readonly Rgb32F Violet = 0xEE82EE;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #F5DEB3.
    /// </summary>
    public static readonly Rgb32F Wheat = 0xF5DEB3;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFFFFF.
    /// </summary>
    public static readonly Rgb32F White = 0xFFFFFF;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #F5F5F5.
    /// </summary>
    public static readonly Rgb32F WhiteSmoke = 0xF5F5F5;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #FFFF00.
    /// </summary>
    public static readonly Rgb32F Yellow = 0xFFFF00;

    /// <summary>
    ///     Gets a <see cref="Rgb32F" /> that has a RGB value of #9ACD32.
    /// </summary>
    public static readonly Rgb32F YellowGreen = 0x9ACD32;
}
