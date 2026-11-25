using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests;

/// <summary>Synthetic test images drawn in code, so expected results are exact.</summary>
internal static class Synthetic
{
    /// <summary>An image where every pixel's colour encodes its own position: R = x, G = y.</summary>
    public static RgbaImage Gradient(int width, int height)
    {
        var image = new RgbaImage(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                image[x, y] = new Rgba((byte)x, (byte)y, 128, 255);
        return image;
    }
}
