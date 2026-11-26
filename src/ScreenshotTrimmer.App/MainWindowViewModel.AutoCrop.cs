using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Detection;

namespace ScreenshotTrimmer.App;

// Auto crop: finding the Subject of the selected image off the interface thread and recording it as its crop.
public sealed partial class MainWindowViewModel
{
    /// <summary>How long detection runs before the progress indication shows.</summary>
    private static readonly TimeSpan ProgressDelay = TimeSpan.FromMilliseconds(300);

    private CancellationTokenSource? _detection;
    private RelayCommand? _autoCropCommand;
    private bool _isDetecting;

    /// <summary>
    /// Finds the Subject of the selected image and records it as its crop, as one Step that replaces only the crop and
    /// shows the Original; an image's first Step also takes its finishing from the remembered settings. When no Subject
    /// is found, nothing is recorded and a notice says so.
    /// </summary>
    public RelayCommand AutoCropCommand => _autoCropCommand ??= new RelayCommand(AutoCrop);

    /// <summary>Whether Auto crop has been running for more than a moment, so the window shows that it is working.</summary>
    public bool IsDetecting
    {
        get => _isDetecting;
        private set => Set(ref _isDetecting, value);
    }

    private async void AutoCrop()
    {
        if (!ShowsSelected(out var image, out var original))
            return;
        CancelDetection();
        using var detection = new CancellationTokenSource();
        _detection = detection;
        var token = detection.Token;
        ShowProgressAfterDelay(detection);
        try
        {
            var subject = await Task.Run(() => SubjectDetection.Find(original, token), token);
            // A late answer never lands on another image: moving away cancels it.
            if (token.IsCancellationRequested || !ShowsSelected(out var shown, out _) || shown.Key != image.Key)
                return;
            if (subject is null)
                AddNotice("NoSubjectFound");
            else
            {
                RecordCrop(subject.ToCrop(), mergeKey: null);
                // Also when the image already had this crop, so nothing was recorded.
                ShowsResult = false;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            ReportFailure(e);
        }
        finally
        {
            if (_detection == detection)
            {
                _detection = null;
                IsDetecting = false;
            }
        }
    }

    private async void ShowProgressAfterDelay(CancellationTokenSource detection)
    {
        await Task.Delay(ProgressDelay);
        if (_detection == detection)
            IsDetecting = true;
    }

    /// <summary>Drops the Auto crop that is running, if any; its answer is never recorded.</summary>
    private void CancelDetection()
    {
        if (_detection is not { } running)
            return;
        _detection = null;
        IsDetecting = false;
        running.Cancel();
    }
}
