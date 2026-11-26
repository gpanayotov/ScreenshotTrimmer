using Avalonia;

namespace ScreenshotTrimmer.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (!args.Contains(SmokeTest.Switch))
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        ParentConsole.AttachWhenOutputGoesNowhere();
        return SmokeTest.Run(Console.Out);
    }

    // Also used by the XAML previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
