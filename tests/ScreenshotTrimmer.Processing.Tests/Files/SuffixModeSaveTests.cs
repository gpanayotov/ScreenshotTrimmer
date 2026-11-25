using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using SkiaSharp;

namespace ScreenshotTrimmer.Processing.Tests.Files;

public class SuffixModeSaveTests
{
    [Fact]
    public void Result_is_written_beside_the_original_as_name_trimmed_png()
    {
        using var folder = new TempFolder();
        var originalPath = folder.File("shot.png");
        EncodedImages.Write(originalPath, EncodedImages.HalfAndHalf(40, 30, SKColors.Red, SKColors.Blue));
        var originalBytes = File.ReadAllBytes(originalPath);
        var result = Renderer.Render(ImageFile.Load(originalPath), new Steps { Crop = Crop.Classic(new PixelRect(0, 0, 10, 5)) }).Result;

        var report = ResultFiles.Save(result, originalPath, new SaveOptions());

        var resultPath = folder.File("shot-trimmed.png");
        Assert.Equal([resultPath], report.Written);
        Assert.Equal(["shot-trimmed.png", "shot.png"], folder.Entries());
        Assert.Equal(originalBytes, File.ReadAllBytes(originalPath));
        var saved = ImageFile.Load(resultPath);
        Assert.True(saved.PixelsEqual(result));
    }

    [Fact]
    public void Result_of_a_jpeg_original_is_saved_as_png()
    {
        using var folder = new TempFolder();
        var originalPath = folder.File("photo.JPG");
        EncodedImages.Write(originalPath, EncodedImages.HalfAndHalf(8, 8, SKColors.Red, SKColors.Blue));

        var report = ResultFiles.Save(Synthetic.Gradient(4, 4), originalPath, new SaveOptions());

        Assert.Equal([folder.File("photo-trimmed.png")], report.Written);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], File.ReadAllBytes(report.Written[0])[..4]);
    }

    [Fact]
    public void Saving_again_replaces_the_earlier_result()
    {
        using var folder = new TempFolder();
        var originalPath = folder.File("shot.png");
        EncodedImages.Write(originalPath, EncodedImages.HalfAndHalf(8, 8, SKColors.Red, SKColors.Blue));
        ResultFiles.Save(Synthetic.Gradient(4, 4), originalPath, new SaveOptions());
        var second = Synthetic.Gradient(6, 3);

        ResultFiles.Save(second, originalPath, new SaveOptions());

        Assert.Equal(["shot-trimmed.png", "shot.png"], folder.Entries());
        Assert.True(ImageFile.Load(folder.File("shot-trimmed.png")).PixelsEqual(second));
    }

    [Fact]
    public void The_suffix_is_configurable()
    {
        using var folder = new TempFolder();
        var originalPath = folder.File("shot.png");
        EncodedImages.Write(originalPath, EncodedImages.HalfAndHalf(8, 8, SKColors.Red, SKColors.Blue));

        var report = ResultFiles.Save(Synthetic.Gradient(4, 4), originalPath, new SaveOptions { ResultSuffix = "_done" });

        Assert.Equal([folder.File("shot_done.png")], report.Written);
    }

    [Fact]
    public void A_failed_write_leaves_no_file_behind_under_the_target_name_or_otherwise()
    {
        using var folder = new TempFolder();
        var originalPath = folder.File("shot.png");
        EncodedImages.Write(originalPath, EncodedImages.HalfAndHalf(8, 8, SKColors.Red, SKColors.Blue));
        // A folder squatting on the target name makes the final rename fail on every platform.
        Directory.CreateDirectory(folder.File("shot-trimmed.png"));

        var failure = Assert.Throws<SaveFailedException>(
            () => ResultFiles.Save(Synthetic.Gradient(4, 4), originalPath, new SaveOptions()));

        Assert.Equal(folder.File("shot-trimmed.png"), failure.TargetPath);
        Assert.Equal(SaveFailureReason.TargetIsFolder, failure.Reason);
        Assert.Equal(["shot-trimmed.png", "shot.png"], folder.Entries());
        Assert.True(Directory.Exists(folder.File("shot-trimmed.png")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("x?")]
    [InlineData("tab\t")]
    public void An_invalid_result_suffix_fails_clearly_and_writes_nothing(string suffix)
    {
        using var folder = new TempFolder();
        var originalPath = folder.File("shot.png");
        EncodedImages.Write(originalPath, EncodedImages.HalfAndHalf(8, 8, SKColors.Red, SKColors.Blue));
        var originalBytes = File.ReadAllBytes(originalPath);

        var failure = Assert.Throws<ArgumentException>(
            () => ResultFiles.Save(Synthetic.Gradient(4, 4), originalPath, new SaveOptions { ResultSuffix = suffix }));

        Assert.Contains("suffix", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["shot.png"], folder.Entries());
        Assert.Equal(originalBytes, File.ReadAllBytes(originalPath));
    }
}
