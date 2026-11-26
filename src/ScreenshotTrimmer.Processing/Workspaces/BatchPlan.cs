namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>What a <b>Batch</b> started now would do, so the user can be told before it starts.</summary>
public sealed record BatchPlan
{
    /// <summary>The images the Batch would process, in strip order.</summary>
    public IReadOnlyList<WorkspaceImage> Images { get; init; } = [];

    /// <summary>How many images would be skipped for each reason; a reason no image would be skipped for is missing.</summary>
    public IReadOnlyDictionary<BatchSkip, int> Skipped { get; init; } = new Dictionary<BatchSkip, int>();

    /// <summary>How many images would be skipped for <paramref name="reason"/>.</summary>
    public int SkippedFor(BatchSkip reason) => Skipped.GetValueOrDefault(reason);
}
