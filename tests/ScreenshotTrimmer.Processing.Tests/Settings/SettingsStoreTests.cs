using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Tests.Settings;

public class SettingsStoreTests
{
    [Fact]
    public void A_missing_file_gives_the_defaults()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.File("ScreenshotTrimmer"));

        var settings = store.Load();

        Assert.Equal(SaveMode.Suffix, settings.SaveMode);
        Assert.Equal("-trimmed", settings.ResultSuffix);
        Assert.Equal("-original", settings.BackupSuffix);
        Assert.Equal(1.0, settings.ShadowSize);
        Assert.Equal(0.35, settings.ShadowOpacity);
        Assert.True(settings.FrameLines);
        Assert.True(settings.RoundedCorners);
        Assert.Equal(Language.System, settings.Language);
    }

    [Fact]
    public void Saving_and_loading_round_trips_every_field()
    {
        using var folder = new TempFolder();
        var saved = new UserSettings
        {
            SaveMode = SaveMode.Backup,
            ResultSuffix = "_done",
            BackupSuffix = "_orig",
            ShadowSize = 1.75,
            ShadowOpacity = 0.6,
            FrameLines = false,
            RoundedCorners = false,
            Language = Language.Bg,
        };

        new SettingsStore(folder.File("ScreenshotTrimmer")).Save(saved);
        var loaded = new SettingsStore(folder.File("ScreenshotTrimmer")).Load();

        Assert.Equal(saved, loaded);
    }

    [Theory]
    [InlineData("{ \"saveMode\": \"backup\", ")]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("[1, 2, 3]")]
    [InlineData("null")]
    public void A_damaged_file_gives_the_defaults(string content)
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);
        File.WriteAllText(store.FilePath, content);

        Assert.Equal(new UserSettings(), store.Load());
    }

    [Fact]
    public void Unknown_fields_are_ignored_and_known_ones_kept()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);
        File.WriteAllText(store.FilePath, """
            {
              "saveMode": "backup",
              "theme": "dark",
              "autoCrop": { "enabled": true, "margins": [1, 2] },
              "shadowOpacity": 0.5,
              "language": "en"
            }
            """);

        var settings = store.Load();

        Assert.Equal(new UserSettings { SaveMode = SaveMode.Backup, ShadowOpacity = 0.5, Language = Language.En }, settings);
    }

    [Theory]
    [InlineData("\"saveMode\": \"sideways\"")]
    [InlineData("\"saveMode\": 1")]
    [InlineData("\"resultSuffix\": \"\"")]
    [InlineData("\"resultSuffix\": \"a/b\"")]
    [InlineData("\"resultSuffix\": \"a\\\\b\"")]
    [InlineData("\"resultSuffix\": \"x?\"")]
    [InlineData("\"backupSuffix\": \"\"")]
    [InlineData("\"backupSuffix\": 7")]
    [InlineData("\"shadowSize\": 0")]
    [InlineData("\"shadowSize\": -1")]
    [InlineData("\"shadowSize\": 100")]
    [InlineData("\"shadowSize\": \"big\"")]
    [InlineData("\"shadowOpacity\": -0.1")]
    [InlineData("\"shadowOpacity\": 1.5")]
    [InlineData("\"frameLines\": \"yes\"")]
    [InlineData("\"roundedCorners\": 0")]
    [InlineData("\"language\": \"fr\"")]
    [InlineData("\"language\": null")]
    public void A_value_outside_its_allowed_range_is_reset_to_the_default_and_the_rest_kept(string badField)
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);
        var custom = new UserSettings
        {
            SaveMode = SaveMode.Backup,
            ResultSuffix = "_done",
            BackupSuffix = "_orig",
            ShadowSize = 2.0,
            ShadowOpacity = 0.8,
            FrameLines = false,
            RoundedCorners = false,
            Language = Language.Bg,
        };
        store.Save(custom);
        var name = badField[1..badField.IndexOf('"', 1)];
        var lines = File.ReadAllLines(store.FilePath)
            .Select(line => line.TrimStart().StartsWith($"\"{name}\"") ? $"  {badField}{(line.TrimEnd().EndsWith(',') ? "," : "")}" : line);
        File.WriteAllLines(store.FilePath, lines);

        var loaded = store.Load();

        var expected = name switch
        {
            "saveMode" => custom with { SaveMode = SaveMode.Suffix },
            "resultSuffix" => custom with { ResultSuffix = "-trimmed" },
            "backupSuffix" => custom with { BackupSuffix = "-original" },
            "shadowSize" => custom with { ShadowSize = 1.0 },
            "shadowOpacity" => custom with { ShadowOpacity = 0.35 },
            "frameLines" => custom with { FrameLines = true },
            "roundedCorners" => custom with { RoundedCorners = true },
            "language" => custom with { Language = Language.System },
            _ => throw new ArgumentException(name),
        };
        Assert.Equal(expected, loaded);
    }

    [Fact]
    public void Values_at_the_edges_of_their_ranges_are_kept()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);
        File.WriteAllText(store.FilePath, """{ "shadowSize": 4, "shadowOpacity": 0 }""");

        var settings = store.Load();

        Assert.Equal(4.0, settings.ShadowSize);
        Assert.Equal(0.0, settings.ShadowOpacity);
    }

    [Fact]
    public void Saving_again_replaces_the_file_and_leaves_nothing_else_in_the_folder()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);
        store.Save(new UserSettings { Language = Language.En });

        store.Save(new UserSettings { Language = Language.Bg });

        Assert.Equal(["settings.json"], folder.Entries());
        Assert.Equal(Language.Bg, store.Load().Language);
    }

    [Fact]
    public void A_failed_save_leaves_no_file_behind()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);
        // A folder squatting on the file name makes the final rename fail on every platform.
        Directory.CreateDirectory(store.FilePath);

        Assert.ThrowsAny<Exception>(() => store.Save(new UserSettings()));

        Assert.Equal(["settings.json"], folder.Entries());
        Assert.True(Directory.Exists(store.FilePath));
    }

    [Fact]
    public void The_current_users_settings_live_in_an_app_subfolder_of_the_application_data_folder()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);

        var store = SettingsStore.ForCurrentUser();

        Assert.Equal(Path.Combine(appData, "ScreenshotTrimmer", "settings.json"), store.FilePath);
    }
}
