using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ScreenshotTrimmer.Processing.Imaging;
using SkiaSharp;

namespace ScreenshotTrimmer.App.Imaging;

/// <summary>Decodes small, upright previews of image files for the strip.</summary>
public static class ThumbnailDecoding
{
    /// <summary>
    /// Decodes the image at <paramref name="path"/> to fit within <paramref name="maxWidth"/> by
    /// <paramref name="maxHeight"/> pixels, never enlarged, with JPEG orientation applied as
    /// <c>ImageFile.Load</c> applies it. Uses the codec's scaled decoding where the format has one, so a large JPEG
    /// is never decoded at full size. Safe to call off the interface thread.
    /// </summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="InvalidDataException">The file is not an image that can be decoded.</exception>
    public static Bitmap Decode(string path, int maxWidth, int maxHeight)
    {
        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream)
            ?? throw new InvalidDataException($"'{path}' is not a supported image.");

        var origin = codec.EncodedOrigin;
        var swapsAxes = SwapsAxes(origin);
        var (storedWidth, storedHeight) = (codec.Info.Width, codec.Info.Height);
        // The box measured in the stored orientation, before the image is turned upright.
        var (boxWidth, boxHeight) = swapsAxes ? (maxHeight, maxWidth) : (maxWidth, maxHeight);
        var scale = Math.Min(1f, Math.Min((float)boxWidth / storedWidth, (float)boxHeight / storedHeight));

        // Only JPEG and WebP decode smaller; PNG and BMP come back at full size and are resized below.
        var scaled = codec.GetScaledDimensions(scale);
        var decoded = new SKBitmap(new SKImageInfo(scaled.Width, scaled.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        try
        {
            var result = codec.GetPixels(decoded.Info, decoded.GetPixels());
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                throw new InvalidDataException($"'{path}' could not be decoded: {result}.");

            var width = Math.Max(1, (int)Math.Round(storedWidth * scale));
            var height = Math.Max(1, (int)Math.Round(storedHeight * scale));
            if (decoded.Width != width || decoded.Height != height)
            {
                var resized = decoded.Resize(decoded.Info.WithSize(width, height), SKFilterQuality.Medium)
                    ?? throw new InvalidDataException($"'{path}' could not be resized.");
                decoded.Dispose();
                decoded = resized;
            }

            using var upright = Upright(decoded, origin);
            return ToBitmap(upright);
        }
        finally
        {
            decoded.Dispose();
        }
    }

    /// <summary>
    /// A thumbnail of <paramref name="image"/>'s pixels, such as a Pasted image's, fitting within
    /// <paramref name="maxWidth"/> by <paramref name="maxHeight"/> pixels and never enlarged. Safe to call off the
    /// interface thread.
    /// </summary>
    public static unsafe Bitmap FromPixels(RgbaImage image, int maxWidth, int maxHeight)
    {
        var scale = Math.Min(1f, Math.Min((float)maxWidth / image.Width, (float)maxHeight / image.Height));
        var width = Math.Max(1, (int)Math.Round(image.Width * scale));
        var height = Math.Max(1, (int)Math.Round(image.Height * scale));
        using var full = new SKBitmap();
        fixed (byte* pixels = image.Bytes)
        {
            full.InstallPixels(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                (nint)pixels, image.Width * 4);
            using var resized = full.Resize(
                    new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), SKFilterQuality.Medium)
                ?? throw new InvalidDataException("The image could not be resized.");
            return ToBitmap(resized);
        }
    }

    private static bool SwapsAxes(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    /// <summary>
    /// A new bitmap with <paramref name="stored"/> drawn the right way up for <paramref name="origin"/>; the same
    /// mapping as the library's per-pixel <c>ImageFile.ApplyOrigin</c>.
    /// </summary>
    private static SKBitmap Upright(SKBitmap stored, SKEncodedOrigin origin)
    {
        var swapsAxes = SwapsAxes(origin);
        var upright = new SKBitmap(stored.Info.WithSize(
            swapsAxes ? stored.Height : stored.Width,
            swapsAxes ? stored.Width : stored.Height));
        using var canvas = new SKCanvas(upright);
        float w = stored.Width, h = stored.Height;
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Scale(-1, 1);
                canvas.Translate(-w, 0);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.RotateDegrees(180);
                canvas.Translate(-w, -h);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Scale(1, -1);
                canvas.Translate(0, -h);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(h, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(h, w);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, w);
                canvas.RotateDegrees(270);
                break;
        }
        canvas.DrawBitmap(stored, 0, 0);
        return upright;
    }

    private static Bitmap ToBitmap(SKBitmap bitmap) =>
        // The Bitmap copies the pixels, so the SKBitmap can be disposed afterwards.
        new(PixelFormat.Rgba8888, AlphaFormat.Premul, bitmap.GetPixels(),
            new PixelSize(bitmap.Width, bitmap.Height), new Vector(96, 96), bitmap.RowBytes);
}
