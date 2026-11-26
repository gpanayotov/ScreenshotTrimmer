using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;

namespace ScreenshotTrimmer.App.Tests;

public class AboutTests
{
    [Fact]
    public void The_version_is_the_assemblys_informational_version()
    {
        var assembly = AssemblyWith(new Version(1, 2, 3), informationalVersion: "0.0.0-dev+13aa106");

        Assert.Equal("0.0.0-dev+13aa106", About.VersionOf(assembly));
    }

    [Fact]
    public void Without_an_informational_version_the_version_is_the_assembly_version()
    {
        var assembly = AssemblyWith(new Version(2, 5, 0, 0), informationalVersion: null);

        Assert.Equal("2.5.0", About.VersionOf(assembly));
    }

    [Fact]
    public void About_shows_the_name_version_licence_and_link_in_English()
    {
        var about = About.Describe(CultureInfo.GetCultureInfo("en"), "1.2.3-rc.1");

        Assert.Equal("About ScreenshotTrimmer", about.Title);
        Assert.Equal("ScreenshotTrimmer", about.Name);
        Assert.Equal("Version 1.2.3-rc.1", about.Version);
        Assert.Equal("Free software under the MIT License.", about.Licence);
        Assert.Equal("https://github.com/gpanayotov/ScreenshotTrimmer", about.Link);
    }

    [Fact]
    public void About_shows_the_name_version_licence_and_link_in_Bulgarian()
    {
        var about = About.Describe(CultureInfo.GetCultureInfo("bg"), "1.2.3-rc.1");

        Assert.Equal("За ScreenshotTrimmer", about.Title);
        Assert.Equal("ScreenshotTrimmer", about.Name);
        Assert.Equal("Версия 1.2.3-rc.1", about.Version);
        Assert.Equal("Свободен софтуер под лиценза MIT.", about.Licence);
        Assert.Equal("https://github.com/gpanayotov/ScreenshotTrimmer", about.Link);
    }

    private static Assembly AssemblyWith(Version version, string? informationalVersion)
    {
        var name = new AssemblyName($"AboutTests{Guid.NewGuid():N}") { Version = version };
        CustomAttributeBuilder[] attributes = informationalVersion is null
            ? []
            :
            [
                new CustomAttributeBuilder(
                    typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!,
                    [informationalVersion]),
            ];
        return AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run, attributes);
    }
}
