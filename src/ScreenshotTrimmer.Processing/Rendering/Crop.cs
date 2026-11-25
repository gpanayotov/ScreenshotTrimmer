using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>
/// The crop Step: the window rectangle to keep, in the Original's pixel coordinates.
/// The same input whether it was drawn by hand (Classic crop) or found by Auto crop.
/// Rectangles are clipped to the image.
/// </summary>
public sealed record Crop(PixelRect Window)
{
    /// <summary>A Classic crop: exactly the rectangle the user drew.</summary>
    public static Crop Classic(PixelRect rectangle) => new(rectangle);
}
