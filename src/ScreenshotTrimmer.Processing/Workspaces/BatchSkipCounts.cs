namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>How many listed images a <b>Batch</b> leaves alone, for each <see cref="BatchSkip"/> reason.</summary>
public readonly record struct BatchSkipCounts(int HasSteps, int AlreadyProcessed, int Result, int Gone)
{
    /// <summary>How many images are skipped for any reason.</summary>
    public int Total => HasSteps + AlreadyProcessed + Result + Gone;

    /// <summary>These counts with one more image skipped for <paramref name="reason"/>.</summary>
    public BatchSkipCounts Plus(BatchSkip reason) => reason switch
    {
        BatchSkip.HasSteps => this with { HasSteps = HasSteps + 1 },
        BatchSkip.AlreadyProcessed => this with { AlreadyProcessed = AlreadyProcessed + 1 },
        BatchSkip.Result => this with { Result = Result + 1 },
        BatchSkip.Gone => this with { Gone = Gone + 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };

    /// <summary>The counts of <paramref name="reasons"/>, one per skipped image, added to these.</summary>
    public BatchSkipCounts Plus(IEnumerable<BatchSkip> reasons) =>
        reasons.Aggregate(this, (counts, reason) => counts.Plus(reason));
}
