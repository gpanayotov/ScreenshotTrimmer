using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

/// <summary>Whether anything is left to save, and what happens to unsaved Steps when the disk takes their file away.</summary>
public class UnsavedWorkTests
{
    private static readonly Steps Cropped = new() { Crop = Crop.Classic(new PixelRect(1, 2, 3, 4)) };

    private static void Touch(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
    }

    private static WorkspaceImage Entry(Workspace workspace, string name) =>
        workspace.Images.Single(i => Path.GetFileName(i.Path) == name);

    private static readonly TimeSpan WatchDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

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
        return next.Task.WaitAsync(Timeout);
    }

    [Fact]
    public void Unsaved_work_follows_the_steps_of_the_listed_images()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        Assert.False(workspace.HasUnsavedWork);

        workspace.SetSteps(Entry(workspace, "b.png"), Cropped);
        Assert.True(workspace.HasUnsavedWork);

        workspace.SetSteps(Entry(workspace, "b.png"), Steps.None);
        Assert.False(workspace.HasUnsavedWork);
    }

    [Fact]
    public async Task A_file_with_unsaved_steps_deleted_on_disk_is_dropped_named_and_its_steps_discarded()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png", "c.png");
        using var workspace = new Workspace(new UserSettings(), WatchDelay);
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "b.png"), Cropped);
        var dropped = ChangeWhere(workspace, c => c.Images.Count == 2);

        File.Delete(folder.File("b.png"));

        Assert.Equal([folder.File("b.png")], (await dropped).DroppedWithUnsavedSteps);
        Assert.False(workspace.HasUnsavedWork);
        var back = ChangeWhere(workspace, c => c.Images.Count == 3);
        Touch(folder, "b.png");
        await back;
        Assert.Same(Steps.None, workspace.Steps(Entry(workspace, "b.png")));
    }

    [Fact]
    public async Task A_file_with_unsaved_steps_moved_out_of_the_listed_folder_is_named()
    {
        using var folder = new TempFolder();
        using var elsewhere = new TempFolder();
        Touch(folder, "a.png", "b.png");
        using var workspace = new Workspace(new UserSettings(), WatchDelay);
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "b.png"), Cropped);
        var dropped = ChangeWhere(workspace, c => c.Images.Count == 1);

        File.Move(folder.File("b.png"), elsewhere.File("b.png"));

        Assert.Equal([folder.File("b.png")], (await dropped).DroppedWithUnsavedSteps);
    }

    [Fact]
    public async Task A_deleted_file_without_unsaved_steps_is_not_named()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png", "c.png");
        using var workspace = new Workspace(new UserSettings(), WatchDelay);
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "a.png"), Cropped);
        var dropped = ChangeWhere(workspace, c => c.Images.Count == 2);

        File.Delete(folder.File("b.png"));

        Assert.Empty((await dropped).DroppedWithUnsavedSteps);
        Assert.True(Entry(workspace, "a.png").HasUnsavedSteps);
    }

    [Fact]
    public async Task A_file_renamed_on_disk_keeps_its_unsaved_steps()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png");
        using var workspace = new Workspace(new UserSettings(), WatchDelay);
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "b.png"), Cropped);
        var renamed = ChangeWhere(workspace, c => c.Images.Any(i => Path.GetFileName(i.Path) == "c.png"));

        File.Move(folder.File("b.png"), folder.File("c.png"));

        Assert.Empty((await renamed).DroppedWithUnsavedSteps);
        Assert.Equal(Cropped, workspace.Steps(Entry(workspace, "c.png")));
        Assert.True(Entry(workspace, "c.png").HasUnsavedSteps);
    }
}
