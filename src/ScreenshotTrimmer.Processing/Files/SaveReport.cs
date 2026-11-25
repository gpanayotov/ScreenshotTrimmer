namespace ScreenshotTrimmer.Processing.Files;

/// <summary>What a save changed on disk, so the interface can update its list and badges.</summary>
public sealed record SaveReport
{
    /// <summary>Full paths of the files written.</summary>
    public required IReadOnlyList<string> Written { get; init; }
}
