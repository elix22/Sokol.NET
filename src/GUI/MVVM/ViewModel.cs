using System;

namespace Sokol.GUI;

/// <summary>
/// Base ViewModel for MVVM.  Extends <see cref="ObservableObject"/> with command support.
/// </summary>
public abstract class ViewModel : ObservableObject { }

/// <summary>
/// Simple delegate-based command.
/// </summary>
public sealed class RelayCommand
{
    private readonly Action         _execute;
    private readonly Func<bool>?    _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute() => _canExecute?.Invoke() ?? true;
    public void Execute() { if (CanExecute()) _execute(); }
}

/// <summary>
/// Parameterized relay command.
/// </summary>
public sealed class RelayCommand<T>
{
    private readonly Action<T>         _execute;
    private readonly Func<T, bool>?    _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(T param) => _canExecute?.Invoke(param) ?? true;
    public void Execute(T param) { if (CanExecute(param)) _execute(param); }
}
