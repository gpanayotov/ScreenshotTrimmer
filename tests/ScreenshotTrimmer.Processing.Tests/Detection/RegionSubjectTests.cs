using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests.Detection;

/// <summary>Auto crop in region: the largest Subject fully inside a rough rectangle the user draws.</summary>
public class RegionSubjectTests
{
    private static readonly Rgba Control = new(222, 224, 228, 255);
    private static readonly Rgba ControlBorder = new(160, 164, 172, 255);
    private static readonly Rgba Ink = new(40, 44, 52, 255);

    /// <summary>A flat control with a 1px border and a line of text, filling <paramref name="bounds"/>.</summary>
    private static void DrawControl(RgbaImage image, PixelRect bounds)
    {
        Synthetic.FillRect(image, bounds, ControlBorder);
        Synthetic.FillRect(image, new PixelRect(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2), Control);
        var text = new PixelRect(bounds.X + 10, bounds.Y + bounds.Height / 2 - 3, Math.Min(50, bounds.Width - 20), 6);
        Synthetic.FillRect(image, text, Ink);
    }

    [Fact]
    public void A_button_among_neighbouring_controls_wins_over_the_frame_of_their_edges()
    {
        // A button 24px high in an 8px gutter, with taller controls left and right and wide ones above and below, whose
        // facing edges form a frame around it.
        var image = Synthetic.Plain(500, 340, Synthetic.LightPage);
        var button = new PixelRect(200, 150, 80, 24);
        DrawControl(image, button);
        DrawControl(image, new PixelRect(60, 110, 132, 110));
        DrawControl(image, new PixelRect(288, 110, 132, 110));
        DrawControl(image, new PixelRect(192, 80, 96, 62));
        DrawControl(image, new PixelRect(192, 182, 96, 60));

        var subject = SubjectDetection.FindInRegion(image, new PixelRect(178, 128, 124, 70));

        Assert.Equal(button, subject?.Window);
    }

    [Fact]
    public void An_open_menu_is_found_with_its_highlighted_item_inside()
    {
        var image = Synthetic.LightApplication(800, 520);
        var menu = new PixelRect(300, 120, 180, 212);
        Synthetic.DrawShadow(image, menu, 4, 120, 4);
        Synthetic.FillRect(image, menu, new Rgba(255, 255, 255, 255));
        // The second item is highlighted, inset 4px from the menu's sides.
        Synthetic.FillRect(image, new PixelRect(menu.X + 4, menu.Y + 34, menu.Width - 8, 28), new Rgba(204, 226, 255, 255));
        for (var item = 0; item < 7; item++)
            Synthetic.FillRect(image, new PixelRect(menu.X + 16, menu.Y + 14 + item * 28, 90 - item * 6, 7), Ink);

        var subject = SubjectDetection.FindInRegion(image, new PixelRect(270, 96, 250, 270));

        Assert.Equal(menu, subject?.Window);
    }

    [Fact]
    public void A_card_in_a_page_of_cards_is_found()
    {
        var image = Synthetic.Plain(1000, 640, Synthetic.LightPage);
        for (var row = 0; row < 2; row++)
            for (var column = 0; column < 3; column++)
            {
                var bounds = new PixelRect(40 + column * 316, 90 + row * 260, 290, 230);
                Synthetic.DrawShadow(image, bounds, 1.2f, 90, 6);
                Synthetic.DrawDialog(image, bounds, 6, new Rgba(255, 255, 255, 255));
            }
        var card = new PixelRect(356, 90, 290, 230);

        var subject = SubjectDetection.FindInRegion(image, new PixelRect(320, 70, 370, 275));

        Assert.Equal(card, subject?.Window);
    }

    [Fact]
    public void A_tile_in_a_grid_of_tiles_wins_over_the_frame_of_its_neighbours()
    {
        // Coloured tiles with a white icon, 4px apart on a dark background.
        Rgba[] colours = [new(0, 120, 215, 255), new(16, 137, 62, 255), new(202, 80, 16, 255), new(135, 100, 184, 255)];
        var image = Synthetic.Plain(540, 420, new Rgba(30, 30, 36, 255));
        for (var row = 0; row < 3; row++)
            for (var column = 0; column < 4; column++)
            {
                var bounds = new PixelRect(20 + column * 124, 20 + row * 124, 120, 120);
                Synthetic.FillRect(image, bounds, colours[(row + column) % colours.Length]);
                Synthetic.FillRect(image, new PixelRect(bounds.X + 40, bounds.Y + 36, 40, 40), new Rgba(255, 255, 255, 255));
            }
        var tile = new PixelRect(144, 144, 120, 120);

        var subject = SubjectDetection.FindInRegion(image, new PixelRect(134, 134, 140, 140));

        Assert.Equal(tile, subject?.Window);
    }

    [Theory]
    [InlineData(300, 200, 200, 120)]
    [InlineData(560, 100, 200, 300)]
    public void Nothing_is_found_in_a_region_with_no_element_inside(int x, int y, int width, int height)
    {
        var image = Synthetic.LightApplication(800, 520);

        Assert.Null(SubjectDetection.FindInRegion(image, new PixelRect(x, y, width, height)));
    }

    [Fact]
    public void A_card_cut_by_the_region_is_not_found()
    {
        var image = Synthetic.LightApplication(800, 520);
        var card = new PixelRect(200, 120, 300, 220);
        Synthetic.DrawShadow(image, card, 1.2f, 90, 6);
        Synthetic.FillRect(image, card, new Rgba(255, 255, 255, 255));

        Assert.Null(SubjectDetection.FindInRegion(image, new PixelRect(180, 100, 200, 260)));
    }

    [Fact]
    public void A_region_drawn_exactly_on_a_buttons_edges_finds_the_button()
    {
        var image = Synthetic.Plain(300, 200, Synthetic.LightPage);
        var button = new PixelRect(110, 80, 72, 22);
        DrawControl(image, button);

        Assert.Equal(button, SubjectDetection.FindInRegion(image, button)?.Window);
    }

    [Fact]
    public void A_region_over_a_whole_full_HD_screenshot_is_answered_within_a_coarse_time_bound()
    {
        // The bound is generous so a slow CI machine does not fail it.
        var image = Synthetic.TexturedWallpaper(1920, 1080);
        var window = new PixelRect(260, 110, 1400, 860);
        Synthetic.DrawShadow(image, window, 6, 140, 8);
        Synthetic.DrawWindow(image, window, new Rgba(40, 50, 60, 255), new Rgba(248, 248, 248, 255), 8);
        SubjectDetection.FindInRegion(Synthetic.Plain(64, 64, Synthetic.LightPage), new PixelRect(0, 0, 64, 64));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var subject = SubjectDetection.FindInRegion(image, new PixelRect(0, 0, 1920, 1080));
        stopwatch.Stop();

        Assert.Equal(window, subject?.Window);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }
}
