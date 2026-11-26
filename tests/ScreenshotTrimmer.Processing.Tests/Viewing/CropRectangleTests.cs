using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Viewing;

namespace ScreenshotTrimmer.Processing.Tests.Viewing;

public class CropRectangleTests
{
    // A 200 x 100 image fitted in an 800 x 600 view: 100%, its top-left corner at view point (300, 250).
    private static readonly Viewport AtActualSize = Viewport.Fitted(200, 100, viewWidth: 800, viewHeight: 600);

    private static CropRectangle Empty => CropRectangle.Of(null, 200, 100);

    private static ViewPoint P(double x, double y) => new(x, y);

    [Theory]
    [InlineData(310, 260, 350, 290)] // top-left to bottom-right
    [InlineData(350, 290, 310, 260)] // bottom-right to top-left
    [InlineData(350, 260, 310, 290)] // top-right to bottom-left
    [InlineData(310, 290, 350, 260)] // bottom-left to top-right
    public void Drawing_in_any_direction_gives_the_same_rectangle(double x1, double y1, double x2, double y2)
    {
        var crop = Empty.Draw(P(x1, y1), P(x2, y2), AtActualSize, scaling: 1);

        Assert.Equal(new PixelRect(10, 10, 40, 30), crop.Rectangle);
    }

    [Fact]
    public void A_rectangle_drawn_partly_outside_the_image_is_clipped_to_it()
    {
        var crop = Empty.Draw(P(250, 200), P(450, 300), AtActualSize, scaling: 1);

        Assert.Equal(new PixelRect(0, 0, 150, 50), crop.Rectangle);
    }

    [Fact]
    public void A_rectangle_drawn_around_the_whole_image_keeps_the_whole_image()
    {
        var crop = Empty.Draw(P(0, 0), P(800, 600), AtActualSize, scaling: 1);

        Assert.Equal(new PixelRect(0, 0, 200, 100), crop.Rectangle);
    }

    [Fact]
    public void A_rectangle_drawn_wholly_outside_the_image_leaves_the_crop_unchanged()
    {
        var before = Empty.Draw(P(310, 260), P(350, 290), AtActualSize, scaling: 1);

        var after = before.Draw(P(10, 10), P(200, 200), AtActualSize, scaling: 1);

        Assert.Equal(new PixelRect(10, 10, 40, 30), after.Rectangle);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(2, -2)]
    public void A_drag_of_a_few_screen_pixels_is_a_click_and_leaves_the_crop_unchanged(double dx, double dy)
    {
        var before = Empty.Draw(P(310, 260), P(350, 290), AtActualSize, scaling: 1);

        var after = before.Draw(P(400, 300), P(400 + dx, 300 + dy), AtActualSize, scaling: 1);

        Assert.Equal(new PixelRect(10, 10, 40, 30), after.Rectangle);
    }

    [Fact]
    public void A_click_on_an_image_with_no_crop_leaves_it_without_one()
    {
        var crop = Empty.Draw(P(400, 300), P(401, 301), AtActualSize, scaling: 1);

        Assert.Null(crop.Rectangle);
    }

    [Fact]
    public void The_click_distance_is_measured_on_screen_not_in_image_pixels()
    {
        // At 3200% three screen pixels are a tenth of an image pixel; at 5% they are sixty.
        var zoomedIn = AtActualSize.ZoomAt(32, 400, 300);
        var zoomedOut = Viewport.Fitted(20000, 10000, 800, 600).ZoomAt(0.05, 400, 300);

        Assert.Null(Empty.Draw(P(400, 300), P(403, 300), zoomedIn, scaling: 1).Rectangle);
        Assert.Null(CropRectangle.Of(null, 20000, 10000).Draw(P(400, 300), P(403, 300), zoomedOut, scaling: 1).Rectangle);
    }

    [Fact]
    public void Drawing_again_replaces_the_rectangle()
    {
        var first = Empty.Draw(P(310, 260), P(350, 290), AtActualSize, scaling: 1);

        var second = first.Draw(P(400, 300), P(450, 340), AtActualSize, scaling: 1);

        Assert.Equal(new PixelRect(100, 50, 50, 40), second.Rectangle);
    }

