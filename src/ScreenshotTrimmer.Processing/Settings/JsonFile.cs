using System.Text.Json;
using ScreenshotTrimmer.Processing.Files;

namespace ScreenshotTrimmer.Processing.Settings;

/// <summary>The small JSON files the settings stores keep: read leniently, written atomically.</summary>
internal static class JsonFile
{
    /// <summary>
    /// What <paramref name="read"/> makes of the file's root object; <c>null</c> when the file is missing, unreadable,
    /// not JSON or not an object. Never throws for a bad file.
    /// </summary>
    public static T? Read<T>(string path, Func<JsonElement, T?> read)
        where T : class
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.ValueKind == JsonValueKind.Object ? read(document.RootElement) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Writes <paramref name="value"/> atomically, creating the folder if needed.</summary>
    public static void Write<T>(string path, T value, JsonSerializerOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.Write(path, stream => JsonSerializer.Serialize(stream, value, options));
    }

    /// <summary>The boolean field <paramref name="name"/>, or <c>null</c> when it is missing or not a boolean.</summary>
    public static bool? Bool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}
