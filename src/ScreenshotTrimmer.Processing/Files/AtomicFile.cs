namespace ScreenshotTrimmer.Processing.Files;

/// <summary>
/// Writes files so that a failure never leaves a partial file under the target name:
/// the content goes to a temporary file in the same folder, which is then renamed over the target.
/// </summary>
internal static class AtomicFile
{
    public static void Write(string targetPath, Action<Stream> writeContent)
    {
        using var pending = Prepare(targetPath, writeContent);
        pending.Commit(overwrite: true);
    }

    /// <summary>
    /// Writes the content to a temporary file beside <paramref name="targetPath"/> without touching the target.
    /// Commit it to rename it into place; disposing it uncommitted removes the temporary file.
    /// </summary>
    public static PendingFile Prepare(string targetPath, Action<Stream> writeContent)
    {
        var fullTarget = Path.GetFullPath(targetPath);
        var folder = Path.GetDirectoryName(fullTarget)!;
        var tempPath = Path.Combine(folder, $".{Path.GetFileName(fullTarget)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            writeContent(stream);
            stream.Flush(flushToDisk: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
        return new PendingFile(tempPath, fullTarget);
    }

    internal sealed class PendingFile(string tempPath, string targetPath) : IDisposable
    {
        private bool _committed;

        /// <summary>Renames the temporary file to the target, replacing it only when <paramref name="overwrite"/> is set.</summary>
        public void Commit(bool overwrite)
        {
            File.Move(tempPath, targetPath, overwrite);
            _committed = true;
        }

        public void Dispose()
        {
            if (!_committed) TryDelete(tempPath);
        }
    }

    // Removes only the library's own temporary file, never a user's file.
    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
