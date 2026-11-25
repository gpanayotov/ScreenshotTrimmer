using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ScreenshotTrimmer.App.Imaging;
using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

/// <summary>The main window's state: the strip, the selected image, the viewer and the notices.</summary>
public sealed class MainWindowViewModel : Observable
{
    private readonly Workspace _workspace;
    private readonly SettingsSession _settings;
    private readonly List<Message> _notices = [];
    private IReadOnlyList<WorkspaceImage>? _listedImages;
    private StripEntry? _selectedEntry;
    private Bitmap? _image;
    private Message? _viewerMessage;
    private int _loadNumber;
    private string? _shownPath;

    public MainWindowViewModel(Workspace workspace, IPathPicker picker, SettingsSession settings)
    {
        _workspace = workspace;
        _settings = settings;
        OpenFilesCommand = new RelayCommand(async () => OpenPicked(await picker.PickFilesAsync()));
        OpenFolderCommand = new RelayCommand(async () => OpenPicked(await picker.PickFolderAsync()));
        _workspace.Changed += OnWorkspaceChanged;
        Localizer.Instance.PropertyChanged += OnLanguageChanged;
        NextCommand = new RelayCommand(_workspace.Next);
        PreviousCommand = new RelayCommand(_workspace.Previous);
        DismissNoticesCommand = new RelayCommand(DismissNotices);
    }

    public ObservableCollection<StripEntry> Entries { get; } = [];

    /// <summary>The selected strip entry; setting it selects that image in the workspace.</summary>
    public StripEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (value is not null && value.Image.Path != _workspace.Selected?.Path)
                _workspace.Select(value.Image);
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

    /// <summary>The decoded selected image, or <c>null</c> while it loads or when it cannot be read.</summary>
    public Bitmap? Image
    {
        get => _image;
        private set
        {
            var old = _image;
            if (Set(ref _image, value))
                old?.Dispose();
        }
    }

    /// <summary>The message shown in the viewer instead of the image, if any.</summary>
    public string? ViewerMessage => _viewerMessage?.Text;

    /// <summary>Whether nothing is listed, so the window shows how to open screenshots.</summary>
    public bool ShowsHint => Entries.Count == 0;

    public string NoticeText => string.Join(Environment.NewLine, _notices.Select(notice => notice.Text));

    public bool HasNotices => _notices.Count > 0;

    public ICommand OpenFilesCommand { get; }

    public ICommand OpenFolderCommand { get; }

    public ICommand NextCommand { get; }

    public ICommand PreviousCommand { get; }

    public ICommand DismissNoticesCommand { get; }

    /// <summary>
    /// Opens <paramref name="paths"/> in the workspace, replacing the list, and reports any path that does not exist
    /// and an empty result as notices.
    /// </summary>
    public void Open(IReadOnlyList<string> paths)
    {
        var missing = _workspace.Open(paths);

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
        if (failure is not null)
        {
            _notices.Add(new Message("SettingsNotSaved", _settings.FilePath, failure));
            RaiseNotices();
        }
    }

    private void OpenPicked(IReadOnlyList<string> paths)
    {
        if (paths.Count > 0)
            Open(paths);
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
            Entries.Clear();
            foreach (var image in change.Images)
                Entries.Add(new StripEntry(image));
            Raise(nameof(ShowsHint));
        }

        var selected = Entries.FirstOrDefault(entry => entry.Image.Path == change.Selected?.Path);
        if (change.Selected?.Path != _shownPath)
            LoadImage(change.Selected?.Path);
        Set(ref _selectedEntry, selected, nameof(SelectedEntry));
    }

    /// <summary>Decodes <paramref name="path"/> off the interface thread; a newer selection wins over an older load.</summary>
    private async void LoadImage(string? path)
    {
        var load = ++_loadNumber;
        _shownPath = path;
        Image = null;
        SetViewerMessage(null);
        if (path is null)
            return;

        Bitmap? bitmap = null;
        Message? failure = null;
        try
        {
            bitmap = await Task.Run(() => BitmapLoading.Load(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            failure = new Message("ImageUnreadable", Path.GetFileName(path));
        }

        if (load != _loadNumber)
        {
            bitmap?.Dispose();
            return;
        }
        Image = bitmap;
        SetViewerMessage(failure);
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

    private void RaiseNotices()
    {
        Raise(nameof(NoticeText));
        Raise(nameof(HasNotices));
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        RaiseNotices();
        Raise(nameof(ViewerMessage));
    }

    /// <summary>A string from the resources with its arguments, formatted in the current language when shown.</summary>
    private sealed record Message(string Key, params object?[] Args)
    {
        public string Text => Localizer.Instance.Format(Key, Args);
    }
}
