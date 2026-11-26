using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Viewing;

namespace ScreenshotTrimmer.Processing.Tests.Viewing;

public class LoupeTests
{
    // A 200 x 100 image fitted in an 800 x 600 view: 100%, its top-left corner at view point (300, 250). The crop
    // (10, 10, 40, 30) keeps the pixels 10..49 across and 10..39 down.
    private static readonly Viewport AtActualSize = Viewport.Fitted(200, 100, viewWidth: 800, viewHeight: 600);

    private static CropRectangle Cropped => CropRectangle.Of(new PixelRect(10, 10, 40, 30), 200, 100);

    private static ViewPoint P(double x, double y) => new(x, y);

    [Theory]
    [InlineData(CropHandle.TopLeft, 10, 10)]
    [InlineData(CropHandle.TopRight, 49, 10)]
    [InlineData(CropHandle.BottomRight, 49, 39)]
    [InlineData(CropHandle.BottomLeft, 10, 39)]
    public void A_corner_aims_at_the_kept_pixel_in_that_corner(CropHandle handle, int x, int y)
    {
        var loupe = Loupe.At(Cropped, handle, pointer: null, AtActualSize, scaling: 1);

        Assert.Equal((x, y), (loupe!.X, loupe.Y));
    }

    [Theory]
    // The pointer at view (360, 270.5) is over image pixel (60, 20); at y 250 it is above the crop.
    [InlineData(CropHandle.Left, 360, 270.5, 10, 20)]
    [InlineData(CropHandle.Right, 360, 270.5, 49, 20)]
    [InlineData(CropHandle.Top, 330.5, 200, 30, 10)]
    [InlineData(CropHandle.Bottom, 330.5, 200, 30, 39)]
    [InlineData(CropHandle.Left, 300, 250, 10, 10)]
    [InlineData(CropHandle.Right, 300, 400, 49, 39)]
    public void A_dragged_edge_aims_at_its_kept_pixel_level_with_the_pointer_within_the_crop(
        CropHandle handle, double pointerX, double pointerY, int x, int y)
    {
        var loupe = Loupe.At(Cropped, handle, P(pointerX, pointerY), AtActualSize, scaling: 1);

        Assert.Equal((x, y), (loupe!.X, loupe.Y));
    }

    [Theory]
    [InlineData(CropHandle.None, 349.5, 289.5, 49, 39)]
    [InlineData(CropHandle.None, 311, 289, 10, 39)]
    [InlineData(CropHandle.Inside, 345, 265, 49, 10)]
    [InlineData(CropHandle.Inside, 312, 262, 10, 10)]
    public void Drawing_or_moving_the_rectangle_aims_at_the_corner_nearest_the_pointer(
        CropHandle handle, double pointerX, double pointerY, int x, int y)
    {
        var loupe = Loupe.At(Cropped, handle, P(pointerX, pointerY), AtActualSize, scaling: 1);

        Assert.Equal((x, y), (loupe!.X, loupe.Y));
    }

    [Fact]
    public void Nudging_the_whole_rectangle_aims_at_its_top_left_corner()
    {
        var loupe = Loupe.At(Cropped, CropHandle.Inside, pointer: null, AtActualSize, scaling: 1);

        Assert.Equal((10, 10), (loupe!.X, loupe.Y));
    }

    [Theory]
    [InlineData(320.5, 260.5, 20, 10)]
    [InlineData(100, 500, 0, 99)]
    public void With_no_rectangle_yet_it_aims_at_the_pixel_under_the_pointer_within_the_image(
        double pointerX, double pointerY, int x, int y)
    {
        var none = CropRectangle.Of(null, 200, 100);

        var loupe = Loupe.At(none, CropHandle.None, P(pointerX, pointerY), AtActualSize, scaling: 1);

        Assert.Equal((x, y), (loupe!.X, loupe.Y));
    }

