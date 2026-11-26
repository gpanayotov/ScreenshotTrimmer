using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class ApplySettingsTests
{
    private static void Touch(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
    }

    private static string[] Names(Workspace workspace) =>
        workspace.Images.Select(i => Path.GetFileName(i.Path!)).ToArray();

    private static string[] NamesWithResult(Workspace workspace) =>
        workspace.Images.Where(i => i.HasResult).Select(i => Path.GetFileName(i.Path!)).ToArray();

    private static string? SelectedName(Workspace workspace) =>
        workspace.Selected is { } selected ? Path.GetFileName(selected.Path) : null;

    private static void SelectNamed(Workspace workspace, string name) =>
        workspace.Select(workspace.Images.Single(i => Path.GetFileName(i.Path) == name));

    [Fact]
    public void A_new_Result_suffix_hides_the_files_with_that_suffix_instead()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot.png", "shot-trimmed.png", "shot-done.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        Assert.Equal(["shot.png", "shot-done.png"], Names(workspace));

        workspace.ApplySettings(new UserSettings { ResultSuffix = "-done" });

        Assert.Equal(["shot.png", "shot-trimmed.png"], Names(workspace));
        Assert.Equal(["shot.png"], NamesWithResult(workspace));
    }

    [Fact]
    public void Backup_mode_lists_every_image_and_marks_no_Result()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot.png", "shot-trimmed.png", "other.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);

        workspace.ApplySettings(new UserSettings { SaveMode = SaveMode.Backup });

        Assert.Equal(["other.png", "shot.png", "shot-trimmed.png"], Names(workspace));
        Assert.Empty(NamesWithResult(workspace));
        Assert.DoesNotContain(workspace.Images, i => i.IsResult);
    }

    [Fact]
    public void Returning_to_Suffix_mode_hides_the_Results_again()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot.png", "shot-trimmed.png");
        using var workspace = new Workspace(new UserSettings { SaveMode = SaveMode.Backup });
        workspace.Open([folder.Path]);
        Assert.Equal(["shot.png", "shot-trimmed.png"], Names(workspace));

        workspace.ApplySettings(new UserSettings { SaveMode = SaveMode.Suffix });

        Assert.Equal(["shot.png"], Names(workspace));
        Assert.Equal(["shot.png"], NamesWithResult(workspace));
    }

    [Fact]
    public void The_selection_is_kept_when_the_selected_file_is_still_listed()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png", "b-trimmed.png", "c.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        SelectNamed(workspace, "c.png");

        workspace.ApplySettings(new UserSettings { SaveMode = SaveMode.Backup });

        Assert.Equal(["a.png", "b.png", "b-trimmed.png", "c.png"], Names(workspace));
        Assert.Equal("c.png", SelectedName(workspace));
    }

    [Fact]
    public void A_selected_file_that_becomes_hidden_passes_the_selection_to_the_entry_now_in_its_place()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "a-trimmed.png", "b.png");
        using var workspace = new Workspace(new UserSettings { SaveMode = SaveMode.Backup });
        workspace.Open([folder.Path]);
        SelectNamed(workspace, "a-trimmed.png");

        workspace.ApplySettings(new UserSettings());

        Assert.Equal(["a.png", "b.png"], Names(workspace));
        Assert.Equal("b.png", SelectedName(workspace));
    }

    [Fact]
    public void Several_opened_files_stay_the_only_ones_listed()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "a-trimmed.png", "b.png", "c.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.File("a.png"), folder.File("a-trimmed.png"), folder.File("c.png")]);
        Assert.Equal(["a.png", "c.png"], Names(workspace));

        workspace.ApplySettings(new UserSettings { SaveMode = SaveMode.Backup });

        Assert.Equal(["a.png", "a-trimmed.png", "c.png"], Names(workspace));
    }

    [Fact]
    public void Applying_settings_announces_one_update()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot.png", "shot-trimmed.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var changes = new List<WorkspaceChange>();
        workspace.Changed += (_, change) => changes.Add(change);

        workspace.ApplySettings(new UserSettings { SaveMode = SaveMode.Backup });

        var change = Assert.Single(changes);
        Assert.Equal([folder.File("shot.png"), folder.File("shot-trimmed.png")], change.Images.Select(i => i.Path));
        Assert.Equal(folder.File("shot.png"), change.Selected?.Path);
    }

    [Fact]
    public void Applying_settings_that_change_only_the_finishing_lists_nothing_again_and_announces_nothing()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot.png", "shot-trimmed.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var images = workspace.Images;
        var changes = 0;
        workspace.Changed += (_, _) => changes++;

        workspace.ApplySettings(new UserSettings { FrameLines = false, ShadowOn = true, ShadowSize = 2 });

        Assert.Equal(0, changes);
        Assert.Same(images, workspace.Images);
    }

    [Fact]
    public void Settings_applied_before_opening_are_used_by_the_next_open()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot.png", "shot-trimmed.png");
        using var workspace = new Workspace(new UserSettings());

        workspace.ApplySettings(new UserSettings { SaveMode = SaveMode.Backup });
        workspace.Open([folder.Path]);

        Assert.Equal(["shot.png", "shot-trimmed.png"], Names(workspace));
    }
}
