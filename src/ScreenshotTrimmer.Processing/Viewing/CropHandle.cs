namespace ScreenshotTrimmer.Processing.Viewing;

/// <summary>
/// The part of a <see cref="CropRectangle"/> a drag or a nudge moves: a corner moves its two edges, an edge only
/// itself, and the inside the whole rectangle without resizing it.
/// </summary>
public enum CropHandle
{
    None,
    Inside,
    Left,
    Top,
    Right,
    Bottom,
    TopLeft,
    TopRight,
    BottomRight,
    BottomLeft,
}
