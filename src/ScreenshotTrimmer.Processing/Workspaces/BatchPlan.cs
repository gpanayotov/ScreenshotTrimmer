namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>What a <b>Batch</b> started now would do, so the user can be told before it starts.</summary>
public sealed record BatchPlan
{
    /// <summary>The images the Batch would process, in strip order.</summary>
    public IReadOnlyList<WorkspaceImage> Images { get; init; } = [];

    /// <summary>How many images would be skipped for each reason; none are <see cref="BatchSkip.Gone"/> yet.</summary>
    public BatchSkipCounts Skipped { get; init; }
}
