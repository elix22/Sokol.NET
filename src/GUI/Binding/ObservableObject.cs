using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Sokol.GUI;

/// <summary>
/// Base class for bindable view-models.
/// Implements a simple property-changed notification compatible with AOT (no reflection).
/// </summary>
public abstract class ObservableObject
{
    public event Action<string>? PropertyChanged;

    protected void SetField<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        RaisePropertyChanged(name);
    }

    protected void RaisePropertyChanged(string name) => PropertyChanged?.Invoke(name);
}