    [Fact]
    public void With_no_rectangle_and_no_pointer_there_is_no_Loupe()
    {
        Assert.Null(Loupe.At(CropRectangle.Of(null, 200, 100), CropHandle.None, pointer: null, AtActualSize, scaling: 1));
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(3, 1)]
    [InlineData(8, 1.5)]
    [InlineData(20, 2)]
    public void After_a_drag_at_any_zoom_it_aims_at_the_pixel_the_edge_landed_on(double zoom, double scaling)
    {
        var viewport = AtActualSize.ZoomAt(zoom, 400, 300);
        // Drags the bottom-right corner, at image (50, 40), 7 pixels right and 3 up: to (57, 37).
        var (fromX, fromY) = viewport.ToView(50, 40);
        var from = P(fromX / scaling, fromY / scaling);
        var to = P((fromX + 7 * zoom) / scaling, (fromY - 3 * zoom) / scaling);
        var (dragged, handle) = Cropped.Drag(CropHandle.BottomRight, from, to, viewport, scaling);

        var loupe = Loupe.At(dragged, handle, to, viewport, scaling);

        Assert.Equal((56, 36), (loupe!.X, loupe.Y));
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(3, 1.25)]
    [InlineData(12, 2)]
    public void While_drawing_at_any_zoom_it_aims_at_the_corner_the_pointer_places(double zoom, double scaling)
    {
        var viewport = AtActualSize.ZoomAt(zoom, 400, 300);
        // From image (20.2, 20.2) to (60.4, 30.4): the drawn rectangle is (20, 20) to (60, 30).
        var (fromX, fromY) = viewport.ToView(20.2, 20.2);
        var (toX, toY) = viewport.ToView(60.4, 30.4);
        var from = P(fromX / scaling, fromY / scaling);
        var to = P(toX / scaling, toY / scaling);
        var drawn = CropRectangle.Of(null, 200, 100).Draw(from, to, viewport, scaling);

        var loupe = Loupe.At(drawn, CropHandle.None, to, viewport, scaling);

        Assert.Equal((59, 29), (loupe!.X, loupe.Y));
    }

    [Fact]
    public void It_magnifies_a_square_of_pixels_centred_on_the_one_aimed_at()
    {
        var loupe = Loupe.At(Cropped, CropHandle.TopLeft, pointer: null, AtActualSize, scaling: 1)!;

        // 17 pixels across, 8 either side of the aimed pixel (10, 10).
        Assert.Equal(new PixelRect(2, 2, 17, 17), loupe.Area);
    }

    [Theory]
    [InlineData(CropHandle.Left, 10, 24)]
    [InlineData(CropHandle.Right, 49, 24)]
    [InlineData(CropHandle.Top, 29, 10)]
    [InlineData(CropHandle.Bottom, 29, 39)]
    public void A_nudged_edge_aims_at_its_middle(CropHandle handle, int x, int y)
    {
        var loupe = Loupe.At(Cropped, handle, pointer: null, AtActualSize, scaling: 1);

        Assert.Equal((x, y), (loupe!.X, loupe.Y));
    }

    [Theory]
    // A 136 x 160 Loupe in an 800 x 600 view, Loupe.Gap (20) away from the anchor.
    [InlineData(400, 300, 420, 320)]
    [InlineData(750, 300, 594, 320)]
    [InlineData(400, 550, 420, 370)]
    [InlineData(790, 590, 634, 410)]
    [InlineData(0, 0, 20, 20)]
    [InlineData(-50, 300, 0, 320)]
    [InlineData(900, 300, 664, 320)]
    [InlineData(400, 700, 420, 440)]
    public void It_sits_below_right_of_the_anchor_and_flips_or_stops_at_the_view_edges(
        double anchorX, double anchorY, double left, double top)
    {
        Assert.Equal(P(left, top), Loupe.Place(P(anchorX, anchorY), 136, 160, viewWidth: 800, viewHeight: 600));
    }

    [Fact]
    public void It_never_covers_the_anchor_nor_leaves_a_view_that_has_room_for_it()
    {
        const double width = 136, height = 160, viewWidth = 400, viewHeight = 380;
        for (var x = 0.0; x <= viewWidth; x += 7)
        {
            for (var y = 0.0; y <= viewHeight; y += 7)
            {
                var at = Loupe.Place(P(x, y), width, height, viewWidth, viewHeight);

                Assert.InRange(at.X, 0, viewWidth - width);
                Assert.InRange(at.Y, 0, viewHeight - height);
                var covers = x >= at.X && x <= at.X + width && y >= at.Y && y <= at.Y + height;
                Assert.False(covers, $"The Loupe at ({at.X}, {at.Y}) covers the anchor ({x}, {y}).");
            }
        }
    }

    [Fact]
    public void In_a_view_smaller_than_the_Loupe_it_starts_at_the_top_left()
    {
        Assert.Equal(P(0, 0), Loupe.Place(P(50, 50), 136, 160, viewWidth: 100, viewHeight: 100));
    }
}
