using System.Globalization;
using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.Processing.SelfChecking;

namespace ScreenshotTrimmer.App;

/// <summary>
/// The hidden <c>--smoke-test</c> switch, for checking a packaged build from the outside: it runs the library's
/// self-check and checks that the Bulgarian resources load, with no window, and reports through the exit code.
/// </summary>
internal static class SmokeTest
{
    public const string Switch = "--smoke-test";

    /// <summary>
    /// A string whose Bulgarian text lives only in the Bulgarian satellite resource assembly, so finding that text
    /// proves the satellite resources were packaged.
    /// </summary>
    private const string SatelliteProbeKey = "FileMenu";
    private const string SatelliteProbeText = "_Файл";

    /// <summary>Runs the checks, prints the app version and the outcome, and returns the exit code: 0 when all passed.</summary>
    public static int Run(TextWriter output)
    {
        output.WriteLine($"{About.AppName} {About.CurrentVersion}");

        var failure = SelfCheck.Run().Failure ?? CheckSatelliteResources();
        output.WriteLine(failure is null ? "Smoke test passed." : $"Smoke test failed. {failure}");
        output.Flush();
        return failure is null ? 0 : 1;
    }

    private static string? CheckSatelliteResources()
    {
        try
        {
            var text = Localizer.Text(SatelliteProbeKey, CultureInfo.GetCultureInfo("bg"));
            return text == SatelliteProbeText
                ? null
                : $"The Bulgarian text of {SatelliteProbeKey} is '{text}', not '{SatelliteProbeText}'.";
        }
        catch (Exception e)
        {
            return $"The Bulgarian resources could not be read: {e.GetType().Name}: {e.Message}";
        }
    }
}
