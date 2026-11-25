using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>Adds transparent padding around an image and draws a blurred, offset black shadow on it.</summary>
internal static class ShadowDrawing
{
    /// <summary>The Gaussian blur sigma at size 1.0.</summary>
    private const double BaseSigma = 6;

    /// <summary>How far down the shadow is moved, in pixels; fixed whatever the size.</summary>
    private const int Offset = 2;

    public static (RgbaImage Result, int Padding) Apply(RgbaImage image, Shadow shadow)
    {
        var sigma = BaseSigma * shadow.Size;
        var padding = (int)Math.Round(2 * sigma + Offset);
        var width = image.Width + 2 * padding;
        var height = image.Height + 2 * padding;

        // The shadow starts as the Subject's alpha, moved down by the offset, so it follows the outline.
        var mask = new double[width * height];
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                mask[(padding + Offset + y) * width + padding + x] = image[x, y].A / 255.0;
        mask = Blur(mask, width, height, sigma);

        var opacity = Math.Clamp(shadow.Opacity, 0, 1);
        var result = new RgbaImage(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var shadowAlpha = mask[y * width + x] * opacity;
                var inside = x >= padding && x < padding + image.Width && y >= padding && y < padding + image.Height;
                result[x, y] = inside ? Over(image[x - padding, y - padding], shadowAlpha) : Black(shadowAlpha);
            }
        return (result, padding);
    }

    private static Rgba Black(double alpha) => new(0, 0, 0, ToByte(alpha * 255));

    /// <summary>The Subject's pixel composited over the black shadow, unpremultiplied.</summary>
    private static Rgba Over(Rgba top, double shadowAlpha)
    {
        var topAlpha = top.A / 255.0;
        var alpha = topAlpha + shadowAlpha * (1 - topAlpha);
        if (alpha <= 0)
            return default;
        // The shadow is black, so it contributes nothing to the colour, only to the alpha.
        var scale = topAlpha / alpha;
        return new Rgba(ToByte(top.R * scale), ToByte(top.G * scale), ToByte(top.B * scale), ToByte(alpha * 255));
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

    /// <summary>A separable Gaussian blur, with values beyond the edges taken as zero.</summary>
    private static double[] Blur(double[] values, int width, int height, double sigma)
    {
        if (sigma <= 0)
            return values;

        var kernel = Kernel(sigma);
        var reach = kernel.Length / 2;
        var across = new double[values.Length];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var sum = 0.0;
                for (var k = -reach; k <= reach; k++)
                {
                    var sx = x + k;
                    if (sx >= 0 && sx < width)
                        sum += values[y * width + sx] * kernel[k + reach];
                }
                across[y * width + x] = sum;
            }

        var blurred = new double[values.Length];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var sum = 0.0;
                for (var k = -reach; k <= reach; k++)
                {
                    var sy = y + k;
                    if (sy >= 0 && sy < height)
                        sum += across[sy * width + x] * kernel[k + reach];
                }
                blurred[y * width + x] = sum;
            }
        return blurred;
    }

    /// <summary>A normalised Gaussian kernel reaching three sigma either side.</summary>
    private static double[] Kernel(double sigma)
    {
        var reach = (int)Math.Ceiling(3 * sigma);
        var kernel = new double[2 * reach + 1];
        for (var i = -reach; i <= reach; i++)
            kernel[i + reach] = Math.Exp(-(i * i) / (2 * sigma * sigma));
        var total = kernel.Sum();
        for (var i = 0; i < kernel.Length; i++)
            kernel[i] /= total;
        return kernel;
    }
}
