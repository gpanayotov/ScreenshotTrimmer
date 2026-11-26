using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class BatchTests
{
    private static readonly UserSettings Remembered = new()
    {
        FrameLines = true, RoundedCorners = false, ShadowOn = true, ShadowSize = 1.5, ShadowOpacity = 0.5,
    };

    /// <summary>A light dialog over a dimmed application page, as a modal dialog screenshot looks.</summary>
    private static RgbaImage DimmedDialog(PixelRect dialog)
    {
        var image = Synthetic.DimmedApplication(800, 500);
        Synthetic.DrawDialog(image, dialog, 8);
        return image;
    }

    private static WorkspaceImage Entry(Workspace workspace, string name) =>
        workspace.Images.Single(i => Path.GetFileName(i.Path) == name);

    [Fact]
    public void Each_dimmed_dialog_gets_one_unsaved_undoable_Step_with_the_found_crop_and_the_remembered_finishing()
    {
        using var folder = new TempFolder();
        var first = DimmedDialog(new PixelRect(100, 80, 400, 250));
        var second = DimmedDialog(new PixelRect(220, 60, 300, 320));
        EncodedImages.WritePng(folder.File("a.png"), first);
        EncodedImages.WritePng(folder.File("b.png"), second);
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);

        workspace.RunBatch(null, CancellationToken.None);

        foreach (var (name, original) in new[] { ("a.png", first), ("b.png", second) })
        {
            var image = Entry(workspace, name);
            Assert.Equal(new Steps
            {
                Crop = SubjectDetection.Find(original)!.ToCrop(),
                FrameLines = true,
                Shadow = new Shadow { Size = 1.5, Opacity = 0.5 },
            }, workspace.Steps(image));
            Assert.True(image.HasUnsavedSteps);
            Assert.False(image.NoSubjectFound);
            workspace.Undo(image);
            Assert.Equal(Steps.None, workspace.Steps(image));
        }
    }

    [Fact]
    public void A_Batch_uses_the_finishing_settings_applied_last()
    {
        using var folder = new TempFolder();
        EncodedImages.WritePng(folder.File("a.png"), DimmedDialog(new PixelRect(100, 80, 400, 250)));
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);

        workspace.ApplySettings(Remembered with { FrameLines = false, ShadowOn = false });
        workspace.RunBatch(null, CancellationToken.None);

        var steps = workspace.Steps(Entry(workspace, "a.png"));
        Assert.False(steps.FrameLines);
        Assert.Null(steps.Shadow);
    }

    [Fact]
    public void A_featureless_image_is_marked_No_Subject_found_and_gets_no_Steps()
    {
        using var folder = new TempFolder();
        EncodedImages.WritePng(folder.File("blank.png"), Synthetic.DimmedApplication(800, 500));
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);

        var summary = workspace.RunBatch(null, CancellationToken.None);

        var blank = Entry(workspace, "blank.png");
        Assert.True(blank.NoSubjectFound);
        Assert.False(blank.HasUnsavedSteps);
        Assert.Equal(Steps.None, workspace.Steps(blank));
        Assert.Equal(0, summary.Processed);
        Assert.Equal(["blank.png"], summary.NoSubjectFound.Select(i => Path.GetFileName(i.Path)));
    }

    [Fact]
    public void Images_with_Steps_already_processed_images_and_Results_are_skipped_and_counted()
    {
        using var folder = new TempFolder();
        var dialog = DimmedDialog(new PixelRect(100, 80, 400, 250));
        EncodedImages.WritePng(folder.File("edited.png"), dialog);
        EncodedImages.WritePng(folder.File("fresh.png"), dialog);
        EncodedImages.WritePng(folder.File("fresh-trimmed.png"), dialog);
        EncodedImages.WritePng(folder.File("processed.png"),
            Renderer.Render(dialog, new Steps { Crop = Crop.Classic(new PixelRect(100, 80, 400, 250)), Shadow = new Shadow() }).Result);
        EncodedImages.WritePng(folder.File("blank.png"), Synthetic.DimmedApplication(800, 500));
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        workspace.ShowResults = true;
        var handMade = new Steps { Crop = Crop.Classic(new PixelRect(10, 10, 50, 50)) };
        workspace.SetSteps(Entry(workspace, "edited.png"), handMade);

        var summary = workspace.RunBatch(null, CancellationToken.None);

        Assert.Equal(handMade, workspace.Steps(Entry(workspace, "edited.png")));
        Assert.Equal(Steps.None, workspace.Steps(Entry(workspace, "processed.png")));
        Assert.Equal(Steps.None, workspace.Steps(Entry(workspace, "fresh-trimmed.png")));
        Assert.NotEqual(Steps.None, workspace.Steps(Entry(workspace, "fresh.png")));
        Assert.DoesNotContain(workspace.Images, i => i.NoSubjectFound && Path.GetFileName(i.Path) != "blank.png");
        Assert.Equal(1, summary.Processed);
        Assert.Single(summary.NoSubjectFound);
        Assert.Equal(1, summary.SkippedFor(BatchSkip.HasSteps));
        Assert.Equal(1, summary.SkippedFor(BatchSkip.AlreadyProcessed));
        Assert.Equal(1, summary.SkippedFor(BatchSkip.Result));
    }

    [Fact]
    public void A_Pasted_image_with_no_Steps_is_processed()
    {
        using var workspace = new Workspace(Remembered);
        var original = DimmedDialog(new PixelRect(150, 100, 350, 200));
        var pasted = workspace.Paste(original);

        var summary = workspace.RunBatch(null, CancellationToken.None);

        Assert.Equal(SubjectDetection.Find(original)!.ToCrop(), workspace.Steps(pasted).Crop);
        Assert.Equal(1, summary.Processed);
    }

    [Fact]
    public void Progress_is_reported_after_each_image_the_Batch_processes()
    {
        using var folder = new TempFolder();
        EncodedImages.WritePng(folder.File("a.png"), DimmedDialog(new PixelRect(100, 80, 400, 250)));
        EncodedImages.WritePng(folder.File("b.png"), Synthetic.DimmedApplication(800, 500));
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        var reports = new List<BatchProgress>();

        workspace.RunBatch(new SynchronousProgress(reports.Add), CancellationToken.None);

        Assert.Equal([new BatchProgress(1, 2), new BatchProgress(2, 2)], reports);
    }

    [Fact]
    public void Progress_counts_only_the_images_the_Batch_processes_as_its_plan_does()
    {
        using var folder = new TempFolder();
        var dialog = DimmedDialog(new PixelRect(100, 80, 400, 250));
        EncodedImages.WritePng(folder.File("a.png"), dialog);
        EncodedImages.WritePng(folder.File("b-edited.png"), dialog);
        EncodedImages.WritePng(folder.File("c.png"), Synthetic.DimmedApplication(800, 500));
        EncodedImages.WritePng(folder.File("d-processed.png"),
            Renderer.Render(dialog, new Steps { Crop = Crop.Classic(new PixelRect(100, 80, 400, 250)), Shadow = new Shadow() }).Result);
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "b-edited.png"), new Steps { Crop = Crop.Classic(new PixelRect(10, 10, 50, 50)) });
        var planned = workspace.PlanBatch().Images.Count;
        var reports = new List<BatchProgress>();

        workspace.RunBatch(new SynchronousProgress(reports.Add), CancellationToken.None);

        Assert.Equal(2, planned);
        Assert.Equal([new BatchProgress(1, 2), new BatchProgress(2, 2)], reports);
    }

    [Fact]
    public void A_Batch_writes_nothing_to_disk()
    {
        using var folder = new TempFolder();
        EncodedImages.WritePng(folder.File("a.png"), DimmedDialog(new PixelRect(100, 80, 400, 250)));
        EncodedImages.WritePng(folder.File("b.png"), Synthetic.DimmedApplication(800, 500));
        var before = Snapshot(folder);
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);

        workspace.RunBatch(null, CancellationToken.None);

        Assert.Equal(before, Snapshot(folder));
    }

    /// <summary>Every file in <paramref name="folder"/> with its contents and last write time.</summary>
    private static string[] Snapshot(TempFolder folder) =>
        folder.Entries()
            .Select(name => $"{name} {Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(folder.File(name))))} {File.GetLastWriteTimeUtc(folder.File(name)):O}")
            .ToArray();

    /// <summary>An <see cref="IProgress{T}"/> that reports on the calling thread, so the reports are in order when the run returns.</summary>
    private sealed class SynchronousProgress(Action<BatchProgress> report) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value) => report(value);
    }
}
