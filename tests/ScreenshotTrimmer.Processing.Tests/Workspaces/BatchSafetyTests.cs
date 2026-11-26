using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

/// <summary>
/// A Batch runs in parallel beside the user: its Steps do not depend on timing, it leaves alone what changed meanwhile,
/// and it can be cancelled. Tests that act during a run do so from the progress report of an image, with one image at
/// a time, so what has run is fixed by the order of the strip rather than by timing.
/// </summary>
public class BatchSafetyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly UserSettings Remembered = new()
    {
        FrameLines = true, RoundedCorners = false, ShadowOn = true, ShadowSize = 1.5, ShadowOpacity = 0.5,
    };

    private static readonly Steps HandMade = new() { Crop = Crop.Classic(new PixelRect(10, 10, 50, 50)) };

    /// <summary>A light dialog over a dimmed application page, as a modal dialog screenshot looks.</summary>
    private static RgbaImage DimmedDialog(PixelRect dialog)
    {
        var image = Synthetic.DimmedApplication(800, 500);
        Synthetic.DrawDialog(image, dialog, 8);
        return image;
    }

    /// <summary>Writes a dimmed dialog screenshot, each name with its dialog in a different place, for each of <paramref name="names"/>.</summary>
    private static void WriteDialogs(TempFolder folder, params string[] names)
    {
        for (var i = 0; i < names.Length; i++)
            EncodedImages.WritePng(folder.File(names[i]), DimmedDialog(new PixelRect(60 + 30 * i, 40 + 15 * i, 300 + 10 * i, 220)));
    }

    private static WorkspaceImage Entry(Workspace workspace, string name) =>
        workspace.Images.Single(i => Path.GetFileName(i.Path) == name);

    private static IEnumerable<string> Names(IEnumerable<WorkspaceImage> images) =>
        images.Select(image => Path.GetFileName(image.Path)!);

    /// <summary>Each listed image's name with its Steps and whether no Subject was found in it.</summary>
    private static string[] Outcome(Workspace workspace) =>
        workspace.Images.Select(image => $"{Path.GetFileName(image.Path)} {workspace.Steps(image)} {image.NoSubjectFound}").ToArray();

    [Fact]
    public void The_same_folder_gives_the_same_Steps_on_every_run_and_with_any_parallelism()
    {
        using var folder = new TempFolder();
        WriteDialogs(folder, "a.png", "b.png", "c.png", "d.png", "e.png", "f.png", "g.png");
        EncodedImages.WritePng(folder.File("blank.png"), Synthetic.DimmedApplication(800, 500));

        var outcomes = new[] { 1, 4, 4, 8 }.Select(parallelism =>
        {
            using var workspace = new Workspace(Remembered);
            workspace.Open([folder.Path]);
            workspace.RunBatch(null, CancellationToken.None, parallelism);
            return Outcome(workspace);
        }).ToList();

        Assert.Contains(outcomes[0], line => line.EndsWith("True", StringComparison.Ordinal));
        Assert.All(outcomes, outcome => Assert.Equal(outcomes[0], outcome));
    }

    [Fact]
    public void An_image_given_Steps_during_the_Batch_is_left_as_the_user_set_it()
    {
        using var folder = new TempFolder();
        WriteDialogs(folder, "a.png", "b.png");
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        var b = Entry(workspace, "b.png");

        var summary = workspace.RunBatch(
            new SynchronousProgress(p => { if (p.Done == 1) workspace.SetSteps(b, HandMade); }), CancellationToken.None, 1);

        Assert.Equal(HandMade, workspace.Steps(Entry(workspace, "b.png")));
        Assert.NotEqual(Steps.None, workspace.Steps(Entry(workspace, "a.png")));
        Assert.Equal(1, summary.Processed);
        Assert.Equal(1, summary.SkippedFor(BatchSkip.HasSteps));
    }

    [Fact]
    public void An_image_removed_from_the_list_during_the_Batch_is_skipped_as_gone()
    {
        using var folder = new TempFolder();
        WriteDialogs(folder, "a.png", "b.png");
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        var b = Entry(workspace, "b.png");

        var summary = workspace.RunBatch(
            new SynchronousProgress(p => { if (p.Done == 1) workspace.Remove(b); }), CancellationToken.None, 1);

        Assert.Equal(["a.png"], Names(workspace.Images));
        Assert.Equal(1, summary.Processed);
        Assert.Equal(1, summary.SkippedFor(BatchSkip.Gone));
        Assert.Empty(summary.Failed);
    }

    [Fact]
    public void An_image_deleted_from_disk_during_the_Batch_is_skipped_as_gone()
    {
        using var folder = new TempFolder();
        WriteDialogs(folder, "a.png", "b.png");
        // The list does not follow the disk during the run, so the Batch finds the file gone when it reads it.
        using var workspace = new Workspace(Remembered, TimeSpan.FromHours(1));
        workspace.Open([folder.Path]);

        var summary = workspace.RunBatch(
            new SynchronousProgress(p => { if (p.Done == 1) File.Delete(folder.File("b.png")); }), CancellationToken.None, 1);

        Assert.Equal(1, summary.Processed);
        Assert.Equal(1, summary.SkippedFor(BatchSkip.Gone));
        Assert.Empty(summary.Failed);
    }

    [Fact]
    public void An_unreadable_image_counts_as_failed_by_name_and_the_rest_are_processed()
    {
        using var folder = new TempFolder();
        WriteDialogs(folder, "a.png", "c.png");
        File.WriteAllBytes(folder.File("b.png"), [1, 2, 3]);
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);

        var summary = workspace.RunBatch(null, CancellationToken.None);

        Assert.Equal(["b.png"], Names(summary.Failed));
        Assert.Equal(2, summary.Processed);
        Assert.Equal(Steps.None, workspace.Steps(Entry(workspace, "b.png")));
        Assert.False(summary.Cancelled);
    }

    [Fact]
    public void Cancelling_keeps_the_Steps_of_finished_images_and_reports_cancelled()
    {
        using var folder = new TempFolder();
        WriteDialogs(folder, "a.png", "b.png", "c.png");
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        using var cancellation = new CancellationTokenSource();

        var summary = workspace.RunBatch(
            new SynchronousProgress(p => { if (p.Done == 1) cancellation.Cancel(); }), cancellation.Token, 1);

        Assert.True(summary.Cancelled);
        Assert.Equal(1, summary.Processed);
        Assert.NotEqual(Steps.None, workspace.Steps(Entry(workspace, "a.png")));
        Assert.Equal(Steps.None, workspace.Steps(Entry(workspace, "b.png")));
        Assert.Equal(Steps.None, workspace.Steps(Entry(workspace, "c.png")));
    }

    [Fact]
    public void A_Batch_cancelled_before_it_starts_does_nothing_and_reports_cancelled()
    {
        using var folder = new TempFolder();
        WriteDialogs(folder, "a.png");
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);

        var summary = workspace.RunBatch(null, new CancellationToken(canceled: true));

        Assert.True(summary.Cancelled);
        Assert.Equal(0, summary.Processed);
        Assert.Equal(Steps.None, workspace.Steps(Entry(workspace, "a.png")));
    }

    [Fact]
    public void A_second_Batch_while_one_runs_is_refused_and_one_after_it_runs()
    {
        using var folder = new TempFolder();
        WriteDialogs(folder, "a.png", "b.png");
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        Exception? refusal = null;

        var summary = workspace.RunBatch(new SynchronousProgress(p =>
        {
            if (p.Done == 1)
                refusal = Record.Exception(() => workspace.RunBatch(null, CancellationToken.None));
        }), CancellationToken.None, 1);

        Assert.IsType<InvalidOperationException>(refusal);
        Assert.Equal(2, summary.Processed);
        Assert.Equal(2, workspace.RunBatch(null, CancellationToken.None).SkippedFor(BatchSkip.HasSteps));
    }

    [Fact]
    public async Task Opening_during_a_Batch_cancels_it_and_waits_for_it_before_the_list_changes()
    {
        using var folder = new TempFolder();
        WriteDialogs(folder, "a.png", "b.png", "c.png");
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        var (batch, release) = StartHeldAfterFirstImage(workspace);

        workspace.Open([folder.Path]);

        Assert.True(batch.IsCompleted);
        var summary = await batch;
        Assert.True(summary.Cancelled);
        Assert.Equal(1, summary.Processed);
        Assert.All(workspace.Images, image => Assert.Equal(Steps.None, workspace.Steps(image)));
        Assert.True(release.IsSet);
    }

    [Fact]
    public async Task Disposing_during_a_Batch_cancels_it_and_waits_for_it()
    {
        using var folder = new TempFolder();
        WriteDialogs(folder, "a.png", "b.png", "c.png");
        var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        var (batch, release) = StartHeldAfterFirstImage(workspace);

        workspace.Dispose();

        Assert.True(batch.IsCompleted);
        var summary = await batch;
        Assert.True(summary.Cancelled);
        Assert.Equal(1, summary.Processed);
        Assert.True(release.IsSet);
    }

    [Fact]
    public async Task Stopping_cancels_the_Batch_without_waiting_and_completes_once_it_has_stopped()
    {
        using var folder = new TempFolder();
        WriteDialogs(folder, "a.png", "b.png", "c.png");
        using var workspace = new Workspace(Remembered);
        workspace.Open([folder.Path]);
        using var release = new ManualResetEventSlim();
        var batch = StartHeldAfterFirstImage(workspace, release);

        var stopped = workspace.StopBatchAsync();

        Assert.False(stopped.IsCompleted);
        release.Set();
        await stopped.WaitAsync(Timeout);
        Assert.True(batch.IsCompleted);
        var summary = await batch;
        Assert.True(summary.Cancelled);
        Assert.Equal(1, summary.Processed);
        Assert.NotEqual(Steps.None, workspace.Steps(Entry(workspace, "a.png")));
        Assert.Equal(Steps.None, workspace.Steps(Entry(workspace, "b.png")));
    }

    [Fact]
    public void Stopping_with_no_Batch_running_is_already_done()
    {
        using var workspace = new Workspace(Remembered);

        Assert.True(workspace.StopBatchAsync().IsCompletedSuccessfully);
    }

    /// <summary>
    /// Starts a Batch, one image at a time, on another thread and returns once its first image is done. The Batch is
    /// held in that image's progress report until <paramref name="release"/> is set.
    /// </summary>
    private static Task<BatchSummary> StartHeldAfterFirstImage(Workspace workspace, ManualResetEventSlim release)
    {
        var reached = new ManualResetEventSlim();
        var batch = Task.Run(() => workspace.RunBatch(new SynchronousProgress(p =>
        {
            if (p.Done != 1)
                return;
            reached.Set();
            Assert.True(release.Wait(Timeout));
        }), CancellationToken.None, 1));
        Assert.True(reached.Wait(Timeout));
        return batch;
    }

    /// <summary>
    /// Starts a Batch, one image at a time, on another thread and returns once its first image is done. The Batch is
    /// held in that image's progress report until the workspace is asked to stop it, so the stop arrives while it runs.
    /// </summary>
    private static (Task<BatchSummary> Batch, ManualResetEventSlim Release) StartHeldAfterFirstImage(Workspace workspace)
    {
        var reached = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        workspace.BatchStopRequested += release.Set;
        var batch = Task.Run(() => workspace.RunBatch(new SynchronousProgress(p =>
        {
            if (p.Done != 1)
                return;
            reached.Set();
            Assert.True(release.Wait(Timeout));
        }), CancellationToken.None, 1));
        Assert.True(reached.Wait(Timeout));
        return (batch, release);
    }

    /// <summary>An <see cref="IProgress{T}"/> that reports on the Batch's own thread, before it goes on.</summary>
    private sealed class SynchronousProgress(Action<BatchProgress> report) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value) => report(value);
    }
}
