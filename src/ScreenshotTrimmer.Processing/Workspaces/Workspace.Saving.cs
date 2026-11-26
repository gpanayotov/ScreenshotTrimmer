using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Workspaces;

// Rendering an image's Result from its Original and writing it to disk.
public sealed partial class Workspace
{
    /// <summary>
    /// The images saved in Backup mode in this list, by listed path, which is where their Result now is. Their listed
    /// file may hold the Result rather than the Original, so they render from <see cref="BackupSave.LoadOriginal"/>.
    /// </summary>
    private readonly Dictionary<ImageKey, BackupSave> _backupSaves = [];

    /// <summary>The pixels of <paramref name="image"/>'s Original, which its Result is rendered from.</summary>
    /// <remarks>
    /// Once a Backup-mode save has given the Result the Original's name, these come from the backup, or from the
    /// Original's pixels kept from before that save when an existing backup was left alone, so no effect is applied
    /// twice. A Pasted image's come from memory, saved or not.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="InvalidDataException">The file is not an image that can be decoded.</exception>
    public RgbaImage LoadOriginal(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        ImageKey key;
        RgbaImage? pastedPixels;
        BackupSave? backupSave;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            key = Images[RequireListed(image)].Key;
            pastedPixels = PastedPixels(key);
            backupSave = _backupSaves.GetValueOrDefault(key);
        }
        // A Pasted image not saved yet always has its pixels, so only a file gets this far.
        return pastedPixels ?? backupSave?.LoadOriginal() ?? ImageFile.Load(key.Path!);
    }

    /// <summary>
    /// Renders <paramref name="image"/>'s current Steps from its Original's pixels and saves the Result under the
    /// current save settings. On success the Steps that were saved become the image's saved Steps, unless they changed
    /// while it was saving, and the list is brought up to date with the new files. Rendering and writing run outside
    /// the workspace's lock, so the list can be read and changed meanwhile.
    /// </summary>
    /// <remarks>
    /// When a Backup-mode save gives the Result a new name (<c>shot.jpg</c> becomes <c>shot.png</c>), the entry
    /// follows it with its Steps. Saving again in Backup mode leaves the backup alone and overwrites the Result. A saved
    /// Pasted image overwrites its own file in either mode. A Pasted image not saved yet is saved with
    /// <see cref="SavePasted"/> instead.
    /// </remarks>
    /// <exception cref="SaveFailedException">The Result could not be written; the Steps stay unsaved.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="image"/> is not listed, or is a Pasted image that has not been saved yet.
    /// </exception>
    /// <exception cref="IOException">The Original cannot be read.</exception>
    /// <exception cref="InvalidDataException">The Original is not an image that can be decoded.</exception>
    public SaveReport Save(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        string path;
        ImageKey key;
        Steps steps;
        SaveOptions options;
        RgbaImage? pastedPixels;
        BackupSave? backupSave;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            path = Images[RequireListed(image)].Path
                ?? throw new ArgumentException("A Pasted image is saved with SavePasted until it has a file.", nameof(image));
            key = ImageKey.OfFile(path);
            steps = StepsOf(key).Current;
            options = SaveOptionsFor(_settings);
            pastedPixels = PastedPixels(key);
            backupSave = _backupSaves.GetValueOrDefault(key);
        }

        var original = pastedPixels ?? backupSave?.LoadOriginal() ?? ImageFile.Load(path);
        var result = Renderer.Render(original, steps).Result;
        // The file already holds this image's Result, so only that file is written.
        var replacing = pastedPixels is not null || (options.Mode == SaveMode.Backup && backupSave is not null);
        var report = replacing ? ResultFiles.Replace(result, path) : SaveFollowingOwnRename(result, path, options);

        Update(() =>
        {
            var saved = key;
            if (options.Mode == SaveMode.Backup && !replacing)
            {
                FollowOwnRename(path, report.Written[0]);
                saved = ImageKey.OfFile(report.Written[0]);
                _backupSaves[saved] = BackupSave.After(report, original);
            }
            // The file holds these Steps whether or not they changed meanwhile; a change ends a run of merged changes
            // only when nothing changed since.
            if (_steps.TryGetValue(saved, out var state))
                _steps[saved] = state with { Saved = steps, MergeKey = state.Current == steps ? null : state.MergeKey };
            // The new files are listed now rather than when the folder watcher notices them, so the badge or the
            // renamed entry shows at once. A hidden Result that was listed only for its unsaved Steps gives way to its
            // Original.
            var previous = Selected;
            var selected = previous?.Key == key ? saved : previous?.Key;
            Relist(_ => (selected is null ? null : Find(selected))
                ?? (previous is { IsResult: true } ? OriginalOf(previous) : null)
                ?? Images.FirstOrDefault());
        });
        return report;
    }

    /// <summary>
    /// Saves every image with unsaved Steps, in strip order, as <see cref="Save"/> does, carrying on past the ones that
    /// fail. An image that leaves the list before its turn is skipped. A Pasted image not saved yet is not saved but
    /// reported as needing a location, so the caller can ask for one and save it with <see cref="SavePasted"/>.
    /// </summary>
    /// <returns>One outcome per image saved, failed or needing a location, in strip order.</returns>
    public IReadOnlyList<SaveOutcome> SaveAll()
    {
        List<WorkspaceImage> unsaved;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            unsaved = Images.Where(image => image.HasUnsavedSteps).ToList();
        }

        var outcomes = new List<SaveOutcome>();
        foreach (var image in unsaved)
        {
            lock (_lock)
            {
                if (IndexOf(image) < 0)
                    continue;
            }
            if (image.IsPasted)
            {
                outcomes.Add(new SaveOutcome { Image = image, NeedsLocation = true });
                continue;
            }
            try
            {
                outcomes.Add(new SaveOutcome { Image = image, Report = Save(image) });
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException
                                          or ArgumentException)
            {
                // ArgumentException: an invalid suffix, or the image left the list just now.
                outcomes.Add(new SaveOutcome { Image = image, Failure = e });
            }
        }
        return outcomes;
    }

    /// <summary>
    /// Saves through <see cref="ResultFiles.Save"/>, telling the folder watcher that a Backup-mode move of the Original
    /// to its backup is the workspace's own, so the entry is not dragged along to the backup's name.
    /// </summary>
    private SaveReport SaveFollowingOwnRename(RgbaImage result, string path, SaveOptions options)
    {
        if (options.Mode != SaveMode.Backup)
            return ResultFiles.Save(result, path, options);
        _folderWatch.IgnoreNextRenameOf(path);
        var moved = false;
        try
        {
            var report = ResultFiles.Save(result, path, options);
            moved = report.Moved.Count > 0;
            return report;
        }
        finally
        {
            _folderWatch.EndIgnoringRenameOf(path, moved);
        }
    }

    /// <summary>Moves the Backup-mode saves of files renamed on disk to their new paths; call under the lock.</summary>
    private void FollowRenamesOfBackupSaves(Func<string, string> renamed)
    {
        foreach (var (from, backupSave) in _backupSaves.ToList())
        {
            var to = from.Renamed(renamed);
            if (to == from)
                continue;
            _backupSaves.Remove(from);
            _backupSaves[to] = backupSave;
        }
    }

    /// <summary>
    /// Moves the entry at <paramref name="from"/>, with its Steps, to <paramref name="to"/>, where a Backup-mode save just
    /// put its Result; call under the lock.
    /// </summary>
    private void FollowOwnRename(string from, string to)
    {
        if (FileNames.Same(from, to))
            return;
        if (_steps.Remove(ImageKey.OfFile(from), out var state))
            _steps[ImageKey.OfFile(to)] = state;
        _openedPaths = _openedPaths.Select(opened => FileNames.Same(opened, from) ? to : opened).ToList();
    }

    private static SaveOptions SaveOptionsFor(Settings.UserSettings settings) => new()
    {
        Mode = settings.SaveMode,
        ResultSuffix = settings.ResultSuffix,
        BackupSuffix = settings.BackupSuffix,
    };

    /// <summary>Where an image saved in Backup mode renders from.</summary>
    /// <param name="BackupPath">The backup the save moved the Original to, or <c>null</c> when none was moved.</param>
    /// <param name="OriginalPixels">
    /// The Original's pixels from before the save, kept when the Result overwrote the Original beside a backup that
    /// already existed; otherwise <c>null</c>.
    /// </param>
    private sealed record BackupSave(string? BackupPath, RgbaImage? OriginalPixels)
    {
        public static BackupSave After(SaveReport report, RgbaImage original) =>
            report.Moved is [var move, ..]
                ? new BackupSave(move.To, null)
                : new BackupSave(null, original);

        /// <summary>The Original's pixels.</summary>
        public RgbaImage LoadOriginal() => OriginalPixels ?? ImageFile.Load(BackupPath!);
    }
}
