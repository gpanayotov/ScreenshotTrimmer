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

    /// <summary>The page of an application behind a modal backdrop: mid grey, darkened by the backdrop.</summary>
    public static readonly Rgba DimmedPage = new(96, 98, 104, 255);

    /// <summary>The page darkened a second time, by the backdrop of a dialog stacked on top.</summary>
    public static readonly Rgba TwiceDimmedPage = new(48, 49, 52, 255);

    /// <summary>A light dialog's background.</summary>
    public static readonly Rgba Dialog = new(250, 250, 250, 255);

    /// <summary>The dialog of <see cref="Dialog"/> dimmed by the backdrop of a dialog stacked on top.</summary>
    public static readonly Rgba DimmedDialog = new(125, 125, 125, 255);

    /// <summary>
    /// An application screenshot behind a modal backdrop: a <see cref="DimmedPage"/> with a darker header bar and rows of
    /// dimmed text, so the page is not one flat colour.
    /// </summary>
    public static RgbaImage DimmedApplication(int width, int height) => Draw(width, height, canvas =>
    {
        canvas.Clear(Sk(DimmedPage));
        Fill(canvas, new PixelRect(0, 0, width, 40), new Rgba(40, 52, 70, 255));
        for (var y = 70; y + 8 < height; y += 22)
            Fill(canvas, new PixelRect(30, y, width / 2, 8), new Rgba(70, 72, 78, 255));
    });

    /// <summary>
    /// Draws a light dialog filling <paramref name="bounds"/> with antialiased rounded corners of <paramref name="radius"/>,
    /// a dark title, rows of text and a blue button inside, the way a modal dialog looks.
    /// </summary>
    public static void DrawDialog(RgbaImage image, PixelRect bounds, float radius, Rgba? background = null)
    {
        var fill = background ?? Dialog;
        var ink = new Rgba((byte)(fill.R / 6), (byte)(fill.G / 6), (byte)(fill.B / 6), 255);
        var accent = new Rgba((byte)(fill.R / 6), (byte)(fill.G / 3), fill.B, 255);
        DrawOn(image, canvas =>
        {
            using var paint = new SKPaint { Color = Sk(fill), IsAntialias = true };
            canvas.DrawRoundRect(new SKRect(bounds.X, bounds.Y, bounds.Right, bounds.Bottom), radius, radius, paint);
            Fill(canvas, new PixelRect(bounds.X + 20, bounds.Y + 18, Math.Min(160, bounds.Width - 40), 12), ink);
            for (var y = bounds.Y + 50; y + 40 < bounds.Bottom - 50; y += 18)
                Fill(canvas, new PixelRect(bounds.X + 20, y, bounds.Width * 2 / 3, 7), ink);
            Fill(canvas, new PixelRect(bounds.Right - 110, bounds.Bottom - 44, 90, 28), accent);
        });
    }

    /// <summary>Fills <paramref name="bounds"/> of <paramref name="image"/> with one colour.</summary>
    public static void FillRect(RgbaImage image, PixelRect bounds, Rgba colour)
    {
        for (var y = bounds.Y; y < bounds.Bottom; y++)
            for (var x = bounds.X; x < bounds.Right; x++)
                image[x, y] = colour;
    }

    /// <summary>An image of one colour.</summary>
    public static RgbaImage Plain(int width, int height, Rgba colour)
    {
        var image = new RgbaImage(width, height);
        FillRect(image, new PixelRect(0, 0, width, height), colour);
        return image;
    }

    private static RgbaImage Draw(int width, int height, Action<SKCanvas> draw)
    {
        var image = new RgbaImage(width, height);
        DrawOn(image, draw);
        return image;
    }

    /// <summary>Draws on <paramref name="image"/> with Skia, which must be opaque.</summary>
    private static void DrawOn(RgbaImage image, Action<SKCanvas> draw)
    {
        var (width, height) = (image.Width, image.Height);
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(image.Bytes.ToArray(), 0, bitmap.GetPixels(), image.Bytes.Length);
        using (var canvas = new SKCanvas(bitmap))
            draw(canvas);
        bitmap.GetPixelSpan().CopyTo(image.Bytes);
    }

    private static void Fill(SKCanvas canvas, PixelRect bounds, Rgba colour)
    {
        using var paint = new SKPaint { Color = Sk(colour) };
        canvas.DrawRect(new SKRect(bounds.X, bounds.Y, bounds.Right, bounds.Bottom), paint);
    }

    private static SKColor Sk(Rgba colour) => new(colour.R, colour.G, colour.B, colour.A);
}
