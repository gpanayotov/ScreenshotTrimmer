using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class BatchSelectionTests
{
    private static readonly RgbaImage Screenshot = Synthetic.Gradient(120, 80);

    private static readonly RgbaImage Shadowed =
        Renderer.Render(Synthetic.RoundedWindow(120, 80, 8, 8, 8, 8), new Steps { Shadow = new Shadow() }).Result;

    private static WorkspaceImage Image(string name, bool isResult = false) =>
        new() { Key = ImageKey.OfFile(Path.GetFullPath($"/shots/{name}")), IsResult = isResult };

    private static readonly WorkspaceImage Pasted = new() { Key = ImageKey.OfPasted(1), HasUnsavedSteps = true };

    [Fact]
    public void A_Batch_takes_images_with_no_Steps_that_are_neither_Results_nor_already_processed()
    {
        Assert.Null(BatchSelection.SkipReason(Image("a.png"), hasSteps: false, () => Screenshot));
        Assert.Equal(BatchSkip.HasSteps, BatchSelection.SkipReason(Image("b.png"), hasSteps: true, () => Screenshot));
        Assert.Equal(BatchSkip.Result,
            BatchSelection.SkipReason(Image("a-trimmed.png", isResult: true), hasSteps: false, () => Shadowed));
        Assert.Equal(BatchSkip.AlreadyProcessed,
            BatchSelection.SkipReason(Image("c.png"), hasSteps: false, () => Shadowed));
    }

    [Fact]
    public void A_Pasted_image_with_no_Steps_is_taken_although_it_is_unsaved()
    {
        Assert.Null(BatchSelection.SkipReason(Pasted, hasSteps: false, () => Screenshot));
    }

    [Fact]
    public void Images_already_ruled_out_by_their_Steps_or_as_Results_are_not_loaded()
    {
        var loaded = 0;
        RgbaImage Load()
        {
            loaded++;
            return Screenshot;
        }

        BatchSelection.SkipReason(Image("b.png"), hasSteps: true, Load);
        BatchSelection.SkipReason(Image("a-trimmed.png", isResult: true), hasSteps: false, Load);

        Assert.Equal(0, loaded);
    }
}
