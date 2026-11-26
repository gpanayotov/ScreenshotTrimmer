using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;
using SkiaSharp;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class BackupModeSavingTests
{
    private static readonly UserSettings Backup = new() { SaveMode = SaveMode.Backup, BackupSuffix = "-orig" };

    private static readonly Steps Cropped = new()
    {
        Crop = Crop.Classic(new PixelRect(4, 2, 20, 10)), Shadow = new Shadow(),
    };

    private static readonly Steps Recropped = new()
    {
        Crop = Crop.Classic(new PixelRect(2, 3, 30, 20)), FrameLines = true, Shadow = new Shadow(),
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
    public void A_backup_mode_save_keeps_the_original_as_the_backup_and_gives_the_result_its_name()
    {
        using var folder = new TempFolder();
        var originalPath = WriteOriginal(folder, "shot.png");
        var original = ImageFile.Load(originalPath);
        using var workspace = new Workspace(Backup);
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "shot.png"), Cropped);

        workspace.Save(Entry(workspace, "shot.png"));

        Assert.Equal(["shot-orig.png", "shot.png"], folder.Entries());
        Assert.True(ImageFile.Load(folder.File("shot-orig.png")).PixelsEqual(original));
        Assert.True(ImageFile.Load(originalPath).PixelsEqual(Renderer.Render(original, Cropped).Result));
        Assert.False(Entry(workspace, "shot.png").HasUnsavedSteps);
        Assert.Equal(Cropped, workspace.Steps(Entry(workspace, "shot.png")));
    }

    [Fact]
    public void Saving_again_after_further_steps_renders_from_the_true_original()
    {
        using var folder = new TempFolder();
        var originalPath = WriteOriginal(folder, "shot.png");
        var original = ImageFile.Load(originalPath);
        using var workspace = new Workspace(Backup);
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "shot.png"), Cropped);
        workspace.Save(Entry(workspace, "shot.png"));

        workspace.SetSteps(Entry(workspace, "shot.png"), Recropped);
        workspace.Save(Entry(workspace, "shot.png"));

        Assert.Equal(["shot-orig.png", "shot.png"], folder.Entries());
        Assert.True(ImageFile.Load(folder.File("shot-orig.png")).PixelsEqual(original));
        Assert.True(ImageFile.Load(originalPath).PixelsEqual(Renderer.Render(original, Recropped).Result));
        Assert.True(workspace.LoadOriginal(Entry(workspace, "shot.png")).PixelsEqual(original));
    }

    [Theory]
    [InlineData("shot.png", "shot-orig.png")]
    [InlineData("shot.jpg", "shot-orig.jpg")]
    public void An_existing_backup_is_left_alone_and_saving_again_still_renders_from_the_true_original(
        string originalName, string backupName)
    {
        using var folder = new TempFolder();
        var originalPath = WriteOriginal(folder, originalName);
        var original = ImageFile.Load(originalPath);
        var backupPath = folder.File(backupName);
        EncodedImages.Write(backupPath, EncodedImages.HalfAndHalf(10, 10, SKColors.Green, SKColors.Yellow));
        var backupBytes = File.ReadAllBytes(backupPath);
        using var workspace = new Workspace(Backup);
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, originalName), Cropped);

        var first = workspace.Save(Entry(workspace, originalName));
        workspace.SetSteps(Entry(workspace, "shot.png"), Recropped);
        workspace.Save(Entry(workspace, "shot.png"));

        Assert.Equal(backupPath, first.ExistingBackup);
        Assert.Equal(backupBytes, File.ReadAllBytes(backupPath));
        var resultPath = folder.File("shot.png");
        Assert.True(ImageFile.Load(resultPath).PixelsEqual(Renderer.Render(original, Recropped).Result));
        Assert.True(workspace.LoadOriginal(Entry(workspace, "shot.png")).PixelsEqual(original));
        Assert.False(Entry(workspace, "shot.png").HasUnsavedSteps);
    }

    [Fact]
    public void Opening_afresh_renders_from_the_listed_file_again()
    {
        using var folder = new TempFolder();
        var originalPath = WriteOriginal(folder, "shot.png");
        using var workspace = new Workspace(Backup);
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "shot.png"), Cropped);
        workspace.Save(Entry(workspace, "shot.png"));

        workspace.Open([folder.Path]);

        Assert.True(workspace.LoadOriginal(Entry(workspace, "shot.png")).PixelsEqual(ImageFile.Load(originalPath)));
    }

    [Fact]
    public async Task The_entry_keeps_its_steps_once_the_folder_watcher_sees_its_own_backup_rename()
    {
        using var folder = new TempFolder();
        var originalPath = WriteOriginal(folder, "shot.png");
        var otherPath = WriteOriginal(folder, "other.png");
        using var workspace = new Workspace(Backup, TimeSpan.FromMilliseconds(150));
        workspace.Open([originalPath, otherPath]);
        workspace.SetSteps(Entry(workspace, "shot.png"), Cropped);
        var unsaved = new List<string>();
        workspace.Changed += (_, change) => unsaved.AddRange(
            change.Images.Where(i => i.HasUnsavedSteps).Select(i => Path.GetFileName(i.Path)));

        workspace.Save(Entry(workspace, "shot.png"));
        await Task.Delay(TimeSpan.FromSeconds(1));

        Assert.Equal(["other.png", "shot.png"], workspace.Images.Select(i => Path.GetFileName(i.Path)));
        Assert.Equal(Cropped, workspace.Steps(Entry(workspace, "shot.png")));
        Assert.False(Entry(workspace, "shot.png").HasUnsavedSteps);
        Assert.Empty(unsaved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_entry_whose_result_takes_a_new_name_follows_it_with_its_steps(bool openTheFilesOnly)
    {
        using var folder = new TempFolder();
        var originalPath = WriteOriginal(folder, "shot.jpg");
        var otherPath = WriteOriginal(folder, "other.png");
        var original = ImageFile.Load(originalPath);
        using var workspace = new Workspace(Backup);
        workspace.Open(openTheFilesOnly ? [originalPath, otherPath] : [folder.Path]);
        workspace.Select(Entry(workspace, "shot.jpg"));
        workspace.SetSteps(Entry(workspace, "shot.jpg"), Cropped);

        var report = workspace.Save(Entry(workspace, "shot.jpg"));

        Assert.Equal(["other.png", "shot-orig.jpg", "shot.png"], folder.Entries());
        Assert.Equal([folder.File("shot.png")], report.Written);
        var saved = Entry(workspace, "shot.png");
        Assert.Equal(saved, workspace.Selected);
        Assert.Equal(Cropped, workspace.Steps(saved));
        Assert.False(saved.HasUnsavedSteps);
        Assert.DoesNotContain(workspace.Images, i => Path.GetFileName(i.Path) == "shot.jpg");

        workspace.SetSteps(saved, Recropped);
        workspace.Save(Entry(workspace, "shot.png"));

        Assert.Equal(["other.png", "shot-orig.jpg", "shot.png"], folder.Entries());
        Assert.True(ImageFile.Load(folder.File("shot.png")).PixelsEqual(Renderer.Render(original, Recropped).Result));
        Assert.False(Entry(workspace, "shot.png").HasUnsavedSteps);
    }
}
