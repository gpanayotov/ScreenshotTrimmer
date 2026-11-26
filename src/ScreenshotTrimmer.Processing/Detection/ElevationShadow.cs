using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Detection;

/// <summary>
/// The soft shadow a floating window casts on what lies below it. Outward from a side, the median brightness of each
/// row or column along it rises steadily for <see cref="Depth"/> pixels, and is still rising at the far end; a card
/// inside a page casts a short shadow that has faded within 3–4px.
/// </summary>
internal static class ElevationShadow
{
    /// <summary>How far outward from a side the shadow is measured, in pixels.</summary>
    private const int Depth = 8;

    /// <summary>The least the brightness rises over the whole depth.</summary>
    private const int MinimumRise = 6;

    /// <summary>The most the brightness may drop from one pixel to the next outward.</summary>
    private const int MaximumDrop = 1;

    /// <summary>The most the brightness may rise from one pixel to the next outward; a colour boundary jumps further.</summary>
    private const int MaximumJump = 30;

    /// <summary>The least the brightness still rises from <see cref="Depth"/> / 2 pixels out to the far end.</summary>
    private const int MinimumFarRise = 3;

    /// <summary>The number of sides of <paramref name="rectangle"/> that cast an elevation shadow.</summary>
    /// <param name="cornerMargin">The pixels at each end of a side left out, for a rounded corner.</param>
    public static int ShadowedSides(RgbaImage image, PixelRect rectangle, int cornerMargin)
    {
        var (top, bottom) = (rectangle.Y + cornerMargin, rectangle.Bottom - cornerMargin);
        var (left, right) = (rectangle.X + cornerMargin, rectangle.Right - cornerMargin);
        var sides = 0;
        if (Casts(depth => ColumnMedian(image, rectangle.X - depth, top, bottom)))
            sides++;
        if (Casts(depth => ColumnMedian(image, rectangle.Right - 1 + depth, top, bottom)))
            sides++;
        if (Casts(depth => RowMedian(image, rectangle.Y - depth, left, right)))
            sides++;
        if (Casts(depth => RowMedian(image, rectangle.Bottom - 1 + depth, left, right)))
            sides++;
        return sides;
    }

    /// <summary>Whether the brightness at 1 to <see cref="Depth"/> pixels out rises the way a shadow fades.</summary>
    /// <param name="median">The median brightness at a depth; -1 outside the image.</param>
    private static bool Casts(Func<int, int> median)
    {
        var levels = new int[Depth + 1];
        for (var depth = 1; depth <= Depth; depth++)
        {
            levels[depth] = median(depth);
            if (levels[depth] < 0)
                return false;
            if (depth > 1)
            {
                var change = levels[depth] - levels[depth - 1];
                if (change < -MaximumDrop || change > MaximumJump)
                    return false;
            }
        }
        return levels[Depth] - levels[1] >= MinimumRise && levels[Depth] - levels[Depth / 2] >= MinimumFarRise;
    }

    private static int ColumnMedian(RgbaImage image, int x, int from, int to)
    {
        if (x < 0 || x >= image.Width || to <= from)
            return -1;
        Span<int> histogram = stackalloc int[256];
        for (var y = from; y < to; y++)
            histogram[Brightness(image[x, y])]++;
        return Median(histogram, to - from);
    }

    private static int RowMedian(RgbaImage image, int y, int from, int to)
    {
        if (y < 0 || y >= image.Height || to <= from)
            return -1;
        Span<int> histogram = stackalloc int[256];
        for (var x = from; x < to; x++)
            histogram[Brightness(image[x, y])]++;
        return Median(histogram, to - from);
    }

    private static int Median(Span<int> histogram, int count)
    {
        var seen = 0;
        for (var level = 0; level < 256; level++)
        {
            seen += histogram[level];
            if (seen * 2 > count)
                return level;
        }
        return 255;
    }

    /// <summary>A pixel's brightness, 0–255, from its colour channels weighted as the eye sees them.</summary>
    private static int Brightness(Rgba pixel) => (299 * pixel.R + 587 * pixel.G + 114 * pixel.B + 500) / 1000;
}
