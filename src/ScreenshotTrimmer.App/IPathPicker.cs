namespace ScreenshotTrimmer.App;

/// <summary>
/// Asks the user for paths to open, starting in <c>startFolder</c> when it is given; an empty list when they cancel.
/// </summary>
public interface IPathPicker
{
    Task<IReadOnlyList<string>> PickFilesAsync(string? startFolder);

    Task<IReadOnlyList<string>> PickFolderAsync(string? startFolder);

    /// <summary>
    /// Asks where to save a PNG named <paramref name="name"/> for display, suggesting <paramref name="suggestedName"/>
    /// in <paramref name="startFolder"/>; <c>null</c> when the user cancels. Does not ask about replacing an existing
    /// file: the caller does, the same way on every platform.
    /// </summary>
    Task<string?> PickSaveFileAsync(string? startFolder, string suggestedName, string name);
}
