using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Tests.Rendering;

public class PopupOverhangTests
{
    private static Rgba At(int x, int y) => new((byte)x, (byte)y, 128, 255);

    [Fact]
    public void A_popup_overhanging_the_bottom_is_kept_and_the_pixels_beside_it_are_transparent()
    {
        // Window 20..120 x 10..70; a menu 30..60 x 50..100 drops 30px below it.
        var original = Synthetic.Gradient(200, 150);
        var crop = Crop.WithPopups(new PixelRect(20, 10, 100, 60), new PixelRect(30, 50, 30, 50));

        var result = Renderer.Render(original, new Steps { Crop = crop }).Result;

        Assert.Equal(100, result.Width);
        Assert.Equal(90, result.Height);
        Assert.Equal(At(20, 10), result[0, 0]);
        Assert.Equal(At(119, 69), result[99, 59]);
        Assert.Equal(At(30, 99), result[10, 89]);
        Assert.Equal(At(59, 70), result[39, 60]);
        Assert.Equal(default, result[0, 60]);
        Assert.Equal(default, result[9, 89]);
        Assert.Equal(default, result[40, 60]);
        Assert.Equal(default, result[99, 89]);
    }

    [Theory]
    // Window 50..150 x 40..100 in every case; the popup and the expected bounding rectangle vary.
    [InlineData(20, 50, 40, 30, 20, 40, 130, 60)] // overhangs the left
    [InlineData(60, 15, 40, 30, 50, 15, 100, 85)] // overhangs the top
    [InlineData(130, 50, 50, 30, 50, 40, 130, 60)] // overhangs the right
    [InlineData(60, 80, 40, 50, 50, 40, 100, 90)] // overhangs the bottom
    public void A_popup_overhanging_any_side_widens_the_Result_to_the_bounding_rectangle(
        int px, int py, int pw, int ph, int bx, int by, int bw, int bh)
    {
        var original = Synthetic.Gradient(200, 150);
        var window = new PixelRect(50, 40, 100, 60);
        var popup = new PixelRect(px, py, pw, ph);

        var result = Renderer.Render(original, new Steps { Crop = Crop.WithPopups(window, popup) }).Result;

        Assert.Equal(bw, result.Width);
        Assert.Equal(bh, result.Height);
        for (var y = 0; y < bh; y++)
            for (var x = 0; x < bw; x++)
            {
                var (sx, sy) = (bx + x, by + y);
                var kept = Inside(window, sx, sy) || Inside(popup, sx, sy);
                Assert.Equal(kept ? At(sx, sy) : default, result[x, y]);
            }
    }

    [Fact]
    public void Two_popups_at_once_are_both_kept()
    {
        // A menu dropping below the window and a submenu opening to its right, past the window's right edge.
        var original = Synthetic.Gradient(200, 150);
        var window = new PixelRect(20, 10, 100, 60);
        var menu = new PixelRect(30, 50, 40, 60);
        var submenu = new PixelRect(70, 80, 70, 40);

        var result = Renderer.Render(original, new Steps { Crop = Crop.WithPopups(window, menu, submenu) }).Result;

        Assert.Equal(120, result.Width);
        Assert.Equal(110, result.Height);
        Assert.Equal(At(30, 109), result[10, 99]);
        Assert.Equal(At(139, 119), result[119, 109]);
        Assert.Equal(default, result[119, 0]);
        Assert.Equal(default, result[0, 109]);
        Assert.Equal(default, result[105, 65]);
    }

    [Fact]
    public void Popups_are_clipped_to_the_image_like_the_window()
    {
        var original = Synthetic.Gradient(200, 150);
        var crop = Crop.WithPopups(new PixelRect(20, 10, 100, 60), new PixelRect(100, 50, 200, 30), new PixelRect(300, 300, 10, 10));

        var result = Renderer.Render(original, new Steps { Crop = crop }).Result;

        Assert.Equal(180, result.Width);
        Assert.Equal(70, result.Height);
        Assert.Equal(At(199, 79), result[179, 69]);
    }

