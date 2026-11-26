using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

// Never losing unsaved Steps by accident: the title marker, and the questions before closing, replacing the list or
// removing an entry.
public sealed partial class MainWindowViewModel
{
    private readonly IQuestions _questions;
    private bool _showsUnsavedWork;
    private bool _askingToLeave;

    /// <summary>Whether any listed image has unsaved Steps, so leaving would ask first.</summary>
    public bool HasUnsavedWork => _workspace.HasUnsavedWork;

    /// <summary>Whether leaving must wait for <see cref="ConfirmLeavingAsync"/>: some work is unsaved or a Batch runs.</summary>
    public bool MustConfirmLeaving => _workspace.HasUnsavedWork || IsBatchRunning;

    /// <summary>
    /// Whether leaving may go ahead. A running Batch is cancelled and waited for first, without blocking the window,
    /// so the Steps it applies before it stops are part of the question. Then leaving goes ahead when nothing is
    /// unsaved, or the user chose to discard it, or chose to save all and every image saved. Otherwise the user stays,
    /// and any save failures are shown as notices.
    /// </summary>
    public async Task<bool> ConfirmLeavingAsync()
    {
        if (_askingToLeave)
            return false;
        _askingToLeave = true;
        try
        {
            await StopBatchAsync();
            if (!_workspace.HasUnsavedWork)
                return true;
            return await _questions.AskToLeaveAsync() switch
            {
                LeaveAnswer.Discard => true,
                LeaveAnswer.SaveAll => await SaveAllAsync() && !_workspace.HasUnsavedWork,
                _ => false,
            };
        }
        finally
        {
            _askingToLeave = false;
        }
    }

    /// <summary>Opens <paramref name="paths"/>, replacing the list, once the user agrees to leave any unsaved work.</summary>
    public async Task OpenAsync(IReadOnlyList<string> paths)
    {
        if (await ConfirmLeavingAsync())
            Open(paths);
    }

    /// <summary>
    /// <see cref="OpenAsync"/> for event handlers that cannot wait for it; anything that goes wrong is reported as a
    /// notice.
    /// </summary>
    public async void OpenAfterAsking(IReadOnlyList<string> paths)
    {
        try
        {
            await OpenAsync(paths);
        }
        catch (Exception e)
        {
            ReportFailure(e);
        }
    }

    private string MarkUnsaved(string title) =>
        _showsUnsavedWork ? Localizer.Instance.Format("UnsavedTitle", title) : title;

    /// <summary>Marks the title while any of <paramref name="images"/> has unsaved Steps.</summary>
    private void ShowUnsavedWork(IReadOnlyList<WorkspaceImage> images)
    {
        var unsaved = images.Any(image => image.HasUnsavedSteps);
        if (unsaved == _showsUnsavedWork)
            return;
        _showsUnsavedWork = unsaved;
        Raise(nameof(Title));
    }

    /// <summary>Tells the user which images' unsaved Steps were discarded because the disk took their files away.</summary>
    private void ReportDroppedWithUnsavedSteps(IReadOnlyList<string> paths)
    {
        AddNotice("UnsavedDropped", string.Join(", ", paths.Select(Path.GetFileName)));
    }
}
