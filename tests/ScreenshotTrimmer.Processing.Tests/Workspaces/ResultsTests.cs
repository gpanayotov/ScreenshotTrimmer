using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Rendering;
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
        workspace.Images.Select(i => Path.GetFileName(i.Path!)).ToArray();

    private static WorkspaceImage Entry(Workspace workspace, string name) =>
        workspace.Images.Single(i => Path.GetFileName(i.Path) == name);

    [Fact]
    public void Results_are_hidden_by_default_and_their_originals_have_the_badge()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "a-trimmed.png", "b.png");
        using var workspace = new Workspace(new UserSettings());

        workspace.Open([folder.Path]);

        Assert.False(workspace.ShowResults);
        Assert.Equal(["a.png", "b.png"], Names(workspace));
        Assert.True(Entry(workspace, "a.png").HasResult);
        Assert.False(Entry(workspace, "b.png").HasResult);
        Assert.All(workspace.Images, image => Assert.False(image.IsResult));
    }

    [Fact]
    public void A_suffixed_file_with_no_original_is_shown_as_a_normal_image()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "lonely-trimmed.png");
        using var workspace = new Workspace(new UserSettings());

        workspace.Open([folder.Path]);

        Assert.Equal(["a.png", "lonely-trimmed.png"], Names(workspace));
        Assert.False(Entry(workspace, "lonely-trimmed.png").IsResult);
    }

    [Fact]
    public void A_result_opened_on_its_own_stays_listed_and_selected()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "a-trimmed.png", "b.png");
        using var workspace = new Workspace(new UserSettings());

        workspace.Open([folder.File("a-trimmed.png")]);

        Assert.Equal(["a.png", "a-trimmed.png", "b.png"], Names(workspace));
        Assert.True(Entry(workspace, "a-trimmed.png").IsResult);
        Assert.Equal(folder.File("a-trimmed.png"), workspace.Selected?.Path);
    }

    [Fact]
    public void In_backup_mode_no_file_is_a_result()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "a-trimmed.png");
        using var workspace = new Workspace(new UserSettings { SaveMode = SaveMode.Backup });

        workspace.Open([folder.Path]);

        Assert.Equal(["a.png", "a-trimmed.png"], Names(workspace));
        Assert.All(workspace.Images, image => Assert.False(image.IsResult || image.HasResult));
    }

    [Fact]
    public void Toggling_results_announces_one_change_and_setting_the_same_value_none()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "a-trimmed.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var changes = new List<WorkspaceChange>();
        workspace.Changed += (_, change) => changes.Add(change);

        workspace.ShowResults = false;
        workspace.ShowResults = true;

        var change = Assert.Single(changes);
        Assert.Equal([folder.File("a.png"), folder.File("a-trimmed.png")], change.Images.Select(i => i.Path));
        Assert.Equal(folder.File("a.png"), change.Selected?.Path);
    }

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

    [Fact]
    public void A_result_with_unsaved_steps_stays_listed_and_selected_when_results_are_hidden()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png", "b-trimmed.png", "c.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.ShowResults = true;
        workspace.Select(Entry(workspace, "b-trimmed.png"));
        workspace.SetSteps(Entry(workspace, "b-trimmed.png"), new Steps { FrameLines = true });

        workspace.ShowResults = false;

        Assert.Equal(["a.png", "b.png", "b-trimmed.png", "c.png"], Names(workspace));
        Assert.Equal(folder.File("b-trimmed.png"), workspace.Selected?.Path);
        Assert.True(workspace.HasUnsavedWork);
    }

    [Fact]
    public void A_file_with_unsaved_steps_stays_listed_when_new_settings_make_it_a_result()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "a-trimmed.png");
        using var workspace = new Workspace(new UserSettings { SaveMode = SaveMode.Backup });
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "a-trimmed.png"), new Steps { FrameLines = true });

        workspace.ApplySettings(new UserSettings { SaveMode = SaveMode.Suffix });

        Assert.Equal(["a.png", "a-trimmed.png"], Names(workspace));
        Assert.True(workspace.HasUnsavedWork);
    }
}
