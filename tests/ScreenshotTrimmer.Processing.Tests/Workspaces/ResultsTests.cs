using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class ResultsTests
{
    private static void Touch(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
    }

    private static string[] Names(Workspace workspace) =>
        workspace.Images.Select(i => Path.GetFileName(i.Path)).ToArray();

    private static WorkspaceImage Entry(Workspace workspace, string name) =>
        workspace.Images.Single(i => Path.GetFileName(i.Path) == name);

    [Fact]
    public void Showing_results_lists_them_in_their_sorted_place_and_keeps_the_selection()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "a-trimmed.png", "b.png", "c.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.Select(Entry(workspace, "b.png"));

        workspace.ShowResults = true;

        Assert.Equal(["a.png", "a-trimmed.png", "b.png", "c.png"], Names(workspace));
        Assert.True(Entry(workspace, "a-trimmed.png").IsResult);
        Assert.Equal(folder.File("b.png"), workspace.Selected?.Path);
    }

    [Fact]
    public void Hiding_results_again_keeps_the_selection()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "a-trimmed.png", "b.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.ShowResults = true;
        workspace.Select(Entry(workspace, "b.png"));

        workspace.ShowResults = false;

        Assert.Equal(["a.png", "b.png"], Names(workspace));
        Assert.Equal(folder.File("b.png"), workspace.Selected?.Path);
    }

    [Fact]
    public void Hiding_results_while_one_is_selected_selects_its_original()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png", "b-trimmed.png", "c.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.ShowResults = true;
        workspace.Select(Entry(workspace, "b-trimmed.png"));

        workspace.ShowResults = false;

        Assert.Equal(["a.png", "b.png", "c.png"], Names(workspace));
        Assert.Equal(folder.File("b.png"), workspace.Selected?.Path);
    }
}
