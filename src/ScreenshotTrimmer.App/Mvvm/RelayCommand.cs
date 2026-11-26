using System.Windows.Input;

namespace ScreenshotTrimmer.App.Mvvm;

/// <summary>A command that always runs <paramref name="execute"/>.</summary>
public sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
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
