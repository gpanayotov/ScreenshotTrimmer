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
    public void The_Already_cropped_notice_is_in_English_and_Bulgarian()
    {
        Assert.StartsWith("Вече изрязано", Localizer.Text("AlreadyCropped", CultureInfo.GetCultureInfo("bg")));
        Assert.StartsWith("Already cropped", Localizer.Text("AlreadyCropped", CultureInfo.GetCultureInfo("en")));
    }

    [Fact]
    public void A_missing_string_shows_its_key_between_exclamation_marks()
    {
        Assert.Equal("!NoSuchKey!", Localizer.Text("NoSuchKey", CultureInfo.GetCultureInfo("bg")));
    }
}
