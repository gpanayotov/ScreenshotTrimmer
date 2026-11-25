using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenshotTrimmer.Processing.Files;

namespace ScreenshotTrimmer.Processing.Settings;

/// <summary>
/// Loads and saves <see cref="UserSettings"/> as one JSON file in a configuration folder.
/// Loading never fails: a missing or damaged file gives the defaults, unknown fields are ignored,
/// and each field that is missing, of the wrong type or outside its allowed range takes its default.
/// </summary>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";

    /// <summary>The subfolder of the per-user application-data folder the settings live in.</summary>
    public const string AppFolderName = "ScreenshotTrimmer";

    public const double MinShadowSize = 0.0;   // exclusive
    public const double MaxShadowSize = 4.0;   // inclusive

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    /// <param name="folder">The folder holding the settings file; created on the first save.</param>
    public SettingsStore(string folder)
    {
        FilePath = Path.Combine(folder, FileName);
    }

    /// <summary>
    /// The store in the operating system's per-user application-data folder
    /// (<c>%APPDATA%</c> on Windows, <c>~/.config</c> on Linux), in a subfolder named after the app.
    /// </summary>
    public static SettingsStore ForCurrentUser() =>
        new(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            AppFolderName));

    /// <summary>The settings file's full path.</summary>
    public string FilePath { get; }

    public UserSettings Load()
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? Read(document.RootElement)
                : new UserSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new UserSettings();
        }
    }

    /// <summary>Writes the settings atomically, creating the folder if needed.</summary>
    /// <exception cref="ArgumentException">A suffix is not a valid <see cref="FileSuffix"/>. Nothing is written.</exception>
    public void Save(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        FileSuffix.Require(settings.ResultSuffix, nameof(settings));
        FileSuffix.Require(settings.BackupSuffix, nameof(settings));
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        AtomicFile.Write(FilePath, stream => JsonSerializer.Serialize(stream, settings, JsonOptions));
    }

    private static UserSettings Read(JsonElement root)
    {
        var defaults = new UserSettings();
        return new UserSettings
        {
            SaveMode = Enum(root, "saveMode", defaults.SaveMode),
            ResultSuffix = Suffix(root, "resultSuffix", defaults.ResultSuffix),
            BackupSuffix = Suffix(root, "backupSuffix", defaults.BackupSuffix),
            ShadowSize = Number(root, "shadowSize", defaults.ShadowSize, v => v > MinShadowSize && v <= MaxShadowSize),
            ShadowOpacity = Number(root, "shadowOpacity", defaults.ShadowOpacity, v => v >= 0 && v <= 1),
            FrameLines = Bool(root, "frameLines", defaults.FrameLines),
            RoundedCorners = Bool(root, "roundedCorners", defaults.RoundedCorners),
            Language = Enum(root, "language", defaults.Language),
        };
    }

    /// <summary>Matches the camel-case name the file is saved with, exactly; numbers are not accepted.</summary>
    private static T Enum<T>(JsonElement root, string name, T fallback) where T : struct, Enum
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return fallback;
        var text = value.GetString();
        foreach (var candidate in System.Enum.GetValues<T>())
        {
            if (JsonNamingPolicy.CamelCase.ConvertName(candidate.ToString()) == text)
                return candidate;
        }
        return fallback;
    }

    private static string Suffix(JsonElement root, string name, string fallback) =>
        root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } suffix
        && FileSuffix.IsValid(suffix)
            ? suffix
            : fallback;

    private static double Number(JsonElement root, string name, double fallback, Func<double, bool> allowed) =>
        root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
        && allowed(number)
            ? number
            : fallback;

    private static bool Bool(JsonElement root, string name, bool fallback) =>
        root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
}
