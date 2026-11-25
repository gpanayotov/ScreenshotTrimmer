namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>
/// What a render measured and decided, for the interface to show.
/// Each later finishing Step adds the properties it reports here.
/// </summary>
public sealed record RenderReport
{
    /// <summary>How many frame lines were removed on each side; all zero when Frame lines is off.</summary>
    public FrameLinesRemoved FrameLinesRemoved { get; init; } = FrameLinesRemoved.None;
}

/// <summary>The number of frame lines removed on each side of the crop.</summary>
public sealed record FrameLinesRemoved(int Left, int Top, int Right, int Bottom)
{
    public static FrameLinesRemoved None { get; } = new(0, 0, 0, 0);
}
