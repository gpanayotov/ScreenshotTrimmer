namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>A <see cref="Workspace"/>'s list and selection after one update.</summary>
public sealed record WorkspaceChange
{
    public required IReadOnlyList<WorkspaceImage> Images { get; init; }

    public WorkspaceImage? Selected { get; init; }
}
