using System.Runtime.InteropServices;
using ScreenshotTrimmer.Processing.Imaging;
using SkiaSharp;

namespace ScreenshotTrimmer.Processing.Tests;

/// <summary>Synthetic images drawn with SkiaSharp and written to disk in each supported format.</summary>
internal static class EncodedImages
{
    /// <summary>An image whose left half is <paramref name="left"/> and right half <paramref name="right"/>.</summary>
    public static SKBitmap HalfAndHalf(int width, int height, SKColor left, SKColor right)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, x < width / 2 ? left : right);
        return bitmap;
    }

    /// <summary>Writes <paramref name="image"/> as a PNG to <paramref name="path"/>.</summary>
    public static void WritePng(string path, RgbaImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bytes = image.Bytes.ToArray();
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            using var pixmap = new SKPixmap(info, handle.AddrOfPinnedObject(), info.RowBytes);
            using var data = pixmap.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("SkiaSharp could not encode the image.");
            using var file = File.Create(path);
            data.SaveTo(file);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>Writes the bitmap in the format named by the path's extension.</summary>
    public static void Write(string path, SKBitmap bitmap)
    {
        var bytes = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => Encode(bitmap, SKEncodedImageFormat.Png, 100),
            ".jpg" or ".jpeg" => Encode(bitmap, SKEncodedImageFormat.Jpeg, 95),
            ".webp" => Encode(bitmap, SKEncodedImageFormat.Webp, 100),
            ".bmp" => EncodeBmp(bitmap),
            var other => throw new ArgumentException($"No test encoder for {other}."),
        };
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>Writes a JPEG carrying an EXIF orientation tag (1–8).</summary>
    public static void WriteJpegWithOrientation(string path, SKBitmap bitmap, ushort orientation)
    {
        var jpeg = Encode(bitmap, SKEncodedImageFormat.Jpeg, 95);

        // APP1 "Exif" segment holding a big-endian TIFF header and one IFD with the Orientation tag.
        byte[] tiff =
        [
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, // header, IFD at offset 8
            0x00, 0x01,                                              // one entry
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01,          // tag 0x0112, SHORT, count 1
            (byte)(orientation >> 8), (byte)orientation, 0x00, 0x00, // value
            0x00, 0x00, 0x00, 0x00,                                  // no next IFD
        ];
        byte[] exifId = [(byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0];
        var segmentLength = 2 + exifId.Length + tiff.Length;

        using var output = new MemoryStream();
        output.Write(jpeg, 0, 2); // SOI
        output.Write([0xFF, 0xE1, (byte)(segmentLength >> 8), (byte)segmentLength]);
        output.Write(exifId);
        output.Write(tiff);
        output.Write(jpeg, 2, jpeg.Length - 2);
        File.WriteAllBytes(path, output.ToArray());
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality)
    {
        using var data = bitmap.Encode(format, quality);
        return data.ToArray();
    }

    /// <summary>A 32-bit bottom-up BMP with an alpha channel (BITMAPV4HEADER); SkiaSharp cannot encode BMP.</summary>
    private static byte[] EncodeBmp(SKBitmap bitmap)
    {
        const int fileHeaderSize = 14, infoHeaderSize = 108;
        var pixelBytes = bitmap.Width * bitmap.Height * 4;
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);

        w.Write((byte)'B'); w.Write((byte)'M');
        w.Write(fileHeaderSize + infoHeaderSize + pixelBytes);
        w.Write(0);
        w.Write(fileHeaderSize + infoHeaderSize);

        w.Write(infoHeaderSize);
        w.Write(bitmap.Width);
        w.Write(bitmap.Height); // positive: bottom-up
        w.Write((short)1);
        w.Write((short)32);
        w.Write(3); // BI_BITFIELDS
        w.Write(pixelBytes);
        w.Write(2835); w.Write(2835);
        w.Write(0); w.Write(0);
        w.Write(0x00FF0000); w.Write(0x0000FF00); w.Write(0x000000FF); w.Write(unchecked((int)0xFF000000));
        w.Write(0x73524742); // LCS_sRGB
        w.Write(new byte[36 + 12]); // endpoints and gamma, unused for sRGB

        for (var y = bitmap.Height - 1; y >= 0; y--)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                w.Write(c.Blue); w.Write(c.Green); w.Write(c.Red); w.Write(c.Alpha);
            }

        w.Flush();
        return stream.ToArray();
    }
}
