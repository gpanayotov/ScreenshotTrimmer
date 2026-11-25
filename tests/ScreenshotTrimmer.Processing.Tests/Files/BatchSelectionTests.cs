using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Tests.Files;

public class BatchSelectionTests
{
    private static readonly RgbaImage Screenshot = Synthetic.Gradient(120, 80);

    private static readonly RgbaImage Shadowed =
        Renderer.Render(Synthetic.RoundedWindow(120, 80, 8, 8, 8, 8), new Steps { Shadow = new Shadow() }).Result;

    private static ListedImage Image(string name, bool isResult = false) =>
        new() { Path = $"/shots/{name}", IsResult = isResult };

    [Fact]
    public void A_Batch_takes_the_listed_images_with_no_pending_Steps_that_are_neither_Results_nor_already_processed()
    {
        var fresh = Image("a.png");
        var withSteps = Image("b.png");
        var result = Image("a-trimmed.png", isResult: true);
        var processed = Image("c.png");
        var alsoFresh = Image("d.png");
        var pixels = new Dictionary<ListedImage, RgbaImage>
        {
            [fresh] = Screenshot, [withSteps] = Screenshot, [result] = Shadowed,
            [processed] = Shadowed, [alsoFresh] = Screenshot,
        };

        var selected = BatchSelection.Select(
            [fresh, withSteps, result, processed, alsoFresh],
            hasPendingSteps: image => image == withSteps,
            loadOriginal: image => pixels[image]);

        Assert.Equal([fresh, alsoFresh], selected);
    }

    [Fact]
    public void Images_already_ruled_out_by_their_Steps_or_as_Results_are_not_loaded()
    {
        var withSteps = Image("b.png");
        var result = Image("a-trimmed.png", isResult: true);
        var loaded = new List<ListedImage>();

        var selected = BatchSelection.Select(
            [withSteps, result],
            hasPendingSteps: image => image == withSteps,
            loadOriginal: image => { loaded.Add(image); return Screenshot; });

        Assert.Empty(selected);
        Assert.Empty(loaded);
    }
}
