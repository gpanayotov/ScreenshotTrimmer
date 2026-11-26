namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>What one <b>Batch</b> did.</summary>
public sealed record BatchSummary
{
    /// <summary>How many images got a Step with the Subject found in them.</summary>
    public int Processed { get; init; }

    /// <summary>The images in which no Subject was found, in strip order, as they were listed when the Batch reached them.</summary>
    public IReadOnlyList<WorkspaceImage> NoSubjectFound { get; init; } = [];

    /// <summary>The images that could not be read, in strip order, as they were listed when the Batch reached them.</summary>
    public IReadOnlyList<WorkspaceImage> Failed { get; init; } = [];

    /// <summary>Whether the Batch was cancelled before it went through every image; the counts say what it did.</summary>
    public bool Cancelled { get; init; }

    /// <summary>How many images were skipped for each reason, at the start or while the Batch ran.</summary>
    public BatchSkipCounts Skipped { get; init; }
}

/// <summary>
/// How far a running <b>Batch</b> has got: <paramref name="Done"/> of the <paramref name="Total"/> images it processes,
/// which leaves out the images its plan skips.
/// </summary>
public readonly record struct BatchProgress(int Done, int Total);
