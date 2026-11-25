using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>
/// Clean edges, part one: removes leftover lines of background along the crop's edges.
/// Lines are checked from the outermost row or column inward, up to <see cref="MaxLinesPerSide"/> per side.
/// A line is removed when it is nearly one colour and clearly differs from the line
/// <see cref="Depth"/> px further in.
/// </summary>
internal static class FrameLines
{
    private const int MaxLinesPerSide = 8;
    private const int Depth = 8;
    private const int Tolerance = 30;
    private const double UniformShare = 0.9;
    private const double DifferentShare = 0.6;

    private enum Side { Left, Top, Right, Bottom }

    /// <summary>
    /// Narrows <paramref name="area"/> of <paramref name="image"/> by the frame lines found along its edges.
    /// Sides with a Popup overhang are skipped, so the popup's edge is not mistaken for a frame.
    /// </summary>
    public static (PixelRect Area, FrameLinesRemoved Removed) Trim(RgbaImage image, PixelRect area, Overhang overhang)
    {
        var removed = new int[4];
        bool[] skip = [overhang.Left, overhang.Top, overhang.Right, overhang.Bottom];

        // Peel one line at a time from each side in turn, so a strip on one side
        // is judged on the area left after its neighbours' strips are gone.
        bool peeled;
        do
        {
            peeled = false;
            foreach (var side in Enum.GetValues<Side>())
            {
                if (skip[(int)side] || removed[(int)side] >= MaxLinesPerSide || !IsFrameLine(image, area, side))
                    continue;
                area = Shrink(area, side);
                removed[(int)side]++;
                peeled = true;
            }
        }
        while (peeled);

        var counts = new FrameLinesRemoved(
            removed[(int)Side.Left], removed[(int)Side.Top], removed[(int)Side.Right], removed[(int)Side.Bottom]);
        return (area, counts);
    }

    private static bool IsFrameLine(RgbaImage image, PixelRect area, Side side)
    {
        var horizontal = side is Side.Top or Side.Bottom;
        var length = horizontal ? area.Width : area.Height;
        var extent = horizontal ? area.Height : area.Width;
        if (extent <= Depth)
            return false;

        // Ignore a tenth at each end, where a neighbouring side's strip or a corner may lie.
        var skip = length / 10;
        var count = length - 2 * skip;
        if (count <= 0)
            return false;

        var outer = new Rgba[count];
        var inner = new Rgba[count];
        for (var i = 0; i < count; i++)
        {
            var along = skip + i;
            (outer[i], inner[i]) = side switch
            {
                Side.Top => (image[area.X + along, area.Y], image[area.X + along, area.Y + Depth]),
                Side.Bottom => (image[area.X + along, area.Bottom - 1], image[area.X + along, area.Bottom - 1 - Depth]),
                Side.Left => (image[area.X, area.Y + along], image[area.X + Depth, area.Y + along]),
                _ => (image[area.Right - 1, area.Y + along], image[area.Right - 1 - Depth, area.Y + along]),
            };
        }

        var median = Median(outer);
        var uniform = 0;
        var different = 0;
        for (var i = 0; i < count; i++)
        {
            if (Difference(outer[i], median) <= Tolerance)
                uniform++;
            if (Difference(outer[i], inner[i]) > Tolerance)
                different++;
        }

        return uniform >= UniformShare * count && different >= DifferentShare * count;
    }

    /// <summary>Colour difference: the largest difference in any channel.</summary>
    private static int Difference(Rgba a, Rgba b) =>
        Math.Max(
            Math.Max(Math.Abs(a.R - b.R), Math.Abs(a.G - b.G)),
            Math.Max(Math.Abs(a.B - b.B), Math.Abs(a.A - b.A)));

    /// <summary>The per-channel median colour of a line.</summary>
    private static Rgba Median(Rgba[] line)
    {
        static byte ChannelMedian(Rgba[] line, Func<Rgba, byte> channel)
        {
            var values = line.Select(channel).Order().ToArray();
            return values[values.Length / 2];
        }

        return new Rgba(
            ChannelMedian(line, p => p.R),
            ChannelMedian(line, p => p.G),
            ChannelMedian(line, p => p.B),
            ChannelMedian(line, p => p.A));
    }

    private static PixelRect Shrink(PixelRect area, Side side) => side switch
    {
        Side.Left => area with { X = area.X + 1, Width = area.Width - 1 },
        Side.Top => area with { Y = area.Y + 1, Height = area.Height - 1 },
        Side.Right => area with { Width = area.Width - 1 },
        _ => area with { Height = area.Height - 1 },
    };

}
