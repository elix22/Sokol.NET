using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sokol.GUI;

/// <summary>
/// AOT-safe string → value parser used by <see cref="WidgetRegistry"/> when
/// applying XML attributes to widgets.
///
/// A small dispatch table maps target <see cref="Type"/> → a parser delegate.
/// Enums are handled generically via <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>
/// (which is AOT-safe because it is a generic method resolved at JIT/AOT time).
/// </summary>
public static class AttributeParser
{
    private static readonly Dictionary<Type, Func<string, object?>> _parsers = new()
    {
        [typeof(string)]       = static s => s,
        [typeof(float)]        = static s => float.Parse(s, CultureInfo.InvariantCulture),
        [typeof(double)]       = static s => double.Parse(s, CultureInfo.InvariantCulture),
        [typeof(int)]          = static s => int.Parse(s, CultureInfo.InvariantCulture),
        [typeof(long)]         = static s => long.Parse(s, CultureInfo.InvariantCulture),
        [typeof(bool)]         = static s => bool.Parse(s),
        [typeof(UIColor)]      = static s => ParseColor(s),
        [typeof(Thickness)]    = static s => ParseThickness(s),
        [typeof(CornerRadius)] = static s => ParseCornerRadius(s),
        [typeof(Vector2)]      = static s => ParseVector2(s),
    };

    /// <summary>
    /// Register a custom parser for a value type. Call once at startup for each
    /// user-defined struct that appears in XML attributes.
    /// </summary>
    public static void RegisterParser<T>(Func<string, T> parser)
    {
        _parsers[typeof(T)] = s => parser(s);
    }

    /// <summary>Parse a string into <typeparamref name="T"/>.</summary>
    public static bool TryParse<T>(string raw, out T? value)
    {
        var target = typeof(T);

        // Enum types are handled generically.
        if (target.IsEnum)
        {
            if (Enum.TryParse(target, raw, ignoreCase: true, out var enumValue))
            {
                value = (T)enumValue!;
                return true;
            }
            value = default;
            return false;
        }

        if (_parsers.TryGetValue(target, out var parser))
        {
            try
            {
                value = (T)parser(raw)!;
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        // Nullable value types: unwrap and parse underlying.
        var nullableUnderlying = Nullable.GetUnderlyingType(target);
        if (nullableUnderlying != null &&
            _parsers.TryGetValue(nullableUnderlying, out var np))
        {
            try
            {
                value = (T)np(raw)!;
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        value = default;
        return false;
    }

    // ─── Built-in parsers ────────────────────────────────────────────────────

    /// <summary>Parses "#RGB", "#RRGGBB", "#RRGGBBAA", or a named color.</summary>
    public static UIColor ParseColor(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return UIColor.Transparent;
        if (s[0] == '#') return UIColor.FromHex(s);

        return s.Trim().ToLowerInvariant() switch
        {
            "transparent" => UIColor.Transparent,
            "black"       => UIColor.Black,
            "white"       => UIColor.White,
            "red"         => UIColor.Red,
            "green"       => UIColor.Green,
            "blue"        => UIColor.Blue,
            "gray" or "grey" => UIColor.Gray,
            "lightgray" or "lightgrey" => UIColor.LightGray,
            "darkgray"  or "darkgrey"  => UIColor.DarkGray,
            "yellow"      => UIColor.Yellow,
            "cyan"        => UIColor.Cyan,
            "magenta"     => UIColor.Magenta,
            _             => UIColor.Transparent,
        };
    }

    /// <summary>Parses "N" | "H,V" | "L,T,R,B" → <see cref="Thickness"/>.</summary>
    public static Thickness ParseThickness(string s)
    {
        var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            1 => new Thickness(ParseFloat(parts[0])),
            2 => new Thickness(ParseFloat(parts[0]), ParseFloat(parts[1])),
            4 => new Thickness(ParseFloat(parts[0]), ParseFloat(parts[1]),
                               ParseFloat(parts[2]), ParseFloat(parts[3])),
            _ => Thickness.Zero,
        };
    }

    /// <summary>Parses "N" | "TL,TR,BR,BL" → <see cref="CornerRadius"/>.</summary>
    public static CornerRadius ParseCornerRadius(string s)
    {
        var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            1 => new CornerRadius(ParseFloat(parts[0])),
            4 => new CornerRadius(ParseFloat(parts[0]), ParseFloat(parts[1]),
                                  ParseFloat(parts[2]), ParseFloat(parts[3])),
            _ => CornerRadius.Zero,
        };
    }

    /// <summary>Parses "X,Y" → <see cref="Vector2"/>.</summary>
    public static Vector2 ParseVector2(string s)
    {
        var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return Vector2.Zero;
        return new Vector2(ParseFloat(parts[0]), ParseFloat(parts[1]));
    }

    private static float ParseFloat(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
