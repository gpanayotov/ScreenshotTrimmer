using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

/// <summary>Reviewing a Batch's Results: the No Subject found markers and moving between the images still to review.</summary>
public class ReviewingTests
{
    private static readonly UserSettings Remembered = new() { ShadowOn = true };

    /// <summary>A light dialog over a dimmed application page, in which a Batch finds a Subject.</summary>
    private static RgbaImage DimmedDialog()
    {
        var image = Synthetic.DimmedApplication(800, 500);
        Synthetic.DrawDialog(image, new PixelRect(100, 80, 400, 250), 8);
        return image;
    }

    /// <summary>A dimmed page with nothing on it, in which a Batch finds no Subject.</summary>
    private static RgbaImage Featureless() => Synthetic.DimmedApplication(800, 500);

    private static WorkspaceImage Entry(Workspace workspace, string name) =>
        workspace.Images.Single(i => Path.GetFileName(i.Path) == name);

    [Fact]
    public void Giving_a_marked_image_Steps_clears_its_No_Subject_found_marker()
    {
        using var folder = new TempFolder();
        EncodedImages.WritePng(folder.File("blank.png"), Featureless());
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        workspace.RunBatch(null, CancellationToken.None);

        workspace.SetSteps(Entry(workspace, "blank.png"), new Steps { Crop = Crop.Classic(new PixelRect(10, 10, 50, 50)) });

        Assert.False(Entry(workspace, "blank.png").NoSubjectFound);
    }

    [Fact]
    public void A_second_Batch_processes_only_the_images_that_still_need_it_and_clears_the_markers_of_those_it_now_processes()
    {
        using var folder = new TempFolder();
        EncodedImages.WritePng(folder.File("a-dialog.png"), DimmedDialog());
        EncodedImages.WritePng(folder.File("b-retaken.png"), Featureless());
        EncodedImages.WritePng(folder.File("c-blank.png"), Featureless());
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        workspace.RunBatch(null, CancellationToken.None);
        var reviewed = new Steps { Crop = Crop.Classic(new PixelRect(20, 20, 300, 200)) };
        workspace.SetSteps(Entry(workspace, "a-dialog.png"), reviewed);
        // The miss is retaken with a dialog on it.
        var retaken = DimmedDialog();
        EncodedImages.WritePng(folder.File("b-retaken.png"), retaken);

        var summary = workspace.RunBatch(null, CancellationToken.None);

        Assert.Equal(reviewed, workspace.Steps(Entry(workspace, "a-dialog.png")));
        Assert.False(Entry(workspace, "b-retaken.png").NoSubjectFound);
        Assert.Equal(SubjectDetection.Find(retaken)!.ToCrop(), workspace.Steps(Entry(workspace, "b-retaken.png")).Crop);
        Assert.True(Entry(workspace, "c-blank.png").NoSubjectFound);
        Assert.Equal(1, summary.Processed);
        Assert.Equal(["c-blank.png"], summary.NoSubjectFound.Select(i => Path.GetFileName(i.Path)));
        Assert.Equal(1, summary.SkippedFor(BatchSkip.HasSteps));
    }

    private static string? SelectedName(Workspace workspace) =>
        workspace.Selected is { } selected ? Path.GetFileName(selected.Path) : null;

    /// <summary>A folder of misses and hits, after a Batch: a, c and e have no Subject found, b and d have Steps.</summary>
    private static Workspace AfterBatch(TempFolder folder)
    {
        foreach (var name in new[] { "a.png", "c.png", "e.png" })
            EncodedImages.WritePng(folder.File(name), Featureless());
        foreach (var name in new[] { "b.png", "d.png" })
            EncodedImages.WritePng(folder.File(name), DimmedDialog());
        var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        workspace.RunBatch(null, CancellationToken.None);
        return workspace;
    }

    [Fact]
    public void Next_and_previous_with_No_Subject_found_skip_the_images_with_a_Subject()
    {
        using var folder = new TempFolder();
        using var workspace = AfterBatch(folder);

        workspace.Next(image => image.NoSubjectFound);
        Assert.Equal("c.png", SelectedName(workspace));
        workspace.Next(image => image.NoSubjectFound);
        Assert.Equal("e.png", SelectedName(workspace));
        workspace.Previous(image => image.NoSubjectFound);
        Assert.Equal("c.png", SelectedName(workspace));
    }

    [Fact]
    public void Next_with_none_after_the_selection_and_previous_with_none_before_it_do_nothing()
    {
        using var folder = new TempFolder();
        using var workspace = AfterBatch(folder);
        var changes = 0;
        workspace.Changed += (_, _) => changes++;

        workspace.Previous(image => image.NoSubjectFound);
        Assert.Equal("a.png", SelectedName(workspace));
        workspace.Previous(image => image.HasUnsavedSteps);
        Assert.Equal("a.png", SelectedName(workspace));
        Assert.Equal(0, changes);

        workspace.Select(Entry(workspace, "e.png"));
        changes = 0;
        workspace.Next(image => image.NoSubjectFound);
        workspace.Next(image => image.HasUnsavedSteps);
        Assert.Equal("e.png", SelectedName(workspace));
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Next_and_previous_with_unsaved_Steps_move_between_the_processed_images()
    {
        using var folder = new TempFolder();
        using var workspace = AfterBatch(folder);

        workspace.Next(image => image.HasUnsavedSteps);
        Assert.Equal("b.png", SelectedName(workspace));
        workspace.Next(image => image.HasUnsavedSteps);
        Assert.Equal("d.png", SelectedName(workspace));
        workspace.Select(Entry(workspace, "e.png"));
        workspace.Previous(image => image.HasUnsavedSteps);
        Assert.Equal("d.png", SelectedName(workspace));
    }

    [Fact]
    public void An_image_has_Batch_Steps_until_its_Steps_change()
    {
        using var folder = new TempFolder();
        using var workspace = AfterBatch(folder);
        var processed = Entry(workspace, "b.png");

        Assert.True(workspace.HasBatchSteps(processed));
        Assert.False(workspace.HasBatchSteps(Entry(workspace, "a.png")));

        workspace.SetSteps(processed, workspace.Steps(processed) with { Crop = Crop.Classic(new PixelRect(5, 5, 50, 50)) });
        Assert.False(workspace.HasBatchSteps(processed));
        workspace.Undo(processed);
        Assert.False(workspace.HasBatchSteps(processed));
    }
}
