using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace ScreenshotTrimmer.App.Localization;

/// <summary><c>Text="{loc:Tr OpenFiles}"</c>: a live binding to <see cref="Localizer"/>'s string for the key.</summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay };
}
