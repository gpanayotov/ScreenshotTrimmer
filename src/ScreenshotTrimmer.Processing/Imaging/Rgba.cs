namespace ScreenshotTrimmer.Processing.Imaging;

/// <summary>One 8-bit, non-premultiplied RGBA pixel.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A);
