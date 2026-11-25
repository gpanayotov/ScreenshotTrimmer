namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>A <see cref="Workspace"/>'s list and selection after one update.</summary>
public sealed record WorkspaceChange
{
    public required IReadOnlyList<WorkspaceImage> Images { get; init; }

    public WorkspaceImage? Selected { get; init; }

    /// <summary>
    /// The opened folders, and folders of opened files, that were deleted or became unreachable with this update, so
    /// the user can be told why their images left the list.
    /// </summary>
    public IReadOnlyList<string> UnavailableFolders { get; init; } = [];
}
