using System.Text.Json;

namespace ScreenshotTrimmer.Processing.Settings;

/// <summary>
/// Loads and saves the <see cref="WindowPlacement"/> as a small JSON file beside the settings file.
/// </summary>
public sealed class WindowPlacementStore
{
    public const string FileName = "window.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <param name="folder">The folder holding the file; created on the first save.</param>
    public WindowPlacementStore(string folder)
    {
        FilePath = Path.Combine(folder, FileName);
    }

    /// <summary>The store in the same per-user folder as <see cref="SettingsStore.ForCurrentUser"/>.</summary>
    public static WindowPlacementStore ForCurrentUser() =>
        new(SettingsStore.CurrentUserFolder);

    /// <summary>The window placement file's full path.</summary>
    public string FilePath { get; }

    /// <summary>
    /// The saved placement, or <c>null</c> when there is none to use: the file is missing or damaged, or any field is
    /// missing, of the wrong type or out of range. Unknown fields are ignored. Never throws for a bad file.
    /// </summary>
    public WindowPlacement? Load() => JsonFile.Read(FilePath, Read);

    /// <summary>Writes the placement atomically, creating the folder if needed.</summary>
    public void Save(WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        JsonFile.Write(FilePath, placement, JsonOptions);
    }

    private static WindowPlacement? Read(JsonElement root) =>
        Integer(root, "x") is { } x
        && Integer(root, "y") is { } y
        && Size(root, "width") is { } width
        && Size(root, "height") is { } height
        && JsonFile.Bool(root, "maximized") is { } maximized
        && Size(root, "stripWidth") is { } stripWidth
            ? new WindowPlacement
            {
                X = x, Y = y, Width = width, Height = height, Maximized = maximized, StripWidth = stripWidth,
            }
            : null;

    private static int? Integer(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    /// <summary>A finite, positive number.</summary>
    private static double? Size(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
        && double.IsFinite(number)
        && number > 0
            ? number
            : null;
}
