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

    private static bool Inside(PixelRect r, int x, int y) => x >= r.X && x < r.Right && y >= r.Y && y < r.Bottom;
}
