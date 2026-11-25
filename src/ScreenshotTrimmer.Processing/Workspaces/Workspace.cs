using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>
/// The images the user opened, in strip order, and which one is selected. Has no interface dependency, so the app's
/// strip and a command-line tool can share it.
/// </summary>
public sealed class Workspace : IDisposable
{
    /// <summary>How long the disk must stay quiet after a change before the list is updated.</summary>
    public static readonly TimeSpan DefaultWatchDelay = TimeSpan.FromMilliseconds(300);

    private readonly Lock _lock = new();
    private readonly TimeSpan _watchDelay;
    private readonly Timer _relistTimer;
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(FileNames.Comparer);
    private bool _disposed;
    private UserSettings _settings;
    private IReadOnlyList<string> _openedPaths = [];
    private bool _showResults;

    public Workspace(UserSettings settings)
        : this(settings, DefaultWatchDelay)
    {
    }

    /// <summary>
    /// A workspace that updates the list once the disk has been quiet for <paramref name="watchDelay"/> after a change
    /// in a watched folder.
    /// </summary>
    public Workspace(UserSettings settings, TimeSpan watchDelay)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfLessThan(watchDelay, TimeSpan.Zero);
        _settings = settings;
        _watchDelay = watchDelay;
        _relistTimer = new Timer(_ => OnDiskQuiet());
    }

    /// <summary>The listed images in strip order.</summary>
    public IReadOnlyList<WorkspaceImage> Images { get; private set; } = [];

    /// <summary>The selected image, or <c>null</c> when nothing is listed.</summary>
    public WorkspaceImage? Selected { get; private set; }

    /// <summary>
    /// Raised once per update to the list or the selection. Updates the caller makes are raised on its thread; updates
    /// that follow the disk are raised on a thread-pool thread.
    /// </summary>
    public event EventHandler<WorkspaceChange>? Changed;

    /// <summary>
    /// Replaces the list with the images for <paramref name="paths"/>: one file lists its whole folder with that file
    /// selected; a folder or several paths list their images with the first selected. Paths that exist neither as a
    /// file nor as a folder are skipped and returned, so the caller can tell the user.
    /// </summary>
    public IReadOnlyList<string> Open(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var fullPaths = paths.Select(Path.GetFullPath).ToList();
        var missing = fullPaths.Where(path => !File.Exists(path) && !Directory.Exists(path)).ToList();
        var existing = fullPaths.Except(missing).ToList();

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _openedPaths = existing;
            var listing = List();
            Selected = listing.Selected is { } opened
                ? Images.First(image => image.Path == opened.Path)
                : Images.FirstOrDefault();
            RaiseChanged();
        }
        return missing;
    }

    /// <summary>
    /// Whether Suffix-mode Results are listed in their sorted place instead of hidden. Session-only and off by default;
    /// changing it keeps the selection, except that a selected Result being hidden gives way to its Original.
    /// </summary>
    public bool ShowResults
    {
        get => _showResults;
        set
        {
            lock (_lock)
            {
                if (value == _showResults)
                    return;
                _showResults = value;
                var selected = Selected;
                List();
                Selected = Images.FirstOrDefault(image => image.Path == selected?.Path)
                    ?? (selected is { IsResult: true } ? OriginalOf(selected) : null)
                    ?? Images.FirstOrDefault();
                RaiseChanged();
            }
        }
    }

    /// <summary>
    /// Lists the opened paths again with <paramref name="settings"/>' save mode and Result suffix, which decide which
    /// files are Results. The selection stays where the selected file is still listed; otherwise it passes to the
    /// entry now in its place.
    /// </summary>
    public void ApplySettings(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_lock)
        {
            _settings = settings;
            if (_openedPaths.Count == 0)
                return;

            var selectedIndex = SelectedIndex;
            var selectedPath = Selected?.Path;
            List();
            Selected = Images.FirstOrDefault(image => image.Path == selectedPath)
                ?? (Images.Count == 0 ? null : Images[Math.Clamp(selectedIndex, 0, Images.Count - 1)]);
            RaiseChanged();
        }
    }

    /// <summary>Selects <paramref name="image"/>, which must be one of <see cref="Images"/>.</summary>
    public void Select(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        lock (_lock)
        {
            var index = IndexOf(image);
            if (index < 0)
                throw new ArgumentException("The image is not listed in this workspace.", nameof(image));
            SelectAt(index);
        }
    }

    /// <summary>Selects the next image; does nothing on the last one.</summary>
    public void Next()
    {
        lock (_lock)
            SelectAt(SelectedIndex + 1);
    }

    /// <summary>Selects the previous image; does nothing on the first one.</summary>
    public void Previous()
    {
        lock (_lock)
            SelectAt(SelectedIndex - 1);
    }

    /// <summary>Stops watching the disk.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
            _relistTimer.Dispose();
            foreach (var watcher in _watchers.Values)
                watcher.Dispose();
            _watchers.Clear();
        }
    }

    /// <summary>The listed Original of the Result <paramref name="result"/>, if any.</summary>
    private WorkspaceImage? OriginalOf(WorkspaceImage result)
    {
        var stem = StemOf(result.Path);
        var suffix = _settings.ResultSuffix;
        if (!stem.EndsWith(suffix, FileNames.Comparison))
            return null;
        var originalStem = stem[..^suffix.Length];
        return Images.FirstOrDefault(image => FileNames.Same(StemOf(image.Path), originalStem));
    }

    /// <summary>
    /// The listed entry for <paramref name="before"/>[<paramref name="index"/>] if it is still listed; otherwise the
    /// nearest still-listed entry after it, or before it when none follows. <c>null</c> when nothing is listed.
    /// </summary>
    private WorkspaceImage? SurvivorNear(IReadOnlyList<WorkspaceImage> before, int index)
    {
        var candidates = before.Skip(index).Concat(before.Take(index).Reverse());
        return candidates
            .Select(old => Images.FirstOrDefault(image => image.Path == old.Path))
            .FirstOrDefault(image => image is not null)
            ?? Images.FirstOrDefault();
    }

    private static string StemOf(string path) =>
        Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path));

    private int SelectedIndex => Selected is null ? -1 : IndexOf(Selected);

    private int IndexOf(WorkspaceImage image)
    {
        for (var i = 0; i < Images.Count; i++)
        {
            if (Images[i].Path == image.Path)
                return i;
        }
        return -1;
    }

    /// <summary>Selects the image at <paramref name="index"/>; an index outside the list, or the selection itself, changes nothing.</summary>
    private void SelectAt(int index)
    {
        if (index < 0 || index >= Images.Count || index == SelectedIndex)
            return;
        Selected = Images[index];
        RaiseChanged();
    }

    /// <summary>
    /// Replaces <see cref="Images"/> with the current listing of the opened paths under the current settings and
    /// <see cref="ShowResults"/>; the one re-list path for <see cref="Open"/>, <see cref="ShowResults"/> and
    /// <see cref="ApplySettings"/>.
    /// </summary>
    private Listing List()
    {
        var listing = ImageListing.List(_openedPaths, ListingOptionsFor(_settings, _showResults));
        Images = listing.Images
            .Select(image => new WorkspaceImage
            {
                Path = image.Path, IsResult = image.IsResult, HasResult = image.HasResult,
                ContentVersion = ContentVersion.Of(image.Path),
            })
            .ToList();
        Watch(FoldersToWatch());
        return listing;
    }

    /// <summary>The folders whose changes can change the list: each opened folder and every folder holding an image.</summary>
    private IEnumerable<string> FoldersToWatch() =>
        _openedPaths.Select(path => Directory.Exists(path) ? path : Path.GetDirectoryName(path)!)
            .Concat(Images.Select(image => Path.GetDirectoryName(image.Path)!))
            .Where(Directory.Exists);

    /// <summary>Watches exactly <paramref name="folders"/>, without their subfolders.</summary>
    private void Watch(IEnumerable<string> folders)
    {
        var wanted = folders.ToHashSet(FileNames.Comparer);
        foreach (var folder in _watchers.Keys.Where(folder => !wanted.Contains(folder)).ToList())
        {
            _watchers[folder].Dispose();
            _watchers.Remove(folder);
        }
        foreach (var folder in wanted.Where(folder => !_watchers.ContainsKey(folder)))
        {
            var watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            watcher.Created += OnDiskChanged;
            watcher.Deleted += OnDiskChanged;
            watcher.Changed += OnDiskChanged;
            watcher.Renamed += OnDiskChanged;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(folder, watcher);
        }
    }

    /// <summary>Puts the re-list off until the disk has been quiet for the watch delay, so a burst gives one update.</summary>
    /// <remarks>
    /// Takes no lock: disposing a watcher waits for its event handlers, and watchers are disposed under the lock.
    /// </remarks>
    private void OnDiskChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            _relistTimer.Change(_watchDelay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // The workspace was disposed while this event was on its way.
        }
    }

    private void OnDiskQuiet()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            var before = Images;
            var selectedIndex = SelectedIndex;
            List();
            Selected = selectedIndex < 0 ? Images.FirstOrDefault() : SurvivorNear(before, selectedIndex);
            RaiseChanged();
        }
    }

    private void RaiseChanged() =>
        Changed?.Invoke(this, new WorkspaceChange { Images = Images, Selected = Selected });

    private static ListingOptions ListingOptionsFor(UserSettings settings, bool includeResults) =>
        ListingOptions.For(
            new SaveOptions
            {
                Mode = settings.SaveMode,
                ResultSuffix = settings.ResultSuffix,
                BackupSuffix = settings.BackupSuffix,
            },
            includeResults);
}
