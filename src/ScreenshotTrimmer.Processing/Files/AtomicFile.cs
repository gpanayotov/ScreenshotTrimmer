namespace ScreenshotTrimmer.Processing.Files;

/// <summary>
/// Writes files so that a failure never leaves a partial file under the target name:
/// the content goes to a temporary file in the same folder, which is then renamed over the target.
/// </summary>
internal static class AtomicFile
{
    public static void Write(string targetPath, Action<Stream> writeContent)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(targetPath))!;
        var tempPath = Path.Combine(folder, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                writeContent(stream);
                stream.Flush(flushToDisk: true);
            }
            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
