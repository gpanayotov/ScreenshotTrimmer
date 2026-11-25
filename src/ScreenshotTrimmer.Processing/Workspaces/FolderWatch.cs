using System.Collections.Concurrent;
using ScreenshotTrimmer.Processing.Files;

namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>
/// Watches a <see cref="Workspace"/>'s folders and calls back once the disk has been quiet for a delay after a change,
/// with the renames seen meanwhile, so a burst of changes gives one update.
/// </summary>
/// <remarks>
/// <see cref="Watch"/>, <see cref="Reset"/> and <see cref="Dispose"/> are called under the workspace's lock. The watcher
/// event handlers take no lock: disposing a watcher waits for its handlers, and watchers are disposed under that lock.
/// </remarks>
internal sealed class FolderWatch : IDisposable
{
    private readonly TimeSpan _quietDelay;
    private readonly Timer _quietTimer;
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(FileNames.Comparer);
    private readonly Dictionary<string, FileSystemWatcher> _parentWatchers = new(FileNames.Comparer);
    private readonly ConcurrentQueue<(string From, string To)> _renames = new();

    /// <param name="quietDelay">How long the disk must stay quiet after a change before <paramref name="onQuiet"/>.</param>
    /// <param name="onQuiet">Called on a thread-pool thread once the disk is quiet.</param>
    public FolderWatch(TimeSpan quietDelay, Action onQuiet)
    {
        _quietDelay = quietDelay;
        _quietTimer = new Timer(_ => onQuiet());
    }

    /// <summary>
    /// Watches <paramref name="folders"/> for changes to their files, and each of <paramref name="homeFolders"/>' parents
    /// for the home folder itself being deleted, renamed or created again. Subfolders are not watched. A watcher that
    /// stopped after an error is replaced, and a folder that cannot be watched is skipped.
    /// </summary>
    public void Watch(IEnumerable<string> folders, IReadOnlyList<string> homeFolders)
    {
        Sync(_watchers, homeFolders.Concat(folders).Where(Directory.Exists), folder => new FileSystemWatcher(folder)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite | NotifyFilters.Size,
        });
        Sync(_parentWatchers, homeFolders, folder =>
            Path.GetDirectoryName(folder) is { } parent && Directory.Exists(parent)
                ? new FileSystemWatcher(parent, Path.GetFileName(folder)) { NotifyFilter = NotifyFilters.DirectoryName }
                : null);
    }

    /// <summary>Forgets the changes and renames seen so far, for a list opened afresh.</summary>
    public void Reset()
    {
        _quietTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _renames.Clear();
    }

    /// <summary>Calls back again once the disk has been quiet for the delay, as if it had changed.</summary>
    public void CallBackSoon()
    {
        try
        {
            _quietTimer.Change(_quietDelay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Disposed while a watcher event was on its way.
        }
    }

    /// <summary>Maps a path to where the renames seen since the last call moved it.</summary>
    public Func<string, string> TakeRenames()
    {
        var moves = new List<(string From, string To)>();
        while (_renames.TryDequeue(out var rename))
            moves.Add(rename);
        return path =>
        {
            foreach (var (from, to) in moves)
            {
                if (FileNames.Same(path, from))
                    path = to;
            }
            return path;
        };
    }

    public void Dispose()
    {
        _quietTimer.Dispose();
        foreach (var watcher in _watchers.Values.Concat(_parentWatchers.Values))
            watcher.Dispose();
        _watchers.Clear();
        _parentWatchers.Clear();
    }

    /// <summary>
    /// Makes <paramref name="watchers"/> hold one running watcher per folder in <paramref name="folders"/>, made by
    /// <paramref name="create"/>.
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

    /// <summary>Puts the callback off until the disk has been quiet for the delay.</summary>
    private void OnDiskChanged(object sender, FileSystemEventArgs e) => CallBackSoon();

    /// <summary>Notes a rename, so the workspace can follow the file to its new name.</summary>
    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        _renames.Enqueue((e.OldFullPath, e.FullPath));
        CallBackSoon();
    }

    /// <summary>A watcher overflowed or lost its folder: call back, and the next <see cref="Watch"/> replaces it.</summary>
    private void OnWatcherError(object sender, ErrorEventArgs e) => CallBackSoon();
}
