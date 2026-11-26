using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

/// <summary>Removing entries from the list: they are hidden while this list is open, and their files are never touched.</summary>
public class RemovingTests
{
    private static void Touch(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
    }

    private static readonly TimeSpan WatchDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static Workspace OpenFolder(TempFolder folder, params string[] names)
    {
        Touch(folder, names);
        var workspace = new Workspace(new UserSettings(), WatchDelay);
        workspace.Open([folder.Path]);
        return workspace;
    }

    private static string[] Names(IEnumerable<WorkspaceImage> images) =>
        images.Select(i => Path.GetFileName(i.Path!)).ToArray();

    private static string? SelectedName(Workspace workspace) =>
        workspace.Selected is { } selected ? Path.GetFileName(selected.Path) : null;

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
    public void A_removed_entry_is_hidden_and_its_file_is_kept()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png", "c.png");

        workspace.Remove(workspace.Images[1]);

        Assert.Equal(["a.png", "c.png"], Names(workspace.Images));
        Assert.Equal(["a.png", "b.png", "c.png"], folder.Entries());
    }

    [Fact]
    public void Removing_the_selected_entry_selects_the_next_one()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png", "c.png");
        workspace.Next();

        workspace.Remove(workspace.Images[1]);

        Assert.Equal("c.png", SelectedName(workspace));
    }

    [Fact]
    public void Removing_the_selected_last_entry_selects_the_previous_one()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png", "c.png");
        workspace.Select(workspace.Images[2]);

        workspace.Remove(workspace.Images[2]);

        Assert.Equal("b.png", SelectedName(workspace));
    }

    [Fact]
    public async Task A_removed_entry_stays_hidden_when_the_folder_changes()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png", "c.png");
        workspace.Remove(workspace.Images[1]);
        var change = ChangeWhere(workspace, c => c.Images.Count == 3);

        Touch(folder, "d.png");
        File.WriteAllBytes(folder.File("b.png"), [1, 2, 3]);

        Assert.Equal(["a.png", "c.png", "d.png"], Names((await change).Images));
    }

    [Fact]
    public async Task A_removed_entry_stays_hidden_when_its_file_is_renamed()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png", "c.png");
        workspace.Remove(workspace.Images[1]);
        var change = ChangeWhere(workspace, c => c.Images.Count == 3);

        File.Move(folder.File("b.png"), folder.File("e.png"));
        Touch(folder, "d.png");

        Assert.Equal(["a.png", "c.png", "d.png"], Names((await change).Images));
    }

    [Fact]
    public void Removed_entries_are_listed_again_when_the_folder_is_opened_again()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png", "c.png");
        workspace.Remove(workspace.Images[1]);

        workspace.Open([folder.Path]);

        Assert.Equal(["a.png", "b.png", "c.png"], Names(workspace.Images));
    }

    [Fact]
    public void A_removed_result_stays_hidden_when_results_are_shown()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "a-trimmed.png", "b.png");
        workspace.ShowResults = true;
        workspace.Remove(workspace.Images[1]);

        workspace.ShowResults = false;
        workspace.ShowResults = true;

        Assert.Equal(["a.png", "b.png"], Names(workspace.Images));
    }
}
