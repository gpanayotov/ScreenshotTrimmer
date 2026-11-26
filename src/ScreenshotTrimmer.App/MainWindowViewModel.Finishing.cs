using System.Windows.Input;
using Avalonia.Media.Imaging;
using ScreenshotTrimmer.App.Imaging;
using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

// The Original / Result view, the live render of the selected image's Result and the finishing panel.
public sealed partial class MainWindowViewModel
{
    /// <summary>The shown image's Original pixels, which every render of its Result starts from; null while it loads.</summary>
    private RgbaImage? _original;
    private Bitmap? _resultImage;
    /// <summary>What the shown Result's render measured, with the Steps it rendered; null until it finishes.</summary>
    private (RenderReport Report, Steps Steps)? _report;
    private bool _showsResult;
    private CleanEdges _cleanEdges = CleanEdges.Shown(Steps.None, new UserSettings());
    private string? _finishingKey;
    private string _manualRadiusText = "";

    /// <summary>The render the Result view should show: the newest wins over any still in progress.</summary>
    private RenderRequest? _wantedRender;

    /// <summary>The render last finished or given up, so the same Steps are not rendered twice.</summary>
    private RenderRequest? _finishedRender;
    private bool _rendering;
    private RelayCommand? _toggleResultViewCommand;
    private RelayCommand? _applyManualRadiusCommand;

    /// <summary>
    /// Whether the viewer shows the rendered Result rather than the Original with the crop. Editing the crop switches
    /// to the Original; changing finishing switches to the Result.
    /// </summary>
    public bool ShowsResult
    {
        get => _showsResult;
        set
        {
            if (!Set(ref _showsResult, value))
                return;
            Raise(nameof(ViewerImage));
            RaiseStatus();
        }
    }

    /// <summary>What the viewer shows: the Result or the Original, as <see cref="ShowsResult"/> says.</summary>
    public Bitmap? ViewerImage => ShowsResult ? ResultImage : Image;

    /// <summary>The selected image's rendered Result, or <c>null</c> until its first render finishes.</summary>
    public Bitmap? ResultImage
    {
        get => _resultImage;
        private set
        {
            var old = _resultImage;
            if (!Set(ref _resultImage, value))
                return;
            // The viewer lets go of the old bitmap before it is disposed.
            Raise(nameof(ViewerImage));
            RaiseStatus();
            old?.Dispose();
        }
    }

    /// <summary>Whether the finishing panel shows, which it does while an image is shown.</summary>
    public bool ShowsFinishing => Image is not null;

    /// <summary>
    /// The frame-line switch, showing the selected image's Steps, or the remembered setting while it has none. Setting
    /// it changes the image's Steps (one Step) and the remembered setting.
    /// </summary>
    public bool FrameLines
    {
        get => _cleanEdges.FrameLines;
        set
        {
            if (value != _cleanEdges.FrameLines)
                ChangeFinishing(CleanEdges.SetFrameLines(value), Settings with { FrameLines = value });
        }
    }

    /// <summary>The rounded-corner switch, shown and changed like <see cref="FrameLines"/>.</summary>
    public bool RoundedCorners
    {
        get => _cleanEdges.RoundedCorners;
        set
        {
            if (value != _cleanEdges.RoundedCorners)
                ChangeFinishing(CleanEdges.SetRoundedCorners(value), Settings with { RoundedCorners = value });
        }
    }

    /// <summary>
    /// The manual radius field's text, in the interface language; empty means automatic. It is applied to the selected
    /// image only, by <see cref="ApplyManualRadiusCommand"/>, and never remembered.
    /// </summary>
    public string ManualRadiusText
    {
        get => _manualRadiusText;
        set => Set(ref _manualRadiusText, value ?? "");
    }

    /// <summary>
    /// Applies <see cref="ManualRadiusText"/> to the selected image as one Step, or, when it is not a radius, puts back
    /// the radius in use and says why.
    /// </summary>
    public ICommand ApplyManualRadiusCommand => _applyManualRadiusCommand ??= new RelayCommand(ApplyManualRadius);

    /// <summary>Switches between the Original with the crop and the Result.</summary>
    public ICommand ToggleResultViewCommand => _toggleResultViewCommand ??= new RelayCommand(() => ShowsResult = !ShowsResult);

    /// <summary>How many frame lines the shown Result's render removed on each side, when it removed frame lines.</summary>
    public string? FrameLinesRemovedText =>
        _report is ({ FrameLinesRemoved: var removed }, { FrameLines: true })
            ? Localizer.Instance.Format("FrameLinesRemoved", removed.Left, removed.Top, removed.Right, removed.Bottom)
            : null;

    /// <summary>The radius the shown Result's render measured at each corner, when it rounded the corners.</summary>
    public string? MeasuredRadiiText =>
        _report?.Report.MeasuredRadii is { } radii
            ? Localizer.Instance.Format("MeasuredRadii", radii.TopLeft, radii.TopRight, radii.BottomRight, radii.BottomLeft)
            : null;

