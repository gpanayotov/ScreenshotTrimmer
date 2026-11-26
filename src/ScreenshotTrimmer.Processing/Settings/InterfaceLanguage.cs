using System.Globalization;

namespace ScreenshotTrimmer.Processing.Settings;

/// <summary>Decides which language the interface is shown in.</summary>
public static class InterfaceLanguage
{
    /// <summary>
    /// <see cref="Language.Bg"/> when <paramref name="setting"/> is Bulgarian, or when it is
    /// <see cref="Language.System"/> and <paramref name="systemCulture"/>'s language is Bulgarian;
    /// <see cref="Language.En"/> otherwise. Never returns <see cref="Language.System"/>.
    /// </summary>
    /// <param name="systemCulture">The operating system's interface culture, as it was when the app started.</param>
    public static Language Resolve(Language setting, CultureInfo systemCulture)
    {
        ArgumentNullException.ThrowIfNull(systemCulture);
        return setting == Language.Bg
            || (setting == Language.System && systemCulture.TwoLetterISOLanguageName == "bg")
                ? Language.Bg
                : Language.En;
    }
}
