using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Detection;

/// <summary>
/// The brightness method, for a light dialog over a page dimmed by a modal backdrop. The image is sampled on a grid;
/// the largest dense connected area of bright samples is taken, with the dialog's own text and controls filled in; the
/// window is the largest rectangle inside that area, since the area can also hold an attached popup; and each edge is
/// then moved to the outermost row or column of pixels that is at least half bright. A dialog stacked under another is
/// dimmed by the top one's backdrop, so the topmost dialog is the one that stays bright.
/// </summary>
internal static class BrightnessMethod
{
    /// <summary>A pixel is bright when its brightest colour channel is at least this.</summary>
    private const int BrightLevel = 190;

    /// <summary>The distance between samples, in pixels.</summary>
    private const int Step = 4;

    /// <summary>The smallest window, in samples along each side.</summary>
    private const int MinimumSamples = 4;

    /// <summary>An area is dense when it fills at least this share of its bounding rectangle, in percent.</summary>
    private const int DensePercent = 50;

    public static PixelRect? FindWindow(RgbaImage image, CancellationToken cancellation)
    {
        var grid = Sample(image, cancellation);
        var area = LargestDenseArea(grid, cancellation);
        if (area is null)
            return null;
        var cells = LargestRectangle(area, grid.Columns, grid.Rows);
        if (cells.Width < MinimumSamples || cells.Height < MinimumSamples)
            return null;
        cancellation.ThrowIfCancellationRequested();
        return SnapEdges(image, cells);
    }

    private sealed record Grid(bool[,] Bright, int Columns, int Rows);

