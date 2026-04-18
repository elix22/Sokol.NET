using System;

namespace Sokol.GUI;

/// <summary>
/// Descriptor produced by <see cref="BindingParser.TryParse"/> — intermediate form
/// consumed by <see cref="XmlLoader"/> to attach a real <see cref="BindingExpression"/>
/// to a widget once its DataContext has been resolved.
/// </summary>
public readonly record struct BindingDescriptor(
    string        Path,
    BindingMode   Mode,
    string?       Converter,
    string?       FallbackValue);

/// <summary>
/// Parses Avalonia-style binding expressions in XML attribute values:
/// <c>"{Binding PropName}"</c>,
/// <c>"{Binding Path=PropName, Mode=TwoWay, Converter=boolToVis}"</c>.
///
/// Returns a <see cref="BindingDescriptor"/>; <see cref="XmlLoader"/> resolves
/// the descriptor into a real <see cref="BindingExpression"/> after the widget's
/// DataContext is known.
/// </summary>
public static class BindingParser
{
    /// <summary>Returns true iff the string looks like a binding expression.</summary>
    public static bool IsBinding(string value)
        => !string.IsNullOrEmpty(value)
           && value.StartsWith("{Binding", StringComparison.OrdinalIgnoreCase)
           && value.EndsWith('}');

    /// <summary>
    /// Parse a <c>{Binding …}</c> expression. Returns null if the string is not
    /// a well-formed binding.
    /// </summary>
    public static BindingDescriptor? TryParse(string value)
    {
        if (!IsBinding(value)) return null;

        // Strip "{Binding" prefix and "}" suffix → inner body.
        const string prefix = "{Binding";
        var body = value.Substring(prefix.Length, value.Length - prefix.Length - 1).Trim();

        // "" = {Binding} → no path specified
        if (body.Length == 0) return null;

        string?     path          = null;
        var         mode          = BindingMode.OneWay;
        string?     converter     = null;
        string?     fallbackValue = null;

        var parts = body.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            int eq   = part.IndexOf('=');

            if (eq < 0)
            {
                // Positional form: "{Binding PropName, ...}" — first token is the Path.
                if (i == 0) path = part;
                continue;
            }

            var key = part.AsSpan(0, eq).Trim();
            var val = part.AsSpan(eq + 1).Trim().ToString();

            if (key.Equals("Path", StringComparison.OrdinalIgnoreCase))
                path = val;
            else if (key.Equals("Mode", StringComparison.OrdinalIgnoreCase))
                Enum.TryParse(val, ignoreCase: true, out mode);
            else if (key.Equals("Converter", StringComparison.OrdinalIgnoreCase))
                converter = val;
            else if (key.Equals("FallbackValue", StringComparison.OrdinalIgnoreCase))
                fallbackValue = val;
        }

        if (string.IsNullOrEmpty(path)) return null;
        return new BindingDescriptor(path!, mode, converter, fallbackValue);
    }
}
