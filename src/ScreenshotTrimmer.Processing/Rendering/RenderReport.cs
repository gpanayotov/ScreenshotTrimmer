namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>
/// What a render measured and decided, for the interface to show.
/// Each later finishing Step adds the properties it reports here.
/// </summary>
public sealed record RenderReport
{
    /// <summary>How many frame lines were removed on each side; all zero when Frame lines is off.</summary>
    public FrameLinesRemoved FrameLinesRemoved { get; init; } = FrameLinesRemoved.None;

    /// <summary>
    /// The radius measured at each corner (0 = square), or null when rounded corners were not applied.
    /// </summary>
    public CornerRadii? MeasuredRadii { get; init; }

    /// <summary>
    /// The radius each corner was actually rounded at (0 = left square), after grouping corners that
    /// agree and applying any manual radius; null when rounded corners were not applied.
    /// </summary>
    public CornerRadii? AppliedRadii { get; init; }

    /// <summary>
    /// The transparent padding the Shadow added on every side, in pixels; 0 when Shadow is off.
    /// The Subject starts this many pixels in from the Result's left and top edges.
    /// </summary>
    public int ShadowPadding { get; init; }

    /// <summary>
    /// Whether the Original already had shadow padding, i.e. was already processed
    /// (see <see cref="AlreadyProcessed.HasShadowPadding"/>), so the interface can warn and leave Shadow off.
    /// </summary>
    public bool OriginalHasShadowPadding { get; init; }
}

/// <summary>The number of frame lines removed on each side of the crop.</summary>
public sealed record FrameLinesRemoved(int Left, int Top, int Right, int Bottom)
{
    public static FrameLinesRemoved None { get; } = new(0, 0, 0, 0);
}
