namespace ScreenshotTrimmer.Processing.Imaging;

/// <summary>
/// An image as 8-bit, non-premultiplied RGBA pixels, row by row from the top-left.
/// This is the pixel type the processing library takes and returns.
/// </summary>
public sealed class RgbaImage
{
    private readonly byte[] _pixels;

    /// <summary>Creates a fully transparent image.</summary>
    public RgbaImage(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        _pixels = new byte[width * height * 4];
    }

    public int Width { get; }

    public int Height { get; }

    public Rgba this[int x, int y]
    {
        get
        {
            var i = Offset(x, y);
            return new Rgba(_pixels[i], _pixels[i + 1], _pixels[i + 2], _pixels[i + 3]);
        }
        set
        {
            var i = Offset(x, y);
            _pixels[i] = value.R;
            _pixels[i + 1] = value.G;
            _pixels[i + 2] = value.B;
            _pixels[i + 3] = value.A;
        }
    }

    /// <summary>The raw RGBA bytes, row by row from the top-left, 4 bytes per pixel.</summary>
    public Span<byte> Bytes => _pixels;

    /// <summary>True when both images have the same size and identical pixels.</summary>
    public bool PixelsEqual(RgbaImage other) =>
        Width == other.Width && Height == other.Height && _pixels.AsSpan().SequenceEqual(other._pixels);

    public RgbaImage Clone()
    {
        var copy = new RgbaImage(Width, Height);
        _pixels.CopyTo(copy._pixels, 0);
        return copy;
    }

    private int Offset(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(x), $"Pixel ({x}, {y}) is outside the {Width}x{Height} image.");
        return (y * Width + x) * 4;
    }
}
