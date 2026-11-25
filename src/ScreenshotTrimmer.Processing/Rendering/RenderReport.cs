namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>
/// What a render measured and decided, for the interface to show.
/// Each later finishing Step adds the properties it reports here.
/// </summary>
public sealed record RenderReport
{
    /// <summary>
    /// The radius measured at each corner (0 = square), or null when rounded corners were not applied.
    /// </summary>
    public CornerRadii? MeasuredRadii { get; init; }

    /// <summary>
    /// The radius each corner was actually rounded at (0 = left square), after grouping corners that
    /// agree and applying any manual radius; null when rounded corners were not applied.
    /// </summary>
    public CornerRadii? AppliedRadii { get; init; }
}
