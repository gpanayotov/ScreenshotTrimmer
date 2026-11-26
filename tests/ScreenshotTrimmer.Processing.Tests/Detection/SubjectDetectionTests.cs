using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Tests.Detection;

public class SubjectDetectionTests
{
    [Theory]
    [InlineData(137, 93, 401, 257, 8)]
    [InlineData(200, 120, 360, 300, 12)]
    [InlineData(64, 64, 512, 256, 0)]
    public void A_light_dialog_over_a_dimmed_page_is_found_to_its_exact_edges(int x, int y, int width, int height, float radius)
    {
        var image = Synthetic.DimmedApplication(800, 500);
        var dialog = new PixelRect(x, y, width, height);
        Synthetic.DrawDialog(image, dialog, radius);

        var subject = SubjectDetection.Find(image).Subject;

        Assert.NotNull(subject);
        Assert.Equal(dialog, subject.Window);
        Assert.Empty(subject.Popups);
    }

    [Fact]
    public void Of_stacked_dialogs_the_topmost_is_found()
    {
        // A large dialog behind, dimmed by the backdrop of a smaller dialog opened on top of it.
        var image = Synthetic.Plain(900, 600, Synthetic.TwiceDimmedPage);
        Synthetic.DrawDialog(image, new PixelRect(100, 60, 700, 480), 10, Synthetic.DimmedDialog);
        var topmost = new PixelRect(311, 187, 283, 205);
        Synthetic.DrawDialog(image, topmost, 10);

        var subject = SubjectDetection.Find(image).Subject;

        Assert.Equal(topmost, subject?.Window);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(3)]
    public void An_image_already_cropped_to_its_light_dialog_is_found_already_cropped(float radius)
    {
        var image = Synthetic.Plain(420, 300, Synthetic.DimmedPage);
        Synthetic.DrawDialog(image, new PixelRect(0, 0, 420, 300), radius);

        Assert.Equal(Finding.AlreadyCropped, SubjectDetection.Find(image));
    }

    [Fact]
    public void An_image_already_cropped_to_its_dark_dialog_is_found_already_cropped()
    {
        var image = Synthetic.Plain(420, 300, Synthetic.DarkPage);
        Synthetic.DrawDialog(image, new PixelRect(0, 0, 420, 300), 8, Synthetic.DarkDialog);

        Assert.Equal(Finding.AlreadyCropped, SubjectDetection.Find(image));
    }

    [Fact]
    public void An_image_already_cropped_with_a_sliver_of_page_left_along_its_edges_is_found_already_cropped()
    {
        var image = Synthetic.Plain(420, 300, Synthetic.DimmedPage);
        Synthetic.DrawDialog(image, new PixelRect(2, 0, 416, 299), 8);

        Assert.Equal(Finding.AlreadyCropped, SubjectDetection.Find(image));
    }

    [Theory]
    [InlineData(255, 255, 255)]
    [InlineData(96, 98, 104)]
    [InlineData(20, 20, 20)]
    public void Nothing_is_found_in_a_featureless_image(byte r, byte g, byte b)
    {
        var image = Synthetic.Plain(640, 400, new Rgba(r, g, b, 255));

        Assert.Equal(Finding.NothingFound, SubjectDetection.Find(image));
    }

    [Fact]
    public void Nothing_is_found_in_a_dimmed_page_with_no_dialog()
    {
        Assert.Equal(Finding.NothingFound, SubjectDetection.Find(Synthetic.DimmedApplication(800, 500)));
    }

    [Fact]
    public void The_same_image_twice_gives_the_same_Subject()
    {
        var image = Synthetic.DimmedApplication(800, 500);
        Synthetic.DrawDialog(image, new PixelRect(151, 99, 377, 263), 10);

        var first = SubjectDetection.Find(image).Subject;
        var second = SubjectDetection.Find(image).Subject;

        Assert.NotNull(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void A_cancelled_search_stops()
    {
        var image = Synthetic.DimmedApplication(800, 500);

        Assert.ThrowsAny<OperationCanceledException>(() => SubjectDetection.Find(image, new CancellationToken(canceled: true)));
    }

    [Fact]
    public void A_found_Subject_becomes_a_crop_to_its_window()
    {
        var image = Synthetic.DimmedApplication(800, 500);
        var dialog = new PixelRect(151, 99, 377, 263);
        Synthetic.DrawDialog(image, dialog, 10);

        var crop = SubjectDetection.Find(image).Subject!.ToCrop();

        Assert.Equal(Crop.Classic(dialog), crop);
    }
}
