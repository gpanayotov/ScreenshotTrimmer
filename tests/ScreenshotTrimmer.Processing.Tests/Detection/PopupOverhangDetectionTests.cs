using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Tests.Detection;

/// <summary>Auto crop keeps an open menu or popup whole: its <b>Popup overhang</b> is part of the Subject.</summary>
public class PopupOverhangDetectionTests
{
    private static readonly PixelRect UndimmedWindow = new(141, 87, 527, 357);

    [Theory]
    [InlineData(300, 380, 180, 160)] // off the bottom
    [InlineData(90, 200, 140, 120)] // off the left
    [InlineData(560, 250, 200, 140)] // off the right
    [InlineData(300, 40, 180, 120)] // off the top
    public void A_popup_overhanging_an_undimmed_window_is_found_with_it(int x, int y, int width, int height)
    {
        var image = UndimmedWindowOnWallpaper();
        var popup = new PixelRect(x, y, width, height);
        DrawUndimmedPopup(image, popup);

        var subject = SubjectDetection.Find(image).Subject;

        Assert.NotNull(subject);
        Assert.Equal(UndimmedWindow, subject.Window);
        Assert.Equal([popup], subject.Popups);
    }

    private static readonly PixelRect DimmedDialog = new(201, 113, 397, 271);

    [Theory]
    [InlineData(217, 333, 365, 120)] // a dropdown off the bottom, inset from the dialog's sides
    [InlineData(150, 200, 120, 130)] // off the left
    [InlineData(520, 170, 150, 140)] // off the right
    [InlineData(330, 60, 160, 110)] // off the top
    public void A_popup_overhanging_a_dimmed_dialog_is_found_with_it(int x, int y, int width, int height)
    {
        var image = DimmedDialogOverPage();
        var popup = new PixelRect(x, y, width, height);
        DrawDimmedPopup(image, popup);

        var subject = SubjectDetection.Find(image).Subject;

        Assert.NotNull(subject);
        Assert.Equal(DimmedDialog, subject.Window);
        Assert.Equal([popup], subject.Popups);
    }

    [Fact]
    public void A_menu_and_its_submenu_overhanging_an_undimmed_window_are_both_found()
    {
        var image = UndimmedWindowOnWallpaper();
        // A menu dropping below the window, and a submenu opened beside it that reaches past the window's right side.
        var menu = new PixelRect(420, 300, 170, 190);
        var submenu = new PixelRect(580, 390, 150, 120);
        DrawUndimmedPopup(image, menu);
        DrawUndimmedPopup(image, submenu);

        var subject = SubjectDetection.Find(image).Subject;

        Assert.NotNull(subject);
        Assert.Equal(UndimmedWindow, subject.Window);
        Assert.Equal([menu, submenu], subject.Popups);
    }

    [Fact]
    public void A_menu_and_its_submenu_overhanging_a_dimmed_dialog_are_both_found()
    {
        var image = DimmedDialogOverPage();
        // A dropdown off the bottom, and a submenu beside it lying wholly below the dialog: it overlaps only the menu.
        var menu = new PixelRect(260, 300, 180, 140);
        var submenu = new PixelRect(430, 400, 150, 90);
        DrawDimmedPopup(image, menu);
        DrawDimmedPopup(image, submenu);

        var subject = SubjectDetection.Find(image).Subject;

        Assert.NotNull(subject);
        Assert.Equal(DimmedDialog, subject.Window);
        Assert.Equal([menu, submenu], subject.Popups);
    }

    [Fact]
    public void A_found_popup_renders_with_the_area_outside_the_window_and_popup_transparent()
    {
        var image = UndimmedWindowOnWallpaper();
        var popup = new PixelRect(300, 380, 180, 160);
        DrawUndimmedPopup(image, popup);

        var crop = SubjectDetection.Find(image).Subject!.ToCrop();
        var result = Renderer.Render(image, new Steps { Crop = crop }).Result;

        // The Result spans the window and the popup below it: 141..668 x 87..540.
        Assert.Equal(527, result.Width);
        Assert.Equal(453, result.Height);
        for (var y = 0; y < result.Height; y++)
            for (var x = 0; x < result.Width; x++)
            {
                var (sx, sy) = (141 + x, 87 + y);
                var kept = Inside(UndimmedWindow, sx, sy) || Inside(popup, sx, sy);
                Assert.Equal(kept ? image[sx, sy] : default, result[x, y]);
            }
    }

    private static bool Inside(PixelRect rectangle, int x, int y) =>
        x >= rectangle.X && x < rectangle.Right && y >= rectangle.Y && y < rectangle.Bottom;

    private static RgbaImage DimmedDialogOverPage()
    {
        var image = Synthetic.DimmedApplication(800, 500);
        Synthetic.DrawDialog(image, DimmedDialog, 8);
        return image;
    }

    /// <summary>A white menu with its own shadow, open above a dialog over a modal backdrop.</summary>
    private static void DrawDimmedPopup(RgbaImage image, PixelRect popup)
    {
        Synthetic.DrawShadow(image, popup, 4, 120, 4);
        Synthetic.DrawDialog(image, popup, 4, new Rgba(255, 255, 255, 255));
    }

    private static RgbaImage UndimmedWindowOnWallpaper()
    {
        var image = Synthetic.TexturedWallpaper(960, 640);
        Synthetic.DrawShadow(image, UndimmedWindow, 6, 140, 8);
        Synthetic.DrawWindow(image, UndimmedWindow, new Rgba(40, 50, 60, 255), new Rgba(250, 250, 250, 255), 8);
        return image;
    }

    /// <summary>A menu a little lighter than the window, with its own shadow, as an open menu looks.</summary>
    private static void DrawUndimmedPopup(RgbaImage image, PixelRect popup)
    {
        Synthetic.DrawShadow(image, popup, 4, 120, 4);
        Synthetic.DrawDialog(image, popup, 4, new Rgba(255, 255, 255, 255));
    }
}
