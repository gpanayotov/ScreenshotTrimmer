using System.Globalization;
using System.Reflection;
using ScreenshotTrimmer.App.Localization;

namespace ScreenshotTrimmer.App;

/// <summary>What Help › About shows: the app's name, the running version, the licence and the repository link.</summary>
public sealed record About(string Title, string Name, string Version, string Licence, string Link)
{
    public const string AppName = "ScreenshotTrimmer";
    public const string RepositoryUrl = "https://github.com/gpanayotov/ScreenshotTrimmer";

    /// <summary>The running app's version, as <see cref="VersionOf"/> reads it.</summary>
    public static string CurrentVersion { get; } = VersionOf(typeof(About).Assembly);

    /// <summary>
    /// The version <paramref name="assembly"/> was built as: its informational version, which a build sets to the
    /// release or development version, or else its assembly version.
    /// </summary>
    public static string VersionOf(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational;
        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    /// <summary>The About texts for <paramref name="version"/> in the language of <paramref name="culture"/>.</summary>
    public static About Describe(CultureInfo culture, string version)
    {
        string Text(string key) => Strings.ResourceManager.GetString(key, culture) ?? $"!{key}!";

        return new About(
            string.Format(culture, Text("AboutTitle"), AppName),
            AppName,
            string.Format(culture, Text("AboutVersion"), version),
            Text("AboutLicence"),
            RepositoryUrl);
    }
}
