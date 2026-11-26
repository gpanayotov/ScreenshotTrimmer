namespace ScreenshotTrimmer.Processing.Files;

/// <summary>A new, empty folder under the system temporary folder, deleted with everything in it on dispose.</summary>
internal sealed class TemporaryFolder : IDisposable
{
    /// <param name="prefix">The start of the folder's name, so a left-over folder says what made it.</param>
    public TemporaryFolder(string prefix)
    {
        Path = Directory.CreateTempSubdirectory(prefix).FullName;
    }

    public string Path { get; }

    /// <summary>The path of <paramref name="name"/> inside the folder.</summary>
    public string File(string name) => System.IO.Path.Combine(Path, name);

    /// <summary>Deletes the folder, quietly leaving it if something still holds it.</summary>
    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
