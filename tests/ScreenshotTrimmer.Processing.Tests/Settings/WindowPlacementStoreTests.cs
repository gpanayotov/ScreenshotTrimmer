using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Tests.Settings;

public class WindowPlacementStoreTests
{
    private static readonly WindowPlacement Sample = new()
    {
        X = -1200, Y = 85, Width = 1024.5, Height = 700, Maximized = true, StripWidth = 310.25,
    };

    [Fact]
    public void Saving_and_loading_round_trips_every_field()
    {
        using var folder = new TempFolder();

        new WindowPlacementStore(folder.File("ScreenshotTrimmer")).Save(Sample);
        var loaded = new WindowPlacementStore(folder.File("ScreenshotTrimmer")).Load();

        Assert.Equal(Sample, loaded);
    }

    [Fact]
    public void A_missing_file_gives_no_placement_so_the_window_opens_in_its_default_layout()
    {
        using var folder = new TempFolder();

        Assert.Null(new WindowPlacementStore(folder.File("ScreenshotTrimmer")).Load());
    }

    [Theory]
    [InlineData("{ \"x\": 10, ")]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("[1, 2, 3]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{ "x": 10, "y": 20, "width": 800, "height": 600, "maximized": false }""")]
    [InlineData("""{ "x": 10, "y": 20, "width": "wide", "height": 600, "maximized": false, "stripWidth": 240 }""")]
    [InlineData("""{ "x": 10.5, "y": 20, "width": 800, "height": 600, "maximized": false, "stripWidth": 240 }""")]
    [InlineData("""{ "x": 10, "y": 20, "width": 0, "height": 600, "maximized": false, "stripWidth": 240 }""")]
    [InlineData("""{ "x": 10, "y": 20, "width": 800, "height": -5, "maximized": false, "stripWidth": 240 }""")]
    [InlineData("""{ "x": 10, "y": 20, "width": 800, "height": 600, "maximized": "no", "stripWidth": 240 }""")]
    [InlineData("""{ "x": 10, "y": 20, "width": 800, "height": 600, "maximized": false, "stripWidth": 0 }""")]
    [InlineData("""{ "x": 10, "y": 20, "width": 1e400, "height": 600, "maximized": false, "stripWidth": 240 }""")]
    public void A_damaged_file_gives_no_placement(string content)
    {
        using var folder = new TempFolder();
        var store = new WindowPlacementStore(folder.Path);
        File.WriteAllText(store.FilePath, content);

        Assert.Null(store.Load());
    }

    [Fact]
    public void Unknown_fields_are_ignored()
    {
        using var folder = new TempFolder();
        var store = new WindowPlacementStore(folder.Path);
        File.WriteAllText(store.FilePath, """
            { "x": 10, "y": 20, "width": 800, "height": 600, "maximized": false, "stripWidth": 240, "panel": { "open": true } }
            """);

        Assert.Equal(
            new WindowPlacement { X = 10, Y = 20, Width = 800, Height = 600, Maximized = false, StripWidth = 240 },
            store.Load());
    }

    [Fact]
    public void Saving_again_replaces_the_file_and_leaves_nothing_else_in_the_folder()
    {
        using var folder = new TempFolder();
        var store = new WindowPlacementStore(folder.Path);
        store.Save(Sample);

        store.Save(Sample with { Maximized = false });

        Assert.Equal(["window.json"], folder.Entries());
        Assert.False(store.Load()!.Maximized);
    }

    [Fact]
    public void The_current_users_placement_lives_beside_the_settings_file()
    {
        var store = WindowPlacementStore.ForCurrentUser();

        Assert.Equal(
            Path.GetDirectoryName(SettingsStore.ForCurrentUser().FilePath),
            Path.GetDirectoryName(store.FilePath));
    }
}
