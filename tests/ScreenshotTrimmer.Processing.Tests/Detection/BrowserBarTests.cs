using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests.Detection;

/// <summary>Auto crop of a browser screenshot with no dialog: the Subject is the web page below the Browser bar.</summary>
public class BrowserBarTests
{
    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1366, 768)]
    public void A_maximised_browser_screenshot_is_cropped_to_the_page_below_the_separator(int width, int height)
    {
        var image = BrowserScreenshot(Synthetic.LightApplication(width, height - BarHeight), separator: true);

        var subject = SubjectDetection.Find(image).Subject;

        Assert.Equal(new PixelRect(0, BarHeight, width, height - BarHeight), subject?.Window);
    }

    [Fact]
    public void A_browser_screenshot_with_no_separator_is_cropped_to_the_page_below_the_bar()
    {
        var image = BrowserScreenshot(Synthetic.LightApplication(1280, 635), separator: false);

        var subject = SubjectDetection.Find(image).Subject;

        Assert.Equal(new PixelRect(0, BarHeight, 1280, 635), subject?.Window);
    }

    [Fact]
    public void A_dimmed_dialog_on_the_page_of_a_browser_screenshot_wins_over_the_page()
    {
        var page = Synthetic.DimmedApplication(1280, 635);
        var dialog = new PixelRect(397, 151, 489, 317);
        Synthetic.DrawDialog(page, dialog, 8);
        var image = BrowserScreenshot(page, separator: true);

        var subject = SubjectDetection.Find(image).Subject;

        Assert.Equal(dialog with { Y = dialog.Y + BarHeight }, subject?.Window);
    }

    [Fact]
    public void An_undimmed_dialog_on_the_page_of_a_browser_screenshot_wins_over_the_page()
    {
        var page = Synthetic.LightApplication(1280, 635);
        var dialog = new PixelRect(367, 133, 521, 341);
        Synthetic.DrawShadow(page, dialog, 5, 110, 8);
        Synthetic.DrawDialog(page, dialog, 8);
        var image = BrowserScreenshot(page, separator: true);

        var subject = SubjectDetection.Find(image).Subject;

        Assert.Equal(dialog with { Y = dialog.Y + BarHeight }, subject?.Window);
    }

    [Fact]
    public void A_page_with_a_full_width_toolbar_but_no_address_field_is_left_uncropped()
    {
        // An application's own header and toolbar: full-width edges near the top, with buttons and a search box on the
        // toolbar, but nothing as wide as an address field.
        var image = Synthetic.LightApplication(1280, 720);
        Synthetic.FillRect(image, new PixelRect(0, 48, 1280, 52), Toolbar);
        Synthetic.FillRect(image, new PixelRect(0, 100, 1280, 1), Separator);
        for (var x = 24; x < 600; x += 120)
            Synthetic.FillRect(image, new PixelRect(x, 60, 100, 28), Icon);
        FillRounded(image, new PixelRect(820, 58, 400, 32), 16, FieldBorder);
        FillRounded(image, new PixelRect(821, 59, 398, 30), 15, Field);

        Assert.Equal(Finding.NothingFound, SubjectDetection.Find(image));
    }

    /// <summary>The height of the Browser bar <see cref="BrowserScreenshot"/> draws, its separator line included.</summary>
    private const int BarHeight = 85;

    private static readonly Rgba TabStrip = new(226, 220, 242, 255);
    private static readonly Rgba Toolbar = new(248, 245, 255, 255);
    private static readonly Rgba Field = new(251, 249, 254, 255);
    private static readonly Rgba FieldBorder = new(214, 213, 218, 255);
    private static readonly Rgba Separator = new(214, 208, 232, 255);
    private static readonly Rgba Icon = new(70, 70, 80, 255);

    /// <summary>
    /// A maximised browser screenshot showing <paramref name="page"/> below a Browser bar as wide as the page: a tab strip
    /// 40px high with one open tab, then a toolbar with buttons, a rounded address field with a 1px border and extension
    /// icons, then (with <paramref name="separator"/>) a 1px separator line. Without a separator the toolbar is 1px taller, so the page
    /// starts at <see cref="BarHeight"/> either way.
    /// </summary>
    private static RgbaImage BrowserScreenshot(RgbaImage page, bool separator)
    {
        var width = page.Width;
        var image = new RgbaImage(width, page.Height + BarHeight);
        Synthetic.FillRect(image, new PixelRect(0, 0, width, 40), TabStrip);
        FillRounded(image, new PixelRect(8, 6, 220, 32), 8, Toolbar);
        Synthetic.FillRect(image, new PixelRect(40, 16, 120, 10), Icon);
        Synthetic.FillRect(image, new PixelRect(width - 40, 14, 14, 14), Icon);
        Synthetic.FillRect(image, new PixelRect(0, 40, width, BarHeight - 40), Toolbar);
        for (var x = 16; x < 160; x += 36)
            Synthetic.FillRect(image, new PixelRect(x, 56, 16, 16), Icon);
        var field = new PixelRect(240, 48, width * 55 / 100, 32);
        FillRounded(image, field, 16, FieldBorder);
        FillRounded(image, new PixelRect(field.X + 1, field.Y + 1, field.Width - 2, field.Height - 2), 15, Field);
        Synthetic.FillRect(image, new PixelRect(field.X + 40, 59, 260, 10), Icon);
        for (var x = width - 220; x < width - 20; x += 36)
            Synthetic.FillRect(image, new PixelRect(x, 56, 16, 16), Icon);
        if (separator)
            Synthetic.FillRect(image, new PixelRect(0, BarHeight - 1, width, 1), Separator);
        for (var y = 0; y < page.Height; y++)
            for (var x = 0; x < width; x++)
                image[x, BarHeight + y] = page[x, y];
        return image;
    }

    /// <summary>Fills <paramref name="bounds"/> with corners rounded to <paramref name="radius"/>, without antialiasing.</summary>
    private static void FillRounded(RgbaImage image, PixelRect bounds, int radius, Rgba colour)
    {
        for (var y = bounds.Y; y < bounds.Bottom; y++)
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                // Distance, in doubled pixel units, from the pixel's centre to the nearest corner circle's centre.
                var dx = Math.Max(0, Math.Max(2 * (bounds.X + radius) - (2 * x + 1), 2 * x + 1 - 2 * (bounds.Right - radius)));
                var dy = Math.Max(0, Math.Max(2 * (bounds.Y + radius) - (2 * y + 1), 2 * y + 1 - 2 * (bounds.Bottom - radius)));
                if (dx * dx + dy * dy <= 4 * radius * radius)
                    image[x, y] = colour;
            }
    }
}
