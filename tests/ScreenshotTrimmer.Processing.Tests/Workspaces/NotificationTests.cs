using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

/// <summary>How <see cref="Workspace.Changed"/> is raised: free of the workspace's own lock, and in update order.</summary>
public class NotificationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static Workspace OpenFolderOf(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
        var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        return workspace;
    }

    [Fact]
    public void A_handler_can_wait_for_another_thread_that_reads_or_updates_the_workspace()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolderOf(folder, "a.png", "b.png", "c.png");
        var otherThreadFinished = false;
        var handled = 0;
        workspace.Changed += (_, _) =>
        {
            if (Interlocked.Increment(ref handled) == 1)
                otherThreadFinished = Task.Run(workspace.Next).Wait(Timeout);
        };

        workspace.Next();

        Assert.True(otherThreadFinished);
        Assert.Equal("c.png", Path.GetFileName(workspace.Selected!.Path));
    }

    [Fact]
    public void An_update_made_from_a_handler_is_announced_after_the_one_being_handled()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolderOf(folder, "a.png", "b.png", "c.png");
        var announced = new List<string>();
        workspace.Changed += (_, change) =>
        {
            announced.Add(Path.GetFileName(change.Selected!.Path));
            if (announced.Count == 1)
                workspace.Next();
        };
        workspace.Changed += (_, change) => announced.Add("second handler: " + Path.GetFileName(change.Selected!.Path));

        workspace.Next();

        Assert.Equal(["b.png", "second handler: b.png", "c.png", "second handler: c.png"], announced);
    }

    [Fact]
    public void A_selection_change_keeps_the_same_list_instance()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolderOf(folder, "a.png", "b.png");
        var listed = workspace.Images;
        WorkspaceChange? change = null;
        workspace.Changed += (_, c) => change = c;

        workspace.Next();

        Assert.Same(listed, workspace.Images);
        Assert.Same(listed, change!.Images);
    }

    [Fact]
    public void Settings_that_list_the_same_images_keep_the_same_list_instance()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolderOf(folder, "a.png", "b.png");
        var listed = workspace.Images;

        workspace.ApplySettings(new UserSettings { ResultSuffix = "-other" });

        Assert.Same(listed, workspace.Images);
    }
}