    [Fact]
    public void A_drag_within_one_image_pixel_still_gives_a_rectangle_of_at_least_one_pixel()
    {
        // At 3200% a straight 10-pixel drag down covers a third of one image pixel, and no width at all.
        var zoomedIn = AtActualSize.ZoomAt(32, 400, 300);

        var drawn = Empty.Draw(P(400, 300), P(400, 310), zoomedIn, scaling: 1);

        Assert.Equal(1, drawn.Rectangle?.Width);
        Assert.Equal(1, drawn.Rectangle?.Height);
    }

    public static TheoryData<string, double, double, double, double, double, PixelRect> Snapping => new()
    {
        // 100%, scaling 1: image point = view point - (300, 250); (10.4, 10.6)-(50.6, 40.4).
        { "100%, scaling 1", 310.4, 260.6, 350.6, 290.4, 1, new PixelRect(10, 11, 41, 29) },
        // 400%, scaling 1: the image's top-left at view point (0, 100); (10.25, 10.25)-(50.75, 40.75).
        { "400%, scaling 1", 41, 141, 203, 263, 1, new PixelRect(10, 10, 41, 31) },
        // 400%, scaling 2: the same physical points, at half the device-independent coordinates.
        { "400%, scaling 2", 20.5, 70.5, 101.5, 131.5, 2, new PixelRect(10, 10, 41, 31) },
        // 400%, scaling 1.5: physical (41, 141)-(203, 263) again.
        { "400%, scaling 1.5", 41 / 1.5, 94, 203 / 1.5, 263 / 1.5, 1.5, new PixelRect(10, 10, 41, 31) },
    };

    [Theory]
    [MemberData(nameof(Snapping))]
    public void Edges_snap_to_the_nearest_whole_image_pixel_at_any_zoom_and_scaling(
        string zoomAndScaling, double x1, double y1, double x2, double y2, double scaling, PixelRect expected)
    {
        var viewport = zoomAndScaling.StartsWith("400%") ? AtActualSize.ZoomAt(4, 400, 300) : AtActualSize;

        var crop = Empty.Draw(P(x1, y1), P(x2, y2), viewport, scaling);

        Assert.Equal(expected, crop.Rectangle);
    }

    [Fact]
    public void Edges_snap_to_whole_image_pixels_when_zoomed_out()
    {
        // A 2000 x 1000 image fitted in 800 x 600 is shown at 40% with its top-left at view point (0, 100). At scaling
        // 1.25 the physical points (4.1, 104.3)-(20.3, 120.1) are image points (10.25, 10.75)-(50.75, 50.25).
        var viewport = Viewport.Fitted(2000, 1000, 800, 600);
        var crop = CropRectangle.Of(null, 2000, 1000);

        var drawn = crop.Draw(P(4.1 / 1.25, 104.3 / 1.25), P(20.3 / 1.25, 120.1 / 1.25), viewport, scaling: 1.25);

        Assert.Equal(new PixelRect(10, 11, 41, 39), drawn.Rectangle);
    }

    [Fact]
    public void Edges_follow_the_whole_screen_pixel_the_image_is_drawn_from()
    {
        // A 201-pixel-wide image in an 800-pixel view is centred at x = 299.5; the viewer draws it from x = 300.
        var viewport = Viewport.Fitted(201, 100, 800, 600);

        var crop = CropRectangle.Of(null, 201, 100).Draw(P(310.4, 260), P(350.6, 290), viewport, scaling: 1);

        Assert.Equal(10, crop.Rectangle?.X);
        Assert.Equal(51, crop.Rectangle?.Right);
    }

    [Fact]
    public void A_rectangle_given_partly_outside_the_image_is_clipped_to_it()
    {
        var crop = CropRectangle.Of(new PixelRect(-5, 90, 20, 20), 200, 100);

        Assert.Equal(new PixelRect(0, 90, 15, 10), crop.Rectangle);
    }
}
