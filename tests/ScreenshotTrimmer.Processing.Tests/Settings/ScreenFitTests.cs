using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Tests.Settings;

public class ScreenFitTests
{
    // A 1920×1080 screen with a 40 px taskbar at the bottom.
    private static readonly PixelRect Laptop = new(0, 0, 1920, 1040);

    // A second screen to the right of it.
    private static readonly PixelRect RightMonitor = new(1920, 0, 2560, 1400);

    [Fact]
    public void A_window_on_a_screen_stays_where_it_was()
    {
        var window = new PixelRect(200, 150, 1100, 750);

        Assert.Equal(window, ScreenFit.Place(window, [Laptop]));
    }

    [Fact]
    public void A_window_partly_off_a_screen_stays_while_its_title_bar_can_still_be_grabbed()
    {
        var window = new PixelRect(1500, 900, 1100, 750);

        Assert.Equal(window, ScreenFit.Place(window, [Laptop]));
    }

    [Fact]
    public void A_window_on_a_screen_that_is_gone_moves_onto_the_remaining_one()
    {
        var window = new PixelRect(2500, 300, 1100, 750);

        Assert.Equal(new PixelRect(820, 290, 1100, 750), ScreenFit.Place(window, [Laptop]));
    }

    [Fact]
    public void A_window_whose_title_bar_is_above_every_screen_moves_down_onto_one()
    {
        var window = new PixelRect(200, -700, 1100, 750);

        Assert.Equal(new PixelRect(200, 0, 1100, 750), ScreenFit.Place(window, [Laptop]));
    }

    [Fact]
    public void A_window_whose_title_bar_is_just_off_the_left_edge_moves_right_onto_the_screen()
    {
        var window = new PixelRect(-1050, 100, 1100, 750);

        Assert.Equal(new PixelRect(0, 100, 1100, 750), ScreenFit.Place(window, [Laptop]));
    }

    [Fact]
    public void A_window_off_every_screen_moves_onto_the_nearest_one()
    {
        var window = new PixelRect(5000, 200, 1100, 750);

        Assert.Equal(new PixelRect(3380, 200, 1100, 750), ScreenFit.Place(window, [Laptop, RightMonitor]));
    }

    [Fact]
    public void A_window_moved_onto_a_smaller_screen_shrinks_to_fit_it()
    {
        var window = new PixelRect(5000, 100, 2400, 1300);

        Assert.Equal(new PixelRect(0, 0, 1920, 1040), ScreenFit.Place(window, [Laptop]));
    }

    [Fact]
    public void With_no_screens_known_the_window_is_left_as_it_was()
    {
        var window = new PixelRect(5000, 200, 1100, 750);

        Assert.Equal(window, ScreenFit.Place(window, []));
    }
}
