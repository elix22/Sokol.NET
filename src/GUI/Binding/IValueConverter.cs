using System;

namespace Sokol.GUI;

/// <summary>
/// Converts values between source and target types for data bindings.
/// Implement this interface to provide custom conversion logic.
/// </summary>
public interface IValueConverter
{
    /// <summary>Converts a source value to the target type.</summary>
    object? Convert(object? value, Type targetType, object? parameter);

    /// <summary>Converts a target value back to the source type (required for TwoWay bindings).</summary>
    object? ConvertBack(object? value, Type targetType, object? parameter);
}
