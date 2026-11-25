using ScreenshotTrimmer.Processing.Files;

namespace ScreenshotTrimmer.Processing.Tests.Files;

public class ListingTests
{
    private static void Touch(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
    }

    private static string[] Names(Listing listing) =>
        listing.Images.Select(i => Path.GetFileName(i.Path)).ToArray();

    [Fact]
    public void Opening_a_folder_lists_its_images()
    {
        using var folder = new TempFolder();
        Touch(folder, "b.png", "a.png");

        var listing = ImageListing.List([folder.Path], new ListingOptions());

        Assert.Equal(["a.png", "b.png"], Names(listing));
        Assert.Equal(folder.File("a.png"), listing.Images[0].Path);
        Assert.Null(listing.Selected);
    }

    [Fact]
    public void Opening_one_file_lists_every_image_in_its_folder_with_that_file_selected()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.jpg", "c.png", "notes.txt");

        var listing = ImageListing.List([folder.File("b.jpg")], new ListingOptions());

        Assert.Equal(["a.png", "b.jpg", "c.png"], Names(listing));
        Assert.Same(listing.Images[1], listing.Selected);
        Assert.Equal(folder.File("b.jpg"), listing.Selected!.Path);
    }

    [Fact]
    public void Opening_several_files_lists_only_those_files()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot-1.png", "shot-2.png", "shot-10.png", "other.png", "notes.txt");

        var listing = ImageListing.List(
            [folder.File("shot-10.png"), folder.File("shot-2.png"), folder.File("notes.txt"), folder.File("shot-2.png")],
            new ListingOptions());

        Assert.Equal(["shot-2.png", "shot-10.png"], Names(listing));
        Assert.Null(listing.Selected);
    }

    [Fact]
    public void A_result_is_hidden_by_default_when_its_original_is_listed()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot.jpg", "shot-trimmed.png", "other.png");

        var listing = ImageListing.List([folder.Path], new ListingOptions());

        Assert.Equal(["other.png", "shot.jpg"], Names(listing));
        Assert.All(listing.Images, image => Assert.False(image.IsResult));
    }

    [Fact]
    public void Results_are_listed_and_marked_when_asked_for()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot.jpg", "shot-trimmed.png", "other.png");

        var listing = ImageListing.List([folder.Path], new ListingOptions { IncludeResults = true });

        Assert.Equal(["other.png", "shot.jpg", "shot-trimmed.png"], Names(listing));
        Assert.Equal([false, false, true], listing.Images.Select(i => i.IsResult));
    }

    [Fact]
    public void A_suffixed_file_without_an_original_is_a_normal_image()
    {
        using var folder = new TempFolder();
        Touch(folder, "lonely-trimmed.png", "shot.png");

        var listing = ImageListing.List([folder.Path], new ListingOptions());

        Assert.Equal(["lonely-trimmed.png", "shot.png"], Names(listing));
        Assert.All(listing.Images, image => Assert.False(image.IsResult));
    }

    [Fact]
    public void Among_several_opened_files_a_result_is_hidden_only_when_its_original_is_opened_too()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "a-trimmed.png", "b.png", "b-trimmed.png");

        var listing = ImageListing.List(
            [folder.File("a.png"), folder.File("a-trimmed.png"), folder.File("b-trimmed.png")],
            new ListingOptions());

        Assert.Equal(["a.png", "b-trimmed.png"], Names(listing));
    }

    [Fact]
    public void An_original_is_marked_when_its_result_exists()
    {
        using var folder = new TempFolder();
        Touch(folder, "done.jpg", "done-trimmed.png", "todo.png");

        var hidden = ImageListing.List([folder.Path], new ListingOptions());
        var shown = ImageListing.List([folder.Path], new ListingOptions { IncludeResults = true });

        Assert.Equal([true, false], hidden.Images.Select(i => i.HasResult));
        Assert.Equal([true, false, false], shown.Images.Select(i => i.HasResult));
    }

    [Fact]
    public void An_opened_original_is_marked_even_when_its_result_was_not_opened()
    {
        using var folder = new TempFolder();
        Touch(folder, "done.png", "done-trimmed.png", "todo.png");

        var listing = ImageListing.List([folder.File("done.png"), folder.File("todo.png")], new ListingOptions());

        Assert.Equal(["done.png", "todo.png"], Names(listing));
        Assert.Equal([true, false], listing.Images.Select(i => i.HasResult));
    }

    [Fact]
    public void Opening_a_result_on_its_own_lists_and_selects_it_while_other_results_stay_hidden()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "a-trimmed.png", "b.png", "b-trimmed.png");

        var listing = ImageListing.List([folder.File("b-trimmed.png")], new ListingOptions());

        Assert.Equal(["a.png", "b.png", "b-trimmed.png"], Names(listing));
        Assert.Equal(folder.File("b-trimmed.png"), listing.Selected?.Path);
        Assert.True(listing.Selected!.IsResult);
    }

    [Fact]
    public void The_result_suffix_is_configurable()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot.png", "shot_done.png", "shot-trimmed.png");

        var listing = ImageListing.List([folder.Path], new ListingOptions { ResultSuffix = "_done" });

        Assert.Equal(["shot.png", "shot-trimmed.png"], Names(listing));
    }

    [Fact]
    public void Outside_suffix_mode_no_file_is_a_result()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot.png", "shot-trimmed.png");

        var listing = ImageListing.List([folder.Path], new ListingOptions { ResultSuffix = null });

        Assert.Equal(["shot.png", "shot-trimmed.png"], Names(listing));
        Assert.All(listing.Images, image => Assert.False(image.IsResult));
    }

    [Fact]
    public void Only_png_jpeg_webp_and_bmp_are_listed_in_any_letter_case()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png", "b.JPG", "c.jpeg", "d.Webp", "e.BMP", "f.gif", "g.txt", "h", "i.png.bak", "j.JPEG");

        var listing = ImageListing.List([folder.Path], new ListingOptions());

        Assert.Equal(["a.png", "b.JPG", "c.jpeg", "d.Webp", "e.BMP", "j.JPEG"], Names(listing));
    }

    [Fact]
    public void Images_are_sorted_naturally_and_case_insensitively()
    {
        using var folder = new TempFolder();
        Touch(folder, "shot-10.png", "Shot-2.png", "shot-1.png", "b.png", "A.png", "shot-02b.png", "shot-100.png", "a10.png", "a9.png");

        var listing = ImageListing.List([folder.Path], new ListingOptions());

        Assert.Equal(
            ["A.png", "a9.png", "a10.png", "b.png", "shot-1.png", "Shot-2.png", "shot-02b.png", "shot-10.png", "shot-100.png"],
            Names(listing));
    }

    [Fact]
    public void Subfolders_are_ignored()
    {
        using var folder = new TempFolder();
        Touch(folder, "a.png");
        Directory.CreateDirectory(folder.File("sub.png"));
        File.WriteAllBytes(Path.Combine(folder.File("sub.png"), "inner.png"), [0]);

        var listing = ImageListing.List([folder.Path], new ListingOptions());

        Assert.Equal(["a.png"], Names(listing));
    }
}
