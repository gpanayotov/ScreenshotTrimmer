using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ScreenshotTrimmer.App.Controls;
using ScreenshotTrimmer.App.Localization;
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
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        // Tunnel, so the strip's own Page Up / Page Down handling does not jump a whole page.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => Viewer.IsSpaceHeld = false;
        Viewer.PropertyChanged += OnViewerPropertyChanged;
        AddViewerShortcuts();
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

    /// <summary>Shows <paramref name="workspace"/> in this window, which picks the paths it opens.</summary>
    public void Attach(Workspace workspace)
    {
        _viewModel = new MainWindowViewModel(workspace, this);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;
    }

    /// <summary>Opens the paths given on the command line, relative to the current folder, like any other open.</summary>
    public void OpenCommandLine(IReadOnlyList<string> args)
    {
        if (args.Count > 0)
            _viewModel?.Open(args.Select(Path.GetFullPath).ToList());
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

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
            Viewer.IsSpaceHeld = false;
    }

    private void OnViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ImageViewer.ZoomProperty && _viewModel is not null)
            _viewModel.Zoom = Viewer.Zoom;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedEntry) && _viewModel?.SelectedEntry is { } entry)
            Strip.ScrollIntoView(entry);
    }
}
