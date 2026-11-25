using System.Globalization;
using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.App;

/// <summary>The settings in use for this run of the app, where they are kept, and the interface language they give.</summary>
public sealed class SettingsSession
{
    private readonly SettingsStore _store;
    private readonly CultureInfo _systemCulture;

    /// <param name="systemCulture">The system's interface culture, captured before the app sets its own.</param>
    public SettingsSession(SettingsStore store, CultureInfo systemCulture)
    {
        _store = store;
        _systemCulture = systemCulture;
        Current = store.Load();
    }

    public UserSettings Current { get; private set; }

    /// <summary>Shows the interface in the language <see cref="Current"/> resolves to.</summary>
    public void ApplyLanguage()
    {
        var language = InterfaceLanguage.Resolve(Current.Language, _systemCulture);
        Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo(language == Language.Bg ? "bg" : "en"));
    }

    /// <summary>
    /// Makes <paramref name="settings"/> current, switches the language and writes them to the settings file. They stay
    /// current for this session even when the file cannot be written.
    /// </summary>
    /// <returns><c>null</c> when saved, otherwise why the file could not be written.</returns>
    public string? Change(UserSettings settings)
    {
        Current = settings;
        ApplyLanguage();
        try
        {
            _store.Save(settings);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    /// <summary>The settings file's full path.</summary>
    public string FilePath => _store.FilePath;
}
