using System.Globalization;
using System.Reflection;
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

    /// <summary>A Bulgarian string and its text, which only loads when the satellite resources are packaged.</summary>
    private const string BulgarianKey = "FileMenu";
    private const string BulgarianText = "_Файл";

    /// <summary>Runs the checks, prints the app version and the outcome, and returns the exit code: 0 when all passed.</summary>
    public static int Run(TextWriter output)
    {
        output.WriteLine($"ScreenshotTrimmer {Version}");

        var failure = SelfCheck.Run().Failure ?? CheckBulgarian();
        output.WriteLine(failure is null ? "Smoke test passed." : $"Smoke test failed. {failure}");
        output.Flush();
        return failure is null ? 0 : 1;
    }

    /// <summary>The informational version set at publish time, such as <c>1.2.3</c> or <c>0.0.0-dev+1a2b3c4</c>.</summary>
    public static string Version =>
        typeof(SmokeTest).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    private static string? CheckBulgarian()
    {
        try
        {
            var text = Strings.ResourceManager.GetString(BulgarianKey, CultureInfo.GetCultureInfo("bg"));
            return text == BulgarianText
                ? null
                : $"The Bulgarian text of {BulgarianKey} is '{text}', not '{BulgarianText}'.";
        }
        catch (Exception e)
        {
            return $"The Bulgarian resources could not be read: {e.GetType().Name}: {e.Message}";
        }
    }
}
