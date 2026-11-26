using SkiaSharp;

namespace ScreenshotTrimmer.Processing.Imaging;

/// <summary>Reads image files into <see cref="RgbaImage"/>s.</summary>
public static class ImageFile
{
    /// <summary>
    /// Decodes a PNG, JPEG, WebP or BMP file as 8-bit RGBA, with any orientation metadata
    /// applied, so the pixels match what the user sees.
    /// </summary>
    /// <exception cref="InvalidDataException">The file is not an image SkiaSharp can decode.</exception>
    public static RgbaImage Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Decode(stream, $"'{path}'");
    }

    /// <summary>
    /// Decodes an encoded PNG, JPEG, WebP or BMP image held in memory, such as one read from the clipboard, as
    /// <see cref="Load"/> does.
    /// </summary>
    /// <exception cref="InvalidDataException">The bytes are not an image SkiaSharp can decode.</exception>
    public static RgbaImage Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        using var stream = new MemoryStream(encoded, writable: false);
        return Decode(stream, "The data");
    }

    /// <param name="described">What is being decoded, for the exception messages.</param>
    private static RgbaImage Decode(Stream stream, string described)
    {
        using var codec = SKCodec.Create(stream)
            ?? throw new InvalidDataException($"{described} is not a supported image.");

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var stored = new RgbaImage(info.Width, info.Height);
        var decoded = codec.GetPixels(info, out var pixels);
        if (decoded is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            throw new InvalidDataException($"{described} could not be decoded: {decoded}.");
        pixels.CopyTo(stored.Bytes);

        return ApplyOrigin(stored, codec.EncodedOrigin);
    }

    private static RgbaImage ApplyOrigin(RgbaImage stored, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
            return stored;

        int w = stored.Width, h = stored.Height;
        var swapsAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var shown = swapsAxes ? new RgbaImage(h, w) : new RgbaImage(w, h);

        for (var dy = 0; dy < shown.Height; dy++)
            for (var dx = 0; dx < shown.Width; dx++)
            {
                var (sx, sy) = origin switch
                {
                    SKEncodedOrigin.TopRight => (w - 1 - dx, dy),
                    SKEncodedOrigin.BottomRight => (w - 1 - dx, h - 1 - dy),
                    SKEncodedOrigin.BottomLeft => (dx, h - 1 - dy),
                    SKEncodedOrigin.LeftTop => (dy, dx),
                    SKEncodedOrigin.RightTop => (dy, h - 1 - dx),
                    SKEncodedOrigin.RightBottom => (w - 1 - dy, h - 1 - dx),
                    SKEncodedOrigin.LeftBottom => (w - 1 - dy, dx),
                    _ => (dx, dy),
                };
                shown[dx, dy] = stored[sx, sy];
            }

        return shown;
    }
}
