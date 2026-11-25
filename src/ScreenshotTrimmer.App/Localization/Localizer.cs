using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace ScreenshotTrimmer.App.Localization;

/// <summary>
/// The source of every visible string, in the current interface language. Bind to it with <c>{loc:Tr Key}</c>; a
/// language change updates every bound text at once.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    private readonly ResourceManager _resources = Strings.ResourceManager;

    private Localizer()
    {
    }

    public static Localizer Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en");

    /// <summary>The string for <paramref name="key"/>, or <c>!key!</c> when there is none, so a typo shows on screen.</summary>
    public string this[string key] => _resources.GetString(key, Culture) ?? $"!{key}!";

    /// <summary>The string for <paramref name="key"/> with <paramref name="args"/> formatted for the current language.</summary>
    public string Format(string key, params object?[] args) => string.Format(Culture, this[key], args);

    public void SetCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        Culture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        // Avalonia refreshes indexer bindings on "Item"; WPF's "Item[]" does not work here.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
    }
}
