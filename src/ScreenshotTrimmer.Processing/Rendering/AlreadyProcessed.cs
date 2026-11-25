using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>
/// Recognises an image that was already processed: one that already has the transparent padding a
/// <b>Shadow</b> adds. The render report and Batch selection share this one check.
/// </summary>
public static class AlreadyProcessed
{
    /// <summary>A border sample with alpha below this counts as transparent.</summary>
    private const byte TransparentBelow = 40;

    /// <summary>More than this share of the border samples must be transparent.</summary>
    private const double RequiredShare = 0.9;

    /// <summary>
    /// True when more than 90% of the pixels along the image's outermost border have alpha below 40.
    /// </summary>
    public static bool HasShadowPadding(RgbaImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var samples = 0;
        var transparent = 0;
        void Sample(int x, int y)
        {
            samples++;
            if (image[x, y].A < TransparentBelow)
                transparent++;
        }

        for (var x = 0; x < image.Width; x++)
        {
            Sample(x, 0);
            if (image.Height > 1)
                Sample(x, image.Height - 1);
        }
        for (var y = 1; y < image.Height - 1; y++)
        {
            Sample(0, y);
            if (image.Width > 1)
                Sample(image.Width - 1, y);
        }

        return transparent > RequiredShare * samples;
    }
}
