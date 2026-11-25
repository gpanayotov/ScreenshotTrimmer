using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>Measures each corner's radius and makes the pixels outside the rounded corners transparent.</summary>
internal static class CornerRounding
{
    /// <summary>A pixel is outside the window when it differs by more than this from the reference.</summary>
    private const int DifferenceThreshold = 40;

    /// <summary>How far in, at most, the reference pixel lies; also the longest stretch walked.</summary>
    private const int MaxLimit = 40;

    /// <summary>Measured radii closer than this are taken to be the same radius.</summary>
    private const double Agreement = 3;

    /// <summary>Samples per pixel side when working out how much of a pixel lies inside the arc.</summary>
    private const int Subsamples = 8;

    public static (CornerRadii Measured, CornerRadii Applied) Apply(RgbaImage image, RoundedCorners settings)
    {
        var measured = Measure(image);
        var applied = Group(measured);
        if (settings.ManualRadius is { } manual)
            applied = new CornerRadii(
                applied.TopLeft > 0 ? manual : 0,
                applied.TopRight > 0 ? manual : 0,
                applied.BottomRight > 0 ? manual : 0,
                applied.BottomLeft > 0 ? manual : 0);
        Clip(image, applied.TopLeft, Corner.TopLeft);
        Clip(image, applied.TopRight, Corner.TopRight);
        Clip(image, applied.BottomRight, Corner.BottomRight);
        Clip(image, applied.BottomLeft, Corner.BottomLeft);
        return (measured, applied);
    }

    /// <summary>
    /// Rounds together the corners whose measured radii agree within <see cref="Agreement"/>, at least two
    /// of them, at the largest of those radii. A corner that agrees with no other stays square.
    /// </summary>
    private static CornerRadii Group(CornerRadii measured)
    {
        double[] radii = [measured.TopLeft, measured.TopRight, measured.BottomRight, measured.BottomLeft];
        var applied = new double[4];
        for (var i = 0; i < 4; i++)
        {
            if (radii[i] <= 0)
                continue;
            var agreeing = radii.Where(r => r > 0 && Math.Abs(r - radii[i]) <= Agreement).ToArray();
            if (agreeing.Length >= 2)
                applied[i] = agreeing.Max();
        }
        return new CornerRadii(applied[0], applied[1], applied[2], applied[3]);
    }

    /// <summary>
    /// Scales the alpha of each pixel in the corner's square by the share of it inside a quarter circle of
    /// <paramref name="radius"/>, so pixels on the arc become partly transparent and those outside fully.
    /// </summary>
    private static void Clip(RgbaImage image, double radius, Corner corner)
    {
        radius = Math.Min(radius, Math.Min(image.Width, image.Height) / 2.0);
        if (radius <= 0)
            return;

        var size = (int)Math.Ceiling(radius);
        for (var dy = 0; dy < size; dy++)
            for (var dx = 0; dx < size; dx++)
            {
                var inside = 0;
                for (var j = 0; j < Subsamples; j++)
                    for (var i = 0; i < Subsamples; i++)
                    {
                        var sx = radius - (dx + (i + 0.5) / Subsamples);
                        var sy = radius - (dy + (j + 0.5) / Subsamples);
                        if (sx <= 0 || sy <= 0 || sx * sx + sy * sy <= radius * radius)
                            inside++;
                    }

                if (inside == Subsamples * Subsamples)
                    continue;

                var (x, y) = ToImage(image, corner, dx, dy);
                var pixel = image[x, y];
                var alpha = (byte)Math.Round(pixel.A * inside / (double)(Subsamples * Subsamples));
                image[x, y] = pixel with { A = alpha };
            }
    }

    private static CornerRadii Measure(RgbaImage image)
    {
        var limit = Math.Min(MaxLimit, Math.Min(image.Width / 4, image.Height / 4));
        if (limit < 1)
            return CornerRadii.Square;

        return new CornerRadii(
            MeasureCorner(image, limit, Corner.TopLeft),
            MeasureCorner(image, limit, Corner.TopRight),
            MeasureCorner(image, limit, Corner.BottomRight),
            MeasureCorner(image, limit, Corner.BottomLeft));
    }

    /// <summary>
    /// Walks the outer row and the outer column from the corner, counting the pixels that differ from a
    /// reference pixel <paramref name="limit"/> px further in, and turns the average count into a radius.
    /// </summary>
    private static double MeasureCorner(RgbaImage image, int limit, Corner corner)
    {
        var rowCount = CountDiffering(image, corner, limit, alongRow: true);
        var columnCount = CountDiffering(image, corner, limit, alongRow: false);
        if (rowCount >= limit || columnCount >= limit)
            return 0;

        var count = (rowCount + columnCount) / 2.0;
        if (count <= 0)
            return 0;

        // The pixel centres on the outer row outside a circle of radius r number r − √(r − ¼);
        // solving r − √(r − ¼) = count gives r = count + √count + ½.
        return count + Math.Sqrt(count) + 0.5;
    }

    private static int CountDiffering(RgbaImage image, Corner corner, int limit, bool alongRow)
    {
        var reference = alongRow ? Pixel(image, corner, limit, 0) : Pixel(image, corner, 0, limit);
        var count = 0;
        for (var i = 0; i < limit; i++)
        {
            var pixel = alongRow ? Pixel(image, corner, i, 0) : Pixel(image, corner, 0, i);
            if (Difference(pixel, reference) > DifferenceThreshold)
                count++;
        }
        return count;
    }

    /// <summary>The pixel <paramref name="dx"/> across and <paramref name="dy"/> down from the corner, both inward.</summary>
    private static Rgba Pixel(RgbaImage image, Corner corner, int dx, int dy)
    {
        var (x, y) = ToImage(image, corner, dx, dy);
        return image[x, y];
    }

    private static (int X, int Y) ToImage(RgbaImage image, Corner corner, int dx, int dy) => corner switch
    {
        Corner.TopLeft => (dx, dy),
        Corner.TopRight => (image.Width - 1 - dx, dy),
        Corner.BottomRight => (image.Width - 1 - dx, image.Height - 1 - dy),
        _ => (dx, image.Height - 1 - dy),
    };

    /// <summary>The largest difference in any channel.</summary>
    private static int Difference(Rgba a, Rgba b) =>
        Math.Max(Math.Max(Math.Abs(a.R - b.R), Math.Abs(a.G - b.G)), Math.Max(Math.Abs(a.B - b.B), Math.Abs(a.A - b.A)));

    private enum Corner { TopLeft, TopRight, BottomRight, BottomLeft }
}
