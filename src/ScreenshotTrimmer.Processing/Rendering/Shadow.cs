namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>
/// The <b>Shadow</b> Step: a soft black drop shadow drawn from the Result's outline, on transparent
/// padding added around it. Colour (black) and offset (2px down) are fixed.
/// </summary>
public sealed record Shadow
{
    /// <summary>The default <see cref="Size"/>: the default look.</summary>
    public const double DefaultSize = 1.0;

    /// <summary>The default <see cref="Opacity"/>.</summary>
    public const double DefaultOpacity = 0.35;

    /// <summary>The largest allowed <see cref="Size"/>; any size above 0 up to this is allowed.</summary>
    public const double MaxSize = 4.0;

    /// <summary>Whether <paramref name="size"/> is an allowed <see cref="Size"/>: above 0, at most <see cref="MaxSize"/>.</summary>
    public static bool IsAllowedSize(double size) => size > 0 && size <= MaxSize;

    /// <summary>Whether <paramref name="opacity"/> is an allowed <see cref="Opacity"/>: from 0 to 1.</summary>
    public static bool IsAllowedOpacity(double opacity) => opacity >= 0 && opacity <= 1;

    /// <summary>Scales the blur and the padding together; 1.0 is a blur sigma of 6 and 14px of padding.</summary>
    public double Size { get; init; } = DefaultSize;

    /// <summary>The shadow's opacity, from 0 to 1.</summary>
    public double Opacity { get; init; } = DefaultOpacity;
}
