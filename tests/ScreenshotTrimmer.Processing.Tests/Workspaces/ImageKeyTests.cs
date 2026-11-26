using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class ImageKeyTests
{
    [Fact]
    public void Keys_of_the_same_file_are_equal_and_hash_alike()
    {
        var path = Path.Combine(Path.GetTempPath(), "shot.png");

        Assert.Equal(ImageKey.OfFile(path), ImageKey.OfFile(path));
        Assert.Equal(ImageKey.OfFile(path).GetHashCode(), ImageKey.OfFile(path).GetHashCode());
    }

    [Fact]
    public void Keys_of_files_compare_as_the_file_system_does()
    {
        var path = Path.Combine(Path.GetTempPath(), "shot.png");
        var otherCase = Path.Combine(Path.GetTempPath(), "SHOT.png");

        Assert.Equal(FileNames.Same(path, otherCase), ImageKey.OfFile(path) == ImageKey.OfFile(otherCase));
    }

    [Fact]
    public void A_Pasted_image_key_never_equals_a_file_key_nor_another_Pasted_image_key()
    {
        var pasted = ImageKey.OfPasted(1);

        Assert.Equal(ImageKey.OfPasted(1), pasted);
        Assert.NotEqual(ImageKey.OfPasted(2), pasted);
        Assert.NotEqual(ImageKey.OfFile(Path.Combine(Path.GetTempPath(), "1")), pasted);
    }

    [Fact]
    public void Renaming_moves_a_file_key_and_leaves_a_Pasted_image_key()
    {
        var from = Path.Combine(Path.GetTempPath(), "a.png");
        var to = Path.Combine(Path.GetTempPath(), "b.png");
        string Renamed(string path) => FileNames.Same(path, from) ? to : path;

        Assert.Equal(ImageKey.OfFile(to), ImageKey.OfFile(from).Renamed(Renamed));
        Assert.Equal(ImageKey.OfPasted(3), ImageKey.OfPasted(3).Renamed(Renamed));
    }
}
