using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ScreenshotTrimmer.App.Controls;
using ScreenshotTrimmer.App.Imaging;
using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Viewing;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

/// <summary>The main window's state: the strip, the selected image, the viewer and the notices.</summary>
public sealed partial class MainWindowViewModel : Observable, IDisposable
{
    private readonly Workspace _workspace;
    private readonly SettingsSession _settings;
    private readonly List<Message> _notices = [];
    private readonly ThumbnailCache _thumbnails = new();
    private IReadOnlyList<WorkspaceImage>? _listedImages;
    private StripEntry? _selectedEntry;
    private Bitmap? _image;
    private Message? _viewerMessage;
    private int _loadNumber;
    /// <summary>The image shown, or being loaded to be shown.</summary>
    private WorkspaceImage? _shown;
    private double _zoom;
    private ViewerBackground _background;
    private string? _openFolder;
    private string? _lastFolder;
    private Crop? _crop;
    private PixelRect? _shownCrop;
    private bool _shownHasShadowPadding;

    public MainWindowViewModel(Workspace workspace, IPathPicker picker, IQuestions questions, IImageClipboard clipboard,
                               SettingsSession settings)
    {
        _workspace = workspace;
        _picker = picker;
        _questions = questions;
        _clipboard = clipboard;
        _settings = settings;
        PasteImageCommand = new RelayCommand(PasteImage);
        CopyResultCommand = new RelayCommand(CopyResult);
        OpenFilesCommand = new RelayCommand(() => OpenPicked(() => picker.PickFilesAsync(_lastFolder)));
        OpenFolderCommand = new RelayCommand(() => OpenPicked(() => picker.PickFolderAsync(_lastFolder)));
        _workspace.Changed += OnWorkspaceChanged;
        Localizer.Instance.PropertyChanged += OnLanguageChanged;
        NextCommand = new RelayCommand(_workspace.Next);
        PreviousCommand = new RelayCommand(_workspace.Previous);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected);
        RemoveEntryCommand = new RelayCommand<StripEntry>(Remove);
        DismissNoticesCommand = new RelayCommand(DismissNotices);
        ToggleShowResultsCommand = new RelayCommand(() => ShowResults = !ShowResults);
        SaveCommand = new RelayCommand(Save, CanSave);
        SaveAllCommand = new RelayCommand(SaveAll, CanSaveAll);
        ClearCropCommand = new RelayCommand(() => RecordCrop(null, mergeKey: null));
        UndoCommand = new RelayCommand(() => ChangeHistory(_workspace.Undo), () => CanChangeHistory(_workspace.CanUndo));
        RedoCommand = new RelayCommand(() => ChangeHistory(_workspace.Redo), () => CanChangeHistory(_workspace.CanRedo));
        ResetCommand = new RelayCommand(() => ChangeHistory(_workspace.Reset),
            () => CanChangeHistory(image => _workspace.Steps(image) != Steps.None));
        ChooseBackgroundCommand = new RelayCommand<ViewerBackground>(background => Background = background);
    }

    public ObservableCollection<StripEntry> Entries { get; } = [];

    /// <summary>The selected strip entry; setting it selects that image in the workspace.</summary>
    public StripEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (value is not null && value.Image.Key != _workspace.Selected?.Key)
                IfStillListed(value.Image, _workspace.Select);
            else
                Set(ref _selectedEntry, value);
        }
    }

    /// <summary>Whether Results are listed in the strip; session-only, off by default.</summary>
    public bool ShowResults
    {
        get => _workspace.ShowResults;
        set
        {
            if (value == _workspace.ShowResults)
                return;
            _workspace.ShowResults = value;
            Raise();
        }
    }

    /// <summary>
    /// The selected image's crop, or <c>null</c> for none, as its Steps hold it. Setting it, as the viewer does at the
    /// end of each drag and on each nudge, records one Step; a run of nudges on the same handle
    /// (<see cref="NudgedHandle"/>) is one Step. An image's first Step also takes its finishing from the remembered
    /// settings.
    /// </summary>
    public Crop? Crop
    {
        get => _crop;
        set => RecordCrop(value, NudgedHandle);
    }

    /// <summary>The handle the viewer nudged to make the crop it sets next, or <c>null</c> when a drag made it.</summary>
    public CropHandle? NudgedHandle { get; set; }

    /// <summary>
    /// Records <paramref name="crop"/> as a Step of the selected image, merged with the previous Step when it has the same
    /// <paramref name="mergeKey"/>, and shows the Original.
    /// </summary>
    private void RecordCrop(Crop? crop, object? mergeKey)
    {
        if (crop == _crop)
            return;
        ChangeShownSteps(steps => steps with { Crop = crop }, mergeKey);
        // The workspace's change has set the crop it recorded; when nothing was recorded, the viewer goes back to the
        // current one.
        Raise(nameof(Crop));
        ShowsResult = false;
    }

    /// <summary>The decoded selected image, or <c>null</c> while it loads or when it cannot be read.</summary>
    public Bitmap? Image
    {
        get => _image;
        private set
        {
            var old = _image;
            if (!Set(ref _image, value))
                return;
            // The viewer lets go of the old bitmap before it is disposed.
            Raise(nameof(ViewerImage));
            Raise(nameof(ShowsFinishing));
            old?.Dispose();
            RaiseStatus();
        }
    }

    /// <summary>The viewer's zoom, 1 for 100%, as the viewer reports it.</summary>
    public double Zoom
    {
        get => _zoom;
        set
        {
            if (Set(ref _zoom, value))
                Raise(nameof(StatusZoom));
        }
    }

    /// <summary>What the viewer draws behind the image. Session-only; starts as the checkerboard.</summary>
    /// <remarks>
    /// The menu's and toolbar's choices show it with <see cref="IsValueConverter"/>; the menu's set it with
    /// <see cref="ChooseBackgroundCommand"/>, since a menu item does not check itself when clicked.
    /// </remarks>
    public ViewerBackground Background
    {
        get => _background;
        set => Set(ref _background, value);
    }

    /// <summary>The crop rectangle the viewer shows, in image pixels, as the viewer reports it, live during a drag.</summary>
    public PixelRect? ShownCrop
    {
        get => _shownCrop;
        set
        {
            if (Set(ref _shownCrop, value))
                Raise(nameof(StatusCrop));
        }
    }

    /// <summary>The crop rectangle's size and position for the status bar, formatted for the current language.</summary>
    public string? StatusCrop =>
        Image is not null && ShownCrop is { } crop
            ? Localizer.Instance.Format("CropSize", crop.Width, crop.Height, crop.X, crop.Y)
            : null;

    /// <summary>The selected image's file name for the status bar.</summary>
    public string? StatusFileName => _shown is null ? null : StripEntry.DisplayName(_shown);

    /// <summary>The shown image's size in pixels (the Original's or the Result's) for the status bar, formatted for the current language.</summary>
    public string? StatusImageSize =>
        ViewerImage is { } image ? Localizer.Instance.Format("ImageSize", image.PixelSize.Width, image.PixelSize.Height) : null;

    /// <summary>The viewer's zoom for the status bar, formatted for the current language.</summary>
    public string? StatusZoom =>
        ViewerImage is not null && Zoom > 0 ? Localizer.Instance.Format("ZoomPercent", Zoom * 100) : null;

    /// <summary>The message shown in the viewer instead of the image, if any.</summary>
    public string? ViewerMessage => _viewerMessage?.Text;

    /// <summary>Whether nothing is listed, so the window shows how to open screenshots.</summary>
    public bool ShowsHint => Entries.Count == 0;

    /// <summary>
    /// The window title: the open folder's name, or the app's name when no single folder is open, marked while any
    /// listed image has unsaved Steps.
    /// </summary>
    public string Title => MarkUnsaved(_openFolder is null
        ? Localizer.Instance["AppTitle"]
        : Localizer.Instance.Format("TitleWithFolder", FolderName(_openFolder)));

    public string NoticeText => string.Join(Environment.NewLine, _notices.Select(notice => notice.Text));

    public bool HasNotices => _notices.Count > 0;

    public ICommand OpenFilesCommand { get; }

    public ICommand OpenFolderCommand { get; }

    public ICommand NextCommand { get; }

    public ICommand PreviousCommand { get; }

    /// <summary>Removes the selected entry from the strip; its file is not touched.</summary>
    public ICommand RemoveSelectedCommand { get; }

    /// <summary>Removes the strip entry given as the parameter from the strip; its file is not touched.</summary>
    public ICommand RemoveEntryCommand { get; }

    public ICommand DismissNoticesCommand { get; }

    /// <summary>Turns <see cref="ShowResults"/> on or off.</summary>
    public ICommand ToggleShowResultsCommand { get; }

    /// <summary>Sets <see cref="Background"/> to the <see cref="ViewerBackground"/> given as the parameter.</summary>
    public ICommand ChooseBackgroundCommand { get; }

    /// <summary>Returns the selected image to the whole image, as one Step; nothing happens when it has no crop.</summary>
    public ICommand ClearCropCommand { get; }

    /// <summary>
    /// Opens <paramref name="paths"/> in the workspace, replacing the list, and reports any path that does not exist
    /// and an empty result as notices. The pickers then start in the folder of the first path that exists, however
    /// the paths were opened.
    /// </summary>
    public void Open(IReadOnlyList<string> paths)
    {
        var missing = _workspace.Open(paths);
        var opened = paths.Select(Path.GetFullPath).Except(missing, FileNames.Comparer).ToList();
        _openFolder = OpenFolder(opened);
        if (opened.Count > 0)
            _lastFolder = FolderOf(opened[0]) ?? _lastFolder;
        Raise(nameof(Title));

        _notices.Clear();
        if (missing.Count > 0)
            _notices.Add(new Message("PathsNotFound", string.Join(", ", missing)));
        if (_workspace.Images.Count == 0)
            _notices.Add(new Message("NoImagesFound"));
        RaiseNotices();
    }

    /// <summary>The settings in use.</summary>
    public UserSettings Settings => _settings.Current;

    /// <summary>
    /// Makes <paramref name="settings"/> current: switches the language, lists the strip again and saves them, reporting
    /// a settings file that cannot be written as a notice.
    /// </summary>
    public void ApplySettings(UserSettings settings)
    {
        var failure = _settings.Change(settings);
        _workspace.ApplySettings(settings);
        ShowFinishing(_workspace.Selected);
        if (failure is not null)
            AddNotice("SettingsNotSaved", _settings.FilePath, failure);
    }

    /// <summary>Reports something that went wrong unexpectedly as a notice, instead of letting it end the app.</summary>
    public void ReportFailure(Exception failure)
    {
        AddNotice("UnexpectedError", failure.Message);
    }

    /// <summary>Lets go of the language, the workspace and the shown image; the window is closing.</summary>
    public void Dispose()
    {
        Localizer.Instance.PropertyChanged -= OnLanguageChanged;
        _workspace.Changed -= OnWorkspaceChanged;
        CancelDetection();
        // A load still running sees it is outdated and disposes its bitmap.
        _loadNumber++;
        Image = null;
        ForgetResult();
    }

    /// <summary>
    /// The folder the opened <paramref name="paths"/> share: a folder itself, or the folder holding a file. <c>null</c>
    /// when nothing was opened or the paths lie in different folders.
    /// </summary>
    private static string? OpenFolder(IEnumerable<string> paths)
    {
        var folders = paths
            .Select(FolderOf)
            .OfType<string>()
            .Distinct(FileNames.Comparer)
            .ToList();
        return folders.Count == 1 ? folders[0] : null;
    }

    /// <summary>The folder <paramref name="path"/> names, or the folder holding the file it names, without a trailing separator.</summary>
    private static string? FolderOf(string path) =>
        (Directory.Exists(path) ? path : Path.GetDirectoryName(path)) is { } folder
            ? Path.TrimEndingDirectorySeparator(folder)
            : null;

    /// <summary>The folder's own name; a root folder such as <c>C:\</c> or <c>/</c>, which has none, shows in full.</summary>
    private static string FolderName(string folder) =>
        Path.GetFileName(folder) is { Length: > 0 } name ? name : folder;

    private void RemoveSelected()
    {
        if (_workspace.Selected is { } selected)
            Remove(selected);
    }

    private void Remove(StripEntry entry) => Remove(entry.Image);

    /// <summary>Removes <paramref name="image"/> from the strip, asking first when that would discard its unsaved Steps.</summary>
    private async void Remove(WorkspaceImage image)
    {
        try
        {
            if (image.HasUnsavedSteps && !await _questions.AskToDiscardAsync(StripEntry.DisplayName(image)))
                return;
            IfStillListed(image, _workspace.Remove);
        }
        catch (Exception e)
        {
            ReportFailure(e);
        }
    }

    /// <summary>
    /// Whether the selected image is the one shown and its Original has loaded, giving both; while another image
    /// loads, the selected one has nothing shown to change or copy yet.
    /// </summary>
    private bool ShowsSelected([NotNullWhen(true)] out WorkspaceImage? image, [NotNullWhen(true)] out RgbaImage? original)
    {
        if (_workspace.Selected is { } selected && _original is { } loaded && selected.Key == _shown?.Key)
        {
            (image, original) = (selected, loaded);
            return true;
        }
        (image, original) = (null, null);
        return false;
    }

    /// <summary>
    /// Changes the shown selected image's Steps with <paramref name="change"/>, its first Step seeded from the
    /// remembered settings, merged with the previous Step when it has the same <paramref name="mergeKey"/>. Nothing
    /// happens while the selected image is not shown yet or has left the list.
    /// </summary>
    private void ChangeShownSteps(Func<Steps, Steps> change, object? mergeKey = null)
    {
        if (!ShowsSelected(out var image, out _))
            return;
        var hasShadowPadding = _shownHasShadowPadding;
        IfStillListed(image, listed => _workspace.SetSteps(listed,
            FirstStep.Apply(_workspace.Steps(listed), Settings, hasShadowPadding, change), mergeKey));
    }

    /// <summary><paramref name="image"/>'s current Steps; <c>Steps.None</c> for no image or one no longer listed.</summary>
    private Steps ListedSteps(WorkspaceImage? image)
    {
        var steps = Steps.None;
        if (image is not null)
            IfStillListed(image, listed => steps = _workspace.Steps(listed));
        return steps;
    }

    /// <summary>
    /// Runs <paramref name="action"/> on <paramref name="image"/> unless the file has left the list on disk meanwhile;
    /// then the strip catches up with the update already posted.
    /// </summary>
    private static void IfStillListed(WorkspaceImage image, Action<WorkspaceImage> action)
    {
        try
        {
            action(image);
        }
        catch (ArgumentException)
        {
        }
    }

    /// <summary>Opens what <paramref name="pick"/> asks the user for; a picker that fails is reported as a notice.</summary>
    private async void OpenPicked(Func<Task<IReadOnlyList<string>>> pick)
    {
        try
        {
            var paths = await pick();
            if (paths.Count > 0)
                await OpenAsync(paths);
        }
        catch (Exception e)
        {
            ReportFailure(e);
        }
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChange change)
    {
        // Opening and selecting raise Changed on the interface thread; anything else must be marshalled.
        if (Dispatcher.UIThread.CheckAccess())
            Apply(change);
        else
            Dispatcher.UIThread.Post(() => Apply(change));
    }

    private void Apply(WorkspaceChange change)
    {
        if (!ReferenceEquals(change.Images, _listedImages))
        {
            _listedImages = change.Images;
            ShowInStrip(change.Images);
            Raise(nameof(ShowsHint));
        }

        var selected = Entries.FirstOrDefault(entry => entry.Image.Key == change.Selected?.Key);
        if (change.Selected?.Key != _shown?.Key)
            LoadImage(change.Selected);
        else if (!string.Equals(change.Selected?.Path, _shown?.Path, StringComparison.Ordinal))
        {
            // The shown file was renamed with only its letter case changed: same image, new name.
            _shown = change.Selected;
            Raise(nameof(StatusFileName));
        }
        Set(ref _selectedEntry, selected, nameof(SelectedEntry));
        ShowCrop(change.Selected);
        ShowFinishing(change.Selected);
        NotifySaveCommands();
        NotifyHistoryCommands();

        if (change.UnavailableFolders.Count > 0)
            ReportUnavailable(change.UnavailableFolders);
        if (change.DroppedWithUnsavedSteps.Count > 0)
            ReportDroppedWithUnsavedSteps(change.DroppedWithUnsavedSteps);
        ShowUnsavedWork(change.Images);
    }

    /// <summary>
    /// Makes the strip show <paramref name="images"/>. When only markers or badges changed, the entries are updated in
    /// place, so the strip neither flickers nor reloads thumbnails.
    /// </summary>
    private void ShowInStrip(IReadOnlyList<WorkspaceImage> images)
    {
        if (images.Count == Entries.Count && images.Select((image, i) => Entries[i].Shows(image)).All(same => same))
        {
            for (var i = 0; i < images.Count; i++)
                Entries[i].Update(images[i]);
            return;
        }
        Entries.Clear();
        foreach (var image in images)
            Entries.Add(new StripEntry(image, _thumbnails, _workspace.LoadOriginal));
    }

    /// <summary>Shows the crop in <paramref name="image"/>'s Steps.</summary>
    private void ShowCrop(WorkspaceImage? image) => Set(ref _crop, ListedSteps(image).Crop, nameof(Crop));

    /// <summary>
    /// Tells the user which folders went away; the title stops naming the open folder once it is gone. The workspace
    /// reports folders without a trailing separator, as <see cref="FolderOf"/> gives them.
    /// </summary>
    private void ReportUnavailable(IReadOnlyList<string> folders)
    {
        AddNotice("FolderUnavailable", string.Join(", ", folders));
        if (_openFolder is not null && folders.Any(folder => FileNames.Same(folder, _openFolder)))
        {
            _openFolder = null;
            Raise(nameof(Title));
        }
    }

    /// <summary>
    /// Decodes <paramref name="image"/>'s Original off the interface thread; a newer selection wins over an older load.
    /// </summary>
    private async void LoadImage(WorkspaceImage? image)
    {
        var load = ++_loadNumber;
        CancelDetection();
        _shown = image;
        Raise(nameof(StatusFileName));
        Image = null;
        ForgetResult();
        SetViewerMessage(null);
        if (image is null)
            return;

        Bitmap? bitmap = null;
        RgbaImage? original = null;
        var hasShadowPadding = false;
        Message? failure = null;
        try
        {
            (bitmap, original, hasShadowPadding) = await Task.Run(() =>
            {
                var pixels = _workspace.LoadOriginal(image);
                return (BitmapLoading.ToBitmap(pixels), pixels, AlreadyProcessed.HasShadowPadding(pixels));
            });
        }
        catch (ArgumentException)
        {
            // The image left the list meanwhile; the change that removed it shows what is selected now.
            return;
        }
        catch (Exception)
        {
            // Unreadable, not an image, or anything unforeseen while decoding: the viewer says so instead.
            failure = new Message("ImageUnreadable", StripEntry.DisplayName(image));
        }

        if (load != _loadNumber)
        {
            bitmap?.Dispose();
            return;
        }
        _shownHasShadowPadding = hasShadowPadding;
        _original = original;
        Image = bitmap;
        SetViewerMessage(failure);
        ShowFinishing(_workspace.Selected);
    }

    private void SetViewerMessage(Message? message)
    {
        _viewerMessage = message;
        Raise(nameof(ViewerMessage));
    }

    private void DismissNotices()
    {
        _notices.Clear();
        RaiseNotices();
    }

    /// <summary>Adds the notice the resource <paramref name="key"/> formats with <paramref name="args"/> and shows it.</summary>
    private void AddNotice(string key, params object?[] args)
    {
        _notices.Add(new Message(key, args));
        RaiseNotices();
    }

    private void RaiseNotices()
    {
        Raise(nameof(NoticeText));
        Raise(nameof(HasNotices));
    }

    private void RaiseStatus()
    {
        Raise(nameof(StatusImageSize));
        Raise(nameof(StatusZoom));
        Raise(nameof(StatusCrop));
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        RaiseNotices();
        RaiseStatus();
        RaiseFinishing();
        ManualRadiusText = FormatRadius(_cleanEdges.ManualRadius);
        Raise(nameof(ViewerMessage));
        Raise(nameof(StatusFileName));
        Raise(nameof(Title));
    }

    /// <summary>A string from the resources with its arguments, formatted in the current language when shown.</summary>
    private sealed record Message(string Key, params object?[] Args)
    {
        public string Text => Localizer.Instance.Format(Key, Args);
    }
}
