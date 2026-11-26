using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests.Samples;

/// <summary>
/// Draws a sample with detection's answer on it, so the maintainer can approve the answer by eye: the window in red,
/// each popup in blue and, when it differs, the approved answer in green. Each rectangle is outlined just outside its
/// edges, so the edge pixels themselves stay visible.
/// </summary>
internal static class SampleOverview
{
    private static readonly Rgba Window = new(255, 0, 0, 255);
    private static readonly Rgba Popup = new(0, 96, 255, 255);
    private static readonly Rgba Approved = new(0, 200, 0, 255);
    private const int Line = 2;

    public static void Write(string path, RgbaImage sample, Subject? found, Subject? approvedWhenDifferent)
    {
        var picture = new RgbaImage(sample.Width, sample.Height);
        sample.Bytes.CopyTo(picture.Bytes);
        if (approvedWhenDifferent is not null)
            Outline(picture, approvedWhenDifferent, Approved, Approved, gap: Line);
        if (found is not null)
            Outline(picture, found, Window, Popup, gap: 0);
        EncodedImages.WritePng(path, picture);
    }

    private static void Outline(RgbaImage picture, Subject subject, Rgba window, Rgba popup, int gap)
    {
        Outline(picture, subject.Window, window, gap);
        foreach (var rect in subject.Popups)
            Outline(picture, rect, popup, gap);
    }

    private static void Outline(RgbaImage picture, PixelRect rect, Rgba colour, int gap)
    {
        for (var ring = gap + 1; ring <= gap + Line; ring++)
        {
            int left = rect.X - ring, top = rect.Y - ring, right = rect.Right - 1 + ring, bottom = rect.Bottom - 1 + ring;
            for (var x = left; x <= right; x++)
            {
                Plot(picture, x, top, colour);
                Plot(picture, x, bottom, colour);
            }
            for (var y = top; y <= bottom; y++)
            {
                Plot(picture, left, y, colour);
                Plot(picture, right, y, colour);
            }
        }
    }

    private static void Plot(RgbaImage picture, int x, int y, Rgba colour)
    {
        if (x >= 0 && y >= 0 && x < picture.Width && y < picture.Height)
            picture[x, y] = colour;
    }
}
