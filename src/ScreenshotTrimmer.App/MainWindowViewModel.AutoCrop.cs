using System.Windows.Input;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.App;

// Auto crop and Auto crop in region: finding the Subject of the selected image off the interface thread and recording it
// as its crop.
public sealed partial class MainWindowViewModel
{
    /// <summary>How long detection runs before the progress indication shows.</summary>
    private static readonly TimeSpan ProgressDelay = TimeSpan.FromMilliseconds(300);

    private CancellationTokenSource? _detection;
    private RelayCommand? _autoCropCommand;
    private RelayCommand<CropTool>? _chooseCropToolCommand;
    private bool _isDetecting;
    private CropTool _cropTool;

    /// <summary>
    /// Finds the Subject of the selected image and records it as its crop, as one Step that replaces only the crop and
    /// shows the Original; an image's first Step also takes its finishing from the remembered settings. When no Subject
    /// is found, nothing is recorded and a notice says so.
    /// </summary>
    public RelayCommand AutoCropCommand => _autoCropCommand ??= new RelayCommand(AutoCrop);

    /// <summary>
    /// What a newly drawn rectangle does: becomes a Classic crop, or is a region for <see cref="AutoCropInRegion"/>.
    /// Session-only; starts as Classic crop and is kept while moving between images.
    /// </summary>
    /// <remarks>
    /// The menu's and toolbar's choices show it with <see cref="IsClassicCropTool"/> and
    /// <see cref="IsAutoCropInRegionTool"/>; the menu's set it with <see cref="ChooseCropToolCommand"/>, since a menu
    /// item does not check itself when clicked.
    /// </remarks>
    public CropTool CropTool
    {
        get => _cropTool;
        set
        {
            if (!Set(ref _cropTool, value))
                return;
            Raise(nameof(IsClassicCropTool));
            Raise(nameof(IsAutoCropInRegionTool));
        }
    }

    /// <summary>
    /// Whether <see cref="CropTool"/> is Classic crop. Setting it to <c>true</c> chooses Classic crop; setting it to
    /// <c>false</c> changes nothing, since a radio choice is unchecked only by checking another.
    /// </summary>
    public bool IsClassicCropTool
    {
        get => CropTool == CropTool.Classic;
        set
        {
            if (value)
                CropTool = CropTool.Classic;
        }
    }

    /// <summary>
    /// Whether <see cref="CropTool"/> is Auto crop in region, so the viewer draws a region rather than a crop. Setting it
    /// to <c>true</c> chooses Auto crop in region; setting it to <c>false</c> changes nothing.
    /// </summary>
    public bool IsAutoCropInRegionTool
    {
        get => CropTool == CropTool.AutoCropInRegion;
        set
        {
            if (value)
                CropTool = CropTool.AutoCropInRegion;
        }
    }

    /// <summary>Sets <see cref="CropTool"/> to the <see cref="App.CropTool"/> given as the parameter.</summary>
    public RelayCommand<CropTool> ChooseCropToolCommand =>
        _chooseCropToolCommand ??= new RelayCommand<CropTool>(tool => CropTool = tool);

    /// <summary>Whether Auto crop has been running for more than a moment, so the window shows that it is working.</summary>
    public bool IsDetecting
    {
        get => _isDetecting;
        private set => Set(ref _isDetecting, value);
    }

    private void AutoCrop() =>
        Detect(SubjectDetection.Find, subject =>
        {
            if (subject is null)
                AddNotice("NoSubjectFound");
            else
                RecordFound(subject.ToCrop());
        });

    /// <summary>
    /// Finds the largest Subject inside <paramref name="region"/> of the selected image, in image pixels, and records it
    /// as its crop, as one Step that shows the Original, like Auto crop. When none is found, the region itself becomes a
    /// Classic crop and a notice says so.
    /// </summary>
    public void AutoCropInRegion(PixelRect region) =>
        Detect((image, token) => SubjectDetection.FindInRegion(image, region, token), subject =>
        {
            if (subject is null)
                AddNotice("NoSubjectInRegion");
            RecordFound(subject?.ToCrop() ?? Crop.Classic(region));
        });

    /// <summary>Records <paramref name="crop"/> as one Step and shows the Original with it.</summary>
    private void RecordFound(Crop crop)
    {
        RecordCrop(crop, mergeKey: null);
        // Also when the image already had this crop, so nothing was recorded.
        ShowsResult = false;
    }

    /// <summary>
    /// Runs <paramref name="find"/> on the selected image's Original off the interface thread, replacing any detection
    /// that is running, then passes its answer to <paramref name="record"/> unless the selection has moved on.
    /// </summary>
    private async void Detect(Func<RgbaImage, CancellationToken, Subject?> find, Action<Subject?> record)
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
            var subject = await Task.Run(() => find(original, token), token);
            // A late answer never lands on another image: moving away cancels it.
            if (token.IsCancellationRequested || !ShowsSelected(out var shown, out _) || shown.Key != image.Key)
                return;
            record(subject);
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
