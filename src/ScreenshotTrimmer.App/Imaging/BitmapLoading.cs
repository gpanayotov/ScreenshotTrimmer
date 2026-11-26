using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.App.Imaging;

/// <summary>Decodes image files into Avalonia bitmaps.</summary>
public static class BitmapLoading
{
    /// <summary>
    /// Decodes the whole image the way the processing library sees it, with JPEG orientation applied. Safe to call
    /// off the interface thread.
    /// </summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="InvalidDataException">The file is not an image that can be decoded.</exception>
    public static Bitmap Load(string path) => ToBitmap(ImageFile.Load(path));

    /// <summary>A bitmap of <paramref name="image"/>'s pixels. Safe to call off the interface thread.</summary>
    public static unsafe Bitmap ToBitmap(RgbaImage image)
    {
        // The Bitmap copies the pixels, so the pinned span only has to live for the call.
        fixed (byte* pixels = image.Bytes)
        {
            return new Bitmap(
                PixelFormat.Rgba8888,
                AlphaFormat.Unpremul,
                (nint)pixels,
                new PixelSize(image.Width, image.Height),
                new Vector(96, 96),
                image.Width * 4);
        }
    }
}
