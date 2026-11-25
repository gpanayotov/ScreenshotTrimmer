using System.Globalization;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Tests.Settings;

public class InterfaceLanguageTests
{
    [Theory]
    [InlineData(Language.System, "bg-BG", Language.Bg)]
    [InlineData(Language.System, "bg", Language.Bg)]
    [InlineData(Language.System, "en-US", Language.En)]
    [InlineData(Language.System, "de-DE", Language.En)]
    [InlineData(Language.System, "", Language.En)]
    [InlineData(Language.En, "bg-BG", Language.En)]
    [InlineData(Language.En, "en-GB", Language.En)]
    [InlineData(Language.En, "fr-FR", Language.En)]
    [InlineData(Language.Bg, "bg-BG", Language.Bg)]
    [InlineData(Language.Bg, "en-US", Language.Bg)]
    [InlineData(Language.Bg, "ru-RU", Language.Bg)]
    public void The_interface_is_Bulgarian_only_when_chosen_or_when_following_a_Bulgarian_system(
        Language setting, string systemCulture, Language expected)
    {
        var resolved = InterfaceLanguage.Resolve(setting, CultureInfo.GetCultureInfo(systemCulture));

        Assert.Equal(expected, resolved);
    }
}
