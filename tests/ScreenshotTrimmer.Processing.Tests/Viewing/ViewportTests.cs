using ScreenshotTrimmer.Processing.Viewing;

namespace ScreenshotTrimmer.Processing.Tests.Viewing;

public class ViewportTests
{
    [Fact]
    public void Fitting_shrinks_a_large_image_to_the_view_and_centres_it()
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 800, viewHeight: 600);

        Assert.Equal(0.4, viewport.Zoom, 9);
        Assert.Equal(0, viewport.ImageLeft, 9);
        Assert.Equal(100, viewport.ImageTop, 9);
        Assert.Equal(800, viewport.ShownWidth, 9);
        Assert.Equal(400, viewport.ShownHeight, 9);
    }

    [Fact]
    public void Fitting_never_enlarges_a_small_image()
    {
        var viewport = Viewport.Fitted(200, 100, viewWidth: 800, viewHeight: 600);

        Assert.Equal(1, viewport.Zoom, 9);
        Assert.Equal(300, viewport.ImageLeft, 9);
        Assert.Equal(250, viewport.ImageTop, 9);
    }

    [Fact]
    public void Zooming_keeps_the_image_point_under_the_anchor_in_place()
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 800, viewHeight: 600);
        // Image point (500, 500) is under view point (200, 300) when fitted at 40%.

        var zoomed = viewport.ZoomAt(2, anchorX: 200, anchorY: 300);

        Assert.Equal(2, zoomed.Zoom, 9);
        Assert.Equal(200, zoomed.ImageLeft + 500 * zoomed.Zoom, 9);
        Assert.Equal(300, zoomed.ImageTop + 500 * zoomed.Zoom, 9);
    }

    [Theory]
    [InlineData(100, 32)]
    [InlineData(0.001, 0.05)]
    public void Zoom_is_limited_to_5_to_3200_percent(double requested, double expected)
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 800, viewHeight: 600);

        Assert.Equal(expected, viewport.ZoomAt(requested, 400, 300).Zoom, 9);
    }
}
