using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

// The Batch: Auto crop and the current finishing for every listed image that needs it, run off the interface thread,
// with its progress, a Cancel button and a summary notice.
public sealed partial class MainWindowViewModel
{
    private RelayCommand? _batchCommand;
    private RelayCommand? _cancelBatchCommand;
    private CancellationTokenSource? _batchCancellation;
    private bool _isBatchRunning;
    private BatchProgress _batchProgress;

    /// <summary>
    /// Runs a Batch over the listed images off the interface thread, showing its progress, then reports what it did as
    /// a notice that stays until dismissed. Enabled while something is listed and no Batch is running.
    /// </summary>
    public RelayCommand BatchCommand => _batchCommand ??= new RelayCommand(RunBatch, CanRunBatch);

    /// <summary>Whether a Batch is running, so the window shows its progress.</summary>
    public bool IsBatchRunning
    {
        get => _isBatchRunning;
        private set
        {
            if (!Set(ref _isBatchRunning, value))
                return;
            BatchCommand.NotifyCanExecuteChanged();
            CancelBatchCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Cancels the running Batch: it starts no more images and stops the ones in flight; the images it finished keep
    /// their Steps, and its summary says it was cancelled.
    /// </summary>
    public RelayCommand CancelBatchCommand => _cancelBatchCommand ??= new RelayCommand(
        () => _batchCancellation?.Cancel(), () => IsBatchRunning);

    /// <summary>How many listed images the running Batch has done.</summary>
    public int BatchDone => _batchProgress.Done;

    /// <summary>How many listed images the running Batch goes through.</summary>
    public int BatchTotal => _batchProgress.Total;

    /// <summary>The running Batch's progress as "n of m".</summary>
    public string BatchProgressText => Localizer.Instance.Format("BatchProgress", BatchDone, BatchTotal);

    private bool CanRunBatch() => !IsBatchRunning && Entries.Count > 0;

    private async void RunBatch()
    {
        if (!CanRunBatch())
            return;
        // The finishing settings are remembered without the workspace hearing of it; the Batch uses the current ones.
        _workspace.ApplySettings(Settings);
        IsBatchRunning = true;
        ShowBatchProgress(new BatchProgress(0, _workspace.Images.Count));
        var progress = new Progress<BatchProgress>(ShowBatchProgress);
        using var cancellation = new CancellationTokenSource();
        _batchCancellation = cancellation;
        try
        {
            // Opening another list or closing the window cancels the Batch in the workspace and waits for it.
            var summary = await Task.Run(() => _workspace.RunBatch(progress, cancellation.Token));
            _notices.Add(new Message(summary.Cancelled ? "BatchCancelled" : "BatchDone", summary.Processed,
                summary.NoSubjectFound.Count, summary.SkippedFor(BatchSkip.HasSteps),
                summary.SkippedFor(BatchSkip.AlreadyProcessed), summary.SkippedFor(BatchSkip.Result),
                summary.SkippedFor(BatchSkip.Gone), summary.Failed.Count));
            if (summary.NoSubjectFound.Count > 0)
            {
                _notices.Add(new Message("BatchNoSubjectFoundIn",
                    string.Join(", ", summary.NoSubjectFound.Select(StripEntry.DisplayName))));
            }
            if (summary.Failed.Count > 0)
                _notices.Add(new Message("BatchFailedIn", string.Join(", ", summary.Failed.Select(StripEntry.DisplayName))));
            RaiseNotices();
        }
        catch (Exception e)
        {
            ReportFailure(e);
        }
        finally
        {
            _batchCancellation = null;
            IsBatchRunning = false;
        }
    }

    private void ShowBatchProgress(BatchProgress progress)
    {
        _batchProgress = progress;
        Raise(nameof(BatchDone));
        Raise(nameof(BatchTotal));
        Raise(nameof(BatchProgressText));
    }
}
