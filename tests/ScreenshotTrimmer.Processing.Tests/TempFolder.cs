using ScreenshotTrimmer.Processing.Files;

namespace ScreenshotTrimmer.Processing.Tests;

/// <summary>A real, empty temporary directory, deleted with everything in it on dispose.</summary>
internal sealed class TempFolder : IDisposable
{
    private readonly TemporaryFolder folder = new("ScreenshotTrimmer.Tests-");

    public string Path => folder.Path;

    public string File(string name) => folder.File(name);

    /// <summary>The names of every entry in the folder, sorted ordinally.</summary>
    public string[] Entries() =>
        Directory.EnumerateFileSystemEntries(Path)
            .Select(System.IO.Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

    public void Dispose() => folder.Dispose();
}
