using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Detection;

/// <summary>
/// Auto crop in region: the rectangle method restricted to a region the user draws, taking the largest rectangle fully
/// inside it. Buttons and cards do not float, so no elevation shadow is required.
/// </summary>
/// <remarks>
/// <para>
/// The region, with a 1px border so that a side lying on the region's own edge is seen, is enlarged <see cref="Scale"/>
/// times by repeating pixels before the rectangle method runs on it. Steps between repeated pixels are 0, so the edges are
/// the original ones, and every length the method measures in pixels is in effect divided by the scale: a button down to
/// 16px high is a rectangle, its small rounded corners are left out, and gaps of 2px are bridged.
/// </para>
/// <para>
/// When the largest rectangle encloses a second one within <see cref="InnerGap"/> pixels on all four sides, separated
/// from it by a plain strip, the inner one wins, and so on inward: the larger one is a frame formed by the edges of
/// neighbouring controls around the Subject, or a plain margin around it.
/// </para>
/// </remarks>
internal static class RegionMethod
{
    /// <summary>How many times the region is enlarged before its rectangles are found.</summary>
    private const int Scale = 2;

    /// <summary>The widest strip, in pixels, between a rectangle and one inside it for which the inner one wins.</summary>
    private const int InnerGap = 12;

    /// <summary>The largest rectangle fully inside <paramref name="region"/>, or <c>null</c> when there is none.</summary>
    public static PixelRect? Find(RgbaImage image, PixelRect region, CancellationToken cancellation)
    {
        var bounds = new PixelRect(0, 0, image.Width, image.Height);
        region = region.Intersect(bounds);
        if (region.IsEmpty)
            return null;
        var area = new PixelRect(region.X - 1, region.Y - 1, region.Width + 2, region.Height + 2).Intersect(bounds);
        var edges = new EdgeMap(Enlarge(image, area), RectangleMethod.MinimumRun, cancellation);
        // Edges lie only between original pixels, so every side found lies on a multiple of the scale.
        var inside = RectangleMethod.FindRectangles(edges, cancellation)
            .Select(found => new PixelRect(
                area.X + found.X / Scale, area.Y + found.Y / Scale, found.Width / Scale, found.Height / Scale))
            .Where(rectangle => region.Contains(rectangle))
            .OrderByDescending(rectangle => (long)rectangle.Width * rectangle.Height)
            .ThenBy(rectangle => rectangle.Y)
            .ThenBy(rectangle => rectangle.X)
            .ToList();
        if (inside.Count == 0)
            return null;
        // Inner rectangles win in turn: a frame may enclose a rectangle that is itself only part frame.
        var subject = inside[0];
        while (inside.FirstOrDefault(inner => IsFramed(inner, subject)
                   && IsPlainStrip(edges, subject.RelativeTo(area), inner.RelativeTo(area))) is { IsEmpty: false } inner)
            subject = inner;
        return subject;
    }

    /// <summary>
    /// Whether <paramref name="inner"/> is a smaller rectangle inside <paramref name="outer"/>, at most
    /// <see cref="InnerGap"/> pixels from each of its sides. A side may be shared: where the corner margin bridges a
    /// narrow gutter, a frame can take one side from a neighbour and the others from the Subject itself.
    /// </summary>
    private static bool IsFramed(PixelRect inner, PixelRect outer) =>
        inner != outer
        && inner.X - outer.X is >= 0 and <= InnerGap
        && inner.Y - outer.Y is >= 0 and <= InnerGap
        && outer.Right - inner.Right is >= 0 and <= InnerGap
        && outer.Bottom - inner.Bottom is >= 0 and <= InnerGap;

    /// <summary>
    /// Whether the strip between <paramref name="outer"/> and <paramref name="inner"/>, in the coordinates of the region
    /// before enlarging, has no edge in it. The corners are left out, where a rounded corner may lie.
    /// </summary>
    private static bool IsPlainStrip(EdgeMap edges, PixelRect outer, PixelRect inner)
    {
        PixelRect[] strips =
        [
            new(outer.X, inner.Y, inner.X - outer.X, inner.Height),
            new(inner.Right, inner.Y, outer.Right - inner.Right, inner.Height),
            new(inner.X, outer.Y, inner.Width, inner.Y - outer.Y),
            new(inner.X, inner.Bottom, inner.Width, outer.Bottom - inner.Bottom),
        ];
        foreach (var strip in strips)
        {
            var (left, top, right, bottom) = (strip.X * Scale, strip.Y * Scale, strip.Right * Scale, strip.Bottom * Scale);
            for (var y = top; y < bottom; y++)
                for (var x = left; x < right; x++)
                    if (x > left && edges.VerticalStep(x, y) >= EdgeMap.EdgeStep
                        || y > top && edges.HorizontalStep(x, y) >= EdgeMap.EdgeStep)
                        return false;
        }
        return true;
    }

    /// <summary>The pixels of <paramref name="area"/>, each repeated <see cref="Scale"/> times across and down.</summary>
    private static RgbaImage Enlarge(RgbaImage image, PixelRect area)
    {
        var enlarged = new RgbaImage(area.Width * Scale, area.Height * Scale);
        ReadOnlySpan<byte> source = image.Bytes;
        var target = enlarged.Bytes;
        var rowBytes = enlarged.Width * 4;
        for (var y = 0; y < area.Height; y++)
        {
            var row = target.Slice(y * Scale * rowBytes, rowBytes);
            for (var x = 0; x < area.Width; x++)
            {
                var pixel = source.Slice(((area.Y + y) * image.Width + area.X + x) * 4, 4);
                for (var copy = 0; copy < Scale; copy++)
                    pixel.CopyTo(row[((x * Scale + copy) * 4)..]);
            }
            for (var copy = 1; copy < Scale; copy++)
                row.CopyTo(target[((y * Scale + copy) * rowBytes)..]);
        }
        return enlarged;
    }
}
