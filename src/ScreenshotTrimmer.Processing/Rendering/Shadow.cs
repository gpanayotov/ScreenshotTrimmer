namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>
/// The <b>Shadow</b> Step: a soft black drop shadow drawn from the Result's outline, on transparent
/// padding added around it. Colour (black) and offset (2px down) are fixed.
/// </summary>
public sealed record Shadow
{
    /// <summary>Scales the blur and the padding together; 1.0 is a blur sigma of 6 and 14px of padding.</summary>
    public double Size { get; init; } = 1.0;

    /// <summary>The shadow's opacity, from 0 to 1.</summary>
    public double Opacity { get; init; } = 0.35;
}
