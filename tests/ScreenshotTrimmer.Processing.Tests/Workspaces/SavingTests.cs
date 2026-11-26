using System.Runtime.Versioning;
using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;
using SkiaSharp;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class SavingTests
{
    private static readonly Steps Cropped = new()
    {
        Crop = Crop.Classic(new PixelRect(4, 2, 20, 10)), Shadow = new Shadow(),
    };

    private static string WriteOriginal(TempFolder folder, string name)
    {
        var path = folder.File(name);
        EncodedImages.Write(path, EncodedImages.HalfAndHalf(40, 30, SKColors.Red, SKColors.Blue));
        return path;
    }

    private static WorkspaceImage Entry(Workspace workspace, string name) =>
        workspace.Images.Single(i => Path.GetFileName(i.Path) == name);

    [Fact]
    public void Saving_in_suffix_mode_writes_the_result_of_the_original_with_its_steps_beside_it()
    {
        using var folder = new TempFolder();
        var originalPath = WriteOriginal(folder, "shot.png");
        var originalBytes = File.ReadAllBytes(originalPath);
        using var workspace = new Workspace(new UserSettings { ResultSuffix = "-done" });
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "shot.png"), Cropped);

        var report = workspace.Save(Entry(workspace, "shot.png"));

        var resultPath = folder.File("shot-done.png");
        Assert.Equal([resultPath], report.Written);
        Assert.Equal(["shot-done.png", "shot.png"], folder.Entries());
        Assert.Equal(originalBytes, File.ReadAllBytes(originalPath));
        var expected = Renderer.Render(ImageFile.Load(originalPath), Cropped).Result;
        Assert.True(ImageFile.Load(resultPath).PixelsEqual(expected));
    }

    [Fact]
    public void A_saved_image_is_no_longer_unsaved_and_shows_the_result_badge()
    {
        using var folder = new TempFolder();
        WriteOriginal(folder, "shot.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "shot.png"), Cropped);
        var changes = new List<WorkspaceChange>();
        workspace.Changed += (_, change) => changes.Add(change);

        workspace.Save(Entry(workspace, "shot.png"));

        var saved = Entry(workspace, "shot.png");
        Assert.False(saved.HasUnsavedSteps);
        Assert.True(saved.HasResult);
        Assert.Equal(Cropped, workspace.Steps(saved));
        Assert.False(workspace.Selected?.HasUnsavedSteps);
        var last = changes.Last();
        Assert.False(last.Images.Single().HasUnsavedSteps);
        Assert.True(last.Images.Single().HasResult);
    }

    [Fact]
    public void A_step_after_a_save_marks_the_image_unsaved_and_saving_again_overwrites_the_result()
    {
        using var folder = new TempFolder();
        var originalPath = WriteOriginal(folder, "shot.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "shot.png"), Cropped);
        workspace.Save(Entry(workspace, "shot.png"));
        var recropped = new Steps { Crop = Crop.Classic(new PixelRect(0, 0, 30, 20)) };

        workspace.SetSteps(Entry(workspace, "shot.png"), recropped);
        Assert.True(Entry(workspace, "shot.png").HasUnsavedSteps);
        workspace.Save(Entry(workspace, "shot.png"));

        Assert.False(Entry(workspace, "shot.png").HasUnsavedSteps);
        Assert.Equal(["shot-trimmed.png", "shot.png"], folder.Entries());
        var expected = Renderer.Render(ImageFile.Load(originalPath), recropped).Result;
        Assert.True(ImageFile.Load(folder.File("shot-trimmed.png")).PixelsEqual(expected));
    }

    [UnixNonRootFact]
    [UnsupportedOSPlatform("windows")]
    public void A_save_into_a_read_only_folder_fails_with_the_reason_and_leaves_the_steps_unsaved()
    {
        using var folder = new TempFolder();
        WriteOriginal(folder, "shot.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "shot.png"), Cropped);
        var writable = File.GetUnixFileMode(folder.Path);
        File.SetUnixFileMode(folder.Path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var failure = Assert.Throws<SaveFailedException>(() => workspace.Save(Entry(workspace, "shot.png")));

            Assert.Equal(SaveFailureReason.AccessDenied, failure.Reason);
            Assert.True(Entry(workspace, "shot.png").HasUnsavedSteps);
            Assert.Equal(Cropped, workspace.Steps(Entry(workspace, "shot.png")));
            Assert.Equal(["shot.png"], folder.Entries());
        }
        finally
        {
            File.SetUnixFileMode(folder.Path, writable);
        }
    }

    [Fact]
    public void A_file_in_the_way_fails_the_save_and_leaves_the_steps_unsaved()
    {
        using var folder = new TempFolder();
        WriteOriginal(folder, "shot.png");
        Directory.CreateDirectory(folder.File("shot-trimmed.png"));
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "shot.png"), Cropped);

        var failure = Assert.Throws<SaveFailedException>(() => workspace.Save(Entry(workspace, "shot.png")));

        Assert.Equal(SaveFailureReason.TargetIsFolder, failure.Reason);
        Assert.True(Entry(workspace, "shot.png").HasUnsavedSteps);
    }

    [Fact]
    public void Loading_the_original_gives_the_files_pixels()
    {
        using var folder = new TempFolder();
        var originalPath = WriteOriginal(folder, "shot.png");
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);

        var original = workspace.LoadOriginal(Entry(workspace, "shot.png"));

        Assert.True(original.PixelsEqual(ImageFile.Load(originalPath)));
    }
}
