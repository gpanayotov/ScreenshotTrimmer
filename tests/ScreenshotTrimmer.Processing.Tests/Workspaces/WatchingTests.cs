using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

/// <summary>
/// The workspace following the disk. Each test changes a real folder and waits, with a timeout, for the workspace's
/// change notification showing the new state; the wait is generous because file events arrive late on busy CI machines.
/// </summary>
public class WatchingTests
{
    private static readonly TimeSpan WatchDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static void Touch(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
    }

    private static string[] Names(IEnumerable<WorkspaceImage> images) =>
        images.Select(i => Path.GetFileName(i.Path!)).ToArray();

    private static string? SelectedName(WorkspaceChange change) =>
        change.Selected is { } selected ? Path.GetFileName(selected.Path) : null;

    private static Workspace OpenFolder(TempFolder folder, params string[] names)
    {
        Touch(folder, names);
        var workspace = new Workspace(new UserSettings(), WatchDelay);
        workspace.Open([folder.Path]);
        return workspace;
    }

    /// <summary>
    /// The first change from <paramref name="workspace"/> that satisfies <paramref name="until"/>. Subscribe before
    /// changing the disk; on a slow file system an intermediate state may be announced first, which is skipped.
    /// </summary>
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
    public async Task An_added_file_appears_in_its_sorted_place()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "shot-1.png", "shot-10.png");
        var change = ChangeWhere(workspace, c => c.Images.Count == 3);

        Touch(folder, "shot-2.png");

        Assert.Equal(["shot-1.png", "shot-2.png", "shot-10.png"], Names((await change).Images));
        Assert.Equal(["shot-1.png", "shot-2.png", "shot-10.png"], Names(workspace.Images));
    }

    [Fact]
    public async Task A_deleted_file_disappears()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png", "c.png");
        var change = ChangeWhere(workspace, c => c.Images.Count == 2);

        File.Delete(folder.File("b.png"));

        Assert.Equal(["a.png", "c.png"], Names((await change).Images));
    }

    [Fact]
    public async Task An_overwritten_file_gets_a_new_content_version()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png");
        var before = workspace.Images.Select(i => i.ContentVersion).ToArray();
        var change = ChangeWhere(workspace, c => c.Images[1].ContentVersion != before[1]);

        File.WriteAllBytes(folder.File("b.png"), [1, 2, 3]);
        // Two writes within the file system's timestamp resolution would otherwise look the same.
        File.SetLastWriteTimeUtc(folder.File("b.png"), DateTime.UtcNow.AddMinutes(1));

        var changed = await change;
        Assert.Equal(["a.png", "b.png"], Names(changed.Images));
        Assert.Equal(before[0], changed.Images[0].ContentVersion);
    }

    [Fact]
    public async Task The_has_result_badge_follows_the_result_appearing_and_disappearing()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png");

        var appeared = ChangeWhere(workspace, c => c.Images[0].HasResult);
        Touch(folder, "a-trimmed.png");
        Assert.Equal(["a.png", "b.png"], Names((await appeared).Images));

        var disappeared = ChangeWhere(workspace, c => !c.Images[0].HasResult);
        File.Delete(folder.File("a-trimmed.png"));
        Assert.Equal(["a.png", "b.png"], Names((await disappeared).Images));
    }

    [Fact]
    public async Task The_selection_is_kept_when_other_files_change()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "b.png", "c.png", "d.png");
        workspace.Next();
        var change = ChangeWhere(workspace, c => Names(c.Images) is ["a.png", "b.png", "c.png"]);

        Touch(folder, "a.png");
        File.Delete(folder.File("d.png"));

        Assert.Equal("c.png", SelectedName(await change));
    }

    [Fact]
    public async Task Deleting_the_selected_file_selects_the_next_one()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png", "c.png");
        workspace.Next();
        var change = ChangeWhere(workspace, c => c.Images.Count == 2);

        File.Delete(folder.File("b.png"));

        Assert.Equal("c.png", SelectedName(await change));
    }

    [Fact]
    public async Task Deleting_the_selected_last_file_selects_the_previous_one()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png", "c.png");
        workspace.Next();
        workspace.Next();
        var change = ChangeWhere(workspace, c => c.Images.Count == 2);

        File.Delete(folder.File("c.png"));

        Assert.Equal("b.png", SelectedName(await change));
    }

    [Fact]
    public async Task A_renamed_file_moves_to_its_new_name_and_place_and_stays_selected()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png", "c.png");
        var change = ChangeWhere(workspace, c => Names(c.Images).Contains("z.png"));

        File.Move(folder.File("a.png"), folder.File("z.png"));

        var changed = await change;
        Assert.Equal(["b.png", "c.png", "z.png"], Names(changed.Images));
        Assert.Equal("z.png", SelectedName(changed));
    }

    [Fact]
    public async Task With_several_files_open_only_those_files_are_kept_under_their_new_names()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png", "c.png", "d.png");
        using var workspace = new Workspace(new UserSettings(), WatchDelay);
        workspace.Open([folder.File("a.png"), folder.File("c.png"), folder.File("d.png")]);
        var change = ChangeWhere(workspace, c => Names(c.Images).Contains("e.png"));

        Touch(folder, "new.png");
        File.Delete(folder.File("d.png"));
        File.Move(folder.File("c.png"), folder.File("e.png"));

        Assert.Equal(["a.png", "e.png"], Names((await change).Images));
    }

    [Fact]
    public async Task With_one_file_open_its_folder_stays_listed_when_that_file_is_deleted()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png", "c.png");
        using var workspace = new Workspace(new UserSettings(), WatchDelay);
        workspace.Open([folder.File("b.png")]);
        var change = ChangeWhere(workspace, c => !Names(c.Images).Contains("b.png"));

        File.Delete(folder.File("b.png"));
        var changed = await change;
        Assert.Equal(["a.png", "c.png"], Names(changed.Images));
        Assert.Equal("c.png", SelectedName(changed));

        var added = ChangeWhere(workspace, c => Names(c.Images).Contains("d.png"));
        Touch(folder, "d.png");
        Assert.Equal(["a.png", "c.png", "d.png"], Names((await added).Images));
    }

    [Fact]
    public async Task A_burst_of_changes_gives_one_update()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        // A long delay, so that a slow machine still writes the whole burst within it.
        using var workspace = new Workspace(new UserSettings(), TimeSpan.FromSeconds(1));
        workspace.Open([folder.Path]);
        var changes = new List<WorkspaceChange>();
        workspace.Changed += (_, change) => { lock (changes) changes.Add(change); };
        var all = ChangeWhere(workspace, c => c.Images.Count == 21);

        for (var i = 1; i <= 20; i++)
            Touch(folder, $"shot-{i}.png");
        await all;
        await Task.Delay(TimeSpan.FromSeconds(2));

        lock (changes)
            Assert.Equal(21, Assert.Single(changes).Images.Count);
    }

    [Fact]
    public async Task A_change_that_lists_the_same_images_gives_no_update()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png");
        var first = ChangeWhere(workspace, _ => true);

        Touch(folder, "notes.txt");
        await Task.Delay(TimeSpan.FromSeconds(1));
        Touch(folder, "b.png");

        Assert.Equal(["a.png", "b.png"], Names((await first).Images));
    }

    [Fact]
    public async Task A_deleted_folder_empties_the_list_and_says_why()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolder(folder, "a.png", "b.png");
        var change = ChangeWhere(workspace, c => c.UnavailableFolders.Count > 0);

        Directory.Delete(folder.Path, recursive: true);

        var changed = await change;
        Assert.Empty(changed.Images);
        Assert.Null(changed.Selected);
        Assert.Equal([folder.Path], changed.UnavailableFolders);
    }

    [Fact]
    public async Task A_deleted_empty_folder_opened_with_a_trailing_separator_says_why()
    {
        using var folder = new TempFolder();
        using var workspace = new Workspace(new UserSettings(), WatchDelay);
        workspace.Open([folder.Path + Path.DirectorySeparatorChar]);
        var change = ChangeWhere(workspace, c => c.UnavailableFolders.Count > 0);

        Directory.Delete(folder.Path);

        Assert.Equal([folder.Path], (await change).UnavailableFolders);
    }

    [Fact]
    public async Task Opening_something_else_stops_watching_the_old_folder()
    {
        using var oldFolder = new TempFolder();
        using var newFolder = new TempFolder();
        using var workspace = OpenFolder(oldFolder, "old.png");
        Touch(newFolder, "a.png");
        workspace.Open([newFolder.Path]);
        var first = ChangeWhere(workspace, _ => true);

        Touch(oldFolder, "old-2.png");
        await Task.Delay(TimeSpan.FromSeconds(1));
        Touch(newFolder, "b.png");

        Assert.Equal(["a.png", "b.png"], Names((await first).Images));
    }

    [Fact]
    public async Task A_disposed_workspace_stops_watching()
    {
        using var folder = new TempFolder();
        var workspace = OpenFolder(folder, "a.png");
        var changes = 0;
        workspace.Changed += (_, _) => Interlocked.Increment(ref changes);

        workspace.Dispose();
        Touch(folder, "b.png");
        await Task.Delay(TimeSpan.FromSeconds(1));

        Assert.Equal(0, changes);
    }
}
