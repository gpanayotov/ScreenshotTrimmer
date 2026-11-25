using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using SkiaSharp;

namespace ScreenshotTrimmer.Processing.Tests.Files;

public class BackupModeSaveTests
{
    private static readonly SaveOptions Backup = new() { Mode = SaveMode.Backup };

    [Fact]
    public void Png_original_is_moved_to_name_original_png_and_the_result_takes_its_name()
    {
        using var folder = new TempFolder();
        var originalPath = folder.File("shot.png");
        EncodedImages.Write(originalPath, EncodedImages.HalfAndHalf(8, 8, SKColors.Red, SKColors.Blue));
        var originalBytes = File.ReadAllBytes(originalPath);
        var result = Synthetic.Gradient(4, 4);

        var report = ResultFiles.Save(result, originalPath, Backup);

        Assert.Equal(["shot-original.png", "shot.png"], folder.Entries());
        Assert.Equal(originalBytes, File.ReadAllBytes(folder.File("shot-original.png")));
        Assert.True(ImageFile.Load(folder.File("shot.png")).PixelsEqual(result));
        Assert.Equal([folder.File("shot.png")], report.Written);
        Assert.Equal([new FileMove(originalPath, folder.File("shot-original.png"))], report.Moved);
        Assert.Null(report.ExistingBackup);
    }

    [Fact]
    public void Jpeg_original_is_moved_to_name_original_jpg_beside_the_new_png_with_no_name_jpg_left()
    {
        using var folder = new TempFolder();
        var originalPath = folder.File("photo.jpg");
        EncodedImages.Write(originalPath, EncodedImages.HalfAndHalf(8, 8, SKColors.Red, SKColors.Blue));
        var originalBytes = File.ReadAllBytes(originalPath);
        var result = Synthetic.Gradient(4, 4);

        var report = ResultFiles.Save(result, originalPath, Backup);

        Assert.Equal(["photo-original.jpg", "photo.png"], folder.Entries());
        Assert.Equal(originalBytes, File.ReadAllBytes(folder.File("photo-original.jpg")));
        Assert.True(ImageFile.Load(folder.File("photo.png")).PixelsEqual(result));
        Assert.Equal([folder.File("photo.png")], report.Written);
        Assert.Equal([new FileMove(originalPath, folder.File("photo-original.jpg"))], report.Moved);
    }

    [Fact]
    public void The_backup_suffix_is_configurable()
    {
        using var folder = new TempFolder();
        var originalPath = folder.File("shot.png");
        EncodedImages.Write(originalPath, EncodedImages.HalfAndHalf(8, 8, SKColors.Red, SKColors.Blue));

        ResultFiles.Save(Synthetic.Gradient(4, 4), originalPath, Backup with { BackupSuffix = ".orig" });

        Assert.Equal(["shot.orig.png", "shot.png"], folder.Entries());
    }

    [Fact]
    public void An_existing_backup_and_the_original_are_left_untouched_and_the_result_is_still_written()
    {
        using var folder = new TempFolder();
        var originalPath = folder.File("photo.jpg");
        var backupPath = folder.File("photo-original.jpg");
        EncodedImages.Write(originalPath, EncodedImages.HalfAndHalf(8, 8, SKColors.Red, SKColors.Blue));
        EncodedImages.Write(backupPath, EncodedImages.HalfAndHalf(8, 8, SKColors.Green, SKColors.White));
        var originalBytes = File.ReadAllBytes(originalPath);
        var backupBytes = File.ReadAllBytes(backupPath);
        var result = Synthetic.Gradient(4, 4);

        var report = ResultFiles.Save(result, originalPath, Backup);

        Assert.Equal(["photo-original.jpg", "photo.jpg", "photo.png"], folder.Entries());
        Assert.Equal(originalBytes, File.ReadAllBytes(originalPath));
        Assert.Equal(backupBytes, File.ReadAllBytes(backupPath));
        Assert.True(ImageFile.Load(folder.File("photo.png")).PixelsEqual(result));
        Assert.Equal([folder.File("photo.png")], report.Written);
        Assert.Empty(report.Moved);
        Assert.Equal(backupPath, report.ExistingBackup);
    }

    [Fact]
    public void A_failed_write_reports_a_reason_and_leaves_the_original_where_it_was()
    {
        using var folder = new TempFolder();
        var originalPath = folder.File("photo.jpg");
        EncodedImages.Write(originalPath, EncodedImages.HalfAndHalf(8, 8, SKColors.Red, SKColors.Blue));
        var originalBytes = File.ReadAllBytes(originalPath);
        // A folder squatting on the Result's name makes the write fail on every platform.
        Directory.CreateDirectory(folder.File("photo.png"));

        var failure = Assert.Throws<SaveFailedException>(
            () => ResultFiles.Save(Synthetic.Gradient(4, 4), originalPath, Backup));

        Assert.Equal(folder.File("photo.png"), failure.TargetPath);
        Assert.Equal(SaveFailureReason.TargetIsFolder, failure.Reason);
        Assert.Equal(["photo.jpg", "photo.png"], folder.Entries());
        Assert.Equal(originalBytes, File.ReadAllBytes(originalPath));
    }

    [UnixNonRootFact]
    public void A_read_only_folder_is_reported_as_access_denied_and_nothing_changes()
    {
        using var folder = new TempFolder();
        var originalPath = folder.File("shot.png");
        EncodedImages.Write(originalPath, EncodedImages.HalfAndHalf(8, 8, SKColors.Red, SKColors.Blue));
        var originalBytes = File.ReadAllBytes(originalPath);
        var writable = File.GetUnixFileMode(folder.Path);
        File.SetUnixFileMode(folder.Path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var failure = Assert.Throws<SaveFailedException>(
                () => ResultFiles.Save(Synthetic.Gradient(4, 4), originalPath, Backup));

            Assert.Equal(folder.File("shot.png"), failure.TargetPath);
            Assert.Equal(SaveFailureReason.AccessDenied, failure.Reason);
            Assert.Equal(["shot.png"], folder.Entries());
            Assert.Equal(originalBytes, File.ReadAllBytes(originalPath));
        }
        finally
        {
            File.SetUnixFileMode(folder.Path, writable);
        }
    }
}
