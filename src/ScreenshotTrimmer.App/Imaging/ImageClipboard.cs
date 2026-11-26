using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.App.Imaging;

/// <summary>Images in and out of the system clipboard. Call on the interface thread.</summary>
public interface IImageClipboard
{
    /// <summary>The image on the clipboard, or <c>null</c> when it holds none that can be read.</summary>
    Task<RgbaImage?> TryGetImageAsync();

    /// <summary>Puts <paramref name="image"/> on the clipboard as a PNG with its transparency.</summary>
    Task SetImageAsync(RgbaImage image);
}

/// <summary>
/// The clipboard through Avalonia's own clipboard, with no platform code. Pasting prefers PNG bytes, which the library
/// decodes exactly, and falls back to the bitmap Avalonia makes of the platform's format (a Windows DIB, an X11 JPEG).
/// Copying hands Avalonia a straight-alpha BGRA bitmap, which it offers as PNG everywhere and as DIB formats on
/// Windows.
/// </summary>
/// <param name="clipboard">The window's clipboard, looked up on each use; <c>null</c> when it has none.</param>
public sealed class ImageClipboard(Func<IClipboard?> clipboard) : IImageClipboard
{
    /// <summary>The MIME type, used on X11 (and so on Wayland through Xwayland) and by some Windows apps.</summary>
    private static readonly DataFormat<byte[]> PngMime = DataFormat.CreateBytesPlatformFormat("image/png");

    /// <summary>The registered Windows clipboard format that browsers, Office and the Snipping Tool offer.</summary>
    private static readonly DataFormat<byte[]> PngWindows = DataFormat.CreateBytesPlatformFormat("PNG");

    /// <summary>The bitmap last put on the clipboard, kept alive because formats are rendered when another app pastes.</summary>
    private WriteableBitmap? _copied;

    public async Task<RgbaImage?> TryGetImageAsync()
    {
        if (clipboard() is not { } board)
            return null;
        using var data = await board.TryGetDataAsync();
        if (data is null)
            return null;

        foreach (var format in new[] { PngMime, PngWindows })
        {
            if (!data.Contains(format) || await data.TryGetValueAsync(format) is not { Length: > 0 } png)
                continue;
            try
            {
                return await Task.Run(() => ImageFile.Decode(png));
            }
            catch (InvalidDataException)
            {
                // Not a PNG after all: try the next format.
            }
        }

        if (!data.Contains(DataFormat.Bitmap))
            return null;
        try
        {
            using var bitmap = await data.TryGetValueAsync(DataFormat.Bitmap);
            return bitmap is null ? null : ToRgbaImage(bitmap);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Avalonia could not decode what the platform offered; to the user that is no image.
            return null;
        }
    }

    public async Task SetImageAsync(RgbaImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (clipboard() is not { } board)
            return;
        var bitmap = ToClipboardBitmap(image);
        await board.SetBitmapAsync(bitmap);
        var previous = _copied;
        _copied = bitmap;
        previous?.Dispose();
    }

    /// <summary>
    /// A straight-alpha bitmap: Avalonia encodes the PNG in the bitmap's own alpha type, so this keeps semi-transparent
    /// Shadow pixels exact. BGRA, because Windows writes its CF_DIB format from the raw bytes as BGRA.
    /// </summary>
    private static WriteableBitmap ToClipboardBitmap(RgbaImage image)
    {
        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var target = bitmap.Lock();
        var source = image.Bytes;
        var row = new byte[image.Width * 4];
        for (var y = 0; y < image.Height; y++)
        {
            var sourceRow = source.Slice(y * image.Width * 4, row.Length);
            for (var x = 0; x < row.Length; x += 4)
            {
                row[x] = sourceRow[x + 2];
                row[x + 1] = sourceRow[x + 1];
                row[x + 2] = sourceRow[x];
                row[x + 3] = sourceRow[x + 3];
            }
            Marshal.Copy(row, 0, target.Address + y * target.RowBytes, row.Length);
        }
        return bitmap;
    }

    /// <summary>
    /// The pixels of a bitmap read from the clipboard as straight RGBA. The bitmap is drawn into a straight-alpha RGBA
    /// bitmap of its size, which converts from whatever format and alpha type the platform gave.
    /// </summary>
    private static RgbaImage ToRgbaImage(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var image = new RgbaImage(size.Width, size.Height);
        using var converted = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using (var buffer = converted.Lock())
        {
            bitmap.CopyPixels(buffer, AlphaFormat.Unpremul);
            var row = new byte[size.Width * 4];
            for (var y = 0; y < size.Height; y++)
            {
                Marshal.Copy(buffer.Address + y * buffer.RowBytes, row, 0, row.Length);
                row.CopyTo(image.Bytes.Slice(y * row.Length, row.Length));
            }
        }
        return image;
    }
}
