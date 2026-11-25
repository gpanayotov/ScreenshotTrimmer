using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>Measures each corner's radius and makes the pixels outside the rounded corners transparent.</summary>
internal static class CornerRounding
{
    /// <summary>A pixel is outside the window when it differs by more than this from the reference.</summary>
    private const int DifferenceThreshold = 40;

    /// <summary>How far in, at most, the reference pixel lies; also the longest stretch walked.</summary>
    private const int MaxReach = 40;

    /// <summary>Measured radii closer than this are taken to be the same radius.</summary>
    private const double Agreement = 3;

    /// <summary>Samples per pixel side when working out how much of a pixel lies inside the arc.</summary>
    private const int Subsamples = 8;

    /// <summary>
    /// Measures the corners of <paramref name="window"/>, the window's rectangle within <paramref name="image"/>,
    /// and makes the pixels outside the rounded corners transparent. The Result's corners on a side with a
    /// Popup overhang are not the window's corners, so they stay square; their measured radius is still reported.
    /// </summary>
    public static (CornerRadii Measured, CornerRadii Applied) Apply(
        RgbaImage image, PixelRect window, RoundedCorners settings, Overhang overhang)
    {
        var measured = Measure(image, window);
        var applied = Group(measured, [
            overhang.Left || overhang.Top,
            overhang.Right || overhang.Top,
            overhang.Right || overhang.Bottom,
            overhang.Left || overhang.Bottom,
        ]);
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
    /// of them, at the largest of those radii. Agreement chains: radii in order, each within
    /// <see cref="Agreement"/> of the next, form one group, so every corner in a group gets the same radius.
    /// A corner that agrees with no other stays square.
    /// </summary>
    /// <param name="square">
    /// Per corner (top-left, top-right, bottom-right, bottom-left), whether it lies on a side with a Popup
    /// overhang. Such a corner stays square but still counts towards its group, so the corner it agrees with is
    /// rounded; its radius, which the popup may distort, does not set the group's radius.
    /// </param>
    private static CornerRadii Group(CornerRadii measured, bool[] square)
    {
        double[] radii = [measured.TopLeft, measured.TopRight, measured.BottomRight, measured.BottomLeft];
        var applied = new double[4];
        var order = Enumerable.Range(0, 4).Where(i => radii[i] > 0).OrderBy(i => radii[i]).ToList();
        var start = 0;
        for (var end = 1; end <= order.Count; end++)
        {
            if (end < order.Count && radii[order[end]] - radii[order[end - 1]] <= Agreement)
                continue;
            var group = order[start..end];
            var rounded = group.Where(i => !square[i]).ToList();
            if (group.Count >= 2 && rounded.Count > 0)
            {
                var radius = rounded.Max(i => radii[i]);
                foreach (var i in rounded)
                    applied[i] = radius;
            }
            start = end;
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

                var (x, y) = ToImage(new PixelRect(0, 0, image.Width, image.Height), corner, dx, dy);
                var pixel = image[x, y];
                var alpha = (byte)Math.Round(pixel.A * inside / (double)(Subsamples * Subsamples));
                image[x, y] = pixel with { A = alpha };
            }
    }

    private static CornerRadii Measure(RgbaImage image, PixelRect window)
    {
        var limit = Math.Min(MaxReach,Math.Min(window.Width / 4, window.Height / 4));
        if (limit < 1)
            return CornerRadii.Square;

        return new CornerRadii(
            MeasureCorner(image, window, limit, Corner.TopLeft),
            MeasureCorner(image, window, limit, Corner.TopRight),
            MeasureCorner(image, window, limit, Corner.BottomRight),
            MeasureCorner(image, window, limit, Corner.BottomLeft));
    }

    /// <summary>
    /// Walks the outer row and the outer column from the corner, counting the pixels that differ from a
    /// reference pixel <paramref name="limit"/> px further in, and turns the average count into a radius.
    /// </summary>
    private static double MeasureCorner(RgbaImage image, PixelRect window, int limit, Corner corner)
    {
        var rowCount = CountDiffering(image, window, corner, limit, alongRow: true);
        var columnCount = CountDiffering(image, window, corner, limit, alongRow: false);
        if (rowCount >= limit || columnCount >= limit)
            return 0;

        var count = (rowCount + columnCount) / 2.0;
        if (count <= 0)
            return 0;

        // The pixel centres on the outer row outside a circle of radius r number r − √(r − ¼);
        // solving r − √(r − ¼) = count gives r = count + √count + ½.
        return count + Math.Sqrt(count) + 0.5;
    }

    private static int CountDiffering(RgbaImage image, PixelRect window, Corner corner, int limit, bool alongRow)
    {
        var reference = alongRow ? Pixel(image, window, corner, limit, 0) : Pixel(image, window, corner, 0, limit);
        var count = 0;
        for (var i = 0; i < limit; i++)
        {
            var pixel = alongRow ? Pixel(image, window, corner, i, 0) : Pixel(image, window, corner, 0, i);
            if (pixel.DifferenceTo(reference) > DifferenceThreshold)
                count++;
        }
        return count;
    }

    /// <summary>
    /// The pixel <paramref name="dx"/> across and <paramref name="dy"/> down from the corner of
    /// <paramref name="window"/>, both inward.
    /// </summary>
    private static Rgba Pixel(RgbaImage image, PixelRect window, Corner corner, int dx, int dy)
    {
        var (x, y) = ToImage(window, corner, dx, dy);
        return image[x, y];
    }

    private static (int X, int Y) ToImage(PixelRect rect, Corner corner, int dx, int dy) => corner switch
    {
        Corner.TopLeft => (rect.X + dx, rect.Y + dy),
        Corner.TopRight => (rect.Right - 1 - dx, rect.Y + dy),
        Corner.BottomRight => (rect.Right - 1 - dx, rect.Bottom - 1 - dy),
        _ => (rect.X + dx, rect.Bottom - 1 - dy),
    };

    private enum Corner { TopLeft, TopRight, BottomRight, BottomLeft }
}
