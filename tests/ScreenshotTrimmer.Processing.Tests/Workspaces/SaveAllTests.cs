using System.Runtime.Versioning;
using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;
using SkiaSharp;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class SaveAllTests
{
    private static readonly Steps Cropped = new()
    {
        Crop = Crop.Classic(new PixelRect(4, 2, 20, 10)), Shadow = new Shadow(),
    };

    private static string WriteOriginal(string path)
    {
        EncodedImages.Write(path, EncodedImages.HalfAndHalf(40, 30, SKColors.Red, SKColors.Blue));
        return path;
    }

    private static WorkspaceImage Entry(Workspace workspace, string name) =>
        workspace.Images.Single(i => Path.GetFileName(i.Path) == name);

    private static bool IsResultOf(string resultPath, string originalPath, Steps steps) =>
        ImageFile.Load(resultPath).PixelsEqual(Renderer.Render(ImageFile.Load(originalPath), steps).Result);

    [Fact]
    public void Save_all_saves_only_the_images_with_unsaved_steps()
    {
        using var folder = new TempFolder();
        var a = WriteOriginal(folder.File("a.png"));
        WriteOriginal(folder.File("b.png"));
        var c = WriteOriginal(folder.File("c.png"));
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "a.png"), Cropped);
        workspace.SetSteps(Entry(workspace, "c.png"), Cropped);

        var outcomes = workspace.SaveAll();

        Assert.Equal(["a.png", "c.png"], outcomes.Select(o => Path.GetFileName(o.Image.Path)));
        Assert.All(outcomes, o => Assert.Null(o.Failure));
        Assert.Equal([folder.File("a-trimmed.png")], outcomes[0].Report!.Written);
        Assert.Equal(["a-trimmed.png", "a.png", "b.png", "c-trimmed.png", "c.png"], folder.Entries());
        Assert.True(IsResultOf(folder.File("a-trimmed.png"), a, Cropped));
        Assert.True(IsResultOf(folder.File("c-trimmed.png"), c, Cropped));
        Assert.DoesNotContain(workspace.Images, i => i.HasUnsavedSteps);
        Assert.True(Entry(workspace, "a.png").HasResult);
    }

    [Fact]
    public void Save_all_carries_on_past_a_file_in_the_way_and_reports_it()
    {
        using var folder = new TempFolder();
        WriteOriginal(folder.File("a.png"));
        var b = WriteOriginal(folder.File("b.png"));
        Directory.CreateDirectory(folder.File("a-trimmed.png"));
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "a.png"), Cropped);
        workspace.SetSteps(Entry(workspace, "b.png"), Cropped);

        var outcomes = workspace.SaveAll();

        Assert.Equal(["a.png", "b.png"], outcomes.Select(o => Path.GetFileName(o.Image.Path)));
        var failure = Assert.IsType<SaveFailedException>(outcomes[0].Failure);
        Assert.Equal(SaveFailureReason.TargetIsFolder, failure.Reason);
        Assert.Null(outcomes[0].Report);
        Assert.Null(outcomes[1].Failure);
        Assert.True(IsResultOf(folder.File("b-trimmed.png"), b, Cropped));
        Assert.True(Entry(workspace, "a.png").HasUnsavedSteps);
        Assert.False(Entry(workspace, "b.png").HasUnsavedSteps);
    }

    [UnixNonRootFact]
    [UnsupportedOSPlatform("windows")]
    public void Save_all_carries_on_past_a_read_only_folder_and_reports_it()
    {
        using var locked = new TempFolder();
        using var open = new TempFolder();
        var a = WriteOriginal(locked.File("a.png"));
        var b = WriteOriginal(open.File("b.png"));
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([a, b]);
        workspace.SetSteps(Entry(workspace, "a.png"), Cropped);
        workspace.SetSteps(Entry(workspace, "b.png"), Cropped);
        var writable = File.GetUnixFileMode(locked.Path);
        File.SetUnixFileMode(locked.Path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var outcomes = workspace.SaveAll();

            var failure = Assert.IsType<SaveFailedException>(outcomes.Single(o => o.Image.Path == a).Failure);
            Assert.Equal(SaveFailureReason.AccessDenied, failure.Reason);
            Assert.Null(outcomes.Single(o => o.Image.Path == b).Failure);
            Assert.Equal(["a.png"], locked.Entries());
            Assert.True(IsResultOf(open.File("b-trimmed.png"), b, Cropped));
            Assert.True(Entry(workspace, "a.png").HasUnsavedSteps);
        }
        finally
        {
            File.SetUnixFileMode(locked.Path, writable);
        }
    }

    [Fact]
    public void Save_all_reports_an_original_that_can_no_longer_be_read()
    {
        using var folder = new TempFolder();
        var a = WriteOriginal(folder.File("a.png"));
        WriteOriginal(folder.File("b.png"));
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "a.png"), Cropped);
        workspace.SetSteps(Entry(workspace, "b.png"), Cropped);
        File.WriteAllBytes(a, [1, 2, 3]);

        var outcomes = workspace.SaveAll();

        Assert.IsType<InvalidDataException>(outcomes[0].Failure);
        Assert.Null(outcomes[1].Failure);
        Assert.Equal(["a.png", "b-trimmed.png", "b.png"], folder.Entries());
    }

    [Fact]
    public void Save_all_with_nothing_unsaved_saves_nothing()
    {
        using var folder = new TempFolder();
        WriteOriginal(folder.File("a.png"));
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);

        Assert.Empty(workspace.SaveAll());
        Assert.Equal(["a.png"], folder.Entries());
    }
}
