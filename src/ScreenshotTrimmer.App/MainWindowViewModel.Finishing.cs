using System.Windows.Input;
using Avalonia.Threading;
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
    private ImageKey? _finishingKey;
    private string _manualRadiusText = "";
    private ShadowControls _shadow = ShadowControls.Shown(Steps.None, new UserSettings(), false);

    /// <summary>The Shadow a slider drag has reached so far, or null when no drag is under way.</summary>
    private ShadowControls? _draggedShadow;
    private bool _draggingShadow;

    /// <summary>Waits for a pause in a slider drag before previewing it, so a fast drag does not render every value.</summary>
    private DispatcherTimer? _shadowPreviewTimer;

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

    /// <summary>The smallest size the Shadow size slider offers; the library allows any size above 0.</summary>
    public static double MinShadowSize => 0.1;

    /// <summary>The largest size the Shadow size slider offers.</summary>
    public static double MaxShadowSize => Shadow.MaxSize;

    /// <summary>
    /// The Shadow switch, showing the selected image's Steps, or the remembered setting while it has none (off for an
    /// Original with shadow padding). Setting it changes the image's Steps (one Step) and the remembered setting.
    /// </summary>
    public bool ShadowOn
    {
        get => _shadow.On;
        set
        {
            if (value != _shadow.On)
                ChangeShadow(_shadow with { On = value });
        }
    }

    /// <summary>
    /// The Shadow size slider. During a drag (<see cref="BeginShadowDrag"/>) each value is only previewed; otherwise,
    /// and at the drag's end, the value is one Step and the remembered size. A size below the slider's range (allowed
    /// in the settings file) is shown at the slider's minimum and changes nothing until the slider moves.
    /// </summary>
    public double ShadowSize
    {
        get => Math.Clamp((_draggedShadow ?? _shadow).Size, MinShadowSize, MaxShadowSize);
        set
        {
            // The slider writing back the value it shows is not a change.
            if (value != ShadowSize)
                SlideShadow((_draggedShadow ?? _shadow) with { Size = Math.Clamp(value, MinShadowSize, MaxShadowSize) });
        }
    }

    /// <summary>The Shadow opacity slider, changed like <see cref="ShadowSize"/>.</summary>
    public double ShadowOpacity
    {
        get => (_draggedShadow ?? _shadow).Opacity;
        set => SlideShadow((_draggedShadow ?? _shadow) with { Opacity = Math.Clamp(value, 0, 1) });
    }

    /// <summary>Whether the shown Result's render found that the Original already has shadow padding.</summary>
    public bool ShowsShadowPaddingWarning => _report?.Report.OriginalHasShadowPadding == true;

    /// <summary>
    /// Starts a Shadow slider drag: until <see cref="EndShadowDrag"/>, slider values update the Result preview but
    /// record nothing.
    /// </summary>
    public void BeginShadowDrag() => _draggingShadow = true;

    /// <summary>Ends a Shadow slider drag, recording the value it reached as one Step.</summary>
    public void EndShadowDrag()
    {
        if (!_draggingShadow)
            return;
        _draggingShadow = false;
        _shadowPreviewTimer?.Stop();
        var dragged = _draggedShadow;
        _draggedShadow = null;
        if (dragged is not null && dragged != _shadow)
            ChangeShadow(dragged);
        else
        {
            // Back to the recorded Shadow, in the sliders and in the Result.
            RaiseFinishing();
            ShowFinishing(_workspace.Selected);
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
        var steps = ListedSteps(image);
        var shown = CleanEdges.Shown(steps, Settings);
        var shadow = ShadowControls.Shown(steps, Settings, _shownHasShadowPadding);
        var sameImage = image?.Key == _finishingKey;
        _finishingKey = image?.Key;
        if (!sameImage)
            _draggedShadow = null;
        if (shown != _cleanEdges || shadow != _shadow || !sameImage)
        {
            _cleanEdges = shown;
            _shadow = shadow;
            ManualRadiusText = FormatRadius(shown.ManualRadius);
            RaiseFinishing();
        }

        if (image is not null && _original is { } original && image.Key == _shown?.Key)
            Render(new RenderRequest(original, steps));
    }

    /// <summary>
    /// Changes the selected image's Steps with <paramref name="change"/> (its first Step seeded from the remembered
    /// settings), makes <paramref name="remembered"/> the remembered settings and shows the Result.
    /// </summary>
    private void ChangeFinishing(Func<Steps, Steps> change, UserSettings remembered)
    {
        if (remembered != Settings)
            Remember(remembered);
        ChangeShownSteps(change);
        ShowsResult = true;
        ShowFinishing(_workspace.Selected);
    }

    private void ChangeShadow(ShadowControls shadow) => ChangeFinishing(shadow.Change, shadow.Remember(Settings));

    /// <summary>A slider moved: previewed while a drag is under way, otherwise recorded at once (keyboard, wheel).</summary>
    private void SlideShadow(ShadowControls shadow)
    {
        if (!_draggingShadow)
        {
            if (shadow != _shadow)
                ChangeShadow(shadow);
            return;
        }
        if (shadow == (_draggedShadow ?? _shadow))
            return;
        _draggedShadow = shadow;
        Raise(nameof(ShadowSize));
        Raise(nameof(ShadowOpacity));
        if (_shadowPreviewTimer is null)
        {
            _shadowPreviewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _shadowPreviewTimer.Tick += (_, _) =>
            {
                _shadowPreviewTimer.Stop();
                PreviewShadow();
            };
        }
        _shadowPreviewTimer.Stop();
        _shadowPreviewTimer.Start();
    }

    /// <summary>Renders the Result with the Shadow the drag has reached, without recording a Step.</summary>
    private void PreviewShadow()
    {
        if (_draggedShadow is not { } dragged || !ShowsSelected(out var image, out var original))
            return;
        ShowsResult = true;
        Render(new RenderRequest(original,
            FirstStep.Apply(ListedSteps(image), Settings, _shownHasShadowPadding, dragged.Change)));
    }

    private void ApplyManualRadius()
    {
        if (!CleanEdges.TryParseManualRadius(ManualRadiusText, Localizer.Instance.Culture, out var radius))
        {
            AddNotice("ManualRadiusInvalid", CleanEdges.MaxManualRadius);
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
        Raise(nameof(ShadowOn));
        Raise(nameof(ShadowSize));
        Raise(nameof(ShadowOpacity));
        Raise(nameof(ShowsShadowPaddingWarning));
        Raise(nameof(FrameLinesRemovedText));
        Raise(nameof(MeasuredRadiiText));
        Raise(nameof(ResultSizeText));
        Raise(nameof(ShowsFinishing));
    }

    /// <summary>The pixels and Steps of one render; equal requests give the same Result.</summary>
    private sealed record RenderRequest(RgbaImage Original, Steps Steps);
}
