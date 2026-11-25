namespace ScreenshotTrimmer.Processing.Tests;

/// <summary>A real, empty temporary directory, deleted with everything in it on dispose.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = Directory.CreateTempSubdirectory("ScreenshotTrimmer.Tests-").FullName;
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    /// <summary>The names of every entry in the folder, sorted ordinally.</summary>
    public string[] Entries() =>
        Directory.EnumerateFileSystemEntries(Path)
            .Select(System.IO.Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
