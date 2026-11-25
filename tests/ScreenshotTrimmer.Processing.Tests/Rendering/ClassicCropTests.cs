using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Tests.Rendering;

public class ClassicCropTests
{
    [Fact]
    public void No_steps_gives_a_result_identical_to_the_original()
    {
        var original = Synthetic.Gradient(40, 30);

        var output = Renderer.Render(original, Steps.None);

        Assert.Equal(40, output.Result.Width);
        Assert.Equal(30, output.Result.Height);
        Assert.True(output.Result.PixelsEqual(original));
    }

    [Fact]
    public void Classic_crop_keeps_exactly_the_given_rectangle()
    {
        var original = Synthetic.Gradient(40, 30);
        var steps = new Steps { Crop = Crop.Classic(new PixelRect(5, 7, 20, 10)) };

        var result = Renderer.Render(original, steps).Result;

        Assert.Equal(20, result.Width);
        Assert.Equal(10, result.Height);
        Assert.Equal(new Rgba(5, 7, 128, 255), result[0, 0]);
        Assert.Equal(new Rgba(24, 16, 128, 255), result[19, 9]);
    }

    [Theory]
    [InlineData(-10, -5, 25, 15, 0, 0, 15, 10)] // past the top-left
    [InlineData(30, 20, 25, 15, 30, 20, 10, 10)] // past the bottom-right
    [InlineData(-3, -3, 100, 100, 0, 0, 40, 30)] // larger than the image
    public void Classic_crop_partly_outside_the_image_is_clipped_to_it(
        int x, int y, int width, int height,
        int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var original = Synthetic.Gradient(40, 30);
        var steps = new Steps { Crop = Crop.Classic(new PixelRect(x, y, width, height)) };

        var result = Renderer.Render(original, steps).Result;

        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
        Assert.Equal(new Rgba((byte)expectedX, (byte)expectedY, 128, 255), result[0, 0]);
        Assert.Equal(
            new Rgba((byte)(expectedX + expectedWidth - 1), (byte)(expectedY + expectedHeight - 1), 128, 255),
            result[expectedWidth - 1, expectedHeight - 1]);
    }
}