    [Fact]
    public void A_Classic_crop_is_a_crop_with_no_popups()
    {
        var rectangle = new PixelRect(5, 7, 20, 10);

        Assert.Equal(Crop.WithPopups(rectangle), Crop.Classic(rectangle));
        Assert.Empty(Crop.Classic(rectangle).Popups);
    }

    private static readonly Rgba Menu = new(250, 250, 255, 255);

    /// <summary>
    /// A window of busy content at 20..80 x 20..60 with background around it, cropped 3px too wide
    /// on every side, and a menu at 35..65 x 55..95 that drops below the window.
    /// </summary>
    private static (RgbaImage Original, Crop Crop) SloppyWindowWithMenuBelow()
    {
        var original = Synthetic.WindowWithStrips(60, 40, left: 20, top: 20, right: 20, bottom: 60);
        for (var y = 55; y < 95; y++)
            for (var x = 35; x < 65; x++)
                original[x, y] = Menu;
        return (original, Crop.WithPopups(new PixelRect(17, 17, 66, 46), new PixelRect(35, 55, 30, 40)));
    }

    [Fact]
    public void No_frame_lines_are_removed_on_the_side_with_an_overhang()
    {
        var (original, crop) = SloppyWindowWithMenuBelow();

        var output = Renderer.Render(original, new Steps { Crop = crop, FrameLines = true });

        // The 3px strips on the left, top and right go; the bottom keeps its strip and the menu below it.
        Assert.Equal(new FrameLinesRemoved(3, 3, 3, 0), output.Report.FrameLinesRemoved);
        var result = output.Result;
        Assert.Equal(60, result.Width);
        Assert.Equal(75, result.Height);
        Assert.Equal(Synthetic.CheckerAt(0, 0), result[0, 0]);
        Assert.Equal(Synthetic.CheckerAt(59, 39), result[59, 39]);
        Assert.Equal(Synthetic.Background, result[0, 42]);
        Assert.Equal(Menu, result[15, 74]);
        Assert.Equal(default, result[0, 74]);
    }

    [Fact]
    public void No_frame_lines_are_removed_on_a_side_a_popup_reaches_flush()
    {
        // The window of SloppyWindowWithMenuBelow, with a narrow menu at 45..50 x 40..63 whose bottom edge
        // is flush with the crop's bottom edge: removing the bottom strip would leave the menu sticking out.
        var original = Synthetic.WindowWithStrips(60, 40, left: 20, top: 20, right: 20, bottom: 60);
        for (var y = 40; y < 63; y++)
            for (var x = 45; x < 50; x++)
                original[x, y] = Menu;
        var crop = Crop.WithPopups(new PixelRect(17, 17, 66, 46), new PixelRect(45, 40, 5, 23));

        var output = Renderer.Render(original, new Steps { Crop = crop, FrameLines = true });

        Assert.Equal(new FrameLinesRemoved(3, 3, 3, 0), output.Report.FrameLinesRemoved);
        var result = output.Result;
        Assert.Equal(60, result.Width);
        Assert.Equal(43, result.Height);
        Assert.Equal(Synthetic.Background, result[0, 42]);
        Assert.Equal(Synthetic.Background, result[59, 42]);
        Assert.Equal(Menu, result[27, 42]);
    }

    /// <summary>
    /// A 160x120 window with 12px rounded corners at 40..200 x 40..160 on a page, and a menu at the given
    /// rectangle sticking out past one of its sides.
    /// </summary>
    private static (RgbaImage Original, Crop Crop) RoundedWindowWithMenu(PixelRect menu)
    {
        var window = Synthetic.RoundedWindow(160, 120, 12, 12, 12, 12);
        var original = new RgbaImage(240, 200);
        for (var y = 0; y < 200; y++)
            for (var x = 0; x < 240; x++)
                original[x, y] = Synthetic.Page;
        for (var y = 0; y < 120; y++)
            for (var x = 0; x < 160; x++)
                original[40 + x, 40 + y] = window[x, y];
        for (var y = menu.Y; y < menu.Bottom; y++)
            for (var x = menu.X; x < menu.Right; x++)
                original[x, y] = Menu;
        return (original, Crop.WithPopups(new PixelRect(40, 40, 160, 120), menu));
    }

