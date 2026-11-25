namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>
/// Identifies one state of a file's content: it changes when the file is written, so something made from one version,
/// such as a thumbnail, can be told apart from the file's current content.
/// </summary>
public readonly record struct ContentVersion(DateTime LastWriteTimeUtc, long Length)
{
    /// <summary>The version of the file at <paramref name="path"/> as it is on disk now; the default when it is gone.</summary>
    public static ContentVersion Of(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? new ContentVersion(file.LastWriteTimeUtc, file.Length) : default;
    }
}
