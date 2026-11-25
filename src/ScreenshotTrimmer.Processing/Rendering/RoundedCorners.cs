namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>
/// The rounded-corners part of <b>Clean edges</b>: the pixels outside a window's rounded corners
/// become transparent. Each corner's radius is measured from the pixels unless
/// <see cref="ManualRadius"/> replaces it.
/// </summary>
public sealed record RoundedCorners
{
    /// <summary>
    /// A radius in pixels that replaces the measurement for every corner that would be rounded,
    /// or null to use the measured radii. Corners measured as square stay square.
    /// </summary>
    public double? ManualRadius { get; init; }
}

/// <summary>A radius in pixels for each corner of the Result; 0 means a square corner.</summary>
public sealed record CornerRadii(double TopLeft, double TopRight, double BottomRight, double BottomLeft)
{
    public static CornerRadii Square { get; } = new(0, 0, 0, 0);
}
