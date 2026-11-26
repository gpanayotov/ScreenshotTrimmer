using System.Globalization;
using ScreenshotTrimmer.App.Localization;

namespace ScreenshotTrimmer.App.Tests;

public class LocalizerTests
{
    [Fact]
    public void A_string_is_looked_up_in_the_given_language()
    {
        Assert.Equal("_Файл", Localizer.Text("FileMenu", CultureInfo.GetCultureInfo("bg")));
        Assert.Equal("_File", Localizer.Text("FileMenu", CultureInfo.GetCultureInfo("en")));
    }

    [Fact]
    public void A_missing_string_shows_its_key_between_exclamation_marks()
    {
        Assert.Equal("!NoSuchKey!", Localizer.Text("NoSuchKey", CultureInfo.GetCultureInfo("bg")));
    }
}
