using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Viewing;

namespace ScreenshotTrimmer.Processing.Tests.Viewing;

public class CropAdjustingTests
{
    // A 200 x 100 image fitted in an 800 x 600 view: 100%, its top-left corner at view point (300, 250). The crop
    // (10, 10, 40, 30) is shown from view point (310, 260) to (350, 290).
    private static readonly Viewport AtActualSize = Viewport.Fitted(200, 100, viewWidth: 800, viewHeight: 600);

    private static CropRectangle Cropped => CropRectangle.Of(new PixelRect(10, 10, 40, 30), 200, 100);

    private static ViewPoint P(double x, double y) => new(x, y);

    [Theory]
    [InlineData(310, 260, CropHandle.TopLeft)]
    [InlineData(350, 260, CropHandle.TopRight)]
    [InlineData(350, 290, CropHandle.BottomRight)]
    [InlineData(310, 290, CropHandle.BottomLeft)]
    [InlineData(330, 260, CropHandle.Top)]
    [InlineData(350, 275, CropHandle.Right)]
    [InlineData(330, 290, CropHandle.Bottom)]
    [InlineData(310, 275, CropHandle.Left)]
    [InlineData(330, 275, CropHandle.Inside)]
    [InlineData(400, 275, CropHandle.None)]
    [InlineData(330, 200, CropHandle.None)]
    public void Hit_testing_finds_each_corner_edge_and_the_inside(double x, double y, CropHandle expected)
    {
        Assert.Equal(expected, Cropped.HitTest(P(x, y), AtActualSize, scaling: 1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void A_handle_is_hit_within_a_fixed_screen_distance_at_any_scaling(double scaling)
    {
        // The edge x = 10 is drawn at physical x = 310, so in device-independent units at 310 / scaling.
        var edge = 310 / scaling;
        var y = 275 / scaling;
        var near = CropRectangle.HandleTolerance - 1;
        var far = CropRectangle.HandleTolerance + 1;

        Assert.Equal(CropHandle.Left, Cropped.HitTest(P(edge - near, y), AtActualSize, scaling));
        Assert.Equal(CropHandle.Left, Cropped.HitTest(P(edge + near, y), AtActualSize, scaling));
        Assert.Equal(CropHandle.None, Cropped.HitTest(P(edge - far, y), AtActualSize, scaling));
        Assert.Equal(CropHandle.Inside, Cropped.HitTest(P(edge + far, y), AtActualSize, scaling));
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(4)]
    [InlineData(16)]
    public void A_handle_is_hit_within_a_fixed_screen_distance_at_any_zoom(double zoom)
    {
        var viewport = AtActualSize.ZoomAt(zoom, 400, 300);
        var crop = CropRectangle.Of(new PixelRect(20, 20, 160, 60), 200, 100);
        var (right, top) = viewport.ToView(180, 20);
        var (_, bottom) = viewport.ToView(180, 80);
        var middle = (top + bottom) / 2;

        Assert.Equal(CropHandle.Right, crop.HitTest(P(right + CropRectangle.HandleTolerance - 1, middle), viewport, 1));
        Assert.NotEqual(CropHandle.Right, crop.HitTest(P(right + CropRectangle.HandleTolerance + 1, middle), viewport, 1));
    }

    [Fact]
    public void On_a_rectangle_small_on_screen_the_nearer_handle_wins()
    {
        // 4 x 4 image pixels at 100%: (310, 260) to (314, 264), smaller than two tolerances.
        var crop = CropRectangle.Of(new PixelRect(10, 10, 4, 4), 200, 100);

        Assert.Equal(CropHandle.TopLeft, crop.HitTest(P(311, 261), AtActualSize, 1));
        Assert.Equal(CropHandle.BottomRight, crop.HitTest(P(313, 263), AtActualSize, 1));
    }

    [Fact]
    public void Without_a_crop_nothing_is_hit()
    {
        Assert.Equal(CropHandle.None, CropRectangle.Of(null, 200, 100).HitTest(P(330, 275), AtActualSize, 1));
    }

    public static TheoryData<CropHandle, PixelRect> HandleDrags => new()
    {
        // Each drag goes 5 pixels right and 3 down from (10, 10, 40, 30), whose right edge is 50 and bottom 40.
        { CropHandle.TopLeft, new PixelRect(15, 13, 35, 27) },
        { CropHandle.TopRight, new PixelRect(10, 13, 45, 27) },
        { CropHandle.BottomRight, new PixelRect(10, 10, 45, 33) },
        { CropHandle.BottomLeft, new PixelRect(15, 10, 35, 33) },
        { CropHandle.Left, new PixelRect(15, 10, 35, 30) },
        { CropHandle.Top, new PixelRect(10, 13, 40, 27) },
        { CropHandle.Right, new PixelRect(10, 10, 45, 30) },
        { CropHandle.Bottom, new PixelRect(10, 10, 40, 33) },
        { CropHandle.Inside, new PixelRect(15, 13, 40, 30) },
    };

    [Theory]
    [MemberData(nameof(HandleDrags))]
    public void Dragging_a_handle_moves_only_its_own_edges(CropHandle handle, PixelRect expected)
    {
        var (dragged, _) = Cropped.Drag(handle, P(330, 275), P(335, 278), AtActualSize, scaling: 1);

        Assert.Equal(expected, dragged.Rectangle);
    }

    [Fact]
    public void A_handle_moves_by_whole_image_pixels_when_zoomed_in()
    {
        // At 400% and scaling 2, 9.6 device-independent units are 19.2 physical pixels, 4.8 image pixels, so 5.
        var zoomed = AtActualSize.ZoomAt(4, 400, 300);

        var (dragged, _) = Cropped.Drag(CropHandle.Right, P(100, 100), P(109.6, 100), zoomed, scaling: 2);

        Assert.Equal(new PixelRect(10, 10, 45, 30), dragged.Rectangle);
    }

    [Fact]
    public void A_handle_stops_at_the_image_border()
    {
        var (dragged, _) = Cropped.Drag(CropHandle.TopLeft, P(310, 260), P(100, 100), AtActualSize, scaling: 1);

        Assert.Equal(new PixelRect(0, 0, 50, 40), dragged.Rectangle);
    }

    [Theory]
    [InlineData(-500, -500, 0, 0)]
    [InlineData(500, 500, 160, 70)]
    [InlineData(500, -3, 160, 7)]
    public void Moving_the_rectangle_keeps_its_size_and_stops_at_the_image_border(double dx, double dy, int x, int y)
    {
        var (moved, _) = Cropped.Drag(CropHandle.Inside, P(330, 275), P(330 + dx, 275 + dy), AtActualSize, scaling: 1);

        Assert.Equal(new PixelRect(x, y, 40, 30), moved.Rectangle);
    }

    [Theory]
    [InlineData(CropHandle.Left, 70, 0, CropHandle.Right, 50, 10, 30, 30)] // left edge from 10 to 80
    [InlineData(CropHandle.Right, -50, 0, CropHandle.Left, 0, 10, 10, 30)] // right edge from 50 to 0
    [InlineData(CropHandle.Top, 0, 40, CropHandle.Bottom, 10, 40, 40, 10)] // top edge from 10 to 50
    [InlineData(CropHandle.Bottom, 0, -35, CropHandle.Top, 10, 5, 40, 5)] // bottom edge from 40 to 5
    [InlineData(CropHandle.TopLeft, 70, 40, CropHandle.BottomRight, 50, 40, 30, 10)]
    [InlineData(CropHandle.BottomRight, -45, 0, CropHandle.BottomLeft, 5, 10, 5, 30)]
    public void Dragging_an_edge_past_its_opposite_flips_the_rectangle(
        CropHandle handle, double dx, double dy, CropHandle flipped, int x, int y, int width, int height)
    {
        var (dragged, handleAfter) = Cropped.Drag(handle, P(330, 275), P(330 + dx, 275 + dy), AtActualSize, scaling: 1);

        Assert.Equal(new PixelRect(x, y, width, height), dragged.Rectangle);
        Assert.Equal(flipped, handleAfter);
    }

    [Theory]
    [InlineData(CropHandle.Left, 40, 49, 1)] // left edge onto the right edge at 50: one pixel left of it
    [InlineData(CropHandle.Right, -40, 10, 1)] // right edge onto the left edge at 10: one pixel right of it
    public void An_edge_dragged_onto_its_opposite_leaves_one_pixel(CropHandle handle, double dx, int x, int width)
    {
        var (dragged, handleAfter) = Cropped.Drag(handle, P(330, 275), P(330 + dx, 275), AtActualSize, scaling: 1);

        Assert.Equal(new PixelRect(x, 10, width, 30), dragged.Rectangle);
        Assert.Equal(handle, handleAfter);
    }

    [Fact]
    public void A_one_pixel_rectangle_at_the_border_stays_one_pixel_when_its_edge_is_dragged_onto_the_other()
    {
        var crop = CropRectangle.Of(new PixelRect(0, 0, 1, 1), 200, 100);

        var (dragged, _) = crop.Drag(CropHandle.TopLeft, P(300, 250), P(301, 251), AtActualSize, scaling: 1);

        Assert.Equal(new PixelRect(0, 0, 1, 1), dragged.Rectangle);
    }

    [Fact]
    public void A_drag_that_ends_where_it_started_leaves_the_crop_unchanged()
    {
        var (dragged, handle) = Cropped.Drag(CropHandle.Right, P(350, 275), P(350.4, 275), AtActualSize, scaling: 1);

        Assert.Equal(Cropped, dragged);
        Assert.Equal(CropHandle.Right, handle);
    }

    public static TheoryData<CropHandle, int, int, PixelRect> Nudges => new()
    {
        // From (10, 10, 40, 30): left 10, top 10, right 50, bottom 40.
        { CropHandle.Left, -1, 0, new PixelRect(9, 10, 41, 30) },
        { CropHandle.Left, 0, 1, new PixelRect(10, 10, 40, 30) }, // an edge ignores the other axis
        { CropHandle.Right, 1, 0, new PixelRect(10, 10, 41, 30) },
        { CropHandle.Top, 0, -1, new PixelRect(10, 9, 40, 31) },
        { CropHandle.Bottom, 0, 1, new PixelRect(10, 10, 40, 31) },
        { CropHandle.Bottom, -1, 0, new PixelRect(10, 10, 40, 30) },
        { CropHandle.TopLeft, 1, 0, new PixelRect(11, 10, 39, 30) },
        { CropHandle.TopLeft, 0, 1, new PixelRect(10, 11, 40, 29) },
        { CropHandle.TopRight, 0, -1, new PixelRect(10, 9, 40, 31) },
        { CropHandle.BottomRight, 1, 0, new PixelRect(10, 10, 41, 30) },
        { CropHandle.BottomLeft, 0, 1, new PixelRect(10, 10, 40, 31) },
        { CropHandle.Inside, 1, 0, new PixelRect(11, 10, 40, 30) },
        { CropHandle.Inside, 0, -1, new PixelRect(10, 9, 40, 30) },
    };

    [Theory]
    [MemberData(nameof(Nudges))]
    public void Nudging_moves_the_selected_handle_by_one_image_pixel(CropHandle handle, int dx, int dy, PixelRect expected)
    {
        Assert.Equal(expected, Cropped.Nudge(handle, dx, dy).Rectangle);
    }

    [Theory]
    [InlineData(CropHandle.Left, -1, 0)]
    [InlineData(CropHandle.Top, 0, -1)]
    [InlineData(CropHandle.Right, 1, 0)]
    [InlineData(CropHandle.Bottom, 0, 1)]
    [InlineData(CropHandle.TopLeft, -1, -1)]
    [InlineData(CropHandle.BottomRight, 1, 1)]
    [InlineData(CropHandle.Inside, -1, 0)]
    [InlineData(CropHandle.Inside, 1, 0)]
    [InlineData(CropHandle.Inside, 0, -1)]
    [InlineData(CropHandle.Inside, 0, 1)]
    public void Nudging_stops_at_the_image_border(CropHandle handle, int dx, int dy)
    {
        var whole = CropRectangle.Of(new PixelRect(0, 0, 200, 100), 200, 100);

        Assert.Equal(new PixelRect(0, 0, 200, 100), whole.Nudge(handle, dx, dy).Rectangle);
    }

    [Fact]
    public void Nudging_an_edge_onto_its_opposite_stops_at_one_pixel()
    {
        var thin = CropRectangle.Of(new PixelRect(10, 10, 1, 30), 200, 100);

        Assert.Equal(new PixelRect(10, 10, 1, 30), thin.Nudge(CropHandle.Left, 1, 0).Rectangle);
        Assert.Equal(new PixelRect(10, 10, 1, 30), thin.Nudge(CropHandle.Right, -1, 0).Rectangle);
    }

    // A detected crop: the window (10, 10, 40, 30) with a popup sticking out past its right edge.
    private static readonly Crop WithOverhang = Crop.WithPopups(new PixelRect(10, 10, 40, 30), new PixelRect(45, 20, 20, 10));

    [Fact]
    public void A_crop_with_popup_overhang_is_kept_until_it_is_edited()
    {
        var editing = CropRectangle.Editing(WithOverhang, 200, 100);

        Assert.Equal(new PixelRect(10, 10, 40, 30), editing.Rectangle);
        Assert.Equal(WithOverhang, editing.Crop);
    }

    public static TheoryData<string> Edits => ["drag a handle", "move", "nudge", "draw"];

    [Theory]
    [MemberData(nameof(Edits))]
    public void Editing_a_crop_with_popup_overhang_makes_it_a_Classic_crop(string edit)
    {
        var editing = CropRectangle.Editing(WithOverhang, 200, 100);

        var edited = edit switch
        {
            "drag a handle" => editing.Drag(CropHandle.Right, P(350, 275), P(355, 275), AtActualSize, 1).Crop,
            "move" => editing.Drag(CropHandle.Inside, P(330, 275), P(331, 276), AtActualSize, 1).Crop,
            "nudge" => editing.Nudge(CropHandle.Top, 0, -1),
            _ => editing.Draw(P(400, 300), P(450, 340), AtActualSize, 1),
        };

        Assert.Equal(Crop.Classic(edited.Rectangle!.Value), edited.Crop);
    }

    [Fact]
    public void An_edit_that_changes_nothing_keeps_the_popup_overhang()
    {
        var atBorder = Crop.WithPopups(new PixelRect(0, 10, 40, 30), new PixelRect(35, 20, 20, 10));

        var nudged = CropRectangle.Editing(atBorder, 200, 100).Nudge(CropHandle.Left, -1, 0);

        Assert.Equal(atBorder, nudged.Crop);
    }

    [Fact]
    public void A_rectangle_edited_back_where_it_was_stays_a_Classic_crop()
    {
        var editing = CropRectangle.Editing(WithOverhang, 200, 100);

        var edited = editing.Nudge(CropHandle.Inside, 1, 0).Nudge(CropHandle.Inside, -1, 0);

        Assert.Equal(Crop.Classic(new PixelRect(10, 10, 40, 30)), edited.Crop);
    }

    [Fact]
    public void No_crop_is_edited_as_no_rectangle()
    {
        var editing = CropRectangle.Editing(null, 200, 100);

        Assert.Null(editing.Rectangle);
        Assert.Null(editing.Crop);
    }
}
