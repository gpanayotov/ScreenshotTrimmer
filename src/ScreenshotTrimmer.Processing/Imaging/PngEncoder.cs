using System.Runtime.InteropServices;
using SkiaSharp;

namespace ScreenshotTrimmer.Processing.Imaging;

/// <summary>Encodes an <see cref="RgbaImage"/> as a lossless PNG with its transparency.</summary>
internal static class PngEncoder
{
    public static void Write(RgbaImage image, Stream destination)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bytes = image.Bytes.ToArray();
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            using var pixmap = new SKPixmap(info, handle.AddrOfPinnedObject(), info.RowBytes);
            using var data = pixmap.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("SkiaSharp could not encode the image as PNG.");
            data.SaveTo(destination);
        }
        finally
        {
            handle.Free();
        }
    }
}
