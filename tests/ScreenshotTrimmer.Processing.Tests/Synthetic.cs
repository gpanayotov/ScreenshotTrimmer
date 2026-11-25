using ScreenshotTrimmer.Processing.Imaging;
using SkiaSharp;

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

    /// <summary>The window colour of <see cref="RoundedWindow"/>: a light title bar and body.</summary>
    public static readonly Rgba Window = new(236, 236, 236, 255);

    /// <summary>
    /// The page behind <see cref="RoundedWindow"/>: grey, 80 darker than the window in every channel, so the
    /// measurement threshold of 40 falls where the arc half-covers a pixel.
    /// </summary>
    public static readonly Rgba Page = new(156, 156, 156, 255);

    /// <summary>
    /// A window filling the whole image, drawn antialiased with the given corner radii
    /// (0 = square) on the <see cref="Page"/> colour, like a tight crop of a real window.
    /// </summary>
    public static RgbaImage RoundedWindow(
        int width, int height, float topLeft, float topRight, float bottomRight, float bottomLeft)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(Page.R, Page.G, Page.B));
            using var shape = new SKRoundRect();
            shape.SetRectRadii(new SKRect(0, 0, width, height),
            [
                new SKPoint(topLeft, topLeft), new SKPoint(topRight, topRight),
                new SKPoint(bottomRight, bottomRight), new SKPoint(bottomLeft, bottomLeft),
            ]);
            using var paint = new SKPaint { Color = new SKColor(Window.R, Window.G, Window.B), IsAntialias = true };
            canvas.DrawRoundRect(shape, paint);
        }

        var image = new RgbaImage(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                image[x, y] = new Rgba(c.Red, c.Green, c.Blue, c.Alpha);
            }
        return image;
    }
}
