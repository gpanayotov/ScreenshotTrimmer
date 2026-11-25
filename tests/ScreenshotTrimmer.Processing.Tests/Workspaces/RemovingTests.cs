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

    private static Workspace OpenFolder(TempFolder folder, params string[] names)
    {
        Touch(folder, names);
        var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        return workspace;
    }

    private static string[] Names(IEnumerable<WorkspaceImage> images) =>
        images.Select(i => Path.GetFileName(i.Path)).ToArray();

    private static string? SelectedName(Workspace workspace) =>
        workspace.Selected is { } selected ? Path.GetFileName(selected.Path) : null;

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
}
