using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class StepsTests
{
    private static readonly Steps Cropped = new() { Crop = Crop.Classic(new PixelRect(1, 2, 3, 4)) };

    private static void Touch(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
    }

    private static WorkspaceImage Entry(Workspace workspace, string name) =>
        workspace.Images.Single(i => Path.GetFileName(i.Path) == name);

    [Fact]
    public void New_images_have_no_steps_and_are_not_unsaved()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png");
        using var workspace = new Workspace(new UserSettings());

        workspace.Open([folder.Path]);

        Assert.All(workspace.Images, image =>
        {
            Assert.Same(Steps.None, workspace.Steps(image));
            Assert.False(image.HasUnsavedSteps);
        });
    }

    [Fact]
    public void Setting_steps_marks_the_image_unsaved_and_announces_it()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var changes = new List<WorkspaceChange>();
        workspace.Changed += (_, change) => changes.Add(change);

        workspace.SetSteps(Entry(workspace, "a.png"), Cropped);

        Assert.Equal(Cropped, workspace.Steps(Entry(workspace, "a.png")));
        Assert.True(Entry(workspace, "a.png").HasUnsavedSteps);
        Assert.False(Entry(workspace, "b.png").HasUnsavedSteps);
        Assert.True(workspace.Selected?.HasUnsavedSteps);
        var change = Assert.Single(changes);
        Assert.Equal([true, false], change.Images.Select(i => i.HasUnsavedSteps));
        Assert.True(change.Selected?.HasUnsavedSteps);
    }

    [Fact]
    public void Setting_steps_equal_to_the_current_ones_is_not_a_change()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.SetSteps(workspace.Images[0], Cropped);
        var changes = new List<WorkspaceChange>();
        workspace.Changed += (_, change) => changes.Add(change);

        workspace.SetSteps(workspace.Images[0], new Steps { Crop = Crop.Classic(new PixelRect(1, 2, 3, 4)) });
        workspace.SetSteps(workspace.Images[0], Cropped);

        Assert.Empty(changes);
    }

    [Fact]
    public void Setting_no_steps_on_a_new_image_is_not_a_change()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var changes = new List<WorkspaceChange>();
        workspace.Changed += (_, change) => changes.Add(change);

        workspace.SetSteps(workspace.Images[0], Steps.None);

        Assert.Empty(changes);
        Assert.False(workspace.Images[0].HasUnsavedSteps);
    }

    [Fact]
    public void Each_image_keeps_its_own_steps_while_the_selection_moves()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var other = Cropped with { FrameLines = true };

        workspace.SetSteps(Entry(workspace, "a.png"), Cropped);
        workspace.Next();
        workspace.SetSteps(Entry(workspace, "b.png"), other);
        workspace.Previous();

        Assert.Equal(Cropped, workspace.Steps(Entry(workspace, "a.png")));
        Assert.Equal(other, workspace.Steps(Entry(workspace, "b.png")));
    }

    [Fact]
    public void Setting_back_to_no_steps_clears_the_unsaved_marker()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.SetSteps(workspace.Images[0], Cropped);

        workspace.SetSteps(workspace.Images[0], Steps.None);

        Assert.False(workspace.Images[0].HasUnsavedSteps);
    }

    [Fact]
    public void Steps_of_an_image_that_is_not_listed_are_refused()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var stranger = new WorkspaceImage { Key = ImageKey.OfFile(folder.File("elsewhere.png")) };

        Assert.Throws<ArgumentException>(() => workspace.Steps(stranger));
        Assert.Throws<ArgumentException>(() => workspace.SetSteps(stranger, Cropped));
    }
}
