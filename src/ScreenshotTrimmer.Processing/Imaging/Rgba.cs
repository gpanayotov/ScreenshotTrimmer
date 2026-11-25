namespace ScreenshotTrimmer.Processing.Imaging;

/// <summary>One 8-bit, non-premultiplied RGBA pixel.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    /// <summary>The colour difference to <paramref name="other"/>: the largest difference in any channel.</summary>
    public int DifferenceTo(Rgba other) =>
        Math.Max(
            Math.Max(Math.Abs(R - other.R), Math.Abs(G - other.G)),
            Math.Max(Math.Abs(B - other.B), Math.Abs(A - other.A)));
}
