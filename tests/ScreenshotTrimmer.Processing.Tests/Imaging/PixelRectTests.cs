using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests.Imaging;

public class PixelRectTests
{
    private static readonly PixelRect Outer = new(10, 10, 40, 30);

    [Theory]
    [InlineData(20, 20, 10, 10)] // well inside
    [InlineData(10, 10, 40, 30)] // the same rectangle
    [InlineData(10, 25, 40, 15)] // sharing three sides
    public void A_rectangle_contains_one_lying_within_it(int x, int y, int width, int height)
    {
        Assert.True(Outer.Contains(new PixelRect(x, y, width, height)));
    }

    [Theory]
    [InlineData(9, 20, 10, 10)] // one pixel past the left side
    [InlineData(20, 20, 31, 10)] // one pixel past the right side
    [InlineData(20, 35, 10, 6)] // one pixel past the bottom
    [InlineData(0, 0, 60, 50)] // around it
    [InlineData(60, 10, 5, 5)] // beside it
    public void A_rectangle_does_not_contain_one_reaching_outside_it(int x, int y, int width, int height)
    {
        Assert.False(Outer.Contains(new PixelRect(x, y, width, height)));
    }
}
