using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class SelectionTests
{
    private static Workspace OpenFolderOf(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
        var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        return workspace;
    }

    private static string? SelectedName(Workspace workspace) =>
        workspace.Selected is { } selected ? Path.GetFileName(selected.Path) : null;

    [Fact]
    public void Next_and_previous_move_through_the_list()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolderOf(folder, "a.png", "b.png", "c.png");

        workspace.Next();
        workspace.Next();
        Assert.Equal("c.png", SelectedName(workspace));

        workspace.Previous();
        Assert.Equal("b.png", SelectedName(workspace));
    }

    [Fact]
    public void Next_on_the_last_image_does_nothing()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolderOf(folder, "a.png", "b.png");
        workspace.Next();
        var changes = 0;
        workspace.Changed += (_, _) => changes++;

        workspace.Next();

        Assert.Equal("b.png", SelectedName(workspace));
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Previous_on_the_first_image_does_nothing()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolderOf(folder, "a.png", "b.png");
        var changes = 0;
        workspace.Changed += (_, _) => changes++;

        workspace.Previous();

        Assert.Equal("a.png", SelectedName(workspace));
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Next_and_previous_with_nothing_listed_do_nothing()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolderOf(folder);

        workspace.Next();
        workspace.Previous();

        Assert.Null(workspace.Selected);
    }

    [Fact]
    public void Selecting_an_entry_makes_it_the_selection_and_announces_it()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolderOf(folder, "a.png", "b.png", "c.png");
        var changes = new List<WorkspaceChange>();
        workspace.Changed += (_, change) => changes.Add(change);

        workspace.Select(workspace.Images[2]);

        Assert.Equal("c.png", SelectedName(workspace));
        Assert.Equal(folder.File("c.png"), Assert.Single(changes).Selected?.Path);
    }

    [Fact]
    public void Selecting_an_entry_that_is_not_listed_throws()
    {
        using var folder = new TempFolder();
        using var workspace = OpenFolderOf(folder, "a.png");

        Assert.Throws<ArgumentException>(() => workspace.Select(new WorkspaceImage { Key = ImageKey.OfFile(folder.File("x.png")) }));
    }
}
