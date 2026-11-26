using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class OpeningTests
{
    private static void Touch(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
    }

    private static string[] Names(Workspace workspace) =>
        workspace.Images.Select(i => Path.GetFileName(i.Path!)).ToArray();

    [Fact]
    public void Opening_a_folder_lists_its_images_with_the_first_selected()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot-10.png", "shot-2.png", "notes.txt");
        using var workspace = new Workspace(new UserSettings());

        workspace.Open([folder.Path]);

        Assert.Equal(["shot-2.png", "shot-10.png"], Names(workspace));
        Assert.Equal(folder.File("shot-2.png"), workspace.Selected?.Path);
    }

    [Fact]
    public void Opening_one_file_lists_its_whole_folder_with_that_file_selected()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.jpg", "c.png");
        using var workspace = new Workspace(new UserSettings());

        workspace.Open([folder.File("b.jpg")]);

        Assert.Equal(["a.png", "b.jpg", "c.png"], Names(workspace));
        Assert.Equal(folder.File("b.jpg"), workspace.Selected?.Path);
    }

    [Fact]
    public void Opening_several_files_lists_only_those_files_with_the_first_selected()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png", "c.png", "d.png");
        using var workspace = new Workspace(new UserSettings());

        workspace.Open([folder.File("d.png"), folder.File("b.png")]);

        Assert.Equal(["b.png", "d.png"], Names(workspace));
        Assert.Equal(folder.File("b.png"), workspace.Selected?.Path);
    }

    [Fact]
    public void Opening_an_empty_folder_lists_nothing_and_selects_nothing()
    {
        using var folder = new TempFolder();
        Touch(folder, "notes.txt");
        using var workspace = new Workspace(new UserSettings());

        workspace.Open([folder.Path]);

        Assert.Empty(workspace.Images);
        Assert.Null(workspace.Selected);
    }

    [Fact]
    public void A_missing_path_is_skipped_as_if_it_had_not_been_opened_and_reported()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png", "c.png");
        var missing = folder.File("gone.png");
        using var workspace = new Workspace(new UserSettings());

        var missingPaths = workspace.Open([folder.File("b.png"), missing]);

        // Only one file is left, so its whole folder is listed with it selected.
        Assert.Equal(["a.png", "b.png", "c.png"], Names(workspace));
        Assert.Equal(folder.File("b.png"), workspace.Selected?.Path);
        Assert.Equal([missing], missingPaths);
    }

    [Fact]
    public void Opening_only_a_missing_path_lists_nothing()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        var missing = folder.File("gone");
        using var workspace = new Workspace(new UserSettings());

        var missingPaths = workspace.Open([missing]);

        Assert.Empty(workspace.Images);
        Assert.Null(workspace.Selected);
        Assert.Equal([missing], missingPaths);
    }

    [Fact]
    public void Opening_something_new_replaces_the_list()
    {
        using var first = new TempFolder();
        using var second = new TempFolder();
        Touch(first, "a.png");
        Touch(second, "x.png", "y.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([first.Path]);

        workspace.Open([second.Path]);

        Assert.Equal(["x.png", "y.png"], Names(workspace));
        Assert.Equal(second.File("x.png"), workspace.Selected?.Path);
    }

    [Fact]
    public void Opening_announces_the_new_list_and_selection()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png");
        using var workspace = new Workspace(new UserSettings());
        var changes = new List<WorkspaceChange>();
        workspace.Changed += (_, change) => changes.Add(change);

        workspace.Open([folder.Path]);

        var change = Assert.Single(changes);
        Assert.Equal([folder.File("a.png"), folder.File("b.png")], change.Images.Select(i => i.Path));
        Assert.Equal(folder.File("a.png"), change.Selected?.Path);
    }
}
