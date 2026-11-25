using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Captured before the app sets its own culture, so "System" keeps meaning the system's language.
            var settings = new SettingsSession(SettingsStore.ForCurrentUser(), CultureInfo.CurrentUICulture);
            settings.ApplyLanguage();

            var workspace = new Workspace(settings.Current);
            var window = new MainWindow();
            window.RememberPlacement(WindowPlacementStore.ForCurrentUser());
            window.Attach(workspace, settings);
            window.Closed += (_, _) => workspace.Dispose();
            desktop.MainWindow = window;
            window.OpenCommandLine(desktop.Args ?? []);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
