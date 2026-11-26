using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

// Undo, redo and reset of the selected image's Steps.
public sealed partial class MainWindowViewModel
{
    /// <summary>Undoes the selected image's last Step. Enabled while it has one to undo.</summary>
    public RelayCommand UndoCommand { get; }

    /// <summary>Redoes the selected image's last undone Step. Enabled while it has one to redo.</summary>
    public RelayCommand RedoCommand { get; }

    /// <summary>Removes all of the selected image's Steps as one undoable Step. Enabled while it has Steps.</summary>
    public RelayCommand ResetCommand { get; }

    /// <summary>Applies <paramref name="change"/> to the selected image's history, if it is still listed.</summary>
    private void ChangeHistory(Action<WorkspaceImage> change)
    {
        if (_workspace.Selected is { } image)
            IfStillListed(image, change);
    }

    private bool CanChangeHistory(Func<WorkspaceImage, bool> can)
    {
        var allowed = false;
        if (_workspace.Selected is { } image)
            IfStillListed(image, listed => allowed = can(listed));
        return allowed;
    }

    private void NotifyHistoryCommands()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
    }
}
