using ScreenshotTrimmer.Processing.Imaging;
using SkiaSharp;

namespace ScreenshotTrimmer.Processing.SelfChecking;

/// <summary>
/// Draws a modal dialog screenshot in code: the image <see cref="SelfCheck"/> runs on, and the base of the test
/// project's synthetic images, so the self-check and the detection tests share one drawing.
/// </summary>
internal static class SyntheticScreenshot
{
    /// <summary>The page of an application behind a modal backdrop: mid grey, darkened by the backdrop.</summary>
    public static readonly Rgba DimmedPage = new(96, 98, 104, 255);

    /// <summary>A light dialog's background.</summary>
    public static readonly Rgba Dialog = new(250, 250, 250, 255);

    /// <summary>Where <see cref="DialogOverDimmedApplication"/> draws the dialog unless told otherwise.</summary>
    public static readonly PixelRect DefaultDialog = new(100, 80, 400, 250);

    /// <summary>
    /// An application screenshot behind a modal backdrop: a <see cref="DimmedPage"/> with a darker header bar and rows of
    /// dimmed text, so the page is not one flat colour.
    /// </summary>
    public static RgbaImage DimmedApplication(int width, int height)
    {
        var image = new RgbaImage(width, height);
        DrawOn(image, canvas =>
        {
            canvas.Clear(Sk(DimmedPage));
            Fill(canvas, new PixelRect(0, 0, width, 40), new Rgba(40, 52, 70, 255));
            for (var y = 70; y + 8 < height; y += 22)
                Fill(canvas, new PixelRect(30, y, width / 2, 8), new Rgba(70, 72, 78, 255));
        });
        return image;
    }

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

    /// <summary>
    /// A light dialog filling <paramref name="dialog"/> (by default <see cref="DefaultDialog"/>) over an 800 by 500
    /// <see cref="DimmedApplication"/>, as a modal dialog screenshot looks; Auto crop finds the dialog as its Subject.
    /// </summary>
    public static RgbaImage DialogOverDimmedApplication(PixelRect? dialog = null)
    {
        var image = DimmedApplication(800, 500);
        DrawDialog(image, dialog ?? DefaultDialog, 8);
        return image;
    }

    /// <summary>Draws on <paramref name="image"/> with Skia, which must be opaque.</summary>
    public static void DrawOn(RgbaImage image, Action<SKCanvas> draw)
    {
        using var bitmap = ToBitmap(image);
        using (var canvas = new SKCanvas(bitmap))
            draw(canvas);
        bitmap.GetPixelSpan().CopyTo(image.Bytes);
    }

    /// <summary>A Skia bitmap holding a copy of <paramref name="image"/>'s pixels.</summary>
    public static SKBitmap ToBitmap(RgbaImage image)
    {
        var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(image.Bytes.ToArray(), 0, bitmap.GetPixels(), image.Bytes.Length);
        return bitmap;
    }

    /// <summary>Fills <paramref name="bounds"/> with one colour, unantialiased.</summary>
    public static void Fill(SKCanvas canvas, PixelRect bounds, Rgba colour)
    {
        using var paint = new SKPaint { Color = Sk(colour) };
        canvas.DrawRect(new SKRect(bounds.X, bounds.Y, bounds.Right, bounds.Bottom), paint);
    }

    public static SKColor Sk(Rgba colour) => new(colour.R, colour.G, colour.B, colour.A);
}
