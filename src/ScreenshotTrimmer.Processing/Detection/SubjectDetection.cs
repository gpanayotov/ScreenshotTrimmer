using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Detection;

/// <summary>
/// Finds the <b>Subject</b> of a screenshot with classical image analysis only (ADR 0001): no network, no randomness and
/// only integer arithmetic, so the same image always gives the same Subject on any machine.
/// </summary>
public static class SubjectDetection
{
    /// <summary>
    /// The Subject in the whole image, or <c>null</c> when none is found, including when the Subject is the whole image
    /// because the screenshot is already cropped to it.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was cancelled.</exception>
    public static Subject? Find(RgbaImage image, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        var window = BrightnessMethod.FindWindow(image, cancellation);
        if (window is not { } found || IsWholeImage(found, image))
            return null;
        return new Subject(found);
    }

    /// <summary>The pixels at each end of a side that may be lost to a rounded corner.</summary>
    private const int CornerMargin = 8;

    private static bool IsWholeImage(PixelRect window, RgbaImage image) =>
        window.X <= CornerMargin && window.Y <= CornerMargin
        && window.Right >= image.Width - CornerMargin && window.Bottom >= image.Height - CornerMargin;
}
