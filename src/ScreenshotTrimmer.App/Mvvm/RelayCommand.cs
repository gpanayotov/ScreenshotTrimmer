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
