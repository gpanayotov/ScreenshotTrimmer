using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

/// <summary>Changing finishing as the app does: the change seeded by <see cref="FirstStep"/> and set on one image.</summary>
public class FinishingTests
{
    private static void Touch(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
    }

    private static WorkspaceImage Entry(Workspace workspace, string name) =>
        workspace.Images.Single(i => Path.GetFileName(i.Path) == name);

    private static void Change(Workspace workspace, string name, UserSettings remembered, Func<Steps, Steps> change)
    {
        var image = Entry(workspace, name);
        workspace.SetSteps(image, FirstStep.Apply(workspace.Steps(image), remembered, originalHasShadowPadding: false, change));
    }

    [Fact]
    public void Changing_finishing_on_one_image_leaves_the_other_images_steps_unchanged()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.png", "c.png");
        var remembered = new UserSettings { FrameLines = true, RoundedCorners = true, ShadowOn = false };
        using var workspace = new Workspace(remembered);
        workspace.Open([folder.Path]);
        var pending = new Steps { Crop = Crop.Classic(new PixelRect(0, 0, 1, 1)), FrameLines = true };
        workspace.SetSteps(Entry(workspace, "b.png"), pending);

        Change(workspace, "a.png", remembered, CleanEdges.SetFrameLines(false));
        Change(workspace, "a.png", remembered with { FrameLines = false }, CleanEdges.SetManualRadius(3));

        Assert.Equal(
            new Steps { RoundedCorners = new RoundedCorners { ManualRadius = 3 } },
            workspace.Steps(Entry(workspace, "a.png")));
        Assert.Equal(pending, workspace.Steps(Entry(workspace, "b.png")));
        Assert.Same(Steps.None, workspace.Steps(Entry(workspace, "c.png")));
        Assert.Equal([true, true, false], workspace.Images.Select(i => i.HasUnsavedSteps));
    }

    [Fact]
    public void A_shadow_slider_drag_previews_each_value_and_records_one_step_at_release()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        var remembered = new UserSettings { FrameLines = false, RoundedCorners = false, ShadowOn = true };
        using var workspace = new Workspace(remembered);
        workspace.Open([folder.Path]);
        var image = Entry(workspace, "a.png");
        var changes = 0;
        workspace.Changed += (_, _) => changes++;

        // While dragging, each value is only previewed from the image's current Steps.
        var previews = new[] { 1.5, 2.0, 2.5 }
            .Select(size => FirstStep.Apply(workspace.Steps(image), remembered, originalHasShadowPadding: false,
                new ShadowControls(On: true, Size: size, Opacity: 0.35).Change))
            .ToList();
        Assert.Equal(0, changes);
        Assert.Same(Steps.None, workspace.Steps(image));

        // The release records the last value as one Step.
        workspace.SetSteps(image, previews[^1]);

        Assert.Equal(1, changes);
        Assert.Equal(new Steps { Shadow = new Shadow { Size = 2.5, Opacity = 0.35 } }, workspace.Steps(Entry(workspace, "a.png")));
    }
}
