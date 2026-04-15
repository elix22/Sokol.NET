using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// AOT-safe property registry.
/// Instead of reflection, callers register typed getter/setter delegates
/// for each (type, propertyName) pair.
/// </summary>
public static class BindingRegistry
{
    // key = (type full name, property name)
    private static readonly Dictionary<(string, string), (Func<object, object?> get, Action<object, object?> set)>
        _props = [];

    public static void Register<TSource, TValue>(
        string            propertyName,
        Func<TSource, TValue>         getter,
        Action<TSource, TValue>?      setter = null)
    {
        string typeKey = typeof(TSource).FullName ?? typeof(TSource).Name;
        _props[(typeKey, propertyName)] = (
            src => getter((TSource)src),
            setter != null ? (src, val) => setter((TSource)src, (TValue)val!) : (_, _) => { }
        );
    }

    public static void Resolve(object source, string propertyName, Action<object?> targetSetter)
    {
        string typeKey = source.GetType().FullName ?? source.GetType().Name;
        if (_props.TryGetValue((typeKey, propertyName), out var entry))
            targetSetter(entry.get(source));
    }

    public static bool TrySetSource(object source, string propertyName, object? value)
    {
        string typeKey = source.GetType().FullName ?? source.GetType().Name;
        if (_props.TryGetValue((typeKey, propertyName), out var entry))
        { entry.set(source, value); return true; }
        return false;
    }
}