    private static bool IsBright(Rgba pixel) => Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) >= BrightLevel;

    private static Grid Sample(RgbaImage image, CancellationToken cancellation)
    {
        var columns = (image.Width - 1) / Step + 1;
        var rows = (image.Height - 1) / Step + 1;
        var bright = new bool[columns, rows];
        for (var row = 0; row < rows; row++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (var column = 0; column < columns; column++)
                bright[column, row] = IsBright(image[column * Step, row * Step]);
        }
        return new Grid(bright, columns, rows);
    }

    /// <summary>
    /// The largest dense connected area of bright samples, with the holes inside it filled, as a mask over the grid;
    /// <c>null</c> when there is none.
    /// </summary>
    private static bool[,]? LargestDenseArea(Grid grid, CancellationToken cancellation)
    {
        var (columns, rows) = (grid.Columns, grid.Rows);
        var label = new int[columns, rows];
        bool[,]? best = null;
        var bestCount = 0;
        var next = 0;
        for (var row = 0; row < rows; row++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (var column = 0; column < columns; column++)
            {
                if (!grid.Bright[column, row] || label[column, row] != 0)
                    continue;
                var bounds = Flood(grid.Bright, label, column, row, ++next);
                var (filled, count) = FillHoles(label, next, bounds);
                if (count * 100 < bounds.Width * bounds.Height * DensePercent || count <= bestCount)
                    continue;
                best = filled;
                bestCount = count;
            }
        }
        return best;
    }

    /// <summary>Labels the 4-connected bright samples reachable from (column, row) and returns their bounds in samples.</summary>
    private static PixelRect Flood(bool[,] bright, int[,] label, int column, int row, int id)
    {
        var (columns, rows) = (bright.GetLength(0), bright.GetLength(1));
        var (left, top, right, bottom) = (column, row, column, row);
        var pending = new Stack<(int Column, int Row)>();
        label[column, row] = id;
        pending.Push((column, row));
        while (pending.Count > 0)
        {
            var (c, r) = pending.Pop();
            (left, top, right, bottom) = (Math.Min(left, c), Math.Min(top, r), Math.Max(right, c), Math.Max(bottom, r));
            foreach (var (nc, nr) in new[] { (c - 1, r), (c + 1, r), (c, r - 1), (c, r + 1) })
            {
                if (nc < 0 || nr < 0 || nc >= columns || nr >= rows || !bright[nc, nr] || label[nc, nr] != 0)
                    continue;
                label[nc, nr] = id;
                pending.Push((nc, nr));
            }
        }
        return new PixelRect(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>
    /// The area labelled <paramref name="id"/> as a grid mask, with every sample it encloses added (a dialog's text,
    /// icons and dark buttons), and the number of samples in it.
    /// </summary>
    private static (bool[,] Mask, int Count) FillHoles(int[,] label, int id, PixelRect bounds)
    {
        var mask = new bool[label.GetLength(0), label.GetLength(1)];
        // Samples in the bounds that are not in the area and can be reached from the bounds' border lie outside it.
        var outside = new bool[bounds.Width, bounds.Height];
        var pending = new Stack<(int Column, int Row)>();
        for (var c = 0; c < bounds.Width; c++)
            for (var r = 0; r < bounds.Height; r++)
            {
                var onBorder = c == 0 || r == 0 || c == bounds.Width - 1 || r == bounds.Height - 1;
                if (onBorder && label[bounds.X + c, bounds.Y + r] != id)
                {
                    outside[c, r] = true;
                    pending.Push((c, r));
                }
            }
        while (pending.Count > 0)
        {
            var (c, r) = pending.Pop();
            foreach (var (nc, nr) in new[] { (c - 1, r), (c + 1, r), (c, r - 1), (c, r + 1) })
            {
                if (nc < 0 || nr < 0 || nc >= bounds.Width || nr >= bounds.Height || outside[nc, nr]
                    || label[bounds.X + nc, bounds.Y + nr] == id)
                    continue;
                outside[nc, nr] = true;
                pending.Push((nc, nr));
            }
        }

        var count = 0;
        for (var c = 0; c < bounds.Width; c++)
            for (var r = 0; r < bounds.Height; r++)
                if (!outside[c, r])
                {
                    mask[bounds.X + c, bounds.Y + r] = true;
                    count++;
                }
        return (mask, count);
    }

    /// <summary>The largest rectangle of samples inside <paramref name="mask"/>; the first found wins a tie.</summary>
    private static PixelRect LargestRectangle(bool[,] mask, int columns, int rows)
    {
        var heights = new int[columns];
        var best = default(PixelRect);
        var bestArea = 0;
        var stack = new Stack<int>();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
                heights[column] = mask[column, row] ? heights[column] + 1 : 0;
            stack.Clear();
            for (var column = 0; column <= columns; column++)
            {
                var height = column < columns ? heights[column] : 0;
                while (stack.Count > 0 && heights[stack.Peek()] >= height)
                {
                    var top = heights[stack.Pop()];
                    var left = stack.Count == 0 ? 0 : stack.Peek() + 1;
                    var area = top * (column - left);
                    if (area <= bestArea)
                        continue;
                    bestArea = area;
                    best = new PixelRect(left, row - top + 1, column - left, top);
                }
                stack.Push(column);
            }
        }
        return best;
    }

    /// <summary>
    /// The window's pixel rectangle from its rectangle of samples: each edge moves to the outermost column or row of
    /// pixels that is at least half bright along the window.
    /// </summary>
    private static PixelRect SnapEdges(RgbaImage image, PixelRect cells)
    {
        var top = cells.Y * Step;
        var bottom = (cells.Bottom - 1) * Step;
        var left = SnapColumn(image, cells.X * Step, -1, top, bottom);
        var right = SnapColumn(image, (cells.Right - 1) * Step, +1, top, bottom);
        top = SnapRow(image, top, -1, left, right);
        bottom = SnapRow(image, bottom, +1, left, right);
        return new PixelRect(left, top, right - left + 1, bottom - top + 1);
    }

    private static int SnapColumn(RgbaImage image, int x, int outward, int top, int bottom)
    {
        bool HalfBright(int column)
        {
            var bright = 0;
            for (var y = top; y <= bottom; y++)
                if (IsBright(image[column, y]))
                    bright++;
            return bright * 2 >= bottom - top + 1;
        }

        return Snap(x, outward, image.Width, HalfBright);
    }

    private static int SnapRow(RgbaImage image, int y, int outward, int left, int right)
    {
        bool HalfBright(int row)
        {
            var bright = 0;
            for (var x = left; x <= right; x++)
                if (IsBright(image[x, row]))
                    bright++;
            return bright * 2 >= right - left + 1;
        }

        return Snap(y, outward, image.Height, HalfBright);
    }

    /// <summary>
    /// The outermost line from <paramref name="line"/> in the <paramref name="outward"/> direction that is half bright,
    /// moving inward first while the starting line is not.
    /// </summary>
    private static int Snap(int line, int outward, int size, Func<int, bool> halfBright)
    {
        for (var inward = 0; inward < Step && !halfBright(line); inward++)
            line -= outward;
        while (line + outward >= 0 && line + outward < size && halfBright(line + outward))
            line += outward;
        return line;
    }
}
