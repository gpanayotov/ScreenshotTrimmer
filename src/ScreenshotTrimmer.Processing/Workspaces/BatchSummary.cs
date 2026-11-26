namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>What one <b>Batch</b> did.</summary>
public sealed record BatchSummary
{
    /// <summary>How many images got a Step with the Subject found in them.</summary>
    public int Processed { get; init; }

    /// <summary>The images in which no Subject was found, in strip order, as they were listed when the Batch reached them.</summary>
    public IReadOnlyList<WorkspaceImage> NoSubjectFound { get; init; } = [];

    /// <summary>How many images were skipped for each reason; a reason no image was skipped for is missing.</summary>
    public IReadOnlyDictionary<BatchSkip, int> Skipped { get; init; } = new Dictionary<BatchSkip, int>();

    /// <summary>How many images were skipped for <paramref name="reason"/>.</summary>
    public int SkippedFor(BatchSkip reason) => Skipped.GetValueOrDefault(reason);
}

/// <summary>How far a running <b>Batch</b> has got: <paramref name="Done"/> of the <paramref name="Total"/> listed images.</summary>
public readonly record struct BatchProgress(int Done, int Total);