    public enum Side { Left, Top, Right, Bottom }

    [Theory]
    [InlineData(Side.Left, 10, 70, 40, 40)]
    [InlineData(Side.Top, 80, 10, 40, 40)]
    [InlineData(Side.Right, 190, 70, 40, 40)]
    [InlineData(Side.Bottom, 80, 150, 40, 40)]
    public void The_corners_on_a_side_with_an_overhang_stay_square_but_report_their_measured_radius(Side side, int mx, int my, int mw, int mh)
    {
        var menu = new PixelRect(mx, my, mw, mh);
        var (original, crop) = RoundedWindowWithMenu(menu);

        var output = Renderer.Render(original, new Steps { Crop = crop, RoundedCorners = new RoundedCorners() });

        // The window's corners in the Original, and whether each lies on the overhang side.
        var (ox, oy) = (Math.Min(40, mx), Math.Min(40, my));
        var applied = output.Report.AppliedRadii!;
        var measured = output.Report.MeasuredRadii!;
        var corners = new (double Radius, double Measured, int X, int Y, bool OnOverhang)[]
        {
            (applied.TopLeft, measured.TopLeft, 40, 40, side is Side.Left or Side.Top),
            (applied.TopRight, measured.TopRight, 199, 40, side is Side.Right or Side.Top),
            (applied.BottomRight, measured.BottomRight, 199, 159, side is Side.Right or Side.Bottom),
            (applied.BottomLeft, measured.BottomLeft, 40, 159, side is Side.Left or Side.Bottom),
        };
        foreach (var (radius, measuredRadius, x, y, onOverhang) in corners)
        {
            // Every corner of the window is measured, whether or not it is rounded. (A menu over the window's
            // edge near a corner can nudge the measurement a little.)
            Assert.InRange(measuredRadius, 11, 14);
            var pixel = output.Result[x - ox, y - oy];
            if (onOverhang)
            {
                Assert.Equal(0, radius);
                Assert.Equal(Synthetic.Page, pixel);
            }
            else
            {
                Assert.InRange(radius, 11, 13);
                Assert.Equal(0, pixel.A);
            }
        }
        Assert.Equal(Menu, output.Result[mx - ox + mw / 2, my - oy + mh / 2]);
    }

    [Fact]
    public void The_Shadow_follows_the_combined_outline_of_window_and_popup()
    {
        // Window 40..200 x 40..160, menu 80..120 x 150..190 dropping 30px below it.
        var (original, crop) = RoundedWindowWithMenu(new PixelRect(80, 150, 40, 40));

        var output = Renderer.Render(original, new Steps { Crop = crop, Shadow = new Shadow() });

        // The Result is the 160x150 bounding rectangle plus 14px of padding; Original (x, y) lands at
        // (x - 40 + 14, y - 40 + 14).
        var result = output.Result;
        Assert.Equal(188, result.Width);
        Assert.Equal(178, result.Height);
        static (int, int) Map(int x, int y) => (x - 26, y - 26);

        // Just below the menu and just below the window beside it, the shadow shows as it does
        // below any straight edge (alpha 53, see ShadowTests).
        var (bx, by) = Map(100, 190);
        var belowMenu = result[bx, by];
        Assert.Equal((0, 0, 0), (belowMenu.R, belowMenu.G, belowMenu.B));
        Assert.InRange(belowMenu.A, 50, 56);
        var (wx, wy) = Map(170, 160);
        Assert.InRange(result[wx, wy].A, 50, 56);

        // Further down beside the menu, past the window's shadow, the transparent area stays clear;
        // the shadow reaches that far only below the menu.
        var (cx, cy) = Map(170, 185);
        Assert.Equal(0, result[cx, cy].A);
        var (mx, my) = Map(100, 195);
        Assert.InRange(result[mx, my].A, 1, 50);
    }

    private static bool Inside(PixelRect r, int x, int y) => x >= r.X && x < r.Right && y >= r.Y && y < r.Bottom;
}
