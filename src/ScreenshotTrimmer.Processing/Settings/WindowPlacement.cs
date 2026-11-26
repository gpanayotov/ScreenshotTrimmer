namespace ScreenshotTrimmer.Processing.Settings;

/// <summary>
/// Where the main window was and how it was laid out when it last closed. Kept apart from <see cref="UserSettings"/>,
/// which holds only the user's choices.
/// </summary>
public sealed record WindowPlacement
{
    /// <summary>The left edge of the window in its normal (not maximised) state, in physical screen pixels.</summary>
    public required int X { get; init; }

    /// <summary>The top edge of the window in its normal state, in physical screen pixels.</summary>
    public required int Y { get; init; }

    /// <summary>The width of the window in its normal state, in device-independent pixels.</summary>
    public required double Width { get; init; }

    /// <summary>The height of the window in its normal state, in device-independent pixels.</summary>
    public required double Height { get; init; }

    public required bool Maximized { get; init; }

    /// <summary>The width of the thumbnail strip, in device-independent pixels.</summary>
    public required double StripWidth { get; init; }
}
