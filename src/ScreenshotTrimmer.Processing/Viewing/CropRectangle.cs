using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Viewing;

/// <summary>
/// A point on screen in a viewer, in device-independent units from the view's top-left corner, as pointer events give
/// it. The display scaling turns it into the physical pixels a <see cref="Viewport"/> works in.
/// </summary>
public readonly record struct ViewPoint(double X, double Y);

/// <summary>
/// The crop rectangle being edited on an image, in whole image pixels and always inside the image, with the rules for
/// editing it through a <see cref="Viewport"/>. Has no interface dependency and knows nothing about where its rectangle
/// came from, so a Classic crop and a detected crop are edited the same way.
/// </summary>
public sealed record CropRectangle
{
    /// <summary>A drag shorter than this, in device-independent screen units, is a click.</summary>
    public const double ClickDistance = 4;

    private CropRectangle(PixelRect? rectangle, int imageWidth, int imageHeight)
    {
        Rectangle = rectangle;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
    }

    /// <summary>The crop rectangle in image pixels, or <c>null</c> for no crop (the whole image).</summary>
    public PixelRect? Rectangle { get; private init; }

    public int ImageWidth { get; }

    public int ImageHeight { get; }

    /// <summary>
    /// <paramref name="rectangle"/> on an image of the given size, clipped to the image; <c>null</c>, or a rectangle
    /// wholly outside the image, gives no crop.
    /// </summary>
    public static CropRectangle Of(PixelRect? rectangle, int imageWidth, int imageHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(imageWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(imageHeight, 1);
        var clipped = rectangle?.Intersect(new PixelRect(0, 0, imageWidth, imageHeight));
        return new CropRectangle(clipped is { IsEmpty: false } ? clipped : null, imageWidth, imageHeight);
    }

    /// <summary>
    /// The rectangle drawn by dragging from <paramref name="from"/> to <paramref name="to"/>, in either order, on the
    /// image shown by <paramref name="viewport"/> at the display <paramref name="scaling"/>. Each edge snaps to the
    /// nearest whole image pixel, the rectangle is clipped to the image and is at least 1 x 1. A drag shorter than
    /// <see cref="ClickDistance"/> on screen, or one wholly outside the image, leaves the crop unchanged.
    /// </summary>
    public CropRectangle Draw(ViewPoint from, ViewPoint to, Viewport viewport, double scaling)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaling);

        if (Math.Abs(to.X - from.X) < ClickDistance && Math.Abs(to.Y - from.Y) < ClickDistance)
            return this;

        var (x1, y1) = viewport.ToImage(from.X * scaling, from.Y * scaling);
        var (x2, y2) = viewport.ToImage(to.X * scaling, to.Y * scaling);
        var (left, right) = Edges(x1, x2, ImageWidth);
        var (top, bottom) = Edges(y1, y2, ImageHeight);
        if (left is null || top is null)
            return this;
        return this with { Rectangle = new PixelRect(left.Value, top.Value, right - left.Value, bottom - top.Value) };
    }

    /// <summary>
    /// The edges, on whole pixels within 0..<paramref name="length"/>, of a span drawn from <paramref name="a"/> to
    /// <paramref name="b"/>, at least one pixel apart; <c>null</c> when the span lies wholly outside the image.
    /// </summary>
    private static (int? Start, int End) Edges(double a, double b, int length)
    {
        var low = Math.Min(a, b);
        var high = Math.Max(a, b);
        if (high <= 0 || low >= length)
            return (null, 0);
        var start = Math.Clamp((int)Math.Round(low), 0, length);
        var end = Math.Clamp((int)Math.Round(high), 0, length);
        if (end > start)
            return (start, end);
        // Narrower than a pixel: keep the pixel the drag lies in.
        start = Math.Clamp((int)Math.Floor(low), 0, length - 1);
        return (start, start + 1);
    }
}
