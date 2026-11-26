using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Detection;

/// <summary>
/// The brightness method, for a light dialog over a page dimmed by a modal backdrop. The image is sampled on a grid;
/// the largest dense connected area of bright samples is taken, with the dialog's own text and controls filled in; the
/// window is a large rectangle inside that area, since the area can also hold an attached popup; and each edge is
/// then moved to the outermost row or column of pixels that is at least half bright, leaving out the popup's part of
/// it. Which of the large rectangles is the window is for the caller to decide. A dialog stacked under another is
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

    /// <summary>
    /// The candidates for the window, largest first: the rectangles inside the bright area that no larger one there
    /// contains, of at least half the largest one's size. The largest is the window unless the area also holds an
    /// attached popup nearly as wide as the window, when the largest is the popup's columns stretched over the window.
    /// </summary>
    public static IReadOnlyList<PixelRect> FindWindows(RgbaImage image, CancellationToken cancellation)
    {
        var grid = Sample(image, cancellation);
        var area = LargestDenseArea(grid, cancellation);
        if (area is null)
            return [];
        var windows = new List<PixelRect>();
        foreach (var cells in LargeRectangles(area, grid.Columns, grid.Rows))
        {
            if (cells.Width < MinimumSamples || cells.Height < MinimumSamples)
                continue;
            cancellation.ThrowIfCancellationRequested();
            var window = SnapEdges(image, area, cells);
            if (!windows.Contains(window))
                windows.Add(window);
        }
        return windows;
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

    /// <summary>
    /// The rectangles of samples inside <paramref name="mask"/> that no other contains, of at least half the largest
    /// one's size, largest first; a tie goes to the one found first, top to bottom.
    /// </summary>
    private static List<PixelRect> LargeRectangles(bool[,] mask, int columns, int rows)
    {
        // Every rectangle as tall as the bright run above its bottom row allows and as wide as that height allows; the
        // ones no other contains are the maximal rectangles of the mask.
        var found = new List<PixelRect>();
        var heights = new int[columns];
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
                    if (top > 0)
                        found.Add(new PixelRect(left, row - top + 1, column - left, top));
                }
                stack.Push(column);
            }
        }
        if (found.Count == 0)
            return found;
        var largest = found.Max(Area);
        // Largest first, so a rectangle that another contains comes after it; the first found wins a tie.
        var large = found
            .Where(cells => Area(cells) * 2 >= largest)
            .Select((cells, order) => (Cells: cells, Order: order))
            .OrderByDescending(entry => Area(entry.Cells))
            .ThenBy(entry => entry.Order)
            .Select(entry => entry.Cells);
        var kept = new List<PixelRect>();
        foreach (var cells in large)
            if (!kept.Any(other => Contains(other, cells)))
                kept.Add(cells);
        return kept;
    }

    private static int Area(PixelRect cells) => cells.Width * cells.Height;

    private static bool Contains(PixelRect outer, PixelRect inner) =>
        inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

    /// <summary>
    /// The window's pixel rectangle from its rectangle of samples: each edge moves to the outermost column or row of
    /// pixels that is at least half bright along the window. Where the bright area carries on past an edge as an
    /// attached popup, the popup's part of the edge is left out, so the edge stops at the window.
    /// </summary>
    private static PixelRect SnapEdges(RgbaImage image, bool[,] area, PixelRect cells)
    {
        var top = cells.Y * Step;
        var bottom = (cells.Bottom - 1) * Step;
        var rows = (From: cells.Y, To: cells.Bottom);
        var columns = (From: cells.X, To: cells.Right);
        var left = SnapColumn(image, cells.X * Step, -1, top, bottom, Popup(area, rows, cells.X, -1, vertical: true));
        var right = SnapColumn(image, (cells.Right - 1) * Step, +1, top, bottom, Popup(area, rows, cells.Right - 1, +1, vertical: true));
        top = SnapRow(image, top, -1, left, right, Popup(area, columns, cells.Y, -1, vertical: false));
        bottom = SnapRow(image, bottom, +1, left, right, Popup(area, columns, cells.Bottom - 1, +1, vertical: false));
        return new PixelRect(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>
    /// Whether pixel <c>p</c> along the window's edge at sample line <paramref name="line"/> lies where the bright
    /// area carries on for at least <see cref="PopupSamples"/> samples past it in the <paramref name="outward"/>
    /// direction, as an attached popup does: along a vertical edge when <paramref name="vertical"/>, else a horizontal
    /// one. A pixel is in a popup's part when a sample within a step of it is.
    /// </summary>
    private static Func<int, bool> Popup(bool[,] area, (int From, int To) span, int line, int outward, bool vertical)
    {
        var (columns, rows) = (area.GetLength(0), area.GetLength(1));
        var carriesOn = new bool[span.To];
        for (var sample = span.From; sample < span.To; sample++)
        {
            var continues = true;
            for (var beyond = 1; beyond <= PopupSamples && continues; beyond++)
            {
                var at = line + outward * beyond;
                continues = vertical
                    ? at >= 0 && at < columns && area[at, sample]
                    : at >= 0 && at < rows && area[sample, at];
            }
            carriesOn[sample] = continues;
        }
        // A popup sticks out from part of the edge, short of its corners; bright area that carries on from an end of the
        // edge is a leftover strip of a loose crop, or a neighbour, and the edge snaps over it.
        for (var sample = span.From; sample < span.To && carriesOn[sample]; sample++)
            carriesOn[sample] = false;
        for (var sample = span.To - 1; sample >= span.From && carriesOn[sample]; sample--)
            carriesOn[sample] = false;
        return p =>
        {
            var sample = p / Step;
            return carriesOn[Math.Clamp(sample, span.From, span.To - 1)]
                || p % Step != 0 && sample + 1 < span.To && carriesOn[sample + 1];
        };
    }

    /// <summary>How many samples past the window's edge the bright area must carry on to count as an attached popup.</summary>
    private const int PopupSamples = 2;

    private static int SnapColumn(RgbaImage image, int x, int outward, int top, int bottom, Func<int, bool> inPopup) =>
        Snap(x, outward, image.Width, column => HalfBright(top, bottom, inPopup, y => IsBright(image[column, y])));

    private static int SnapRow(RgbaImage image, int y, int outward, int left, int right, Func<int, bool> inPopup) =>
        Snap(y, outward, image.Height, row => HalfBright(left, right, inPopup, x => IsBright(image[x, row])));

    /// <summary>
    /// Whether at least half the pixels <paramref name="from"/> to <paramref name="to"/> (inclusive) along a line are
    /// bright, leaving out a popup's part, or counting the whole line when the popup covers all of it.
    /// </summary>
    private static bool HalfBright(int from, int to, Func<int, bool> inPopup, Func<int, bool> isBright)
    {
        int bright = 0, counted = 0;
        for (var p = from; p <= to; p++)
            if (!inPopup(p))
            {
                counted++;
                if (isBright(p))
                    bright++;
            }
        if (counted == 0)
            for (var p = from; p <= to; p++)
            {
                counted++;
                if (isBright(p))
                    bright++;
            }
        return bright * 2 >= counted;
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
