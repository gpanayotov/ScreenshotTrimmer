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
    private readonly Dictionary<string, BackupSave> _backupSaves = new(FileNames.Comparer);

    /// <summary>The pixels of <paramref name="image"/>'s Original, which its Result is rendered from.</summary>
    /// <remarks>
    /// Once a Backup-mode save has given the Result the Original's name, these come from the backup, or from the
    /// Original's pixels kept from before that save when an existing backup was left alone, so no effect is applied
    /// twice.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="InvalidDataException">The file is not an image that can be decoded.</exception>
    public RgbaImage LoadOriginal(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        string path;
        BackupSave? backupSave;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            path = Images[RequireListed(image)].Path;
            backupSave = _backupSaves.GetValueOrDefault(path);
        }
        return backupSave?.LoadOriginal() ?? ImageFile.Load(path);
    }

    /// <summary>
    /// Renders <paramref name="image"/>'s current Steps from its Original's pixels and saves the Result under the
    /// current save settings. On success the Steps that were saved become the image's saved Steps, unless they changed
    /// while it was saving, and the list is brought up to date with the new files. Rendering and writing run outside
    /// the workspace's lock, so the list can be read and changed meanwhile.
    /// </summary>
    /// <remarks>
    /// When a Backup-mode save gives the Result a new name (<c>shot.jpg</c> becomes <c>shot.png</c>), the entry
    /// follows it with its Steps. Saving again in Backup mode leaves the backup alone and overwrites the Result.
    /// </remarks>
    /// <exception cref="SaveFailedException">The Result could not be written; the Steps stay unsaved.</exception>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    /// <exception cref="IOException">The Original cannot be read.</exception>
    /// <exception cref="InvalidDataException">The Original is not an image that can be decoded.</exception>
    public SaveReport Save(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        string path;
        Steps steps;
        SaveOptions options;
        BackupSave? backupSave;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            path = Images[RequireListed(image)].Path;
            steps = StepsOf(path).Current;
            options = SaveOptionsFor(_settings);
            backupSave = _backupSaves.GetValueOrDefault(path);
        }

        var original = backupSave?.LoadOriginal() ?? ImageFile.Load(path);
        var result = Renderer.Render(original, steps).Result;
        var resaving = options.Mode == SaveMode.Backup && backupSave is not null;
        var report = resaving ? ResultFiles.Replace(result, path) : SaveFollowingOwnRename(result, path, options);

        Update(() =>
        {
            var savedPath = path;
            if (options.Mode == SaveMode.Backup && !resaving)
            {
                savedPath = report.Written[0];
                FollowOwnRename(path, savedPath);
                _backupSaves[savedPath] = BackupSave.After(report, original);
            }
            var state = StepsOf(savedPath);
            if (state.Current == steps)
                _steps[savedPath] = state with { Saved = steps, MergeKey = null };
            if (_openedPaths.Count == 0)
                return;
            // The new files are listed now rather than when the folder watcher notices them, so the badge or the
            // renamed entry shows at once.
            var selected = Selected is { } current && FileNames.Same(current.Path, path) ? savedPath : Selected?.Path;
            Relist(_ => (selected is null ? null : Find(selected)) ?? Images.FirstOrDefault());
        });
        return report;
    }

    /// <summary>
    /// Saves every image with unsaved Steps, in strip order, as <see cref="Save"/> does, carrying on past the ones that
    /// fail. An image that leaves the list before its turn is skipped.
    /// </summary>
    /// <returns>One outcome per image saved or failed, in strip order.</returns>
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
            if (!moved)
                _folderWatch.StopIgnoringRenameOf(path);
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
        if (_steps.Remove(from, out var state))
            _steps[to] = state;
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
