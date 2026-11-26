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
    /// <remarks>
    /// A light dialog over a page dimmed by a modal backdrop is found by the brightness method, which keeps its exact
    /// edges. Anything else is found by the rectangle method: a dialog over an undimmed page, or a window over another
    /// application or a wallpaper. Of several windows that cast a shadow, the largest is the Subject, so a window wins
    /// over a menu open on top of it.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was cancelled.</exception>
    public static Subject? Find(RgbaImage image, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        var window = ChooseWindow(image, cancellation);
        if (window is not { } found || IsWholeImage(found, image))
            return null;
        return new Subject(found);
    }

    /// <summary>The pixels at each end of a side that may be lost to a rounded corner.</summary>
    private const int CornerMargin = RectangleMethod.CornerMargin;

    private static PixelRect? ChooseWindow(RgbaImage image, CancellationToken cancellation)
    {
        var edges = new EdgeMap(image, RectangleMethod.MinimumRun, cancellation);
        var rectangles = RectangleMethod.FindRectangles(edges, cancellation);
        var windows = RectangleMethod.Windows(image, rectangles);
        if (BrightnessMethod.FindWindow(image, cancellation) is not { } dialog
            || !IsDimmedDialog(dialog, image, edges, rectangles, windows))
            return windows.Count > 0 ? windows[0] : null;
        // A light window body under a dark title bar is bright too, with a wallpaper around it that is not: the window
        // the rectangle method finds around it, sharing its other sides, is the whole window.
        foreach (var window in windows)
            if (IsBodyOf(dialog, window))
                return window;
        return dialog;
    }

    /// <summary>
    /// Whether the brightness method's <paramref name="window"/> is a dialog over a dimmed page:
    /// <list type="bullet">
    /// <item>A page lies around it on all four sides. The bright body of an undimmed light page reaches the image's edge.</item>
    /// <item>Its sides are edges, or else no window overlaps it partly. The bright area of undimmed windows overlapping each
    /// other on a darker wallpaper ends where no edge is.</item>
    /// </list>
    /// </summary>
    private static bool IsDimmedDialog(
        PixelRect window, RgbaImage image, EdgeMap edges, IReadOnlyList<PixelRect> rectangles, IReadOnlyList<PixelRect> windows) =>
        IsSurroundedByPage(window, image)
        && (RectangleMethod.IsOutlined(edges, window, rectangles) || !windows.Any(other => OverlapsPartly(window, other)));

    private static bool OverlapsPartly(PixelRect a, PixelRect b) =>
        !a.Intersect(b).IsEmpty && !Contains(a, b) && !Contains(b, a);

    private static bool Contains(PixelRect outer, PixelRect inner) =>
        inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

    /// <summary>
    /// Whether a page lies around <paramref name="window"/> on all four sides. A bright area that reaches the image's
    /// edge is the body of an undimmed light page rather than a dialog over a dimmed one.
    /// </summary>
    private static bool IsSurroundedByPage(PixelRect window, RgbaImage image) =>
        window.X > 0 && window.Y > 0 && window.Right < image.Width && window.Bottom < image.Height;

    /// <summary>
    /// Whether <paramref name="body"/> is the bright body of <paramref name="window"/>: inside it, sharing at least two of
    /// its sides, and short of it by more than the corner margin on another.
    /// </summary>
    private static bool IsBodyOf(PixelRect body, PixelRect window)
    {
        if (!Contains(window, body))
            return false;
        int[] gaps = [body.X - window.X, body.Y - window.Y, window.Right - body.Right, window.Bottom - body.Bottom];
        return gaps.Count(gap => gap <= CornerMargin) >= 2 && gaps.Any(gap => gap > CornerMargin);
    }

    private static bool IsWholeImage(PixelRect window, RgbaImage image) =>
        window.X <= CornerMargin && window.Y <= CornerMargin
        && window.Right >= image.Width - CornerMargin && window.Bottom >= image.Height - CornerMargin;
}
