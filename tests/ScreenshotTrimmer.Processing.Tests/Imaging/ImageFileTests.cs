using ScreenshotTrimmer.Processing.Imaging;
using SkiaSharp;

namespace ScreenshotTrimmer.Processing.Tests.Imaging;

public class ImageFileTests
{
    private static readonly SKColor Red = new(220, 30, 40);
    private static readonly SKColor Blue = new(20, 60, 200);

    [Theory]
    [InlineData("shot.png", 0)]
    [InlineData("shot.bmp", 0)]
    [InlineData("shot.webp", 0)]
    [InlineData("shot.jpg", 12)]
    [InlineData("shot.JPEG", 12)]
    public void Supported_formats_load_as_8_bit_rgba(string name, int tolerance)
    {
        using var folder = new TempFolder();
        var path = folder.File(name);
        EncodedImages.Write(path, EncodedImages.HalfAndHalf(32, 16, Red, Blue));

        var image = ImageFile.Load(path);

        Assert.Equal(32, image.Width);
        Assert.Equal(16, image.Height);
        AssertColour(Red, image[4, 8], tolerance);
        AssertColour(Blue, image[27, 8], tolerance);
    }

    [Fact]
    public void Png_transparency_is_kept()
    {
        using var folder = new TempFolder();
        var path = folder.File("transparent.png");
        EncodedImages.Write(path, EncodedImages.HalfAndHalf(8, 8, SKColors.Transparent, new SKColor(10, 20, 30, 128)));

        var image = ImageFile.Load(path);

        Assert.Equal(0, image[1, 1].A);
        Assert.Equal(new Rgba(10, 20, 30, 128), image[6, 6]);
    }

    [Fact]
    public void Jpeg_orientation_metadata_is_applied_on_load()
    {
        using var folder = new TempFolder();
        var path = folder.File("rotated.jpg");
        // Stored 32 wide, left half red, right half blue, tagged "rotate 90° clockwise to display".
        EncodedImages.WriteJpegWithOrientation(path, EncodedImages.HalfAndHalf(32, 16, Red, Blue), orientation: 6);

        var image = ImageFile.Load(path);

        // As displayed: 16 wide, 32 tall, the stored left half now on top.
        Assert.Equal(16, image.Width);
        Assert.Equal(32, image.Height);
        AssertColour(Red, image[8, 4], 12);
        AssertColour(Blue, image[8, 27], 12);
    }

    private static void AssertColour(SKColor expected, Rgba actual, int tolerance)
    {
        Assert.Equal(expected.Alpha, actual.A);
        Assert.InRange(actual.R, expected.Red - tolerance, expected.Red + tolerance);
        Assert.InRange(actual.G, expected.Green - tolerance, expected.Green + tolerance);
        Assert.InRange(actual.B, expected.Blue - tolerance, expected.Blue + tolerance);
    }
}
