using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>
/// The images the user opened, in strip order, and which one is selected. Has no interface dependency, so the app's
/// strip and a command-line tool can share it.
/// </summary>
public sealed partial class Workspace : IDisposable
{
    /// <summary>How long the disk must stay quiet after a change before the list is updated.</summary>
    public static readonly TimeSpan DefaultWatchDelay = TimeSpan.FromMilliseconds(300);

    private readonly Lock _lock = new();
    private readonly FolderWatch _folderWatch;
    private readonly Queue<WorkspaceChange> _pendingChanges = new();
    private readonly Lock _announceLock = new();
    private bool _announcing;
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
        _folderWatch = new FolderWatch(watchDelay, OnDiskQuiet);
    }

    /// <summary>
    /// The listed images in strip order. The same list instance is kept until the listed images change, so a caller
    /// can tell an update that only moved the selection by comparing references.
    /// </summary>
    public IReadOnlyList<WorkspaceImage> Images { get; private set; } = [];

    /// <summary>The selected image, or <c>null</c> when nothing is listed.</summary>
    public WorkspaceImage? Selected { get; private set; }

    /// <summary>
    /// Raised once per update to the list or the selection, in the order the updates were made, after the workspace has
    /// released its lock, so a handler may read or update it from any thread. An update is announced on the thread that
    /// made it (a thread-pool thread for updates that follow the disk), unless another thread is announcing at the time:
    /// that thread then announces it after the ones before it. An update made from a handler is announced once every
    /// handler has seen the current one.
    /// </summary>
    public event EventHandler<WorkspaceChange>? Changed;

    /// <summary>
    /// Replaces the list with the images for <paramref name="paths"/>: one file lists its whole folder with that file
    /// selected; a folder or several paths list their images with the first selected. Removed entries are listed
    /// again, Pasted images leave the list, and every image starts with no Steps. Paths that exist neither as a file nor as a folder are skipped and returned, so the caller can tell the
    /// user.
    /// </summary>
    public IReadOnlyList<string> Open(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var fullPaths = paths.Select(Path.GetFullPath).ToList();
        var missing = fullPaths.Where(path => !File.Exists(path) && !Directory.Exists(path)).ToList();
        var existing = fullPaths.Except(missing).ToList();

        Update(() =>
        {
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
            _steps.Clear();
            _backupSaves.Clear();
            _noSubjectFound.Clear();
            ForgetPasted();
            _folderWatch.Reset();
            Relist(listing => listing.Selected is { } opened ? Find(opened.Path) : Images.FirstOrDefault());
        });
        return missing;
    }

    /// <summary>
    /// Whether Suffix-mode Results are listed in their sorted place instead of hidden. Session-only and off by default;
    /// changing it keeps the selection, except that a selected Result being hidden gives way to its Original. A Result
    /// with unsaved Steps is never hidden, so no unsaved work is out of sight.
    /// </summary>
    public bool ShowResults
    {
        get => _showResults;
        set => Update(() =>
        {
            if (value == _showResults)
                return;
            _showResults = value;
            var selected = Selected;
            Relist(_ => (selected is null ? null : Find(selected.Key))
                ?? (selected is { IsResult: true } ? OriginalOf(selected) : null)
                ?? Images.FirstOrDefault());
        });
    }

    /// <summary>
    /// Lists the opened paths again with <paramref name="settings"/>' save mode and Result suffix, which decide which
    /// files are Results. The selection stays where the selected file is still listed; otherwise it passes to the
    /// entry now in its place.
    /// </summary>
    public void ApplySettings(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Update(() =>
        {
            _settings = settings;
            if (_openedPaths.Count == 0)
                return;
            var selectedIndex = SelectedIndex;
            var selected = Selected;
            Relist(_ => (selected is null ? null : Find(selected.Key))
                ?? (Images.Count == 0 ? null : Images[Math.Clamp(selectedIndex, 0, Images.Count - 1)]));
        });
    }

    /// <summary>
    /// Hides <paramref name="image"/>, which must be one of <see cref="Images"/>, for as long as this list is open; its
    /// file is not touched. A Pasted image that has not been saved is dropped with its pixels.
    /// </summary>
    public void Remove(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Update(() =>
        {
            RequireListed(image);
            var before = Images.Select(image => image.Key).ToList();
            var selectedIndex = SelectedIndex;
            _noSubjectFound.Remove(image.Key);
            if (image.Path is { } path)
                _removed.Add(path);
            else
                DropPasted(image);
            Relist(_ => SurvivorNear(before, selectedIndex));
        });
    }

    /// <summary>Selects <paramref name="image"/>, which must be one of <see cref="Images"/>.</summary>
    public void Select(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Update(() => SelectAt(RequireListed(image)));
    }

    /// <summary>Selects the next image; does nothing on the last one.</summary>
    public void Next() => Update(() => SelectAt(SelectedIndex + 1));

    /// <summary>Selects the previous image; does nothing on the first one.</summary>
    public void Previous() => Update(() => SelectAt(SelectedIndex - 1));

    /// <summary>Stops watching the disk. Every later update throws <see cref="ObjectDisposedException"/>.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
            _folderWatch.Dispose();
        }
    }

    /// <summary>
    /// Runs <paramref name="change"/> under the lock on a workspace that is not disposed, then announces the updates it
    /// made.
    /// </summary>
    private void Update(Action change)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            change();
        }
        Announce();
    }

    /// <summary>
    /// Lists the opened paths again, selects what <paramref name="reselect"/> picks from the new list, and queues the
    /// change to be announced.
    /// </summary>
    private void Relist(Func<Listing, WorkspaceImage?> reselect)
    {
        var listing = List();
        Selected = reselect(listing);
        QueueChange();
    }

    /// <summary>
    /// Replaces <see cref="Images"/> with the current listing of the opened paths under the current settings and
    /// <see cref="ShowResults"/>, then the saved Pasted images not listed among them, less the removed entries, then
    /// the Pasted images not saved yet; the one re-list path, so removed entries stay hidden and Pasted images stay
    /// listed through every update. A listing equal to the current one keeps the current list instance.
    /// </summary>
    private Listing List()
    {
        var listing = ImageListing.List(_openedPaths, ListingOptionsFor(_settings, _showResults) with
        {
            KeptResults = _steps.Keys.Where(HasUnsavedSteps).Select(key => key.Path).OfType<string>()
                .ToHashSet(FileNames.Comparer),
        });
        var files = listing.Images
            .Select(image => (image.Path, image.IsResult, image.HasResult))
            .Concat(SavedPastedPaths().Select(path => (Path: path, IsResult: false, HasResult: false)))
            .Where(file => !_removed.Contains(file.Path))
            .DistinctBy(file => file.Path, FileNames.Comparer);
        var images = files
            .Select(file =>
            {
                var key = ImageKey.OfFile(file.Path);
                return new WorkspaceImage
                {
                    Key = key, IsResult = file.IsResult, HasResult = file.HasResult,
                    ContentVersion = ContentVersion.Of(file.Path),
                    HasUnsavedSteps = HasUnsavedSteps(key),
                    NoSubjectFound = _noSubjectFound.Contains(key),
                };
            })
            .Concat(PastedEntries())
            .ToList();
        // Keys compare paths as the file system does; a rename that only changes case is still a new listing.
        if (!images.SequenceEqual(Images) || !images.Select(image => image.Path).SequenceEqual(Images.Select(image => image.Path)))
            Images = images;
        Watch();
        return listing;
    }

    /// <summary>Watches the home folders and every folder holding a listed image.</summary>
    private void Watch() =>
        _folderWatch.Watch(Images.Select(image => image.Path).OfType<string>()
            .Select(path => Path.GetDirectoryName(path)!), _homeFolders);

    private void OnDiskQuiet()
    {
        lock (_lock)
        {
            if (!_disposed)
                FollowDisk();
        }
        Announce();
    }

    /// <summary>
    /// Lists the opened paths again after the disk changed, following renames, and queues a change only when there is
    /// something new to show.
    /// </summary>
    private void FollowDisk()
    {
        var renamed = _folderWatch.TakeRenames();
        _openedPaths = _openedPaths.Select(renamed).ToList();
        _removed = _removed.Select(renamed).ToHashSet(FileNames.Comparer);
        FollowRenamesOfSavedPasted(renamed);
        FollowRenamesWithSteps(renamed);
        FollowRenamesOfNoSubjectFound(renamed);
        FollowRenamesOfBackupSaves(renamed);
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
                _folderWatch.CallBackSoon();
                return;
            }
            if (Images.Count > 0)
                Images = [];
            Watch();
            unavailable = _homeFolders.ToHashSet(FileNames.Comparer);
        }
        Selected = selectedIndex < 0
            ? Images.FirstOrDefault()
            : SurvivorNear(old.Select(image => image.Key.Renamed(renamed)).ToList(), selectedIndex);
        var newlyUnavailable = _homeFolders
            .Where(folder => unavailable.Contains(folder) && !_unavailableFolders.Contains(folder))
            .ToList();
        _unavailableFolders = unavailable;
        var dropped = DiscardStepsOfDropped(old.Select(image => image.Path).OfType<string>().Select(renamed));
        ForgetSavedPastedNotListed();

        // Events for files that are not listed, or writes that left a file as it was, change nothing to show.
        if (ReferenceEquals(Images, old) && Selected == oldSelected && newlyUnavailable.Count == 0)
            return;
        QueueChange(newlyUnavailable, dropped);
    }

    /// <summary>The listed Original of the Result <paramref name="result"/>, if any.</summary>
    private WorkspaceImage? OriginalOf(WorkspaceImage result)
    {
        if (result.Path is not { } path)
            return null;
        var stem = StemOf(path);
        var suffix = _settings.ResultSuffix;
        if (!stem.EndsWith(suffix, FileNames.Comparison))
            return null;
        var originalStem = stem[..^suffix.Length];
        return Images.FirstOrDefault(image => image.Path is { } candidate && FileNames.Same(StemOf(candidate), originalStem));
    }

    /// <summary>
    /// The listed entry with the key <paramref name="before"/>[<paramref name="index"/>] if it is still listed; otherwise the
    /// nearest still-listed entry after it, or before it when none follows. <c>null</c> when nothing is listed.
    /// </summary>
    private WorkspaceImage? SurvivorNear(IReadOnlyList<ImageKey> before, int index)
    {
        var candidates = before.Skip(index).Concat(before.Take(index).Reverse());
        return candidates.Select(Find).FirstOrDefault(image => image is not null) ?? Images.FirstOrDefault();
    }

    private static string StemOf(string path) =>
        Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path));

    /// <summary>The listed entry whose <see cref="WorkspaceImage.Key">key</see> is <paramref name="key"/>, or <c>null</c>.</summary>
    private WorkspaceImage? Find(ImageKey key) => Images.FirstOrDefault(image => image.Key == key);

    /// <summary>The listed entry of the file at <paramref name="path"/>, or <c>null</c>.</summary>
    private WorkspaceImage? Find(string path) => Find(ImageKey.OfFile(path));

    private int SelectedIndex => Selected is null ? -1 : IndexOf(Selected);

    private int IndexOf(WorkspaceImage image)
    {
        for (var i = 0; i < Images.Count; i++)
        {
            if (Images[i].Key == image.Key)
                return i;
        }
        return -1;
    }

    /// <summary>The index of <paramref name="image"/> in <see cref="Images"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    private int RequireListed(WorkspaceImage image)
    {
        var index = IndexOf(image);
        if (index < 0)
            throw new ArgumentException("The image is not listed in this workspace.", nameof(image));
        return index;
    }

    /// <summary>Selects the image at <paramref name="index"/>; an index outside the list, or the selection itself, changes nothing.</summary>
    private void SelectAt(int index)
    {
        if (index < 0 || index >= Images.Count || index == SelectedIndex)
            return;
        Selected = Images[index];
        QueueChange();
    }

    /// <summary>Queues the current list and selection to be announced; call under the lock, then <see cref="Announce"/>.</summary>
    private void QueueChange(
        IReadOnlyList<string>? unavailableFolders = null, IReadOnlyList<string>? droppedWithUnsavedSteps = null) =>
        _pendingChanges.Enqueue(new WorkspaceChange
        {
            Images = Images, Selected = Selected, UnavailableFolders = unavailableFolders ?? [],
            DroppedWithUnsavedSteps = droppedWithUnsavedSteps ?? [],
        });

    /// <summary>
    /// Raises <see cref="Changed"/> for every queued update, in order, outside the workspace's lock. Only one thread
    /// announces at a time; a call while another is announcing, or from a handler, leaves its update to that one.
    /// </summary>
    private void Announce()
    {
        lock (_announceLock)
        {
            if (_announcing)
                return;
            _announcing = true;
        }
        while (true)
        {
            WorkspaceChange? change;
            lock (_lock)
            {
                if (!_pendingChanges.TryDequeue(out change))
                {
                    // Under the workspace's lock, so an update queued from now on finds no one announcing and announces itself.
                    lock (_announceLock)
                        _announcing = false;
                    return;
                }
            }
            Changed?.Invoke(this, change);
        }
    }

    private static ListingOptions ListingOptionsFor(UserSettings settings, bool includeResults) =>
        ListingOptions.For(SaveOptionsFor(settings), includeResults);
}
