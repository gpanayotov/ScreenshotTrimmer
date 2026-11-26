using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests.Detection;

/// <summary>Auto crop of screenshots with no modal backdrop: a dialog over a page, or a window over a wallpaper.</summary>
public class UndimmedSubjectTests
{
    [Theory]
    [InlineData(211, 143, 377, 251, 8)]
    [InlineData(150, 120, 500, 330, 0)]
    public void An_undimmed_dialog_with_a_soft_shadow_over_a_light_page_is_found_to_its_exact_edges(
        int x, int y, int width, int height, float radius)
    {
        var image = Synthetic.LightApplication(800, 520);
        var dialog = new PixelRect(x, y, width, height);
        Synthetic.DrawShadow(image, dialog, 5, 110, radius);
        Synthetic.DrawDialog(image, dialog, radius);

        var subject = SubjectDetection.Find(image);

        Assert.Equal(dialog, subject?.Window);
    }

    [Fact]
    public void Nothing_is_found_in_a_light_page_under_a_dark_header_with_no_dialog()
    {
        Assert.Null(SubjectDetection.Find(Synthetic.LightApplication(800, 520)));
    }

    [Theory]
    [InlineData(40, 50, 60, 248, 248, 248, 10)]
    [InlineData(230, 230, 232, 252, 252, 252, 0)]
    public void A_window_on_a_textured_wallpaper_is_found_with_its_title_bar(
        byte titleR, byte titleG, byte titleB, byte bodyR, byte bodyG, byte bodyB, float radius)
    {
        var image = Synthetic.TexturedWallpaper(960, 640);
        var window = new PixelRect(173, 91, 541, 397);
        Synthetic.DrawShadow(image, window, 6, 140, radius);
        Synthetic.DrawWindow(image, window, new Rgba(titleR, titleG, titleB, 255), new Rgba(bodyR, bodyG, bodyB, 255), radius);

        var subject = SubjectDetection.Find(image);

        Assert.Equal(window, subject?.Window);
    }

    [Fact]
    public void A_window_that_differs_from_the_wallpaper_only_in_hue_is_found()
    {
        // Equally bright: the wallpaper is blue, the window's title bar a greener blue and its body a muted orange. The
        // faint shadow darkens the wallpaper beside the window by less than an edge's step.
        var wallpaper = new Rgba(60, 100, 180, 255);
        var image = Synthetic.Plain(900, 600, wallpaper);
        var window = new PixelRect(187, 113, 463, 331);
        Synthetic.DrawShadow(image, window, 6, 70, 8);
        Synthetic.DrawWindow(image, window, new Rgba(40, 112, 140, 255), new Rgba(122, 84, 56, 255), 8);

        var subject = SubjectDetection.Find(image);

        Assert.Equal(window, subject?.Window);
    }

    [Fact]
    public void Nothing_is_found_in_a_page_of_cards_with_short_shadows()
    {
        var image = Synthetic.LightApplication(1000, 640);
        for (var row = 0; row < 2; row++)
            for (var column = 0; column < 3; column++)
            {
                var card = new PixelRect(40 + column * 316, 90 + row * 260, 290, 230);
                Synthetic.DrawShadow(image, card, 1.2f, 90, 6);
                Synthetic.DrawDialog(image, card, 6, new Rgba(255, 255, 255, 255));
            }

        Assert.Null(SubjectDetection.Find(image));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(1, 8)]
    [InlineData(2, 8)]
    public void A_crisp_shadow_line_beside_an_edge_does_not_move_the_edge(int lineWidth, float radius)
    {
        var image = Synthetic.LightApplication(800, 520);
        var dialog = new PixelRect(203, 131, 389, 263);
        Synthetic.DrawShadow(image, dialog, 5, 110, radius);
        Synthetic.DrawOutline(image, dialog, lineWidth, new Rgba(186, 186, 189, 255), radius);
        Synthetic.DrawDialog(image, dialog, radius);

        var subject = SubjectDetection.Find(image);

        Assert.Equal(dialog, subject?.Window);
    }

    [Theory]
    [InlineData(300, 380, 180, 160, 250)]
    [InlineData(120, 300, 200, 120, 250)]
    [InlineData(560, 250, 200, 140, 250)]
    [InlineData(300, 380, 180, 160, 180)]
    [InlineData(120, 300, 200, 120, 180)]
    [InlineData(560, 250, 200, 140, 180)]
    public void A_window_partly_hidden_under_a_menu_lying_on_top_is_still_found(
        int x, int y, int width, int height, byte windowLevel)
    {
        var image = Synthetic.TexturedWallpaper(960, 640);
        var window = new PixelRect(141, 87, 527, 357);
        var body = new Rgba(windowLevel, windowLevel, windowLevel, 255);
        Synthetic.DrawShadow(image, window, 6, 140, 8);
        Synthetic.DrawWindow(image, window, new Rgba(40, 50, 60, 255), body, 8);
        // A menu opened from the window, sticking out past one of its sides, a little lighter than the window.
        var menu = new PixelRect(x, y, width, height);
        var menuLevel = (byte)(windowLevel + 5);
        Synthetic.DrawShadow(image, menu, 4, 120, 4);
        Synthetic.DrawDialog(image, menu, 4, new Rgba(menuLevel, menuLevel, menuLevel, 255));

        var subject = SubjectDetection.Find(image);

        Assert.Equal(window, subject?.Window);
    }

    [Fact]
    public void A_full_HD_screenshot_is_answered_within_a_coarse_time_bound()
    {
        // The bound is generous so a slow CI machine does not fail it; on a desktop this takes well under a second.
        var image = Synthetic.TexturedWallpaper(1920, 1080);
        var window = new PixelRect(260, 110, 1400, 860);
        Synthetic.DrawShadow(image, window, 6, 140, 8);
        Synthetic.DrawWindow(image, window, new Rgba(40, 50, 60, 255), new Rgba(248, 248, 248, 255), 8);
        for (var row = 0; row < 3; row++)
            for (var column = 0; column < 4; column++)
                Synthetic.DrawDialog(image, new PixelRect(330 + column * 320, 180 + row * 250, 290, 220), 6, new Rgba(236, 238, 242, 255));
        SubjectDetection.Find(Synthetic.Plain(64, 64, Synthetic.LightPage));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var subject = SubjectDetection.Find(image);
        stopwatch.Stop();

        Assert.Equal(window, subject?.Window);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }
}
