using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;
using SkiaSharp;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class PastingTests
{
    private static readonly TimeSpan WatchDelay = TimeSpan.FromMilliseconds(150);

    private static readonly Steps Cropped = new()
    {
        Crop = Crop.Classic(new PixelRect(4, 2, 20, 10)), Shadow = new Shadow(),
    };

    private static void Touch(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
    }

    private static string[] Names(IEnumerable<WorkspaceImage> images) =>
        images.Select(i => i.Path is null ? $"pasted {i.PastedNumber}" : Path.GetFileName(i.Path)).ToArray();

    /// <summary>The first change from <paramref name="workspace"/> that satisfies <paramref name="until"/>.</summary>
    private static Task<WorkspaceChange> ChangeWhere(Workspace workspace, Func<WorkspaceChange, bool> until)
    {
        var next = new TaskCompletionSource<WorkspaceChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WorkspaceChange>? handler = null;
        handler = (_, change) =>
        {
            if (!until(change))
                return;
            workspace.Changed -= handler;
            next.TrySetResult(change);
        };
        workspace.Changed += handler;
        return next.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Paste_adds_a_selected_unsaved_entry_at_the_end()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);

        var pasted = workspace.Paste(Synthetic.Gradient(40, 30));

        Assert.Equal(["a.png", "b.png", "pasted 1"], Names(workspace.Images));
        Assert.Equal(pasted, workspace.Selected);
        Assert.Null(pasted.Path);
        Assert.True(pasted.HasUnsavedSteps);
        Assert.Equal(Steps.None, workspace.Steps(pasted));
    }

    [Fact]
    public async Task A_pasted_image_survives_a_folder_relist()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        using var workspace = new Workspace(new UserSettings(), WatchDelay);
        workspace.Open([folder.Path]);
        var pasted = workspace.Paste(Synthetic.Gradient(40, 30));
        workspace.SetSteps(pasted, Cropped);
        var relisted = ChangeWhere(workspace, c => c.Images.Count == 3);

        Touch(folder, "b.png");

        var change = await relisted;
        Assert.Equal(["a.png", "b.png", "pasted 1"], Names(change.Images));
        Assert.Equal(1, change.Selected?.PastedNumber);
        Assert.Equal(Cropped, workspace.Steps(change.Selected!));
        Assert.True(change.Selected!.HasUnsavedSteps);
    }

    [Fact]
    public void Pasted_images_are_numbered_in_order()
    {
        using var workspace = new Workspace(new UserSettings());

        workspace.Paste(Synthetic.Gradient(40, 30));
        workspace.Paste(Synthetic.Gradient(20, 10));

        Assert.Equal(["pasted 1", "pasted 2"], Names(workspace.Images));
        Assert.Equal(2, workspace.Selected?.PastedNumber);
    }

    [Fact]
    public void A_pasted_image_renders_from_its_pixels()
    {
        using var workspace = new Workspace(new UserSettings());
        var pixels = Synthetic.Gradient(40, 30);

        var pasted = workspace.Paste(pixels);

        Assert.True(workspace.LoadOriginal(pasted).PixelsEqual(Synthetic.Gradient(40, 30)));
    }

    [Fact]
    public void Removing_a_pasted_image_drops_it()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var pasted = workspace.Paste(Synthetic.Gradient(40, 30));

        workspace.Remove(pasted);

        Assert.Equal(["a.png"], Names(workspace.Images));
        Assert.Equal("a.png", Path.GetFileName(workspace.Selected?.Path));
    }

    [Fact]
    public void Opening_something_else_drops_pasted_images()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Paste(Synthetic.Gradient(40, 30));

        workspace.Open([folder.Path]);

        Assert.Equal(["a.png"], Names(workspace.Images));
    }

    [Fact]
    public void Save_pasted_writes_only_the_result_and_lists_it_as_that_file()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var pasted = workspace.Paste(Synthetic.Gradient(40, 30));
        workspace.SetSteps(pasted, Cropped);

        var report = workspace.SavePasted(pasted, folder.File("shot.png"), overwrite: false);

        Assert.Equal([folder.File("shot.png")], report.Written);
        Assert.Equal(["a.png", "shot.png"], folder.Entries());
        Assert.True(ImageFile.Load(folder.File("shot.png"))
            .PixelsEqual(Renderer.Render(Synthetic.Gradient(40, 30), Cropped).Result));
        Assert.Equal(["a.png", "shot.png"], Names(workspace.Images));
        var saved = workspace.Selected!;
        Assert.Equal(folder.File("shot.png"), saved.Path);
        Assert.False(saved.IsPasted);
        Assert.False(saved.HasUnsavedSteps);
        Assert.Equal(Cropped, workspace.Steps(saved));
        Assert.True(workspace.LoadOriginal(saved).PixelsEqual(Synthetic.Gradient(40, 30)));
    }

    [Fact]
    public async Task A_saved_pasted_image_is_not_duplicated_by_the_folder_watcher()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        using var workspace = new Workspace(new UserSettings(), WatchDelay);
        workspace.Open([folder.Path]);
        var pasted = workspace.Paste(Synthetic.Gradient(40, 30));
        workspace.SavePasted(pasted, folder.File("shot.png"), overwrite: false);
        var relisted = ChangeWhere(workspace, c => c.Images.Any(i => Path.GetFileName(i.Path) == "b.png"));

        Touch(folder, "b.png");

        Assert.Equal(["a.png", "b.png", "shot.png"], Names((await relisted).Images));
    }

    [Fact]
    public void A_pasted_image_saved_outside_the_opened_folder_stays_listed()
    {
        using var opened = new TempFolder();
        using var elsewhere = new TempFolder();
        Touch(opened, "a.png", "b.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([opened.File("a.png")]);
        var pasted = workspace.Paste(Synthetic.Gradient(40, 30));

        workspace.SavePasted(pasted, elsewhere.File("shot.png"), overwrite: false);
        workspace.ShowResults = true; // re-lists

        Assert.Equal(["a.png", "b.png", "shot.png"], Names(workspace.Images));
        Assert.Equal(elsewhere.File("shot.png"), workspace.Selected?.Path);
    }

    [Theory]
    [InlineData(SaveMode.Suffix)]
    [InlineData(SaveMode.Backup)]
    public void Saving_a_saved_pasted_image_again_overwrites_its_own_file(SaveMode mode)
    {
        using var folder = new TempFolder();
        using var workspace = new Workspace(new UserSettings { SaveMode = mode });
        var pasted = workspace.Paste(Synthetic.Gradient(40, 30));
        workspace.SetSteps(pasted, Cropped);
        workspace.SavePasted(pasted, folder.File("shot.png"), overwrite: false);
        var saved = workspace.Selected!;
        var recropped = Cropped with { Crop = Crop.Classic(new PixelRect(0, 0, 10, 10)) };
        workspace.SetSteps(saved, recropped);

        workspace.Save(saved);

        Assert.Equal(["shot.png"], folder.Entries());
        Assert.True(ImageFile.Load(folder.File("shot.png"))
            .PixelsEqual(Renderer.Render(Synthetic.Gradient(40, 30), recropped).Result));
        Assert.False(workspace.Selected!.HasUnsavedSteps);
    }

    [Fact]
    public void Save_pasted_asks_before_replacing_a_file()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot.png");
        using var workspace = new Workspace(new UserSettings());
        var pasted = workspace.Paste(Synthetic.Gradient(40, 30));

        var failure = Assert.Throws<SaveFailedException>(
            () => workspace.SavePasted(pasted, folder.File("shot.png"), overwrite: false));

        Assert.Equal(SaveFailureReason.TargetExists, failure.Reason);
        Assert.Equal(1, new FileInfo(folder.File("shot.png")).Length);
        Assert.True(Assert.Single(workspace.Images).IsPasted);

        workspace.SavePasted(pasted, folder.File("shot.png"), overwrite: true);

        Assert.True(ImageFile.Load(folder.File("shot.png")).PixelsEqual(Synthetic.Gradient(40, 30)));
    }

    [Fact]
    public void Save_all_reports_unsaved_pasted_images_as_needing_a_location()
    {
        using var folder = new TempFolder();
        EncodedImages.Write(folder.File("a.png"), EncodedImages.HalfAndHalf(40, 30, SKColors.Red, SKColors.Blue));
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.SetSteps(workspace.Images[0], Cropped);
        var pasted = workspace.Paste(Synthetic.Gradient(40, 30));

        var outcomes = workspace.SaveAll();

        Assert.Equal(2, outcomes.Count);
        Assert.NotNull(outcomes[0].Report);
        Assert.Equal(pasted, outcomes[1].Image);
        Assert.True(outcomes[1].NeedsLocation);
        Assert.Null(outcomes[1].Report);
        Assert.Null(outcomes[1].Failure);
        Assert.Equal(["a-trimmed.png", "a.png"], folder.Entries());
        Assert.True(workspace.Images[^1].HasUnsavedSteps);
    }
}
