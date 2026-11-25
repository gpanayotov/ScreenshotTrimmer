using System.Collections.Concurrent;
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
    private readonly Dictionary<string, FileSystemWatcher> _parentWatchers = new(FileNames.Comparer);
    private readonly ConcurrentQueue<(string From, string To)> _renames = new();
    private bool _disposed;
    private UserSettings _settings;
    private IReadOnlyList<string> _openedPaths = [];
    private bool _openedOneFile;
    private IReadOnlyList<string> _homeFolders = [];
    private IReadOnlySet<string> _unavailableFolders = new HashSet<string>();
    private bool _lastRelistFailed;
    private bool _showResults;
    private HashSet<string> _removed = new(FileNames.Comparer);

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
    /// selected; a folder or several paths list their images with the first selected. Removed entries are listed
    /// again. Paths that exist neither as a file nor as a folder are skipped and returned, so the caller can tell the
    /// user.
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
            _openedOneFile = existing is [var only] && File.Exists(only);
            _homeFolders = existing
                .Select(path => Directory.Exists(path) ? path : Path.GetDirectoryName(path)!)
                .Select(Path.TrimEndingDirectorySeparator)
                .Distinct(FileNames.Comparer)
                .ToList();
            _unavailableFolders = new HashSet<string>();
            _lastRelistFailed = false;
            _removed = new HashSet<string>(FileNames.Comparer);
            _renames.Clear();
            _relistTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
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

    /// <summary>
    /// Hides <paramref name="image"/>, which must be one of <see cref="Images"/>, for as long as this list is open; its
    /// file is not touched.
    /// </summary>
    public void Remove(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        lock (_lock)
        {
            if (IndexOf(image) < 0)
                throw new ArgumentException("The image is not listed in this workspace.", nameof(image));
            var before = Images.Select(listed => listed.Path).ToList();
            var selectedIndex = SelectedIndex;
            _removed.Add(image.Path);
            List();
            Selected = SurvivorNear(before, selectedIndex);
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
            foreach (var watcher in _watchers.Values.Concat(_parentWatchers.Values))
                watcher.Dispose();
            _watchers.Clear();
            _parentWatchers.Clear();
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
    /// The listed entry at the path <paramref name="before"/>[<paramref name="index"/>] if it is still listed; otherwise the
    /// nearest still-listed entry after it, or before it when none follows. <c>null</c> when nothing is listed.
    /// </summary>
    private WorkspaceImage? SurvivorNear(IReadOnlyList<string> before, int index)
    {
        var candidates = before.Skip(index).Concat(before.Take(index).Reverse());
        return candidates
            .Select(path => Images.FirstOrDefault(image => image.Path == path))
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
    /// <see cref="ShowResults"/>, less the removed entries; the one re-list path, so removed entries stay hidden
    /// through every update.
    /// </summary>
    private Listing List()
    {
        var listing = ImageListing.List(_openedPaths, ListingOptionsFor(_settings, _showResults));
        Images = listing.Images
            .Where(image => !_removed.Contains(image.Path))
            .Select(image => new WorkspaceImage
            {
                Path = image.Path, IsResult = image.IsResult, HasResult = image.HasResult,
                ContentVersion = ContentVersion.Of(image.Path),
            })
            .ToList();
        Watch();
        return listing;
    }

    /// <summary>
    /// Watches the home folders and every folder holding a listed image for changes to their files, and each home
    /// folder's parent for the home folder itself being deleted, renamed or created again. Subfolders are not watched.
    /// </summary>
    private void Watch()
    {
        var folders = _homeFolders.Concat(Images.Select(image => Path.GetDirectoryName(image.Path)!))
            .Where(Directory.Exists);
        Sync(_watchers, folders, folder => new FileSystemWatcher(folder)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite | NotifyFilters.Size,
        });
        Sync(_parentWatchers, _homeFolders, folder =>
            Path.GetDirectoryName(folder) is { } parent && Directory.Exists(parent)
                ? new FileSystemWatcher(parent, Path.GetFileName(folder)) { NotifyFilter = NotifyFilters.DirectoryName }
                : null);
    }

    /// <summary>
    /// Makes <paramref name="watchers"/> hold one running watcher per folder in <paramref name="folders"/>, made by
    /// <paramref name="create"/>; a watcher that stopped after an error is replaced, and a folder that cannot be
    /// watched is skipped.
    /// </summary>
    private void Sync(
        Dictionary<string, FileSystemWatcher> watchers,
        IEnumerable<string> folders,
        Func<string, FileSystemWatcher?> create)
    {
        var wanted = folders.ToHashSet(FileNames.Comparer);
        var stale = watchers.Where(pair => !wanted.Contains(pair.Key) || !pair.Value.EnableRaisingEvents).ToList();
        foreach (var (folder, watcher) in stale)
        {
            watcher.Dispose();
            watchers.Remove(folder);
        }
        foreach (var folder in wanted.Where(folder => !watchers.ContainsKey(folder)))
        {
            FileSystemWatcher? watcher = null;
            try
            {
                watcher = create(folder);
                if (watcher is null)
                    continue;
                watcher.IncludeSubdirectories = false;
                watcher.Created += OnDiskChanged;
                watcher.Deleted += OnDiskChanged;
                watcher.Changed += OnDiskChanged;
                watcher.Renamed += OnRenamed;
                watcher.Error += OnWatcherError;
                watcher.EnableRaisingEvents = true;
                watchers.Add(folder, watcher);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Gone or unreadable since it was checked; the next re-list reports it.
                watcher?.Dispose();
            }
        }
    }

    /// <summary>A watcher overflowed or lost its folder: list again, which also replaces a watcher that stopped.</summary>
    private void OnWatcherError(object sender, ErrorEventArgs e) => RelistSoon();

    /// <summary>Puts the re-list off until the disk has been quiet for the watch delay, so a burst gives one update.</summary>
    /// <remarks>
    /// Takes no lock: disposing a watcher waits for its event handlers, and watchers are disposed under the lock.
    /// </remarks>
    private void OnDiskChanged(object sender, FileSystemEventArgs e) => RelistSoon();

    private void RelistSoon()
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

    /// <summary>Notes a rename, so the opened paths and the selection can follow the file to its new name.</summary>
    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        _renames.Enqueue((e.OldFullPath, e.FullPath));
        RelistSoon();
    }

    /// <summary>Maps a path to where the renames reported since the last re-list moved it.</summary>
    private Func<string, string> TakeRenames()
    {
        var moves = new List<(string From, string To)>();
        while (_renames.TryDequeue(out var rename))
            moves.Add(rename);
        return path =>
        {
            foreach (var (from, to) in moves)
            {
                if (path == from)
                    path = to;
            }
            return path;
        };
    }

    private void OnDiskQuiet()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            var renamed = TakeRenames();
            _openedPaths = _openedPaths.Select(renamed).ToList();
            _removed = _removed.Select(renamed).ToHashSet(FileNames.Comparer);
            if (_openedOneFile && _openedPaths is [var file] && !File.Exists(file))
            {
                // The file opened on its own is gone; the rest of its folder stays listed.
                _openedPaths = [Path.GetDirectoryName(file)!];
                _openedOneFile = false;
            }
            var old = Images;
            var oldSelected = Selected;
            var selectedIndex = SelectedIndex;
            var unavailable = _homeFolders.Where(folder => !Directory.Exists(folder)).ToHashSet(FileNames.Comparer);
            try
            {
                List();
                _lastRelistFailed = false;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A folder vanished while it was being listed: try once more after the delay, then give up on all of them.
                if (!_lastRelistFailed)
                {
                    _lastRelistFailed = true;
                    RelistSoon();
                    return;
                }
                Images = [];
                Watch();
                unavailable = _homeFolders.ToHashSet(FileNames.Comparer);
            }
            Selected = selectedIndex < 0
                ? Images.FirstOrDefault()
                : SurvivorNear(old.Select(image => renamed(image.Path)).ToList(), selectedIndex);
            var newlyUnavailable = _homeFolders
                .Where(folder => unavailable.Contains(folder) && !_unavailableFolders.Contains(folder))
                .ToList();
            _unavailableFolders = unavailable;

            // Events for files that are not listed, or writes that left a file as it was, change nothing to show.
            if (Images.SequenceEqual(old) && Selected == oldSelected && newlyUnavailable.Count == 0)
            {
                Images = old;
                Selected = oldSelected;
                return;
            }
            RaiseChanged(newlyUnavailable);
        }
    }

    private void RaiseChanged(IReadOnlyList<string>? unavailableFolders = null) =>
        Changed?.Invoke(this, new WorkspaceChange
        {
            Images = Images, Selected = Selected, UnavailableFolders = unavailableFolders ?? [],
        });

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
