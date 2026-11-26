using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Workspaces;

using static ScreenshotTrimmer.Processing.Tests.Workspaces.BatchTesting;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class BatchPlanTests
{
    private static readonly PixelRect Dialog = new(100, 80, 400, 250);

    /// <summary>
    /// A folder mixing two new images (one featureless), one edited, one already processed and one Result, opened with
    /// Results shown and the edited one given Steps.
    /// </summary>
    private static Workspace MixedFolder(TempFolder folder)
    {
        var dialog = Synthetic.DialogOverDimmedApplication(Dialog);
        EncodedImages.WritePng(folder.File("a-new.png"), dialog);
        EncodedImages.WritePng(folder.File("b-edited.png"), dialog);
        EncodedImages.WritePng(folder.File("c-new.png"), Synthetic.DimmedApplication(800, 500));
        EncodedImages.WritePng(folder.File("c-new-trimmed.png"), dialog);
        EncodedImages.WritePng(folder.File("d-processed.png"),
            Renderer.Render(dialog, new Steps { Crop = Crop.Classic(Dialog), Shadow = new Shadow() }).Result);
        var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        workspace.ShowResults = true;
        workspace.SetSteps(Entry(workspace, "b-edited.png"), new Steps { Crop = Crop.Classic(new PixelRect(10, 10, 50, 50)) });
        return workspace;
    }

    [Fact]
    public void The_plan_lists_the_images_a_Batch_then_processes_and_counts_the_ones_it_skips_for_each_reason()
    {
        using var folder = new TempFolder();
        using var workspace = MixedFolder(folder);

        var plan = workspace.PlanBatch();

        Assert.Equal(["a-new.png", "c-new.png"], Names(plan.Images));
        Assert.Equal(1, plan.Skipped.HasSteps);
        Assert.Equal(1, plan.Skipped.AlreadyProcessed);
        Assert.Equal(1, plan.Skipped.Result);

        var summary = workspace.RunBatch(null, CancellationToken.None);

        Assert.Equal(["a-new.png"], Names(workspace.Images.Where(i => i.HasUnsavedSteps && Path.GetFileName(i.Path) != "b-edited.png")));
        Assert.Equal(["c-new.png"], Names(summary.NoSubjectFound));
        Assert.Equal(plan.Images.Count, summary.Processed + summary.NoSubjectFound.Count);
        Assert.Equal(plan.Skipped, summary.Skipped);
    }

    [Fact]
    public void A_plan_after_a_file_is_overwritten_with_a_processed_image_skips_it()
    {
        using var folder = new TempFolder();
        var dialog = Synthetic.DialogOverDimmedApplication(Dialog);
        EncodedImages.WritePng(folder.File("shot.png"), dialog);
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        Assert.Equal(["shot.png"], Names(workspace.PlanBatch().Images));

        EncodedImages.WritePng(folder.File("shot.png"),
            Renderer.Render(dialog, new Steps { Crop = Crop.Classic(Dialog), Shadow = new Shadow() }).Result);
        workspace.Open([folder.Path]);
        var plan = workspace.PlanBatch();

        Assert.Empty(plan.Images);
        Assert.Equal(1, plan.Skipped.AlreadyProcessed);
    }

    [Fact]
    public void A_Pasted_image_in_a_new_list_is_planned_by_its_own_pixels()
    {
        using var workspace = new Workspace(Remembered);
        workspace.Paste(Synthetic.DialogOverDimmedApplication(Dialog));
        Assert.Single(workspace.PlanBatch().Images);

        workspace.Open([]);
        workspace.Paste(Renderer.Render(Synthetic.DialogOverDimmedApplication(Dialog), new Steps { Crop = Crop.Classic(Dialog), Shadow = new Shadow() }).Result);

        Assert.Empty(workspace.PlanBatch().Images);
    }

    [Fact]
    public void An_image_that_cannot_be_read_is_planned_so_the_Batch_reports_it()
    {
        using var folder = new TempFolder();
        File.WriteAllText(folder.File("broken.png"), "not an image");
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);

        var plan = workspace.PlanBatch();

        Assert.Equal(["broken.png"], Names(plan.Images));
    }
}
