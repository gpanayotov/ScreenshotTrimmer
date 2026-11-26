using System.Windows.Input;

namespace ScreenshotTrimmer.App.Mvvm;

/// <summary>
/// A command that runs <paramref name="execute"/>, when <paramref name="canExecute"/> allows it (always, when it is not
/// given). Call <see cref="NotifyCanExecuteChanged"/> when the answer may have changed.
/// </summary>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
            execute();
    }

    /// <summary>Tells the controls bound to this command to ask <see cref="CanExecute"/> again.</summary>
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>A command that always runs <paramref name="execute"/> with its parameter, or <c>null</c> when it is not a <typeparamref name="T"/>.</summary>
public sealed class RelayCommand<T>(Action<T?> execute) : ICommand
    where T : class
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute(parameter as T);
}
