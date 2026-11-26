using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Detection;

/// <summary>
/// Finds the web page below the <b>Browser bar</b> of a maximised browser screenshot. The bar ends at the first edge
/// spanning the full width within the top <see cref="SearchHeight"/> rows that has an address field above it: two
/// partial-width edges <see cref="MinimumFieldHeight"/> to <see cref="MaximumFieldHeight"/> pixels apart, one over the
/// other. A separator line directly below that edge belongs to the bar.
/// </summary>
/// <remarks>
/// Edges are the same colour steps the rectangle method uses (<see cref="EdgeMap.EdgeStep"/> in any channel), grouped into
/// runs along each row that bridge gaps of up to <see cref="EdgeMap.MaxGap"/> pixels.
/// </remarks>
internal static class BrowserBar
{
    /// <summary>How far down, in pixels, the edge ending the bar may lie.</summary>
    private const int SearchHeight = 160;

    /// <summary>The nearest an address field's top and bottom edges lie to each other.</summary>
    private const int MinimumFieldHeight = 20;

    /// <summary>The farthest an address field's top and bottom edges lie from each other.</summary>
    private const int MaximumFieldHeight = 50;

    /// <summary>The thickest separator line, in pixels, directly below the bar's edge that still belongs to the bar.</summary>
    private const int MaximumSeparator = 3;

    /// <summary>
    /// How far, in pixels, the ends of an address field's top and bottom edges may differ, as its rounded ends round
    /// them off.
    /// </summary>
    private const int EndTolerance = RectangleMethod.CornerMargin;

    /// <summary>
    /// The page below the Browser bar, from the bar's end to the image's other edges, or <c>null</c> when the image shows
    /// no Browser bar.
    /// </summary>
    public static PixelRect? FindPage(RgbaImage image, CancellationToken cancellation)
    {
        var rows = RowRuns(image, Math.Min(SearchHeight + MaximumSeparator, image.Height - 1), cancellation);
        for (var y = 1; y < rows.Length && y <= SearchHeight; y++)
        {
            if (!SpansFullWidth(rows[y], image.Width) || !HasAddressFieldAbove(rows, y, image.Width))
                continue;
            var end = y;
            for (var below = y + 1; below < rows.Length && below <= y + MaximumSeparator; below++)
                if (SpansFullWidth(rows[below], image.Width))
                {
                    end = below;
                    break;
                }
            return new PixelRect(0, end, image.Width, image.Height - end);
        }
        return null;
    }

    private static bool SpansFullWidth(EdgeMap.Run[] runs, int width) =>
        runs.Any(run => run.Start <= EdgeMap.MaxGap && run.End >= width - 1 - EdgeMap.MaxGap);

    /// <summary>
    /// Whether an address field lies above boundary <paramref name="bar"/>: a long partial-width edge with another 20–50
    /// pixels below it, starting and ending at nearly the same columns.
    /// </summary>
    private static bool HasAddressFieldAbove(EdgeMap.Run[][] rows, int bar, int width)
    {
        for (var bottom = bar; bottom > MinimumFieldHeight; bottom--)
            foreach (var lower in rows[bottom])
            {
                if (!IsFieldEdge(lower, width))
                    continue;
                for (var top = Math.Max(1, bottom - MaximumFieldHeight); top <= bottom - MinimumFieldHeight; top++)
                    foreach (var upper in rows[top])
                        if (IsFieldEdge(upper, width)
                            && Math.Abs(upper.Start - lower.Start) <= EndTolerance
                            && Math.Abs(upper.End - lower.End) <= EndTolerance)
                            return true;
            }
        return false;
    }

    /// <summary>
    /// Whether <paramref name="run"/> can be an address field's edge: at least a third of the width, so that an
    /// application's buttons and search box do not pass, yet short of the full width.
    /// </summary>
    private static bool IsFieldEdge(EdgeMap.Run run, int width) =>
        run.Length >= width / 3 && !(run.Start <= EdgeMap.MaxGap && run.End >= width - 1 - EdgeMap.MaxGap);

    /// <summary>The runs of edge pixels along horizontal boundaries 1 to <paramref name="last"/>, by boundary.</summary>
    private static EdgeMap.Run[][] RowRuns(RgbaImage image, int last, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        ReadOnlySpan<byte> pixels = image.Bytes;
        var width = image.Width;
        var rows = new EdgeMap.Run[Math.Max(1, last + 1)][];
        rows[0] = [];
        var runs = new List<EdgeMap.Run>();
        for (var y = 1; y <= last; y++)
        {
            runs.Clear();
            int start = 0, end = int.MinValue / 2;
            for (var x = 0; x < width; x++)
            {
                if (EdgeMap.Step(pixels, ((y - 1) * width + x) * 4, (y * width + x) * 4) < EdgeMap.EdgeStep)
                    continue;
                if (x - end - 1 > EdgeMap.MaxGap)
                {
                    if (end >= start)
                        runs.Add(new EdgeMap.Run(start, end));
                    start = x;
                }
                end = x;
            }
            if (end >= start)
                runs.Add(new EdgeMap.Run(start, end));
            rows[y] = [.. runs];
        }
        return rows;
    }
}
