using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Files;

/// <summary>
/// Saves Results to disk under the save rules. Every Result is a PNG and every write is atomic.
/// The library never deletes a file: the Backup-mode move is the only change to an existing Original.
/// </summary>
public static class ResultFiles
{
    /// <summary>
    /// Saves <paramref name="result"/>, the Result of the Original at <paramref name="originalPath"/>,
    /// under <see cref="SaveOptions.Mode"/>.
    /// </summary>
    /// <remarks>
    /// Suffix mode writes <c>name&lt;suffix&gt;.png</c> beside the Original, replacing an earlier Result,
    /// and leaves the Original untouched. Backup mode moves the Original to
    /// <c>name&lt;backup suffix&gt;.&lt;original extension&gt;</c>, then writes the Result as <c>name.png</c>;
    /// if that backup already exists, neither it nor the Original is moved and the report notes it.
    /// An existing <c>name.png</c> beside a different Original that is still in place (e.g. <c>shot.png</c>
    /// beside <c>shot.jpg</c>) fails the save (<see cref="SaveFailureReason.TargetExists"/>) and nothing changes.
    /// </remarks>
    /// <exception cref="SaveFailedException">The target could not be written. Nothing is left behind.</exception>
    /// <exception cref="ArgumentException">The suffix the mode uses is not a valid <see cref="FileSuffix"/>. Nothing changes.</exception>
    public static SaveReport Save(RgbaImage result, string originalPath, SaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);

        var fullOriginal = Path.GetFullPath(originalPath);
        var folder = Path.GetDirectoryName(fullOriginal)!;
        var name = Path.GetFileNameWithoutExtension(fullOriginal);

        if (options.Mode == SaveMode.Backup)
        {
            FileSuffix.Require(options.BackupSuffix, nameof(options));
            var backupPath = Path.Combine(folder, name + options.BackupSuffix + Path.GetExtension(fullOriginal));
            var targetPath = Path.Combine(folder, name + ".png");
            return WithFailureReason(targetPath, () => SaveWithBackup(result, fullOriginal, backupPath, targetPath));
        }

        FileSuffix.Require(options.ResultSuffix, nameof(options));
        var resultPath = Path.Combine(folder, name + options.ResultSuffix + ".png");
        return WithFailureReason(resultPath, () =>
        {
            AtomicFile.Write(resultPath, stream => PngEncoder.Write(result, stream));
            return new SaveReport { Written = [resultPath] };
        });
    }

    /// <summary>
    /// Saves <paramref name="result"/>, the Result of a Pasted image, as a PNG to <paramref name="targetPath"/>.
    /// An existing file there is replaced only when <paramref name="overwrite"/> is set.
    /// </summary>
    /// <exception cref="SaveFailedException">The target could not be written. Nothing is left behind.</exception>
    public static SaveReport SavePasted(RgbaImage result, string targetPath, bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(result);

        var fullTarget = Path.GetFullPath(targetPath);
        return WithFailureReason(fullTarget, () =>
        {
            using var pending = AtomicFile.Prepare(fullTarget, stream => PngEncoder.Write(result, stream));
            if (overwrite)
                pending.Commit(overwrite: true);
            else
                CommitNew(pending, fullTarget);
            return new SaveReport { Written = [fullTarget] };
        });
    }

    private static SaveReport SaveWithBackup(RgbaImage result, string originalPath, string backupPath, string targetPath)
    {
        using var pending = AtomicFile.Prepare(targetPath, stream => PngEncoder.Write(result, stream));

        // A file at the target beside a different Original that is still in place is someone else's
        // (e.g. shot.png beside shot.jpg), and the library never deletes a file. Once a Backup-mode save has
        // moved the Original away, the file at the target is the earlier Result and is replaced.
        if (File.Exists(targetPath) && !FileNames.Same(targetPath, originalPath) && File.Exists(originalPath))
            throw new SaveFailedException(targetPath, SaveFailureReason.TargetExists);

        if (File.Exists(backupPath))
        {
            pending.Commit(overwrite: true);
            return new SaveReport { Written = [targetPath], ExistingBackup = backupPath };
        }

        File.Move(originalPath, backupPath);
        try
        {
            CommitNew(pending, targetPath);
        }
        catch
        {
            File.Move(backupPath, originalPath);
            throw;
        }
        return new SaveReport { Written = [targetPath], Moved = [new FileMove(originalPath, backupPath)] };
    }

    /// <summary>Commits <paramref name="pending"/> without replacing a file, failing with <see cref="SaveFailureReason.TargetExists"/> if one appeared.</summary>
    private static void CommitNew(AtomicFile.PendingFile pending, string targetPath)
    {
        try
        {
            pending.Commit(overwrite: false);
        }
        catch (IOException e) when (File.Exists(targetPath))
        {
            throw new SaveFailedException(targetPath, SaveFailureReason.TargetExists, e);
        }
    }

    /// <summary>Runs a save, turning any file-system failure into a <see cref="SaveFailedException"/> with a reason.</summary>
    private static SaveReport WithFailureReason(string targetPath, Func<SaveReport> save)
    {
        try
        {
            return save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException && e is not SaveFailedException)
        {
            throw new SaveFailedException(targetPath, Classify(targetPath, e), e);
        }
    }

    private static SaveFailureReason Classify(string targetPath, Exception e)
    {
        if (Directory.Exists(targetPath)) return SaveFailureReason.TargetIsFolder;
        return e switch
        {
            DirectoryNotFoundException => SaveFailureReason.FolderNotFound,
            UnauthorizedAccessException => SaveFailureReason.AccessDenied,
            _ => SaveFailureReason.Other,
        };
    }
}
