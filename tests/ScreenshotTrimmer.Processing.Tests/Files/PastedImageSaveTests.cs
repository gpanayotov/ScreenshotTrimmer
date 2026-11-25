using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests.Files;

public class PastedImageSaveTests
{
    [Fact]
    public void Result_is_written_as_png_to_exactly_the_given_path_and_nothing_else()
    {
        using var folder = new TempFolder();
        var targetPath = folder.File("pasted.png");
        var result = Synthetic.Gradient(5, 3);

        var report = ResultFiles.SavePasted(result, targetPath, overwrite: false);

        Assert.Equal(["pasted.png"], folder.Entries());
        Assert.True(ImageFile.Load(targetPath).PixelsEqual(result));
        Assert.Equal([targetPath], report.Written);
        Assert.Empty(report.Moved);
    }

    [Fact]
    public void An_existing_file_is_kept_and_reported_when_overwriting_is_not_allowed()
    {
        using var folder = new TempFolder();
        var targetPath = folder.File("pasted.png");
        byte[] existing = [1, 2, 3];
        File.WriteAllBytes(targetPath, existing);

        var failure = Assert.Throws<SaveFailedException>(
            () => ResultFiles.SavePasted(Synthetic.Gradient(5, 3), targetPath, overwrite: false));

        Assert.Equal(targetPath, failure.TargetPath);
        Assert.Equal(SaveFailureReason.TargetExists, failure.Reason);
        Assert.Equal(["pasted.png"], folder.Entries());
        Assert.Equal(existing, File.ReadAllBytes(targetPath));
    }

    [Fact]
    public void An_existing_file_is_replaced_when_overwriting_is_allowed()
    {
        using var folder = new TempFolder();
        var targetPath = folder.File("pasted.png");
        File.WriteAllBytes(targetPath, [1, 2, 3]);
        var result = Synthetic.Gradient(5, 3);

        var report = ResultFiles.SavePasted(result, targetPath, overwrite: true);

        Assert.Equal(["pasted.png"], folder.Entries());
        Assert.True(ImageFile.Load(targetPath).PixelsEqual(result));
        Assert.Equal([targetPath], report.Written);
    }

    [Fact]
    public void A_target_in_a_missing_folder_is_reported_as_a_failure_and_nothing_is_created()
    {
        using var folder = new TempFolder();
        var targetPath = Path.Combine(folder.File("missing"), "pasted.png");

        var failure = Assert.Throws<SaveFailedException>(
            () => ResultFiles.SavePasted(Synthetic.Gradient(5, 3), targetPath, overwrite: false));

        Assert.Equal(targetPath, failure.TargetPath);
        Assert.Equal(SaveFailureReason.FolderNotFound, failure.Reason);
        Assert.Contains(targetPath, failure.Message);
        Assert.Empty(folder.Entries());
    }
}
