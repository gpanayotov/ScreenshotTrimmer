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

    [Fact]
    public void Zooming_by_a_factor_stops_at_100_percent_when_it_would_cross_it()
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 800, viewHeight: 600).ZoomAt(0.9, 400, 300);

        var zoomedIn = viewport.ZoomBy(1.25, 400, 300);
        var zoomedInAgain = zoomedIn.ZoomBy(1.25, 400, 300);

        Assert.Equal(1, zoomedIn.Zoom, 9);
        Assert.Equal(1.25, zoomedInAgain.Zoom, 9);
        Assert.Equal(1, zoomedInAgain.ZoomBy(0.5, 400, 300).Zoom, 9);
    }

    [Fact]
    public void Panning_moves_the_image_with_the_pointer()
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 800, viewHeight: 600).ZoomAt(1, 400, 300);

        var panned = viewport.PanBy(30, -20);

        Assert.Equal(viewport.ImageLeft + 30, panned.ImageLeft, 9);
        Assert.Equal(viewport.ImageTop - 20, panned.ImageTop, 9);
    }

    [Fact]
    public void Panning_stops_at_the_image_edges()
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 800, viewHeight: 600).ZoomAt(1, 400, 300);

        var pannedFar = viewport.PanBy(5000, -5000);

        Assert.Equal(0, pannedFar.ImageLeft, 9);
        Assert.Equal(600 - 1000, pannedFar.ImageTop, 9);
    }

    [Fact]
    public void An_axis_where_the_image_is_smaller_than_the_view_stays_centred_when_panning()
    {
        var viewport = Viewport.Fitted(2000, 100, viewWidth: 800, viewHeight: 600).ZoomAt(1, 400, 300);

        var panned = viewport.PanBy(0, 200);

        Assert.Equal(250, panned.ImageTop, 9);
    }

    [Fact]
    public void The_scroll_range_is_the_part_of_the_image_outside_the_view()
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 800, viewHeight: 600).ZoomAt(1, 0, 0);

        Assert.Equal(1200, viewport.HorizontalScrollMax, 9);
        Assert.Equal(400, viewport.VerticalScrollMax, 9);
        Assert.Equal(0, viewport.HorizontalScroll, 9);
        Assert.Equal(0, viewport.VerticalScroll, 9);
    }

    [Fact]
    public void Scrolling_moves_the_view_over_the_image()
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 800, viewHeight: 600).ZoomAt(1, 0, 0);

        var scrolled = viewport.ScrollTo(300, 100);

        Assert.Equal(-300, scrolled.ImageLeft, 9);
        Assert.Equal(-100, scrolled.ImageTop, 9);
        Assert.Equal(300, scrolled.HorizontalScroll, 9);
        Assert.Equal(100, scrolled.VerticalScroll, 9);
    }

    [Fact]
    public void A_fitted_image_has_nothing_to_scroll()
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 800, viewHeight: 600);

        Assert.Equal(0, viewport.HorizontalScrollMax, 9);
        Assert.Equal(0, viewport.VerticalScrollMax, 9);
    }

    [Fact]
    public void A_fitted_viewport_fits_again_when_the_view_is_resized()
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 800, viewHeight: 600);

        var resized = viewport.Resize(400, 600);

        Assert.True(resized.IsFitted);
        Assert.Equal(0.2, resized.Zoom, 9);
    }

    [Fact]
    public void A_zoomed_viewport_keeps_its_zoom_and_position_when_the_view_is_resized()
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 800, viewHeight: 600).ZoomAt(1, 0, 0).ScrollTo(300, 100);

        var resized = viewport.Resize(1000, 700);

        Assert.False(resized.IsFitted);
        Assert.Equal(1, resized.Zoom, 9);
        Assert.Equal(-300, resized.ImageLeft, 9);
        Assert.Equal(-100, resized.ImageTop, 9);
    }

    [Fact]
    public void Zooming_leaves_the_fitted_view_and_fitting_returns_to_it()
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 800, viewHeight: 600);

        Assert.True(viewport.IsFitted);
        Assert.False(viewport.ZoomAt(1, 0, 0).IsFitted);
        Assert.True(viewport.ZoomAt(1, 0, 0).Fit().IsFitted);
    }

    [Fact]
    public void Fitting_a_huge_image_stops_at_5_percent()
    {
        var viewport = Viewport.Fitted(100_000, 1000, viewWidth: 800, viewHeight: 600);

        Assert.Equal(0.05, viewport.Zoom, 9);
    }

    [Fact]
    public void An_empty_view_does_not_break_the_viewport()
    {
        var viewport = Viewport.Fitted(2000, 1000, viewWidth: 0, viewHeight: 0);

        Assert.True(viewport.Zoom > 0);
        Assert.True(double.IsFinite(viewport.ImageLeft));
        Assert.True(double.IsFinite(viewport.ImageTop));
    }
}
