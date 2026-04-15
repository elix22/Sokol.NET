using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Tracks all active <see cref="BindingExpression"/> for a widget tree,
/// allowing bulk disposal when the tree is torn down.
/// </summary>
public sealed class BindingContext : IDisposable
{
    private readonly List<BindingExpression> _bindings = [];

    public ObservableObject? DataObject { get; set; }

    /// <summary>Create and track a one-way binding.</summary>
    public BindingExpression Bind(
        ObservableObject  source,
        string            property,
        Action<object?>   setter,
        Func<object?>?    getter = null,
        BindingMode       mode   = BindingMode.OneWay)
    {
        var expr = new BindingExpression(source, property, setter, getter, mode);
        _bindings.Add(expr);
        return expr;
    }

    public void Dispose()
    {
        foreach (var b in _bindings) b.Dispose();
        _bindings.Clear();
    }
}
