using System;

namespace Sokol.GUI;

/// <summary>
/// Binding modes.
/// </summary>
public enum BindingMode
{
    OneWay,     // source → target
    TwoWay,     // source ↔ target
    OneWayToSource, // target → source
}

/// <summary>
/// A single live binding from a source object's named property to a setter on a widget.
/// AOT-safe: uses delegates instead of reflection.
/// </summary>
public sealed class BindingExpression : IDisposable
{
    private readonly ObservableObject       _source;
    private readonly string                 _propertyName;
    private readonly Action<object?>        _targetSetter;
    private readonly Func<object?>?         _targetGetter;
    private readonly BindingMode            _mode;

    public BindingExpression(
        ObservableObject  source,
        string            propertyName,
        Action<object?>   targetSetter,
        Func<object?>?    targetGetter = null,
        BindingMode       mode         = BindingMode.OneWay)
    {
        _source       = source;
        _propertyName = propertyName;
        _targetSetter = targetSetter;
        _targetGetter = targetGetter;
        _mode         = mode;

        // Subscribe: source → target
        if (mode != BindingMode.OneWayToSource)
            _source.PropertyChanged += OnSourceChanged;
    }

    private void OnSourceChanged(string name)
    {
        if (name != _propertyName) return;
        // Caller must provide a source getter via BindableProperty.
        // This is resolved through the BindingContext set on the widget.
        BindingRegistry.Resolve(_source, _propertyName, _targetSetter);
    }

    /// <summary>Push current target value back to source (for TwoWay).</summary>
    public void PushToSource() { /* implementation handled by BindableProperty */ }

    public void Dispose() => _source.PropertyChanged -= OnSourceChanged;
}
