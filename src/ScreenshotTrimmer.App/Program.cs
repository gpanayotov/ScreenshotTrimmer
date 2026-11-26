using Avalonia;

namespace ScreenshotTrimmer.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) =>
        args.Contains(SmokeTest.Switch)
            ? SmokeTest.Run(Console.Out)
            : BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Also used by the XAML previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
