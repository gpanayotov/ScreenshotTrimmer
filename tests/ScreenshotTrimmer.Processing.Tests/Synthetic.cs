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

    /// <summary>The light background a sloppy crop leaves along a window's edges.</summary>
    public static readonly Rgba Background = new(240, 240, 240, 255);

    private static readonly Rgba CheckerDark = new(30, 60, 120, 255);
    private static readonly Rgba CheckerLight = new(200, 170, 90, 255);

    /// <summary>
    /// A window of busy content (a 3px checkerboard, so no row or column of it is one colour)
    /// with strips of <see cref="Background"/> of the given widths along each side.
    /// </summary>
    public static RgbaImage WindowWithStrips(int contentWidth, int contentHeight, int left = 0, int top = 0, int right = 0, int bottom = 0)
    {
        var image = new RgbaImage(left + contentWidth + right, top + contentHeight + bottom);
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                image[x, y] = Background;
        for (var y = 0; y < contentHeight; y++)
            for (var x = 0; x < contentWidth; x++)
                image[left + x, top + y] = (x / 3 + y / 3) % 2 == 0 ? CheckerDark : CheckerLight;
        return image;
    }

    /// <summary>The colour <see cref="WindowWithStrips"/> draws at content position (x, y).</summary>
    public static Rgba CheckerAt(int x, int y) => (x / 3 + y / 3) % 2 == 0 ? CheckerDark : CheckerLight;
}