    /// <summary>The Result's size in pixels, formatted for the current language.</summary>
    public string? ResultSizeText =>
        ResultImage is { } result
            ? Localizer.Instance.Format("ResultSize", result.PixelSize.Width, result.PixelSize.Height)
            : null;

    /// <summary>
    /// Shows <paramref name="image"/>'s Clean edges choices in the panel and renders its Result again when its Steps
    /// changed. Does nothing to the render until the image's Original has loaded.
    /// </summary>
    private void ShowFinishing(WorkspaceImage? image)
    {
        var steps = Steps.None;
        if (image is not null)
            IfStillListed(image, listed => steps = _workspace.Steps(listed));

        var shown = CleanEdges.Shown(steps, Settings);
        var sameImage = SamePath(image?.Key, _finishingKey);
        _finishingKey = image?.Key;
        if (shown != _cleanEdges || !sameImage)
        {
            _cleanEdges = shown;
            ManualRadiusText = FormatRadius(shown.ManualRadius);
            RaiseFinishing();
        }

        if (image is not null && _original is { } original && SamePath(image.Key, _shown?.Key))
            Render(new RenderRequest(original, steps));
    }

    /// <summary>
    /// Changes the selected image's Steps with <paramref name="change"/> (its first Step seeded from the remembered
    /// settings), makes <paramref name="remembered"/> the remembered settings and shows the Result.
    /// </summary>
    private void ChangeFinishing(Func<Steps, Steps> change, UserSettings remembered)
    {
        if (remembered != Settings && _settings.Change(remembered) is { } failure)
        {
            _notices.Add(new Message("SettingsNotSaved", _settings.FilePath, failure));
            RaiseNotices();
        }
        if (_workspace.Selected is { } image && _original is not null && SamePath(image.Key, _shown?.Key))
        {
            var hasShadowPadding = _shownHasShadowPadding;
            IfStillListed(image, listed => _workspace.SetSteps(listed,
                FirstStep.Apply(_workspace.Steps(listed), Settings, hasShadowPadding, change)));
        }
        ShowsResult = true;
        ShowFinishing(_workspace.Selected);
    }

    private void ApplyManualRadius()
    {
        if (!CleanEdges.TryParseManualRadius(ManualRadiusText, Localizer.Instance.Culture, out var radius))
        {
            _notices.Add(new Message("ManualRadiusInvalid", CleanEdges.MaxManualRadius));
            RaiseNotices();
            ManualRadiusText = FormatRadius(_cleanEdges.ManualRadius);
            return;
        }
        ManualRadiusText = FormatRadius(radius);
        if (radius != _cleanEdges.ManualRadius)
            ChangeFinishing(CleanEdges.SetManualRadius(radius), Settings);
    }

    private static string FormatRadius(double? radius) =>
        radius is { } value ? value.ToString("0.##", Localizer.Instance.Culture) : "";

    /// <summary>
    /// Renders <paramref name="request"/> off the interface thread. While a render runs, only the newest request is
    /// kept, and it starts when the running one ends, so a burst of changes never queues up renders and an older render
    /// never replaces a newer one.
    /// </summary>
    private async void Render(RenderRequest request)
    {
        _wantedRender = request;
        if (_rendering)
            return;
        _rendering = true;
        try
        {
            while (_wantedRender is { } wanted && wanted != _finishedRender)
            {
                Bitmap? bitmap = null;
                RenderReport? report = null;
                try
                {
                    (bitmap, report) = await Task.Run(() =>
                    {
                        var rendered = Renderer.Render(wanted.Original, wanted.Steps);
                        return (BitmapLoading.ToBitmap(rendered.Result), rendered.Report);
                    });
                }
                catch (Exception e)
                {
                    if (wanted == _wantedRender)
                        ReportFailure(e);
                }
                _finishedRender = wanted;
                if (wanted != _wantedRender)
                {
                    bitmap?.Dispose();
                    continue;
                }
                ResultImage = bitmap;
                _report = report is null ? null : (report, wanted.Steps);
                RaiseFinishing();
            }
        }
        finally
        {
            _rendering = false;
        }
    }

    /// <summary>Forgets the shown image's Result, when another image is about to be shown.</summary>
    private void ForgetResult()
    {
        _original = null;
        _wantedRender = null;
        _finishedRender = null;
        _report = null;
        ResultImage = null;
        RaiseFinishing();
    }

    private void RaiseFinishing()
    {
        Raise(nameof(FrameLines));
        Raise(nameof(RoundedCorners));
        Raise(nameof(FrameLinesRemovedText));
        Raise(nameof(MeasuredRadiiText));
        Raise(nameof(ResultSizeText));
        Raise(nameof(ShowsFinishing));
    }

    /// <summary>The pixels and Steps of one render; equal requests give the same Result.</summary>
    private sealed record RenderRequest(RgbaImage Original, Steps Steps);
}
