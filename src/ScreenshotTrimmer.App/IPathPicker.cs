namespace ScreenshotTrimmer.App;

/// <summary>
/// Asks the user for paths to open, starting in <c>startFolder</c> when it is given; an empty list when they cancel.
/// </summary>
public interface IPathPicker
{
    Task<IReadOnlyList<string>> PickFilesAsync(string? startFolder);

    Task<IReadOnlyList<string>> PickFolderAsync(string? startFolder);
}
