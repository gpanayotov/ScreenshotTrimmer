using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ScreenshotTrimmer.App.Localization;
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
            var settings = SettingsStore.ForCurrentUser().Load();
            Localizer.Instance.SetCulture(StartCulture(settings.Language));

            var workspace = new Workspace(settings);
            var window = new MainWindow();
            window.Attach(workspace);
            window.Closed += (_, _) => workspace.Dispose();
            desktop.MainWindow = window;
            window.OpenCommandLine(desktop.Args ?? []);
        }

        base.OnFrameworkInitializationCompleted();
    }

    // English when the setting is System, until the System rule comes in with the settings window.
    private static CultureInfo StartCulture(Language language) =>
        CultureInfo.GetCultureInfo(language == Language.Bg ? "bg" : "en");
}
