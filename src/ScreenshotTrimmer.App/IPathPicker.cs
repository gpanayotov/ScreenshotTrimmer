namespace ScreenshotTrimmer.App;

/// <summary>Asks the user for paths to open; an empty list when they cancel.</summary>
public interface IPathPicker
{
    Task<IReadOnlyList<string>> PickFilesAsync();

    Task<IReadOnlyList<string>> PickFolderAsync();
}
