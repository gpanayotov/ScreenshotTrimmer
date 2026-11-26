using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Detection;

/// <summary>
/// The edges of an image, indexed for the rectangle method. An edge lies on a boundary between two neighbouring pixels
/// whose colours differ by at least <see cref="EdgeStep"/> in any channel, so a difference in hue alone counts. Boundary
/// x lies between columns x - 1 and x, and boundary y between rows y - 1 and y, so a rectangle's sides are its own
/// <see cref="PixelRect.X"/>, <see cref="PixelRect.Right"/>, <see cref="PixelRect.Y"/> and <see cref="PixelRect.Bottom"/>.
/// Edge pixels along each boundary are grouped into runs that bridge gaps of up to <see cref="MaxGap"/> pixels.
/// </summary>
internal sealed class EdgeMap
{
    /// <summary>The smallest colour step, in any channel, that makes an edge.</summary>
    public const int EdgeStep = 14;

    /// <summary>The longest gap, in pixels, that a run of edge pixels bridges.</summary>
    public const int MaxGap = 4;

    /// <summary>A run of edge pixels along one boundary: the first and last edge pixel, inclusive.</summary>
    public readonly record struct Run(int Start, int End)
    {
        public int Length => End - Start + 1;
    }

    // The colour step across each boundary, by pixel: index y * Width + x. Column 0 of the vertical map and row 0 of the
    // horizontal map are unused, as no boundary lies before the first pixel.
    private readonly byte[] _vertical;
    private readonly byte[] _horizontal;
    private readonly Run[][] _columnRuns;
    private readonly Run[][] _rowRuns;

    public EdgeMap(RgbaImage image, int minimumRun, CancellationToken cancellation)
    {
        Width = image.Width;
        Height = image.Height;
        _vertical = new byte[Width * Height];
        _horizontal = new byte[Width * Height];
        ReadOnlySpan<byte> pixels = image.Bytes;
        for (var y = 0; y < Height; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            var row = y * Width;
            for (var x = 1; x < Width; x++)
                _vertical[row + x] = Step(pixels, (row + x - 1) * 4, (row + x) * 4);
            if (y > 0)
                for (var x = 0; x < Width; x++)
                    _horizontal[row + x] = Step(pixels, (row - Width + x) * 4, (row + x) * 4);
        }
        _columnRuns = ColumnRuns(minimumRun, cancellation);
        _rowRuns = RowRuns(minimumRun, cancellation);
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The colour step across vertical boundary <paramref name="x"/> at row <paramref name="y"/>.</summary>
    public int VerticalStep(int x, int y) => _vertical[y * Width + x];

    /// <summary>The colour step across horizontal boundary <paramref name="y"/> at column <paramref name="x"/>.</summary>
    public int HorizontalStep(int x, int y) => _horizontal[y * Width + x];

    /// <summary>The runs of edge rows along vertical boundary <paramref name="x"/>, top to bottom.</summary>
    public IReadOnlyList<Run> ColumnRunsAt(int x) => _columnRuns[x];

    /// <summary>
    /// Whether one run along the boundary covers pixels <paramref name="from"/> to <paramref name="to"/> (exclusive), up
    /// to a gap of <see cref="MaxGap"/> at either end; that is, no gap along the span is longer than <see cref="MaxGap"/>.
    /// </summary>
    public bool Covers(bool vertical, int line, int from, int to)
    {
        var runs = vertical ? _columnRuns[line] : _rowRuns[line];
        // The last run starting at or before the span's first pixel allowed past a gap.
        int low = 0, high = runs.Length - 1, found = -1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (runs[middle].Start <= from + MaxGap)
            {
                found = middle;
                low = middle + 1;
            }
            else
                high = middle - 1;
        }
        return found >= 0 && runs[found].End >= to - 1 - MaxGap;
    }

    /// <summary>
    /// The colour step between the pixels at byte offsets <paramref name="a"/> and <paramref name="b"/>: the largest
    /// difference in any channel.
    /// </summary>
    public static byte Step(ReadOnlySpan<byte> pixels, int a, int b) =>
        (byte)Math.Max(
            Math.Abs(pixels[a] - pixels[b]),
            Math.Max(Math.Abs(pixels[a + 1] - pixels[b + 1]), Math.Abs(pixels[a + 2] - pixels[b + 2])));

    private Run[][] ColumnRuns(int minimumRun, CancellationToken cancellation)
    {
        var runs = new List<Run>[Width];
        var start = new int[Width];
        var last = new int[Width];
        for (var x = 0; x < Width; x++)
        {
            runs[x] = [];
            last[x] = int.MinValue / 2;
        }
        for (var y = 0; y < Height; y++)
        {
            if (y % 64 == 0)
                cancellation.ThrowIfCancellationRequested();
            var row = y * Width;
            for (var x = 1; x < Width; x++)
            {
                if (_vertical[row + x] < EdgeStep)
                    continue;
                if (y - last[x] - 1 > MaxGap)
                {
                    Close(runs[x], start[x], last[x], minimumRun);
                    start[x] = y;
                }
                last[x] = y;
            }
        }
        var result = new Run[Width][];
        for (var x = 0; x < Width; x++)
        {
            Close(runs[x], start[x], last[x], minimumRun);
            result[x] = [.. runs[x]];
        }
        return result;
    }

    private Run[][] RowRuns(int minimumRun, CancellationToken cancellation)
    {
        var result = new Run[Height][];
        result[0] = [];
        var runs = new List<Run>();
        for (var y = 1; y < Height; y++)
        {
            if (y % 64 == 0)
                cancellation.ThrowIfCancellationRequested();
            runs.Clear();
            var row = y * Width;
            int start = 0, last = int.MinValue / 2;
            for (var x = 0; x < Width; x++)
            {
                if (_horizontal[row + x] < EdgeStep)
                    continue;
                if (x - last - 1 > MaxGap)
                {
                    Close(runs, start, last, minimumRun);
                    start = x;
                }
                last = x;
            }
            Close(runs, start, last, minimumRun);
            result[y] = [.. runs];
        }
        return result;
    }

    private static void Close(List<Run> runs, int start, int last, int minimumRun)
    {
        if (last >= start && last - start + 1 >= minimumRun)
            runs.Add(new Run(start, last));
    }
}
