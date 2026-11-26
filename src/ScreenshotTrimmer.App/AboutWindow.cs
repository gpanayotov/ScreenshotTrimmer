using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ScreenshotTrimmer.App.Localization;

namespace ScreenshotTrimmer.App;

/// <summary>Help › About: the app's icon, name and running version, the licence and the repository link.</summary>
public sealed class AboutWindow : Window
{
    public AboutWindow()
    {
        var about = About.Describe(Localizer.Instance.Culture, About.CurrentVersion);
        Title = about.Title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var close = new Button
        {
            Content = Localizer.Instance["AboutClose"],
            IsDefault = true,
            IsCancel = true,
            MinWidth = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        close.Click += (_, _) => Close();

        using var icon = AssetLoader.Open(new Uri("avares://ScreenshotTrimmer/Assets/Icon/screenshot-trimmer-128.png"));
        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8,
            Children =
            {
                new Image { Source = new Bitmap(icon), Width = 64, Height = 64, HorizontalAlignment = HorizontalAlignment.Left },
                new TextBlock { Text = about.Name, FontSize = 20, FontWeight = FontWeight.SemiBold },
                // Selectable, so the version can be copied into a problem report.
                new SelectableTextBlock { Text = about.Version, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = about.Licence, TextWrapping = TextWrapping.Wrap },
                new HyperlinkButton { Content = about.Link, NavigateUri = new Uri(about.Link), Padding = new Thickness(0) },
                close,
            },
        };

        Opened += (_, _) => close.Focus();
    }
}
