using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ScreenshotTrimmer.App.Controls;
using ScreenshotTrimmer.App.Imaging;
using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

public partial class MainWindow : Window, IPathPicker, IQuestions
{
    private MainWindowViewModel? _viewModel;
    private readonly Dictionary<Control, StripEntry> _shownEntries = [];
    private bool _mayClose;

    public MainWindow()
    {
        InitializeComponent();
        ExitItem.Click += (_, _) => Close();
        SettingsItem.Click += (_, _) => EditSettings();
        AboutItem.Click += async (_, _) => await new AboutWindow().ShowDialog(this);
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Control), Command = new RelayCommand(EditSettings),
        });
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        // Tunnel, so the strip's own Page Up / Page Down handling does not jump a whole page.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => Viewer.IsSpaceHeld = false;
        Viewer.PropertyChanged += OnViewerPropertyChanged;
        AddViewerShortcuts();
        Viewer.AddHandler(PointerPressedEvent, OnViewerPointerPressed, RoutingStrategies.Tunnel);
        Viewer.KeyDown += OnViewerKeyDown;
        Viewer.RegionDrawn += (_, region) => _viewModel?.AutoCropInRegion(region);
        ManualRadiusBox.AddHandler(KeyDownEvent, OnManualRadiusKeyDown, RoutingStrategies.Tunnel);
        ManualRadiusBox.LostFocus += (_, _) => _viewModel?.ApplyManualRadiusCommand.Execute(null);
        WatchShadowDrags(ShadowSizeSlider);
        WatchShadowDrags(ShadowOpacitySlider);
        Strip.ContainerPrepared += OnStripContainerPrepared;
        Strip.ContainerClearing += OnStripContainerClearing;
    }

    /// <summary>
    /// Tells the view model when a pointer drag on a Shadow slider starts and ends, whether it grabs the thumb or the
    /// track, so the whole drag becomes one Step. Tunnel and handled events too, as the slider handles the pointer itself.
    /// </summary>
    private void WatchShadowDrags(Slider slider)
    {
        slider.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(slider).Properties.IsLeftButtonPressed)
                _viewModel?.BeginShadowDrag();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        slider.AddHandler(PointerReleasedEvent, (_, _) => _viewModel?.EndShadowDrag(),
            RoutingStrategies.Tunnel, handledEventsToo: true);
        slider.AddHandler(PointerCaptureLostEvent, (_, _) => _viewModel?.EndShadowDrag(), handledEventsToo: true);
    }

    /// <summary>The zoom shortcuts, with the numeric keypad's keys and Ctrl+Shift+= for Ctrl+Plus too.</summary>
    private void AddViewerShortcuts()
    {
        void Add(ICommand command, params string[] gestures)
        {
            foreach (var gesture in gestures)
                KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse(gesture), Command = command });
        }

        Add(Viewer.FitCommand, "Ctrl+D0", "Ctrl+NumPad0");
        Add(Viewer.ActualSizeCommand, "Ctrl+D1", "Ctrl+NumPad1");
        Add(Viewer.ZoomInCommand, "Ctrl+OemPlus", "Ctrl+Shift+OemPlus", "Ctrl+Add");
        Add(Viewer.ZoomOutCommand, "Ctrl+OemMinus", "Ctrl+Subtract");
    }

    /// <summary>
    /// Shows <paramref name="workspace"/> in this window, which picks the paths it opens and edits
    /// <paramref name="settings"/>.
    /// </summary>
    public void Attach(Workspace workspace, SettingsSession settings)
    {
        _viewModel = new MainWindowViewModel(workspace, this, this, new ImageClipboard(() => Clipboard), settings);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;
        Closing += OnClosing;
        Closed += (_, _) => _viewModel.Dispose();
    }

    /// <summary>
    /// Keeps the window open while a running Batch stops and while it asks about unsaved work, then closes it once the
    /// user has discarded the work or saved all of it.
    /// </summary>
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_mayClose || _viewModel is null || !_viewModel.MustConfirmLeaving)
            return;
        e.Cancel = true;
        try
        {
            if (!await _viewModel.ConfirmLeavingAsync())
                return;
            _mayClose = true;
            Close();
        }
        catch (Exception failure)
        {
            _viewModel.ReportFailure(failure);
        }
    }

    public Task<LeaveAnswer> AskToLeaveAsync() =>
        new QuestionWindow<LeaveAnswer>(
            Localizer.Instance["LeaveTitle"],
            Localizer.Instance["LeaveQuestion"],
            [
                (Localizer.Instance["SaveAllAnswer"], LeaveAnswer.SaveAll),
                (Localizer.Instance["DiscardAnswer"], LeaveAnswer.Discard),
                (Localizer.Instance["Cancel"], LeaveAnswer.Cancel),
            ]).AskAsync(this);

    public Task<bool> AskToDiscardAsync(string name) =>
        new QuestionWindow<bool>(
            Localizer.Instance["LeaveTitle"],
            Localizer.Instance.Format("DiscardQuestion", name),
            [
                (Localizer.Instance["DiscardAnswer"], true),
                (Localizer.Instance["Cancel"], false),
            ]).AskAsync(this);

    public Task<bool> AskToOverwriteAsync(string path) =>
        new QuestionWindow<bool>(
            Localizer.Instance["OverwriteTitle"],
            Localizer.Instance.Format("OverwriteQuestion", path),
            [
                (Localizer.Instance["ReplaceAnswer"], true),
                (Localizer.Instance["Cancel"], false),
            ]).AskAsync(this);

    public Task<bool> AskToStartBatchAsync(BatchPlan plan) =>
        new QuestionWindow<bool>(
            Localizer.Instance["BatchStartTitle"],
            Localizer.Instance.Format("BatchStartQuestion", plan.Images.Count, plan.Skipped.HasSteps,
                plan.Skipped.AlreadyProcessed, plan.Skipped.Result),
            [
                (Localizer.Instance["BatchStartAnswer"], true),
                (Localizer.Instance["Cancel"], false),
            ]).AskAsync(this);

    /// <summary>Opens the paths given on the command line, relative to the current folder, like any other open.</summary>
    public void OpenCommandLine(IReadOnlyList<string> args)
    {
        if (args.Count > 0)
            _viewModel?.Open(args.Select(Path.GetFullPath).ToList());
    }

    private async void EditSettings()
    {
        if (_viewModel is null)
            return;
        try
        {
            var edited = await new SettingsWindow(_viewModel.Settings).ShowDialog<UserSettings?>(this);
            if (edited is not null)
                _viewModel.ApplySettings(edited);
        }
        catch (Exception e)
        {
            _viewModel.ReportFailure(e);
        }
    }

    public async Task<IReadOnlyList<string>> PickFilesAsync(string? startFolder)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Localizer.Instance["OpenFilesTitle"],
            AllowMultiple = true,
            SuggestedStartLocation = await FolderAsync(startFolder),
            FileTypeFilter =
            [
                new FilePickerFileType(Localizer.Instance["ImagesFilter"])
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"],
                    MimeTypes = ["image/png", "image/jpeg", "image/webp", "image/bmp"],
                },
                new FilePickerFileType(Localizer.Instance["AllFilesFilter"]) { Patterns = ["*"] },
            ],
        });
        return LocalPaths(files);
    }

    public async Task<IReadOnlyList<string>> PickFolderAsync(string? startFolder)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Localizer.Instance["OpenFolderTitle"],
            AllowMultiple = false,
            SuggestedStartLocation = await FolderAsync(startFolder),
        });
        return LocalPaths(folders);
    }

    public async Task<string?> PickSaveFileAsync(string? startFolder, string suggestedName, string name)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Localizer.Instance.Format("SavePastedTitle", name),
            SuggestedStartLocation = await FolderAsync(startFolder),
            SuggestedFileName = suggestedName,
            DefaultExtension = "png",
            ShowOverwritePrompt = false,
            FileTypeChoices =
            [
                new FilePickerFileType(Localizer.Instance["PngFilter"]) { Patterns = ["*.png"], MimeTypes = ["image/png"] },
            ],
        });
        return file?.TryGetLocalPath();
    }

    private async Task<IStorageFolder?> FolderAsync(string? path) =>
        path is null ? null : await StorageProvider.TryGetFolderFromPathAsync(path);

    private static List<string> LocalPaths(IEnumerable<IStorageItem> items) =>
        items.Select(item => item.TryGetLocalPath()).OfType<string>().ToList();

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        // Dropped folders come through as storage items too.
        var paths = LocalPaths(e.DataTransfer.TryGetFiles() ?? []);
        if (paths.Count > 0)
            _viewModel?.OpenAfterAsking(paths);
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            // Space with a left-drag pans. Claim the key only over the viewer, so it still works elsewhere.
            Viewer.IsSpaceHeld = true;
            e.Handled = Viewer.IsPointerOver;
            return;
        }
        if (_viewModel is null || e.KeyModifiers != KeyModifiers.None)
            return;
        if (e.Key == Key.PageDown)
            _viewModel.NextCommand.Execute(null);
        else if (e.Key == Key.PageUp)
            _viewModel.PreviousCommand.Execute(null);
        else
            return;
        e.Handled = true;
    }

    /// <summary>
    /// A press of any button on the viewer gives it focus, so Tab toggles the view and the arrow keys nudge the crop;
    /// a left-button press on the Result starts editing the crop, so it switches to the Original first.
    /// </summary>
    private void OnViewerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Viewer.Focus();
        if (_viewModel is { ShowsResult: true } && e.GetCurrentPoint(Viewer).Properties.IsLeftButtonPressed
            && !Viewer.IsSpaceHeld)
            _viewModel.ShowsResult = false;
    }

    /// <summary>Tab, while the viewer has focus, switches between the Original and the Result.</summary>
    private void OnViewerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || e.KeyModifiers != KeyModifiers.None || _viewModel is null)
            return;
        _viewModel.ToggleResultViewCommand.Execute(null);
        e.Handled = true;
    }

    private void OnManualRadiusKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel is null)
            return;
        _viewModel.ApplyManualRadiusCommand.Execute(null);
        e.Handled = true;
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
            Viewer.IsSpaceHeld = false;
    }

    private void OnViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;
        if (e.Property == ImageViewer.ZoomProperty)
            _viewModel.Zoom = Viewer.Zoom;
        else if (e.Property == ImageViewer.ShownCropProperty)
            _viewModel.ShownCrop = Viewer.ShownCrop;
        else if (e.Property == ImageViewer.NudgedHandleProperty)
            _viewModel.NudgedHandle = Viewer.NudgedHandle;
    }

    // The strip recycles its containers as it scrolls; remembering which entry each one shows tells an entry when it
    // leaves the view, so thumbnails queued for entries scrolled past are not decoded and shown ones can be let go.
    private void OnStripContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (Strip.ItemFromContainer(e.Container) is StripEntry entry)
            _shownEntries[e.Container] = entry;
    }

    private void OnStripContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (_shownEntries.Remove(e.Container, out var entry))
            entry.Unload();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedEntry) && _viewModel?.SelectedEntry is { } entry)
            Strip.ScrollIntoView(entry);
    }
}
