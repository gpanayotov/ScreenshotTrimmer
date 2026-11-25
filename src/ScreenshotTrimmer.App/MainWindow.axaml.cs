using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

public partial class MainWindow : Window, IPathPicker
{
    private string? _lastFolder;
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        ExitItem.Click += (_, _) => Close();
        SettingsItem.Click += (_, _) => EditSettings();
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Control), Command = new RelayCommand(EditSettings),
        });
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        // Tunnel, so the strip's own Page Up / Page Down handling does not jump a whole page.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Shows <paramref name="workspace"/> in this window, which picks the paths it opens and edits
    /// <paramref name="settings"/>.
    /// </summary>
    public void Attach(Workspace workspace, SettingsSession settings)
    {
        _viewModel = new MainWindowViewModel(workspace, this, settings);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;
    }

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
        var edited = await new SettingsWindow(_viewModel.Settings).ShowDialog<UserSettings?>(this);
        if (edited is not null)
            _viewModel.ApplySettings(edited);
    }

    public async Task<IReadOnlyList<string>> PickFilesAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Localizer.Instance["OpenFilesTitle"],
            AllowMultiple = true,
            SuggestedStartLocation = await LastFolderAsync(),
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
        var paths = LocalPaths(files);
        if (paths.Count > 0)
            _lastFolder = Path.GetDirectoryName(paths[0]);
        return paths;
    }

    public async Task<IReadOnlyList<string>> PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Localizer.Instance["OpenFolderTitle"],
            AllowMultiple = false,
            SuggestedStartLocation = await LastFolderAsync(),
        });
        var paths = LocalPaths(folders);
        if (paths.Count > 0)
            _lastFolder = paths[0];
        return paths;
    }

    private async Task<IStorageFolder?> LastFolderAsync() =>
        _lastFolder is null ? null : await StorageProvider.TryGetFolderFromPathAsync(_lastFolder);

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
            _viewModel?.Open(paths);
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
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

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedEntry) && _viewModel?.SelectedEntry is { } entry)
            Strip.ScrollIntoView(entry);
    }
}
