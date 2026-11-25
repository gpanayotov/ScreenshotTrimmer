namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>
/// The pending <b>Steps</b> for one image: what <see cref="Renderer.Render"/> applies to the Original.
/// Each kind of Step is one property; a property left at its default means that Step is not applied.
/// The Steps are always applied in a fixed order (crop, frame lines, rounded corners, Shadow),
/// whatever order they were set in.
/// </summary>
public sealed record Steps
{
    /// <summary>No Steps: the Result equals the Original.</summary>
    public static Steps None { get; } = new();

    /// <summary>The crop, or null to keep the whole image.</summary>
    public Crop? Crop { get; init; }

    /// <summary>Frame lines (Clean edges): remove leftover lines of background along the crop's edges.</summary>
    public bool FrameLines { get; init; }

    /// <summary>Rounded corners (Clean edges): make the pixels outside the rounded corners transparent, or null to leave the corners as they are.</summary>
    public RoundedCorners? RoundedCorners { get; init; }

    /// <summary>The Shadow, or null for none. Applied last, after Clean edges.</summary>
    public Shadow? Shadow { get; init; }
}
